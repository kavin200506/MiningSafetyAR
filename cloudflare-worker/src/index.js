/**
 * MiningSafetyAR quiz-feedback Worker.
 *
 * The ONLY LLM call in the adaptive post-training MCQ system. QuizSelectionService.cs (Unity
 * client) has already selected questions (rule-based, tag-driven — no LLM involved) and scored
 * the attempt BEFORE calling this endpoint. This Worker takes that finished score + weakness-tag
 * summary and asks Gemini for a short personalized feedback paragraph — it never picks or
 * generates quiz questions, and never sees raw question content.
 *
 * Provider/hosting choice (see chat history for the full reasoning): originally built as a
 * Firebase Cloud Function using Vertex AI, but this GCP project can't go on the Blaze billing
 * plan, which Cloud Functions (any generation) and Vertex AI both require regardless of billing
 * plan. Cloudflare Workers' free tier needs no billing account at all, and Google's separate
 * Gemini Developer API (generativelanguage.googleapis.com, a plain API key from
 * https://aistudio.google.com/apikey) also has a genuine no-billing free tier — so this calls
 * that API directly via fetch() instead of the Vertex AI SDK (which needs GCP service-account
 * auth that doesn't work outside GCP compute anyway).
 *
 * The API key lives ONLY as a Worker secret (`wrangler secret put GEMINI_API_KEY`) — never in
 * this file, never in wrangler.toml, and never in the Unity client, which only ever calls this
 * Worker's HTTPS URL.
 */

// gemini-2.0-flash was retired by Google (404). gemini-3.6-flash works but is a "thinking" model
// that burns a large, budget-proportional chunk of maxOutputTokens on invisible reasoning before
// writing the actual answer (~30s latency, needed maxOutputTokens:2048 just to avoid truncation)
// — a bad fit for "show feedback right after the quiz score". gemini-3.5-flash-lite has no such
// overhead for a prompt this simple: ~1s latency, clean finishReason:STOP, no thoughtsTokenCount
// at all in testing. Use the lite model; only reconsider if quality genuinely suffers in practice.
const MODEL = "gemini-3.5-flash-lite";
const GEMINI_URL = (apiKey) =>
  `https://generativelanguage.googleapis.com/v1beta/models/${MODEL}:generateContent?key=${apiKey}`;

function jsonResponse(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: {
      "Content-Type": "application/json",
      // Unity's UnityWebRequest doesn't need CORS, but harmless to allow browser-based testing.
      "Access-Control-Allow-Origin": "*",
    },
  });
}

/**
 * Deliberately sends only the aggregate score + weakness-tag summary the client already
 * computed — never raw question stems/answers/correctness per-question — to keep the payload
 * small and the model's job narrow (one paragraph of coaching text, not re-grading).
 */
function buildPrompt(body) {
  const { moduleId, submoduleId, scorePercent, correctCount, total, topWeaknessTag, weaknessTagCounts } = body;

  const tagSummary =
    Object.entries(weaknessTagCounts || {})
      .sort((a, b) => b[1] - a[1])
      .map(([tag, count]) => `${tag.replace(/_/g, " ")} (missed ${count}x)`)
      .join(", ") || "none — no specific weak tags this attempt";

  return [
    "You are a safety-training coach writing feedback for a mine worker who just finished a short",
    "post-training multiple-choice quiz on an AR safety simulator.",
    `Module: ${moduleId || "unknown"} / ${submoduleId || "unknown"}.`,
    `Score: ${Math.round(scorePercent)}% (${correctCount}/${total} correct).`,
    `Topics missed this attempt: ${tagSummary}.`,
    topWeaknessTag ? `Their single biggest weak spot: ${String(topWeaknessTag).replace(/_/g, " ")}.` : "",
    "",
    "Write ONE short, encouraging, plain-language paragraph (2-3 sentences, no bullet points, no",
    "markdown) telling them what to focus on next. Assume the reader may have low literacy and is",
    "not a native English speaker — use simple, direct words. Do not invent facts about mine safety",
    "procedure beyond what's implied by the topic names above. Do not mention 'AI', 'model', or that",
    "this text was generated.",
  ]
    .filter(Boolean)
    .join("\n");
}

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "POST, OPTIONS",
          "Access-Control-Allow-Headers": "Content-Type",
        },
      });
    }

    if (request.method !== "POST") {
      return jsonResponse({ error: "POST only" }, 405);
    }

    let body;
    try {
      body = await request.json();
    } catch {
      return jsonResponse({ error: "invalid JSON body" }, 400);
    }

    if (typeof body.scorePercent !== "number" || typeof body.total !== "number") {
      return jsonResponse({ error: "scorePercent and total are required" }, 400);
    }

    const apiKey = env.GEMINI_API_KEY;
    if (!apiKey) {
      console.log("GEMINI_API_KEY secret not configured");
      return jsonResponse({ error: "server misconfigured" }, 500);
    }

    try {
      const geminiResp = await fetch(GEMINI_URL(apiKey), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contents: [{ role: "user", parts: [{ text: buildPrompt(body) }] }],
          generationConfig: { maxOutputTokens: 300, temperature: 0.6 },
        }),
      });

      if (!geminiResp.ok) {
        const errText = await geminiResp.text();
        console.log("Gemini API error", geminiResp.status, errText);
        return jsonResponse({ error: "feedback generation failed" }, 502);
      }

      const data = await geminiResp.json();
      const text = data?.candidates?.[0]?.content?.parts?.[0]?.text?.trim();

      if (!text) {
        console.log("Gemini returned no text", JSON.stringify(data));
        return jsonResponse({ feedback: null });
      }

      return jsonResponse({ feedback: text });
    } catch (err) {
      console.log("generateQuizFeedback failed", err.message);
      // Client (QuizSelectionService.FetchFeedbackText) already has a local fallback message for
      // exactly this case — fail loudly here but don't retry-storm the client.
      return jsonResponse({ error: "feedback generation failed" }, 502);
    }
  },
};
