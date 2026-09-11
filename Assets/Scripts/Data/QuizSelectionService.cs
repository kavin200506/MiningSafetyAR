using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.Data
{
    /// <summary>
    /// Adaptive post-training MCQ selection: rule-based tagged question selection driven by tracked
    /// mistakes (see MistakeTags / FirestoreService.LogMistakeEvent). No LLM is involved in choosing
    /// or generating questions — the only LLM call in this feature is the personalized feedback text
    /// generated server-side AFTER scoring (see SubmitQuizResult -> cloudflare-worker/src/index.js).
    /// </summary>
    public class QuizSelectionService : MonoBehaviour
    {
        public static QuizSelectionService Instance { get; private set; }

        [Header("Selection Tuning")]
        [SerializeField] int defaultQuestionCount = 5;
        [Tooltip("Exponential recency decay half-life, in days, for weighting mistake events (more recent = higher weight).")]
        [SerializeField] float recencyHalfLifeDays = 7f;
        [Tooltip("Flat weight every question gets regardless of tag match, so untagged/baseline questions can still be picked.")]
        [SerializeField] float baseSelectionWeight = 0.15f;
        [Tooltip("Multiplier applied when a candidate's difficulty matches the worker's current adaptive difficulty level.")]
        [SerializeField] float difficultyMatchBonus = 1.5f;

        [Header("Adaptive Difficulty")]
        [SerializeField] float scoreThresholdForHarder = 75f;
        [SerializeField] float scoreThresholdForEasier = 50f;

        [Header("Feedback Backend (Step 4 — see cloudflare-worker/src/index.js)")]
        [Tooltip("Deployed Cloudflare Worker HTTPS URL. SubmitQuizResult degrades to a generic local fallback message if this is empty or unreachable.")]
        [SerializeField] string feedbackEndpointUrl = "https://minesafetyar-quiz-feedback.kmahari2007.workers.dev";

        static readonly string[] DifficultyOrder = { "easy", "medium", "hard" };

        [Serializable]
        public class QuizAnswer
        {
            public string questionId;
            public int selectedIndex;
        }

        [Serializable]
        public class QuizSubmissionResult
        {
            public int correctCount;
            public int total;
            public float scorePercent;
            public bool passed;
            public string previousDifficulty;
            public string newDifficulty;
            public Dictionary<string, int> missedTagCounts = new Dictionary<string, int>();
            public string feedbackText;
            public bool feedbackFromLLM;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // PUBLIC API
        // ================================================================

        public void GetAdaptiveQuiz(string uid, string moduleId, string submoduleId, int questionCount, Action<List<QuestionBankItem>> onComplete)
        {
            if (questionCount <= 0) questionCount = defaultQuestionCount;
            StartCoroutine(GetAdaptiveQuizRoutine(uid, moduleId, submoduleId, questionCount, onComplete));
        }

        public void SubmitQuizResult(string uid, string moduleId, string submoduleId, List<QuestionBankItem> askedQuestions, List<QuizAnswer> answers, Action<QuizSubmissionResult> onComplete)
        {
            StartCoroutine(SubmitQuizResultRoutine(uid, moduleId, submoduleId, askedQuestions, answers, onComplete));
        }

        // ================================================================
        // SELECTION
        // ================================================================

        IEnumerator GetAdaptiveQuizRoutine(string uid, string moduleId, string submoduleId, int questionCount, Action<List<QuestionBankItem>> onComplete)
        {
            Debug.Log($"[INFO] QuizSelectionService GetAdaptiveQuiz started for worker {uid}, module={moduleId}/{submoduleId}, count={questionCount}.");

            Dictionary<string, float> weaknessVector = null;
            List<QuestionBankItem> bank = null;
            HashSet<string> askedIds = null;
            string currentDifficulty = "medium";

            yield return FetchWeaknessVector(uid, moduleId, submoduleId, v => weaknessVector = v);
            yield return FetchQuestionBank(moduleId, submoduleId, b => bank = b);
            yield return FetchAskedIds(uid, moduleId, submoduleId, ids => askedIds = ids);
            yield return FetchDifficulty(uid, moduleId, submoduleId, d => currentDifficulty = d);

            if (bank == null || bank.Count == 0)
            {
                Debug.LogWarning($"[WARN] QuizSelectionService No question bank content for {moduleId}/{submoduleId} — returning empty quiz.");
                onComplete?.Invoke(new List<QuestionBankItem>());
                yield break;
            }

            var selected = SelectQuestions(bank, weaknessVector, askedIds, currentDifficulty, questionCount);

            foreach (var q in selected)
            {
                FirestoreService.Instance.MarkQuestionAsked(uid, moduleId, submoduleId, q.id);
            }

            Debug.Log($"[INFO] QuizSelectionService Selected {selected.Count} question(s) for worker {uid}: [{string.Join(", ", selected.Select(q => q.id))}].");
            onComplete?.Invoke(selected);
        }

        IEnumerator FetchWeaknessVector(string uid, string moduleId, string submoduleId, Action<Dictionary<string, float>> onDone)
        {
            var weakness = new Dictionary<string, float>();
            bool done = false;
            FirestoreService.Instance.GetMistakeHistory(uid, moduleId, submoduleId, (ok, docs) =>
            {
                if (ok && docs != null)
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (var doc in docs)
                    {
                        var fields = doc.TryGetValue("fields", out var f) ? f as Dictionary<string, object> : doc;
                        if (fields == null) continue;
                        string tag = FirestoreService.GetstringValue(fields, "tag");
                        int severity = FirestoreService.GetintValue(fields, "severity");
                        string tsStr = FirestoreService.GetstringValue(fields, "timestamp");
                        if (string.IsNullOrEmpty(tag)) continue;

                        double daysAgo = 0;
                        if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                            daysAgo = Math.Max(0, (now - ts.ToUniversalTime()).TotalDays);

                        float recencyWeight = (float)Math.Pow(0.5, daysAgo / Math.Max(0.01f, recencyHalfLifeDays));
                        float weight = Mathf.Max(1, severity) * recencyWeight;

                        weakness[tag] = weakness.TryGetValue(tag, out var existing) ? existing + weight : weight;
                    }
                }
                done = true;
            });
            yield return new WaitUntil(() => done);
            onDone(weakness);
        }

        IEnumerator FetchQuestionBank(string moduleId, string submoduleId, Action<List<QuestionBankItem>> onDone)
        {
            List<QuestionBankItem> items = new List<QuestionBankItem>();
            bool done = false;
            FirestoreService.Instance.GetQuestionBank(moduleId, submoduleId, (ok, docs) =>
            {
                if (ok && docs != null)
                {
                    foreach (var doc in docs)
                    {
                        var item = QuestionBankItem.FromFirestoreDocument(doc);
                        if (item != null) items.Add(item);
                    }
                }
                done = true;
            });
            yield return new WaitUntil(() => done);
            onDone(items);
        }

        IEnumerator FetchAskedIds(string uid, string moduleId, string submoduleId, Action<HashSet<string>> onDone)
        {
            var ids = new HashSet<string>();
            bool done = false;
            FirestoreService.Instance.GetAskedQuestionIds(uid, moduleId, submoduleId, (ok, docs) =>
            {
                if (ok && docs != null)
                {
                    foreach (var doc in docs)
                    {
                        if (doc.TryGetValue("name", out var nameObj) && nameObj is string name)
                            ids.Add(name.Substring(name.LastIndexOf('/') + 1));
                    }
                }
                done = true;
            });
            yield return new WaitUntil(() => done);
            onDone(ids);
        }

        IEnumerator FetchDifficulty(string uid, string moduleId, string submoduleId, Action<string> onDone)
        {
            string difficulty = "medium";
            bool done = false;
            FirestoreService.Instance.GetSubmoduleQuizState(uid, moduleId, submoduleId, (ok, json) =>
            {
                if (ok)
                {
                    var fields = FirestoreService.ParseFirestoreFields(json);
                    string stored = fields != null ? FirestoreService.GetstringValue(fields, "difficultyLevel") : "";
                    if (!string.IsNullOrEmpty(stored)) difficulty = stored;
                }
                done = true;
            });
            yield return new WaitUntil(() => done);
            onDone(difficulty);
        }

        /// <summary>
        /// Weighted sampling biased toward tags the worker has recently/severely struggled with, with
        /// a soft preference for the worker's current adaptive difficulty level, while guaranteeing at
        /// least one question from any tag that would otherwise go completely unrepresented.
        /// </summary>
        List<QuestionBankItem> SelectQuestions(List<QuestionBankItem> bank, Dictionary<string, float> weaknessVector, HashSet<string> askedIds, string currentDifficulty, int questionCount)
        {
            var pool = bank.Where(q => !askedIds.Contains(q.id)).ToList();
            if (pool.Count < questionCount)
            {
                Debug.LogWarning($"[WARN] QuizSelectionService Not enough unseen questions ({pool.Count}) for requested count ({questionCount}) — allowing repeats.");
                pool = new List<QuestionBankItem>(bank);
            }
            if (pool.Count == 0) return new List<QuestionBankItem>();

            questionCount = Mathf.Min(questionCount, pool.Count);

            float Weight(QuestionBankItem q)
            {
                float w = baseSelectionWeight;
                if (q.tags != null)
                    foreach (var t in q.tags)
                        if (weaknessVector != null && weaknessVector.TryGetValue(t, out var tw)) w += tw;
                if (string.Equals(q.difficulty.ToString(), currentDifficulty, StringComparison.OrdinalIgnoreCase)) w *= difficultyMatchBonus;
                return Mathf.Max(0.01f, w);
            }

            var remaining = new List<QuestionBankItem>(pool);
            var selected = new List<QuestionBankItem>();
            var rng = new System.Random();

            while (selected.Count < questionCount && remaining.Count > 0)
            {
                float totalWeight = remaining.Sum(Weight);
                float roll = (float)rng.NextDouble() * totalWeight;
                float cumulative = 0f;
                QuestionBankItem picked = remaining[remaining.Count - 1];
                foreach (var q in remaining)
                {
                    cumulative += Weight(q);
                    if (roll <= cumulative) { picked = q; break; }
                }
                selected.Add(picked);
                remaining.Remove(picked);
            }

            // Baseline coverage: any tag present in the pool but absent from the selection gets
            // forced in, swapping out the lowest-weighted already-selected question (never dropping
            // below questionCount, and never evicting the last remaining representative of some
            // other under-covered tag by only ever evicting from the lowest-weight end).
            var coveredTags = new HashSet<string>(selected.SelectMany(q => q.tags ?? Array.Empty<string>()));
            var allPoolTags = pool.SelectMany(q => q.tags ?? Array.Empty<string>()).Distinct();
            foreach (var tag in allPoolTags)
            {
                if (coveredTags.Contains(tag)) continue;
                var candidate = pool.FirstOrDefault(q => !selected.Contains(q) && q.tags != null && q.tags.Contains(tag));
                if (candidate == null) continue;
                if (selected.Count == 0) { selected.Add(candidate); }
                else
                {
                    var weakest = selected.OrderBy(Weight).First();
                    selected.Remove(weakest);
                    selected.Add(candidate);
                }
                coveredTags.Add(tag);
            }

            return selected;
        }

        // ================================================================
        // SUBMISSION + ADAPTIVE DIFFICULTY + FEEDBACK
        // ================================================================

        IEnumerator SubmitQuizResultRoutine(string uid, string moduleId, string submoduleId, List<QuestionBankItem> askedQuestions, List<QuizAnswer> answers, Action<QuizSubmissionResult> onComplete)
        {
            var result = new QuizSubmissionResult { total = askedQuestions?.Count ?? 0 };
            var questionsById = (askedQuestions ?? new List<QuestionBankItem>()).ToDictionary(q => q.id, q => q);

            foreach (var answer in answers ?? new List<QuizAnswer>())
            {
                if (!questionsById.TryGetValue(answer.questionId, out var q)) continue;
                bool correct = answer.selectedIndex == q.correctIndex;
                if (correct) result.correctCount++;
                else if (q.tags != null)
                    foreach (var t in q.tags)
                        result.missedTagCounts[t] = result.missedTagCounts.TryGetValue(t, out var c) ? c + 1 : 1;
            }

            result.scorePercent = result.total > 0 ? (float)result.correctCount / result.total * 100f : 0f;
            result.passed = result.scorePercent >= Modules.ScoringConstants.PassThresholdPercentage;

            Debug.Log($"[INFO] QuizSelectionService SubmitQuizResult worker={uid} module={moduleId}/{submoduleId} score={result.scorePercent:F1}% ({result.correctCount}/{result.total}).");

            // Log each missed tag as a fresh mistake signal — a wrong answer on a tagged question is
            // itself evidence of that weakness, feeding back into future selection even without the
            // AR sim re-detecting it live.
            foreach (var kv in result.missedTagCounts)
            {
                for (int i = 0; i < kv.Value; i++)
                    FirestoreService.Instance.LogMistakeEvent(moduleId, submoduleId, kv.Key, 1);
            }

            yield return FetchDifficulty(uid, moduleId, submoduleId, d => result.previousDifficulty = d);
            result.newDifficulty = AdjustDifficulty(result.previousDifficulty, result.scorePercent);

            var stateFields = new Dictionary<string, object>
            {
                { "difficultyLevel", result.newDifficulty },
                { "lastScorePercent", (double)result.scorePercent },
                { "lastAttemptAt", DateTime.UtcNow.ToString("o") },
            };
            bool saveDone = false;
            FirestoreService.Instance.SaveSubmoduleQuizState(uid, moduleId, submoduleId, MiniJSON.Json.Serialize(stateFields), (ok, resp) => saveDone = true);
            yield return new WaitUntil(() => saveDone);

            // Feedback text is NOT fetched here — callers (e.g. AdaptiveQuizPageController) show the
            // score immediately from this callback, then separately call the public
            // FetchFeedbackText() below so the UI can display "generating feedback..." while that
            // (possibly slower, network-dependent) call is in flight rather than blocking on it.
            onComplete?.Invoke(result);
        }

        string AdjustDifficulty(string current, float scorePercent)
        {
            int idx = Array.IndexOf(DifficultyOrder, (current ?? "medium").ToLowerInvariant());
            if (idx < 0) idx = 1;

            if (scorePercent >= scoreThresholdForHarder) idx = Mathf.Min(idx + 1, DifficultyOrder.Length - 1);
            else if (scorePercent < scoreThresholdForEasier) idx = Mathf.Max(idx - 1, 0);

            return DifficultyOrder[idx];
        }

        // ================================================================
        // FEEDBACK TEXT — calls the backend Cloudflare Worker (Step 4). NEVER calls an LLM API
        // directly from the client, and NEVER holds an LLM API key here — see
        // cloudflare-worker/src/index.js.
        // ================================================================

        /// <summary>
        /// Public entry point so callers (AdaptiveQuizPageController) can show the score immediately
        /// from SubmitQuizResult's callback, then fetch feedback text as a visibly separate step —
        /// rather than SubmitQuizResult silently blocking on this network call. Always calls back
        /// with usable text: falls back to a local templated message (see BuildFallbackFeedback) if
        /// feedbackEndpointUrl is unconfigured or the request fails, so callers never need to handle
        /// an error case themselves.
        /// </summary>
        public void FetchFeedbackText(QuizSubmissionResult result, string moduleId, string submoduleId, Action<(string text, bool fromLLM)> onDone)
        {
            StartCoroutine(FetchFeedbackTextRoutine(result, moduleId, submoduleId, onDone));
        }

        IEnumerator FetchFeedbackTextRoutine(QuizSubmissionResult result, string moduleId, string submoduleId, Action<(string text, bool fromLLM)> onDone)
        {
            if (string.IsNullOrEmpty(feedbackEndpointUrl))
            {
                Debug.LogWarning("[WARN] QuizSelectionService feedbackEndpointUrl not configured — using local fallback feedback text.");
                onDone((BuildFallbackFeedback(result), false));
                yield break;
            }

            var topTag = result.missedTagCounts.OrderByDescending(kv => kv.Value).Select(kv => (string?)kv.Key).FirstOrDefault();
            var requestBody = new Dictionary<string, object>
            {
                { "moduleId", moduleId },
                { "submoduleId", submoduleId },
                { "scorePercent", (double)result.scorePercent },
                { "correctCount", result.correctCount },
                { "total", result.total },
                { "topWeaknessTag", topTag ?? "" },
                { "weaknessTagCounts", result.missedTagCounts.ToDictionary(kv => kv.Key, kv => (object)kv.Value) },
            };
            string bodyJson = MiniJSON.Json.Serialize(requestBody);

            using var req = new UnityWebRequest(feedbackEndpointUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var response = MiniJSON.Json.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
                string feedback = response != null && response.TryGetValue("feedback", out var fb) ? fb as string : null;
                if (!string.IsNullOrEmpty(feedback))
                {
                    Debug.Log("[INFO] QuizSelectionService Received personalized feedback text from backend.");
                    onDone((feedback, true));
                    yield break;
                }
            }

            Debug.LogWarning($"[WARN] QuizSelectionService Feedback endpoint call failed ({req.error}) — using local fallback.");
            onDone((BuildFallbackFeedback(result), false));
        }

        string BuildFallbackFeedback(QuizSubmissionResult result)
        {
            if (result.missedTagCounts.Count == 0)
                return result.passed
                    ? "Great work — no specific weak spots this attempt."
                    : "Review the explanations for the questions you missed and try again.";

            string topTag = result.missedTagCounts.OrderByDescending(kv => kv.Value).First().Key;
            return $"You struggled most with \"{topTag.Replace('_', ' ')}\" this attempt — review that topic before your next try.";
        }
    }
}
