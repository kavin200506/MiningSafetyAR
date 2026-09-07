# MiningSafetyAR

**AR-based vocational safety training and digital certification platform for industrial/mine workers**, built for Smart India Hackathon SIH26041 (Govt. of Jharkhand — Dept. of Higher & Technical Education). Unity 6 mobile app (Android) + Firebase backend + an external web verification portal.

This document reflects the **current code**, not the project's older planning docs (`README.md`, `TECH_STACK.md`, the roadmap file, etc.), which predate the offline-support and scoring work described below and are stale in places — where they disagree with the code, this file follows the code.

---

## 1. What it actually is

A worker installs the Android app, logs in, and works through short **AR-simulated emergency drills** (e.g. put out a fire with a real-feeling extinguisher motion, respond to a gas leak) combined with a multiple-choice quiz per module. Passing a module earns a **digitally signed, QR-verifiable certificate** that anyone — a supervisor, an inspector, an auditor — can check is genuine by scanning the QR code, with or without the app installed, even if the worker earned it while completely offline underground.

Who it's for: mine/industrial workers (the trainees), and whoever needs to verify their safety certification (site supervisors, DGMS-style compliance checks).

---

## 2. Core user journey, end to end

1. **Splash → Login/Register.** A worker registers with a worker ID, name, organization, sector, phone, and language preference. Login uses Firebase Identity Toolkit (email/password under the hood, keyed by worker ID) via REST calls — no native Firebase SDK dependency for auth.
2. **Dashboard** — overview of progress, shortcuts into the training catalogue, progress tracker, settings, certificates list.
3. **Training Catalogue → Module Detail → Learning Content** — pick a module (Fire & Explosion Safety, Gas Leak & Confined Space, and sub-modules like Fire Extinguisher Protocol), read/watch the instructional content in the selected language (with slowed-down 0.85× voiceover for comprehension).
4. **Location Capture** — before the AR drill starts, the app acquires the worker's GPS coordinates (shown as a radar-style capture screen) and tags the upcoming attempt with them.
5. **AR Simulation** — the actual hands-on drill (see §4).
6. **Assessment (quiz)** — a short MCQ quiz follows the drill.
7. **Results** — combined score, pass/fail, competency breakdown bars.
8. **Certificate** (if passed with a high enough score) — a generated, signed certificate with a QR code.
9. **QR Verify** — either in-app or via the public web portal, scanning/opening the QR resolves to a live pass/fail verification of that specific certificate.

---

## 3. Training modules & how scoring actually works

Two module families exist in code today: **Fire & Explosion Safety** (with a sub-module, Fire Extinguisher Protocol) and **Gas Leak & Confined Space**. (Older docs mention machinery/electrical/heights-safety modules too — those aren't implemented in the current `Modules` folder; only fire safety and gas leak have real managers.)

**Fire Safety drill** (`FireSafetyModuleManager.cs`) is a multi-step sequence: grab the extinguisher, pull the pin, aim, squeeze & sweep the fire out, then evacuate. Each step is scored, and a few specific mechanics matter:

- **Alarm scoring** (added 2026-09-05): a 3D emergency alarm button is spawned in AR next to the extinguisher. Sounding it *before* grabbing the extinguisher gives the full Hazard Recognition bonus (+15); sounding it *after* grabbing gives a smaller bonus (+5); never sounding it at all applies a penalty (−15). This is *only* a Hazard Recognition input, not a separate top-level score — it's baked into `ComputeHazardRecognitionScore()` alongside a penalty for standing too close to the fire (proximity breaches).
- **Extinguisher technique**: pull-pin / aim-spray / squeeze-sweep steps each contribute to an Extinguisher Use score; squeeze-and-sweep quality is measured from actual sweep motion intensity, with a floor so "did it standing still" isn't scored as zero.
- **Evacuation**: scored on lateness and mistakes, also with a floor.
- **Time**: points are deducted per second over an expected completion time.

**Gas Leak drill** (`GasLeakModuleManager.cs`) is a 4-step sequence: correctly identify the gas leak source, select proper PPE (must choose SCBA — a dust mask is marked wrong), signal a buddy, then evacuate — each with audio guidance in the selected language.

**Final module score** = `70% × drill (AR simulation) performance + 30% × quiz score` (`ScoringConstants.DrillWeight/QuizWeight`, `AssessmentPageController.cs`). A module counts as **passed/Completed at ≥70%**. A **certificate is only issued at ≥75%** — a deliberately higher bar than "passed," enforced in `AppDataService.UpdateLocalProgress()`.

---

## 4. AR features — what you actually see through the camera

Two AR mechanisms, both via AR Foundation 6.3 (ARCore/ARKit):

- **Marker (image) tracking** (`ARImageTrackingManager.cs`): point the camera at a printed **Fire Extinguisher poster** or **Exit Sign poster** (physical marker images, e.g. `fire_extinguisher_marker.jpg`, `exit_sign_marker.jpg` in this repo), and the app overlays a 3D fire extinguisher model or exit sign directly on top of it in real time — plus spawns the 3D emergency alarm button next to the extinguisher marker.
- **Plane detection / surface placement** (`ARPlacementManager.cs`, scene `AR Plane Detection Placement.unity`): detects real-world flat surfaces (floor/table) so 3D safety equipment or hazard props can be placed into the room for the drill without needing a printed marker.

During the fire drill, this is what makes the "aim and squeeze" extinguisher motion feel physical — the extinguisher is a real 3D object sitting in your actual room, not a flat video.

---

## 5. Competency scoring & progress tracking

Every module attempt produces a **per-skill competency breakdown**, not just one number: `hazardRecognition, extinguisherUse, ppeSelection, evacuation, emergencyResponse, timeManagement, quizScore` (each 0–100). These are rendered as score bars on the Results page, Progress page, Module Detail page, and Assessment page (e.g. "Hazard Recognition: 80%, Extinguisher Use: 65%..."), so a worker can see exactly *which* skill needs work, not just their overall grade. This breakdown is stored per-module (`workers/{uid}/progress/{moduleId}`) — it is not duplicated anywhere else.

---

## 6. Certificates & QR verification, end to end

1. On a qualifying attempt (≥75%), `CertificateGenerator` builds a certificate record (worker name, module, score, organization, issue/expiry dates) and signs it with **HMAC-SHA256**.
2. The signed certificate is written to Firestore in **two places at once**: a private copy under the worker (`workers/{uid}/certificates/{certId}`) and a **public** copy (`certificates/{certId}`) with no auth required — this is what makes QR verification work for someone who isn't logged in as that worker at all.
3. A QR code is generated locally (`QRCodeTextureGenerator`) encoding a verification URL (`https://cert-veri.web.app/verify?cert={certId}`).
4. **The verification portal itself is a separate, external web project** — it is not part of this Unity repo, only referenced by URL. It reads the same public `certificates/{certId}` document to show a pass/fail verification result to anyone who scans the code.
5. The in-app Certificate page shows the same certificate — its *own* frozen `score`/`issuedDate`/etc. fields, not the worker's live best score, so the in-app view and the portal always agree (this consistency was a bug fix made during this project's development).

---

## 7. Offline support — how it works with no signal

Built specifically for underground/no-connectivity conditions:

- **Persistent, offline-capable login**: a Firebase refresh token is stored locally; the app silently re-authenticates on launch when online, and falls back to a locally-trusted "offline session" (no server round-trip) when there's no connectivity at all — the worker stays logged in until they manually log out.
- **Local JSON store** (`offline_store.json`, plain JSON — not SQLite, not PlayerPrefs): holds the session, a queue of pending writes, and all locally cached worker/progress/results/certificate data.
- **Write-queue-and-flush**: every Firestore write (profile, module progress, training result, certificate) goes through a single choke point — if offline, it's queued locally instead of lost; a background `CloudSyncManager` polls connectivity every ~15s and replays the queue once a connection is available, using the exact same typed save methods (so dual-writes like the certificate's public+private copies still happen correctly on sync, not just a raw field dump).
- Net effect: a worker can complete an entire module, pass, and receive a certificate **entirely offline**, and it will sync — and become QR-verifiable — automatically the next time the device gets signal. This was verified end-to-end on a real device via `adb logcat` during development.

---

## 8. Data & Firebase structure

Firestore project `minesafetyar`, accessed via REST (no native SDK dependency for Firestore itself):

```
workers/{uid}                          — profile: name, org, sector, phone, language,
                                          profilePicUrl, joinDate, overallProgress,
                                          certificatesEarned, totalAttempts
workers/{uid}/progress/{moduleId}      — status, progress %, bestScore, attempts,
                                          lastAttempt, certificateId, competencyScores{}
workers/{uid}/results/{resultId}       — one doc per assessment attempt (score, mistakes,
                                          time taken, GPS location fields, offline flag)
workers/{uid}/certificates/{certId}    — private copy of every certificate earned
certificates/{certId}                  — PUBLIC copy, used by QR/portal verification
```

A schema cleanup pass was done on this data during development: several worker documents had leftover fields from an earlier, pre-refactor code path (flat competency fields and a broken serialized string sitting directly on the worker doc, which the current code never writes there), and two entire leftover top-level collections (`phase0_tests`, `trainingResults`) existed from superseded code. These were identified by directly querying live Firestore against what the current code actually reads/writes, and removed — every worker document is now confirmed to have an identical, minimal field set, with subcollections present only where a worker has genuinely done that activity (no progress yet → no `progress` subcollection, etc. — that variation is expected, not corruption).

---

## 9. Language support

Four languages: **English, Hindi, Santali, Tamil** (`Language` enum). `LanguageManager` drives per-language instructional voiceover, deliberately played at **0.85× speed** for comprehension by non-native speakers. A `VoiceCommandManager` also exists for **offline, hands-free voice navigation** (useful with gloves on) using the Vosk offline speech-recognition engine for Hindi/Tamil/Indian English; Santali has no STT model, so it falls back to simple keyword matching. Voice control is optional/plugin-gated (`VOSK_AVAILABLE`), not guaranteed active in every build.

---

## 10. Location / GPS feature

Before each drill, `TrainingLocationCapture` acquires the device's GPS coordinates and reverse-geocoded address, shown to the worker on a dedicated capture screen. Each `TrainingResult` is tagged with `latitude, longitude, locationAccuracyMeters, locationName, hasLocation, capturedOffline` — this exists for regional/site-level training analytics (e.g. "which site/region is this training happening at") and as a lightweight proof-of-location for the attempt, including when captured with no connectivity.

---

## 11. Tech stack

- **Unity 6000.3.23f1 LTS**, UI Toolkit (UXML/USS) for all screens — no legacy uGUI.
- **AR Foundation 6.3.5** with ARCore + ARKit provider packages (image tracking + plane detection).
- **Firebase**: Identity Toolkit REST API for auth (no native SDK), Firestore REST API for all data (no native SDK) — chosen so the whole backend layer is plain `UnityWebRequest` calls, easy to make offline-safe.
- **Local persistence**: hand-rolled JSON file store (`Application.persistentDataPath`), not SQLite.
- **Target platform**: Android, API 29+ (Android 10+).
- **Optional**: Vosk offline speech recognition for voice commands.

---

## 12. What you'll actually see (tangible outputs)

- A **3D fire extinguisher / exit sign / alarm button** appearing over a real printed poster or real floor when you point the phone camera at it, that you physically "use" to complete the drill.
- A **Results screen** with an overall pass/fail score plus a bar chart of per-skill competencies (hazard recognition, extinguisher use, PPE selection, evacuation, emergency response, time, quiz).
- A **Progress dashboard** tracking completion % and best scores across all modules over time.
- On passing at ≥75%: a **DGMS-style digital certificate** (worker name, module, org, score, issue/expiry date) with an embedded **QR code**, viewable and shareable from the app.
- Scanning that QR code (from any phone, app installed or not) opens the **external web verification portal**, which shows a live VALID/INVALID result pulled directly from Firestore's public certificate record — matching exactly what the app itself shows.
- All of the above continues to work **with the phone in airplane mode**, syncing automatically the moment connectivity returns.

---

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0122MYSzwZGoQSr5K7bNkkiS
