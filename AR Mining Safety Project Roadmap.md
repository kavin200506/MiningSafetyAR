# AR-Based Vocational Training Simulator — Build Roadmap
**Stack:** Unity 6000.3.23f1 LTS + AR Foundation 6.3 + ARCore XR Plugin 6.3 + Firebase (Auth/Firestore/Storage/Hosting) + Bhashini (Hindi/Santali audio) + Google Antigravity (C# scripting)

---

## PHASE 0 — Environment Setup

### Stage 1: Confirm Unity install modules
Open Unity Hub → your 6000.3.23f1 install → check installed modules include **Android Build Support**, **OpenJDK**, and **Android SDK & NDK Tools**. If missing, add them via Unity Hub → Installs → gear icon → Add Modules.

### Stage 2: Set up Google Antigravity
Download Antigravity from antigravity.google/download, install the IDE (or the editor extension if you prefer VS Code-style), and sign in. Choose Gemini 3 Pro or Claude Sonnet 4.5 as your default agent model — either works well for Unity C#.

### Stage 3: Create the Unity project
In Unity Hub, create a new **3D (URP)** project named e.g. `MiningSafetyAR`. URP (Universal Render Pipeline) is lighter than Built-in and works better on mid-range Android phones, which matches your PS's hardware constraint.

### Stage 4: Set Android as build platform
File → Build Profiles (or Build Settings) → select **Android** → Switch Platform. This must be done early since AR Foundation project settings depend on the active platform.

### Stage 5: Install AR Foundation + ARCore XR Plugin
Window → Package Manager → Unity Registry → search and install:
- `AR Foundation` (should auto-resolve to 6.3 for your Editor version)
- `Google ARCore XR Plugin` (6.3)

Then go to Edit → Project Settings → XR Plug-in Management → Android tab → check **ARCore**.

### Stage 6: Configure Android player settings
Project Settings → Player → Android tab:
- Minimum API Level: Android 10 (API 29), matching your PS requirement
- Set package name (e.g. `com.yourteam.miningsafetyar`)
- Under XR Plug-in Management, confirm ARCore is required (not optional) unless you want non-AR devices to still install the app

### Stage 7: Test on your physical Android device
Enable Developer Mode + USB Debugging on your phone. Connect via USB, hit **File → Build and Run** with an empty AR scene (just an AR Session + XR Origin) to confirm the whole pipeline works before building anything else.

---

## PHASE 1 — Core AR Foundation Scene

### Stage 8: Build your first working AR scene
In your scene, add: `XR Origin (AR)` and `AR Session` GameObjects (Unity auto-adds required components). Prompt Antigravity: *"Add an AR Raycast Manager and AR Plane Manager to my XR Origin so I can detect flat surfaces."*

### Stage 9: Add plane visualization
Use Unity's `AR Default Plane` prefab so you can literally see detected floor/table surfaces during testing — this confirms plane detection is working before you build real content on top of it.

### Stage 10: Implement tap-to-place object placement
Prompt Antigravity: *"Write a C# script using ARRaycastManager that lets the user tap the screen to place a 3D prefab at the hit point on a detected plane."* Test this with any placeholder cube/sphere first.

---

## PHASE 2 — Module 1: Fire & Explosion Response

### Stage 11: Source or create 3D assets
Get free/low-poly models (fire extinguisher, exit sign, flame effect) from Unity Asset Store, Sketchfab, or model them yourself in Blender given your existing 3D skills. Keep poly counts low for mobile performance.

### Stage 12: Add a Vuforia (or marker-based) image target — optional but recommended
Since indoor/mine-like lighting breaks markerless tracking, prompt Antigravity to help wire an image target (a printed extinguisher label or hazard sign) so your extinguisher/exit sequence anchors reliably regardless of lighting.

### Stage 13: Build the interaction sequence
Design as a simple state machine: Step 1 (identify nearest exit — tap the correct AR-highlighted door) → Step 2 (grab extinguisher, see AR PASS technique animation) → Step 3 (evacuation path highlighted with AR arrows). Prompt Antigravity for each state's script separately rather than all at once — easier to debug.

### Stage 14: Add feedback and scoring
Wrong taps should trigger a visual/audio cue (e.g., red flash + "wrong exit" sound) and log a mistake count; correct sequence completion stores a module score locally (see Stage 17).

---

## PHASE 3: Module 2: Gas Leak & Confined Space Protocol

### Stage 15: Repeat the module pattern
Duplicate your Module 1 scene structure. Build hazard-zone recognition (tap to identify gas leak source), PPE selection (choose correct gear from an AR-presented set of options), and buddy-system procedure (sequence-based interaction, e.g., tap "signal buddy" before "enter zone").

### Stage 16: Keep both modules structurally consistent
Use a shared `ModuleManager.cs` base script (prompt Antigravity to refactor Module 1's logic into a reusable base class) so Module 2 doesn't duplicate all your scoring/state logic — this also makes a third module trivial to add later, which you can mention to judges as your extensibility answer.

---

## PHASE 4 — Local Data + Offline Layer

### Stage 17: Local score storage
Prompt Antigravity: *"Write a C# class that saves quiz/module results (module name, score, timestamp, synced boolean) to a local JSON file using Application.persistentDataPath, with methods to add a result and read all unsynced results."*

### Stage 18: Build the assessment engine
Create a simple MCQ or in-AR decision quiz (5-10 questions per module) that runs after each module. Store per-question correctness plus a final pass/fail threshold (e.g., 70%) in your local JSON.

---

## PHASE 5 — Firebase Integration

### Stage 19: Set up Firebase project
Go to the Firebase Console, create a project, add an Android app with your exact package name from Stage 6, download `google-services.json`, and place it in your Unity `Assets` folder. Import the Firebase Unity SDK (`.unitypackage`) for **Auth**, **Firestore**, and **Storage**.

### Stage 20: Implement login (username/password via Firebase Auth)
Prompt Antigravity: *"Create a Unity UI login screen with username/password fields, a login button, and a back button, that calls Firebase Auth SignInWithEmailAndPasswordAsync, showing an error message on failure."* Use worker ID as the "email" field format (e.g., `workerid@miningsafety.app`) since Firebase Auth needs email-shaped identifiers.

### Stage 21: Sync local results to Firestore
Prompt Antigravity: *"Write a sync manager that checks internet connectivity (Application.internetReachability), and if online, uploads all unsynced local JSON results to a Firestore collection called 'trainingResults', then marks them synced locally."* Trigger this check on app start and periodically (e.g., every 60 seconds) rather than requiring a manual "sync" button.

---

## PHASE 6 — QR Certification

### Stage 22: Generate the certificate QR
Import a free QR code Asset Store package (e.g., "QRCode Encoder/Decoder"). Prompt Antigravity: *"Write a script that takes worker ID, module name, score, and timestamp, converts it to a JSON string, and encodes it as a QR code texture displayed on a UI RawImage."*

### Stage 23: Add signing (even a simple version counts)
For a hackathon-safe approach: write a Firebase Cloud Function (Node.js) that receives the certificate data, signs it with a private key (Node's built-in `crypto` module), returns a signed token, and your Unity app embeds that signed token in the QR instead of raw JSON. Prompt Antigravity for both the Cloud Function code and the Unity-side HTTP call to it.

### Stage 24: Build a verification web page
A simple HTML+JS page (hosted on Firebase Hosting) that reads a QR-decoded token (paste or scan via phone camera using a JS QR library like `jsQR`), calls a verification Cloud Function to check the signature, and displays "Valid Certificate — [Worker], [Module], [Score]" or "Invalid."

---

## PHASE 7 — Localization

### Stage 25: Generate Hindi + Santali audio
Register for Bhashini API access, call the `IITM/TTS` model for your fixed set of module instructions and quiz questions in Hindi and Santali, download the resulting audio files, and import them into Unity's `Assets/Audio` folder as `AudioClip`s.

### Stage 26: Wire language switching
Prompt Antigravity: *"Create a LanguageManager singleton that stores the selected language (Hindi/Santali/English) in PlayerPrefs, and an AudioSource wrapper that plays the correct clip variant based on the current language."* Add a simple language-select screen at app launch.

---

## PHASE 8 — Admin Compliance Dashboard

### Stage 27: Build the dashboard page
A plain HTML/JS page (or React if you have time) hosted on Firebase Hosting, using the Firebase JS SDK to query the `trainingResults` Firestore collection, and Chart.js to show: total workers trained, pass rate per module, and a table of recent certifications.

### Stage 28: Add basic filtering
Add dropdowns to filter by module, date range, or mine/site name (add a `siteName` field to your Firestore documents in Stage 21 to support this) — this is what makes it read as a genuine "compliance dashboard" for DGMS rather than just a results list.

---

## PHASE 9 — Polish, Testing, and Submission

### Stage 29: End-to-end test on a real device with Wi-Fi off
This is your most important test — do the full flow (login → module 1 → module 2 → quiz → certificate QR generated) with the phone in airplane mode, then turn Wi-Fi back on and confirm the sync + dashboard update happens automatically.

### Stage 30: Polish UI and add back-button navigation everywhere
Prompt Antigravity to audit every scene for a consistent back-button pattern and fix any dead-end screens — small polish like this visibly separates a "hackathon prototype" from a "finished-feeling app" to judges.

### Stage 31: Record the demo video
Script it exactly in the order evaluators care about: open with the DGMS fatality statistic → show login → AR module in action (both modules) → quiz → QR certificate generated → scan-and-verify on the web page → dashboard updating live → mention offline-tested and Santali audio explicitly since both are named deliverables.

### Stage 32: Prepare the GitHub repository
Clean commit history, a README with setup instructions, screenshots, and an architecture diagram (Unity app → Firebase → Cloud Functions → Dashboard) — a public, well-documented repo is an explicit deliverable, not optional polish.

---

## Suggested Team Split (if not solo)

| Person | Focus |
|---|---|
| You (Antigravity + Unity) | AR modules (Stages 8-16), core app logic |
| Teammate 1 | Firebase + Cloud Functions + QR signing (Stages 19-24) |
| Teammate 2 | Dashboard + verification web page (Stages 24, 27-28) |
| Teammate 3 | Bhashini audio generation, 3D assets, demo video/pitch (Stages 25-26, 31-32) |

## Order of Priority If Time Runs Short

Do Stages 1-18 first no matter what (a working offline AR + assessment core is non-negotiable). Stages 19-21 (Firebase sync) come next since "offline functionality" is judged by seeing sync work, not just offline mode alone. QR certification (22-24) and localization (25-26) can run in parallel once core AR is stable. The dashboard (27-28) is the safest thing to simplify or cut down in scope if you're tight on time, since it's the least AR-dependent and easiest to explain as a "next step" if partially incomplete.
