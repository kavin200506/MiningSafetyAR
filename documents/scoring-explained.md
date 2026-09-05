# How Scoring Will Work — Plain-Language Walkthrough

This is the non-technical companion to [`scoring.md`](scoring.md). That document is the technical spec (what's real in the code today, exact formulas, file/line references, open engineering questions). This one just walks through **what a worker actually experiences, and how their final grade gets built**, once the redesign in `scoring.md` is implemented. No code, no file paths — just the flow and the reasoning.

---

## 1. The drill starts

The worker gets a short mission briefing — a fire has started, here's the scenario. This briefing still mentions sounding the alarm and grabbing an extinguisher for scene-setting/pacing, but **these are no longer separately graded steps.** There's no real alarm to sound and no choice of extinguisher to make in the app today, so instead of quietly handing out free points for something that doesn't actually happen, they're dropped from scoring entirely. Nothing pretends to have been tested that wasn't.

Everything that actually gets graded starts from here.

---

## 2. The four things that are actually scored during the drill

### Step 1 — Pull the Pin
The worker physically pulls the safety pin on the extinguisher. This is a real, tracked action with its own timer, starting the moment the drill begins and ending the moment the pin comes out.

### Step 2 — Aim & Test Spray
The worker aims the nozzle and gives it a test squeeze. Also real and independently timed.

### Step 3 — Squeeze & Sweep
The worker sprays the base of the fire, sweeping side-to-side. This is where the biggest quality signal lives: the app is already measuring, in real time, how much genuine side-to-side motion is happening versus standing still and spraying one spot. A worker who sweeps properly puts the fire out faster *and* scores higher on this step; a worker who just holds the trigger still eventually puts it out too (so they're not stuck), but slower and at a lower score for the step — technique is rewarded, not just the end result.

### Step 4 — Evacuate to a Safe Distance *(new)*
This is the step that doesn't exist yet today — right now the drill just ends the instant the fire goes out, which isn't how a real emergency response works. Once the fire is extinguished, an on-screen arrow (already built into the app, just currently switched off) lights up and points the worker toward a safe distance away from where the fire was. The worker has to actually walk there and stay clear for a moment — not just glance in that direction. Only once they've done that does the drill actually finish.

**Every one of these four steps starts at 100 points and loses points for mistakes made during it** — nothing here is a flat pass/fail; getting it done sloppily still costs you, getting it done cleanly keeps the full score.

---

## 3. What loses points, and why those specific amounts

| Mistake | Point cost | Why this amount |
|---|---|---|
| Standing within 3.5 ft of the active fire | −50 | This is a live physical-safety violation, not a technique slip — it should sting more than an ordinary mistake |
| Spraying without sweeping (low technique quality) | Reduces the Squeeze & Sweep step's score proportionally | Rewards real skill, not just "eventually got there" |
| Taking too long to reach the safe evacuation distance | Partial credit, degrading the longer it takes | Same idea as the sweep step — slow-but-eventually-correct still beats never doing it, but shouldn't score the same as doing it promptly |
| Extinguisher runs out before the fire is out | The drill ends immediately as a failed attempt | This isn't a point deduction — it's a real failure state, same as it is in an actual emergency |

Every other kind of mistake (in the current build) costs a flat 25 points off whichever step it happened during.

---

## 4. Then comes the quiz

After the drill, the worker takes the short knowledge-check quiz — the same multiple-choice questions that already exist today (P.A.S.S. technique, where to aim the nozzle, etc.). This part isn't changing; it already works correctly and produces a real, honest score based on what was actually answered.

---

## 5. How the final grade is built

Today, the "final grade" is secretly mostly a made-up number — the quiz is real, but it gets blended with a hardcoded stand-in for the drill performance that never actually reflects what the worker did. Under the redesign, both halves are real:

```
Drill Score  =  average of the 4 real step scores above
Quiz Score   =  percentage of quiz questions answered correctly
Final Score  =  70% Drill Score  +  30% Quiz Score
```

The drill carries more weight because it's the hands-on skill being trained — the quiz is a knowledge check layered on top, not the main event. (This 70/30 split is a proposed default, not locked in — see the open questions in `scoring.md`.)

A worker needs a Final Score of 70% or higher to pass. That's also proposed but not yet finalized — right now there are actually four different pass thresholds scattered around the app disagreeing with each other (70%, another 70%, 75%, and one screen's text that says 60%), and part of this fix is picking one number and using it everywhere.

---

## 6. What the Competency Scores mean

At the end, the results screen shows four skill categories. Today these are entirely quiz-based — a worker's hands-on performance never touches them. Under the redesign, each one is grounded in what actually happened during the drill:

- **Hazard Recognition** — based on how many times the worker got too close to the live fire. Fewer safety-distance violations, higher score.
- **Extinguisher Use** — the average of the Pull Pin, Aim & Spray, and Squeeze & Sweep step scores. This is the core hands-on technique grade.
- **Time** — how the worker's total time compares to a target "par" time for the drill. (Today this bar is actually mislabeled — it silently shows an unrelated quiz score under the "Time" heading. This fixes that; it becomes a real time-based number.)
- **Evacuation** — the new evacuation step's score, directly.

There's also a fifth number, "Emergency Response," that the app already calculates internally but never actually shows the worker anywhere — still open whether to finally surface it as a fifth bar or fold its meaning into one of the four above.

---

## 7. What the worker actually sees at the end

**During the drill**, tapping the score dropdown shows a running table like:

| Step | Errors | Time | Score |
|---|---|---|---|
| Pull Pin | 0 | 00:04 | 100/100 |
| Aim & Test Spray | 0 | 00:06 | 100/100 |
| Squeeze & Sweep | 1 | 00:14 | 75/100 |
| Evacuate to Safe Distance | 0 | 00:09 | 100/100 |

*(Sound Alarm and Select Extinguisher no longer appear here at all — not as rows, not as zeroes. If it isn't real, it isn't shown.)*

**TOTAL: 00:33 · SCORE: 375/400**

**After the quiz**, the Results screen shows the blended Final Score, pass/fail badge, the four Competency Score bars described above, and (if passed) the certificate option — all driven by the one real number computed in §5, not two disagreeing numbers like today.

---

## 8. Why this is trustworthy in a way the current version isn't

The single sentence version: **every number a worker sees will trace back to something they actually did, measured once, shown once, and stored once** — instead of today's system, where a real per-step score gets computed correctly and then silently overwritten by a hardcoded stand-in before it's ever saved or shown. Nothing in this design invents new mechanics that don't exist in the app yet, except the one you specifically asked for (Evacuation) — and even that reuses a navigation arrow that was already built and just sitting unused.

For the full technical breakdown — exact formulas, file references, what's currently broken and why, and the remaining open decisions — see [`scoring.md`](scoring.md).
