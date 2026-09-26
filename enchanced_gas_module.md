# Enhanced Plan — Module 2: Gas Leak & Confined Space Protocol

This replaces `gas_module.md`. That file was a great *narrative* (the alarm → withdraw → check-atmosphere → branch decision tree) but on its own it didn't fully cover what the SIH problem statement actually asks for, and it said nothing about the environment, the physical interactions, or the 3D assets. This document fixes all three.

---

## 1. The Scenario — what the worker actually experiences

The worker puts on the headset/phone and is dropped into a **coal mine tunnel**, not a menu. They're doing an ordinary task (a prop pickaxe/ore tap, just for immersion). Their multi-gas detector is clipped to their belt, their SCSR pouch is on their belt too — both are just *there*, doing nothing, exactly like real PPE that sits unused until it's needed.

Then the alarm goes off. From here it's **one continuous physical walk-through**, not a quiz:

1. They physically **look down** at their belt (tilt the phone down) to actually read the detector instead of a menu popping up for them.
2. They **hold down a radio call** (press-and-hold, like a real push-to-talk) to report the alarm before doing anything else.
3. They **physically walk** (real steps in their room, tracked the same way the Fire module already tracks steps) down the tunnel toward the marked escape route.
4. Partway down the escape corridor, they **look down at the detector again** — this is the actual decision point. The reading is either fine or bad.
5. If fine → they just keep walking out. No mask, nothing else happens. That restraint *is* the lesson.
6. If bad → they must **physically raise the phone toward face height and hold it there** (the donning motion) — and they have to pick the SCSR specifically from beside a decoy dust mask, not just tap "yes." Only then do they keep walking through the hazy section.
7. On the way out they pass a **ventilation stopping/hatch** — a confined-space transition point — and have to acknowledge/seal it, which is where the "Confined Space Protocol" half of this module's name actually gets satisfied.
8. They reach the lit exit sign / fresh-air refuge point and the drill ends.

Nothing in this is "select the correct option from a list." Every gate is something the worker's body actually does — tilt, hold, walk, raise — the same design language the Fire module already uses (shake-to-sweep, physically walk to the safe distance).

---

## 2. Full flow, step by step

| # | What happens in the mine | What the worker physically does | What gets scored |
|---|---|---|---|
| 0 | Normal work — ambient mine sounds, idle prop interaction | Nothing required; optional tap on a pickaxe/ore prop | Not scored — immersion only |
| 1 | 🚨 Gas detector alarm fires (audio + haptic buzz) | **Tilt phone down** past a pitch threshold to "look at" the belt-mounted detector and reveal its reading | Reaction time → feeds Hazard Recognition |
| 2 | Worker must report before moving | **Press-and-hold** a radio/PTT control for ≥1s (release early = "signal not sent") | Emergency Response competency; gates step 3 |
| 3 | Begin withdrawal down the tunnel | **Physically walk** — real-world step displacement (reusing the Fire module's AR-camera-displacement step tracker) advances the worker down the modeled tunnel corridor toward a waypoint arrow | Time Management; wrong-direction walk = mistake |
| 4 | Reach the escape-route checkpoint | **Tilt phone down** again to take a second detector reading (O₂ / CH₄ / CO shown in-world next to the device, not a flat UI popup) | Hazard Recognition |
| 5a | Branch A — atmosphere OK | Just keep walking; **do NOT** don the SCSR | Correct restraint scores full PPE-selection marks; donning here anyway is a mistake ("unnecessary PPE donning") |
| 5b | Branch B — atmosphere unsafe | **Tap the SCSR** (not the decoy dust mask sitting next to it) → **raise phone to face height and hold ~2s** to don it → then keep walking through the hazy section | PPE Selection competency; wrong item picked = `MissedPpeCheck` (already defined) |
| 6 | Pass the ventilation stopping/hatch (confined-space transition) | **Tap the hatch/stopping** to seal it behind them before continuing | Confined-space protocol adherence — new mistake tag if skipped/rushed |
| 7 | Reach fresh air / refuge point | Arrive within the marked radius and **stay put ~1.5s** (same "sustain in radius" arrival check the Fire module's evacuation step already uses) | Evacuation competency |
| 8 | Drill complete | — | Final drill score → feeds the same 70% drill / 30% quiz formula every other module uses |

---

## 2A. How each gesture actually gets detected on the phone — not narrative, real sensor data

This is the part that makes the difference between "feels real" and "is actually a menu wearing a costume." Every gesture below reads real AR Foundation camera-pose data — the same 6-DOF pose ARCore already derives on-device from the camera + IMU (accelerometer/gyro) fused together — which is the exact same data source the Fire module already uses live, on real phones, for its sweep-intensity scoring and evacuation-distance checks. Nothing here needs a new sensor API; it needs new *thresholds* applied to a data source this app already reads every frame.

| Gesture | Real data read every frame | Detection logic | Proven precedent already in this codebase |
|---|---|---|---|
| **Tilt phone down** (check detector) | `Camera.main.transform.forward` (or `.eulerAngles`) — the AR camera's real-world orientation, driven by ARCore's visual-inertial tracking on-device | Compute downward pitch: `float pitch = Mathf.Asin(-cam.transform.forward.y) * Mathf.Rad2Deg;` — if `pitch > thresholdDegrees` (e.g. 30–40°) and held for a short sustain window (~0.5s, to reject a quick flick), fire "detector checked" | `FireSafetyModuleManager.BeginEvacuation()` already reads `cam.transform.position`/`.forward` every frame to compute retreat direction and distance — same camera-transform read, different formula |
| **Physically walk** (withdrawal) | `Camera.main.transform.position`, sampled every `Update()` | Horizontal (X-Z) displacement between frames, accumulated into `totalDistanceWalkedMeters`, divided by `averageStepLengthMeters` to get a step count — literally the existing algorithm | `ARStepCounterTracker.TrackPhysicalWalkingSteps()` — this exact code already exists, already runs on real Android hardware for the Fire module's "walk 7–13 steps to find the wall-mounted extinguisher" mechanic. The gas module reuses this unchanged, just pointed at a longer corridor and gated to tunnel-segment progress instead of a fixed step target |
| **Raise phone to face height, hold** (don SCSR) | `Camera.main.transform.position.y`, relative to a baseline Y captured when the drill/step started | `float raise = cam.transform.position.y - baselineY;` — if `raise > thresholdMeters` (e.g. 0.15–0.25m) sustained for ~2s, donning completes; releasing early or never raising = mistake | Same "sustained arrival" primitive as `FireSafetyModuleManager.UpdateEvacuationCheck()` (`evacuationSustainedSince` timer, `evacuationSustainDuration`), just measured on the Y-axis instead of horizontal distance-to-target |
| **Press-and-hold radio/PTT** (buddy signal) | Standard touch input (`PointerDown`/`PointerUp` via the Input System, already used throughout the UI Toolkit screens), not an AR sensor at all | Hold duration ≥ 1s before release = signal sent; release early = `RegisterMistake` | Standard UI hold-button pattern, no new mechanism needed |

**Important honesty point:** these are all currently *proposed* thresholds (30–40°, 0.15–0.25m, 1–2s holds) — they haven't been tuned or tested on a real device for this module yet. The Fire module's equivalents (sweep intensity threshold, evacuation arrival radius) were tuned by actually running the drill on hardware, per `documents/technical_scoring_explained.md`. The gas module's numbers will need the same on-device tuning pass before they can be called "done" — the mechanism is proven, the specific numbers aren't yet.

**One real risk to flag now, not later:** ARCore's camera-pose tracking can drift or momentarily lose confidence (poor lighting, fast motion, textureless walls) — this is a known limitation of visual-inertial tracking on any phone, not specific to this app. All three AR-based gestures above inherit that risk. This is exactly why the Fire module's evacuation check already uses a *sustain* window instead of a single-frame check (see `evacuationSustainDuration`) — it absorbs a one-frame tracking hiccup instead of failing the worker for it. The gas module's thresholds above are written to use the same sustain pattern for the same reason.

---

## 3. PS requirement coverage — does this satisfy everything asked?

| PS / roadmap requirement | Where it's satisfied now |
|---|---|
| Hazard-zone recognition (tap to identify gas leak source) | Reframed as **detector-reading recognition** (steps 1 & 4) — arguably stronger than a single tap, since it's tested twice under different conditions |
| PPE selection from an AR-presented set of options | Step 5b — SCSR sits beside a decoy dust mask; worker must choose correctly, *and* choosing to not-equip in Branch A is itself correct behavior |
| Buddy-system, sequence-based interaction (signal before entering) | Step 2 — hold-to-signal radio call is a hard gate before withdrawal can even begin |
| "Confined Space Protocol" (the module's own name) | Step 6 — the ventilation stopping/hatch tap. This was **completely missing** from both the old `gas_module.md` draft and, until now |
| Consistent with Module 1's structure (shared base class, AR Foundation, competency scoring) | Reuses `BaseModuleManager` pattern via a richer subclass (like `FireSafetyModuleManager`), same competency fields, same `MistakeTags` system |

Nothing here is invented busywork — every added piece (buddy gate, PPE decoy, hatch) was a real gap identified against the PS wording and the existing `GasLeakModuleManager.cs`/`MistakeTags.cs`, not a nice-to-have bolted on top.

---

## 4. Environment design — making it feel like being underground

**Note up front:** I read the file list for `demo_vid/vidssave.com Mine 360P.mp4` but I can't watch video content directly — I can only reason from its filename/existence, not its visuals. If there's a specific look (lighting, tunnel width, prop density) from that video you want matched, tell me and I'll match it explicitly, or I can extract still frames from it as images if that would help me see the reference.

**The good news: the environment-switching mechanism already exists in this repo**, uncommitted but working:

- `SimulationEnvironmentController.cs` (new, untracked) already does exactly "hide the real camera passthrough, show a virtual 3D environment instead, anchor it to wherever the player is currently standing and facing." It was clearly built with this in mind — it's environment-agnostic, just needs a Mine-tunnel root object instead of whatever room it currently points at.
- `ARPlacementManager.SimulationMode` already redirects every raycast (placement, reticle, wall-scan) from real AR planes to **Physics colliders on the virtual Mine environment** — so tap-to-interact and surface-detection logic doesn't need to change at all, it just needs colliders on the tunnel prefabs.
- `ARStepCounterTracker.cs` already converts real-world 6-DOF camera displacement into "steps," used today to make the Fire module's extinguisher-search phase require *actually walking around your room*. The gas module's withdrawal (step 3) is the same mechanic pointed at a longer distance, with the character advancing down a tunnel corridor instead of searching a room.

**What's new to build:** assembling `Assets/Mine`'s modular tunnel kit (`Tunnel1/2/3`, `TurnL/TurnR/Turn3`, `RampUp/RampDown`) end-to-end into one continuous corridor for the new `gas_module.unity` scene: normal-work alcove → alarm point → escape corridor (split into a "clear" segment and a "hazy" segment, whichever branch the worker's detector reading sends them down) → hatch/stopping prop → refuge point marked with the exit sign model. This is a level-layout task, not new engineering — the raycast/anchoring/step-tracking plumbing is already there.

---

## 5. 3D models / assets — full inventory, down to individual meshes

### 5.1 Already available — reuse as-is, zero new modeling

| Asset | File | Use in this module |
|---|---|---|
| Tunnel segments | `Assets/Mine/Models/Base/Tunnel1.fbx`, `Tunnel2.fbx`, `Tunnel3.fbx` | Corridor segments — normal work area, escape route |
| Junctions | `TurnL.fbx`, `TurnR.fbx`, `Turn3.fbx` | Branch points / direction changes along the escape route |
| Elevation | `RampUp.fbx`, `RampDown.fbx` | If the refuge point sits on a different level |
| Ventilation stopping | `Props/Hatch.fbx` (+ prefab) | **Doubles as the confined-space transition prop for step 6** — no new model needed |
| Set dressing | `Props/Crate.fbx`, `Support1.fbx`, `Support2.fbx`, `Planks.fbx` | Timber supports, stacked crates — realism only |
| Ambient light prop | `Props/Lantern.fbx` | Light source along the corridor |
| Idle-task prop | `Props/Pickaxe.fbx` | Step 0's optional "normal work" tap |
| Wall/floor dressing | `Rocks/Rock1.fbx`, `Rock2.fbx`, `Rocks/Ore.fbx` (+ Red/Green/Blue ore materials) | Visual texture of the tunnel walls |
| Refuge/exit marker | `Assets/Prefabs/ExitSignModel.prefab` | Already built for Module 1 marker tracking — reused as-is for the fresh-air refuge marker in step 7 |

### 5.2 Already available — reusable VFX (particle systems, not meshes)

Found in `Assets/Vefects/Free Fire VFX URP/` — built for the Fire module but generic enough to reuse for the gas haze with a recolor, not a rebuild:

| Asset | Reuse |
|---|---|
| `Particles/VFX_Fire_01_Medium_Smoke.prefab`, `VFX_Fire_Floor_01_Smoke.prefab` | Base for the "unsafe atmosphere" haze in the Branch-B corridor segment — retint the material from smoke-grey toward a sickly yellow-green and lower opacity/density for a haze rather than thick smoke |
| `Materials/M_VFX_Dust_02.mat`, `Textures/T_VFX_Dust_01.tga` | Ambient dust motes for the whole tunnel, unrelated to the hazard branch — cheap atmosphere |
| `Materials/M_VFX_Heat_Haze_01/02.mat`, `Shaders/SH_Vefects_VFX_URP_Heat_Haze_01.shader` | Could double as a subtle shimmer/distortion on the hazy segment if you want more than just particles |

This means the gas-haze visual cue needs **art direction (recolor + retune), not new VFX authoring from scratch**.

### 5.2A ✅ Required props obtained — verified in `Assets/Prefabs/Gas_Module/`

All three required hand-held props from §5.3 below are now in the project and correctly recognized by Unity's importers:

| File | Format | Verified contents | Status |
|---|---|---|---|
| `multi_gas_detector_-_low_poly.glb` | GLB, glTFast ScriptedImporter confirmed (`com.unity.cloud.gltfast 6.10.2` is installed) | `DetectorBody` mesh + `Buttons` mesh (plus small Cube submeshes), one baseColor texture. Simpler than the full §5.3-A breakdown (no separate screen/bumper/nozzle/LED/clip meshes) but sufficient — the reading display can be a world-space UI canvas rather than modeled geometry, as already noted | ✅ Ready to use |
| `gasmask/model.glb` | GLB, glTFast ScriptedImporter confirmed | This is a **full filtering respirator/gas mask** (glass eyepiece lens + gunmetal/olive/black material variants), not a plain N95 dust mask as originally spec'd | ✅ Ready to use — see framing note below |
| `SCSR_Training.fbx` | Native FBX import | Exceptionally well-built for this: separate named meshes for **both states** — stowed (`SCSR_CaseBody`, `SCSR_CaseLid`, `SCSR_BeltLoop`) and donned (`SCSR_Mouthpiece`, `SCSR_NoseClip`, `SCSR_CanisterBody`, `SCSR_Hose`, `SCSR_NeckStrap`), each with its own material (`SCSR_CaseMaterial`, `SCSR_CanisterMaterial`, `SCSR_MetalMaterial`, `SCSR_PlasticMaterial`, `SCSR_RubberMaterial`, `SCSR_StrapsMaterial`) | ✅ Ready to use — matches the §5.3-B spec almost exactly, can toggle case-open/donning purely by enabling/disabling the right child objects, no new rigging needed |

**One content-framing note, not a blocker:** since the decoy turned out to be a full gas mask/respirator rather than a plain dust mask, update the wrong-choice feedback line accordingly — the real safety point is even sharper this way: *"A filtering respirator only filters contaminants out of the air you breathe — it does not supply oxygen. In an oxygen-deficient or IDLH atmosphere it is not sufficient. Use the SCSR."* This also directly counters the instinct a lot of workers might have ("gas leak → grab the gas mask"), which is arguably a better teaching moment than a dust mask would have been.

### 5.3 Must be newly modeled — full mesh breakdown (superseded for A/B/C by §5.2A above — kept for reference on the optional D/E props)

None of these exist anywhere in the project (`Assets/Mine`, `Assets/Prefabs`, `Assets/Firebase`, `Assets/ExternalDependencyManager` were all searched). Each can start as a primitive-built placeholder exactly like `ARStepCounterTracker.SpawnExtinguisherOnWall()` already does for the fire extinguisher fallback (cylinder body + cube handle + sphere pin + cylinder nozzle) — so none of this blocks scene work while real models are sourced.

**A. Multi-gas detector** (handheld, belt-clipped — steps 1 & 4)
Real reference: MSA Altair / Dräger X-am style portable detector.

| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `Detector_Body` | Rounded box / cube (scaled) | Main chassis, ~10×6×3cm real-world scale |
| `Detector_RubberBumper` | Slightly larger rounded box, offset outward | Contrasting color (yellow/black), sits around the body edge |
| `Detector_Screen` | Flat quad/plane inset on the front face | This is where O₂/CH₄/CO readings render — can be a world-space UI canvas rather than geometry |
| `Detector_Buttons` ×2–3 | Small cylinders/capsules | Below the screen |
| `Detector_IntakeNozzle` | Small cylinder protruding from the top | Gas sensor inlet |
| `Detector_StatusLED` | Tiny sphere | Emissive material, blinks red on alarm |
| `Detector_BeltClip` | Thin bent box/plane on the back | Attaches to the belt anchor point |

**B. SCSR — Self-Contained Self-Rescuer** (step 5b — needs both a stowed and a donned state)
Real reference: CSE SR-100 / Ocenco EBA style filter/oxygen self-rescuer.

*Stowed (on belt, closed case):*
| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `SCSR_CaseBody` | Rounded rectangular box | Hard case, DGMS-standard colour (often yellow/orange) |
| `SCSR_CaseLid` | Thin box, hinged | Can be a separate mesh if you want it to visually "pop open" on donning |
| `SCSR_BeltLoop` | Thin curved strip | Attaches to belt anchor |

*Donned (worn/active — shown rising into first-person view during the raise-to-face gesture):*
| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `SCSR_Mouthpiece` | Small tube/capsule | What the worker "bites" — the focal point of the donning animation |
| `SCSR_NoseClip` | Two small curved prongs | Pinches the nose — simple bent-cylinder pair |
| `SCSR_CanisterBody` | Cylinder | The oxygen-generating/scrubber canister, hangs at chest level |
| `SCSR_Hose` | Thin capsule/cylinder chain | Connects mouthpiece to canister |
| `SCSR_NeckStrap` | Thin torus/curved strip | Holds the mouthpiece assembly in place |

**C. Decoy dust mask** (step 5b — the deliberately-wrong choice)
Real reference: standard N95-style cup respirator. Deliberately kept simple/cheap since it's supposed to *look* insufficient next to the SCSR.

| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `DustMask_Cup` | Hemisphere/dome | Main mask body |
| `DustMask_NoseFoam` | Thin strip along the top edge | Cosmetic only |
| `DustMask_EarStraps` ×2 | Thin curved cylinders | Simple elastic bands |
| `DustMask_ExhaleValve` | Small flat disc, front-center | Optional, cosmetic |

**D. Optional — radio/PTT handset**, only if you want a real 3D prop instead of a screen-space UI button for step 2 (current plan defaults to UI-only since it's usually chest-mounted and off-screen):

| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `Radio_Body` | Rounded box | |
| `Radio_Antenna` | Thin tapered cylinder | |
| `Radio_PTTButton` | Small cylinder on the side | |
| `Radio_BeltClip` | Thin bent bracket | |

**E. Optional — standalone isolation valve**, only needed if you want the confined-space beat (step 6) to be a distinct valve-wheel interaction instead of just the hatch tap (the existing `GasLeakModuleManager.OnIsolationValveClosed()` stub implies this was once planned separately):

| Mesh part | Primitive equivalent | Notes |
|---|---|---|
| `Valve_Wheel` | Torus with 4–6 radial spokes (or a flattened cylinder with cross-bars) | The part the worker would tap-and-turn |
| `Valve_Housing` | Short cylinder/pipe fitting | Where the wheel mounts, sits on the tunnel wall near the hatch |

### 5.4 Summary

Everything in §5.1/§5.2 means **the corridor and its hazard-branch visuals can be built today with zero new art**. The real asset-sourcing work is three small hand-held/worn props (detector, SCSR, decoy mask) — each 4–7 simple meshes, all primitive-shaped, all mobile-poly-count-friendly — plus two optional props (radio, standalone valve) only needed if you don't want to fall back to UI/hatch-reuse for those two beats.

### 5.5 Delivery format — match the existing `Assets/Mine` pipeline exactly

Whoever models these should deliver in the same format every existing Mine asset already uses, so it drops straight into the pipeline with no conversion step:

| Thing | Format/convention | Matches |
|---|---|---|
| Mesh | **`.fbx`** | Every model in `Assets/Mine/Models/**` is FBX — no exceptions, don't send `.blend`/`.obj`/`.gltf` |
| Textures | **`.png`**, one file per map, suffixed by map type | Existing convention: `_AlbedoSmoothness` (or `_Albedo` / `_AlbedoTransparency` if the part needs alpha), `_Normal`, `_MetallicSmoothness`, `_AO`, `_Height` — see `Rock1_AlbedoSmoothness.png` / `Rock1_Normal.png`, or `Hatch_AlbedoTransparency.png` / `Hatch_MetallicSmoothness.png` / `Hatch_Normal.png` as the two patterns already in use |
| Material | **`.mat`**, URP Lit shader | One material per distinct surface (e.g. `Detector.mat`, `SCSR_Case.mat`, `DustMask.mat`), same as `Crate.mat`/`Lantern.mat`/etc. |
| Scale | **Real-world meters, 1 Unity unit = 1 meter, modeled at true size** | This one matters — the fire extinguisher FBX was imported at ~1.6m tall unscaled and had to be corrected in code (`extinguisherSpawnScale = 0.42f` in `ARStepCounterTracker`). Model these at their actual real-world size up front (detector ~10×6×3cm, SCSR case ~15×10×8cm, dust mask ~12×8×6cm) so no compensating scale hack is needed later |
| Pivot/origin | **At the logical attach point, not mesh center** | Belt-worn props (detector, SCSR case, dust mask, radio) should have their pivot where they'd clip to the belt, not their geometric center — spawn/anchor code positions objects by their pivot directly, same as every existing prefab here |
| Hinges (case lid, valve wheel) | Model as a **separate mesh from its own pivot point**, no skeletal rig needed | These only need a `Transform.Rotate`-style hinge animation in code, not a bone/skin — keep it a rigid multi-part mesh like `Hatch.fbx` already is |
| Folder placement | `Assets/Mine/Models/Props/<Name>.fbx` + `Assets/Mine/Models/Props/Materials/<Name>_*.png`, prefab in `Assets/Mine/Prefabs/Props/<Name>.prefab` | Matches where `Crate`, `Ladder`, `Lantern`, `Pickaxe` already live |
| Poly budget | Roughly **500–2,000 tris per prop** (all primitive-shaped, no organic detail needed) | Android target is API 29+, mid-range phones — keep it light, consistent with the low-poly look of the existing Mine kit |

---

## 6. Scoring & data model

Follows the same competency framework already defined in `MiningSafetyAR.md` §5 and used by `FireSafetyModuleManager` (richer than the current flat `GasLeakModuleManager`/`BaseModuleManager` pattern):

- **Hazard Recognition** — detector-check reaction time (steps 1 & 4)
- **PPE Selection** — correct SCSR pick in Branch B, correct *non*-donning in Branch A
- **Emergency Response** — buddy/radio signal timing (step 2)
- **Evacuation** — arrival + lateness at the refuge point (step 7), same formula as Fire's `CompleteEvacuation()`
- **Time Management** — total drill time vs. par, same formula as Fire's `ComputeTimeScore()`

Existing `MistakeTags.cs` entries already cover part of this (`WrongHazardLocalization`, `MissedPpeCheck`, `UnsafeZoneEntry`). Two new tags will be needed for the new gates this plan adds:
- `withdrawal_without_signal` — walked before completing the buddy radio call
- `unnecessary_ppe_donning` — donned the SCSR in Branch A when the atmosphere was fine

---

## 7. Code/scene artifacts this implies (not built yet — just the punch list)

- New scene: `Assets/Scenes/gas_module.unity`
- Rewrite `GasLeakModuleManager.cs` from its current flat 4-step sequence into a branching state machine (mirroring `FireSafetyModuleManager`'s richer pattern, not `BaseModuleManager`'s generic one)
- New controllers, analogous to the Fire module's `FireExtinguisherGrabController` / `GroundFireController`:
  - `MultiGasDetectorController` (tilt-to-check gesture + reading display)
  - `SCSRDonningController` (decoy selection + raise-to-face gesture)
  - `BuddySignalController` (hold-to-signal gate)
  - A generalized version of `ARStepCounterTracker`'s walk-distance tracking (currently fire-specific) for the withdrawal corridor
- Reuse as-is: `ARWaypointNavigationPointer`, `ARPlacementManager.SimulationMode`, `SimulationEnvironmentController`

---

## 8. Open questions before implementation starts

- Placeholder-vs-real assets: for the detector/SCSR/decoy-mask, start with primitive-built placeholders (fast, unblocks the scene now) and swap in real models later, or wait for real models first?
- Should the ventilation stopping/hatch tap (step 6) carry its own scoring weight, or just be a narrative beat with no penalty for skipping quickly?
- Confirm the buddy signal (step 2) should be a UI hold-button rather than a 3D radio prop, given it'd usually be off-screen on the worker's chest.
