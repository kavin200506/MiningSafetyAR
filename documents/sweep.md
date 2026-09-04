# Sweep Mechanic Redesign — Squeeze & Sweep (P.A.S.S. Step 4)

Status: **Design approved, pending implementation.**
Scope: `Assets/Scripts/AR/FireExtinguisherGrabController.cs`, `Assets/Scripts/Modules/GroundFireController.cs`, `Assets/Scripts/Modules/FireSafetyModuleManager.cs`, `Assets/Scripts/UI/Pages/ARSimulationPageController.cs`.

---

## 1. Problem with the current implementation

The current "Sweep" step is not actually driven by player motion. In
`FireExtinguisherGrabController.UpdateFoamSpray()` (lines 1925–1938):

```csharp
if (currentPassState == PassStepState.HandleSqueezed && targetExtinguisher != null)
{
    Vector3 currentPos = targetExtinguisher.transform.position;
    float sweepDelta = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastSweepPosition.x, 0, lastSweepPosition.z));
    sweepAccumulated += (sweepDelta + 0.08f * Time.deltaTime);
    lastSweepPosition = currentPos;

    if (sweepAccumulated >= sweepThreshold && currentPassState == PassStepState.HandleSqueezed)
    {
        currentPassState = PassStepState.SweepComplete;
        OnSweepDetected?.Invoke();
    }
}
```

Two problems:

1. **Free progress regardless of movement.** The `+ 0.08f * Time.deltaTime` term accumulates even if the player never moves at all. With `sweepThreshold = 0.3f`, that means `0.3 / 0.08 ≈ 3.75s` of simply holding squeeze still is enough to "complete" the sweep — no side-to-side motion is actually required.
2. **No direction discrimination.** Even when it does react to movement, it measures raw XZ distance travelled, which doesn't distinguish "walking toward the fire" from "sweeping side to side." Forward approach counts exactly the same as a real sweep.

Net effect: the Sweep step is currently a disguised timer, not a skill check.

---

## 2. Real-world basis (why this redesign, not a stricter gate)

In the real P.A.S.S. technique:

- **Aim** is a one-time, deliberate action: point the nozzle at the *base* of the fire before discharging.
- **Squeeze** is continuous: the lever is open the entire time you're holding it, spring-loaded shut the instant you release.
- **Sweep** is continuous and simultaneous with Squeeze: your arm keeps moving the nozzle side to side across the base of the fire for the whole discharge, gradually advancing as the fire shrinks. Sweeping faster/wider doesn't exist as a "pass/fail" moment in reality — poor technique (standing still, spraying one spot) simply puts the fire out slower and less completely; good technique puts it out efficiently.

That last point is the key design insight: **sweep quality should change the *rate* the fire goes out, not gate a discrete checkbox.** This is both more realistic and removes the awkward question of "what exactly counts as a completed sweep."

---

## 3. New design (conceptual)

- **Squeeze stays exactly as it is today** — touch-and-hold on `Upper_Handle_Grip` / `Lever_Pivot`, on while held, off the instant released. This already matches the real spring-loaded lever correctly.
- **While squeezing and within range of an active fire**, the fire's health drains every frame at a rate made of two parts:
  - a **base rate** that always applies, just from spraying (standing still still works, just slowly — stays forgiving for a nervous first-time trainee).
  - a **sweep bonus rate**, scaled 0–1 by how much genuine left-right motion the player is currently doing, added on top of the base rate.
- There is **no more discrete "Sweep Complete" flag.** The step is proven complete by the fire actually going out (`GroundFireController.OnFireExtinguished`), which now can't happen quickly without real sweeping — it happens naturally instead of being independently declared.
- The player gets real-time visual feedback (fire shrinks faster when swept correctly; optionally a small HUD sweep-intensity indicator) so the causality is felt during training, not hidden behind an invisible timer.

---

## 4. Technical implementation

### 4.1 New motion-sensing logic — `FireExtinguisherGrabController`

**Remove:**
- `private float sweepAccumulated = 0f;` (field, line 106)
- `private float sweepThreshold = 0.3f;` (field, line 108)
- `PassStepState.SweepComplete` enum value (line 34)
- `public event Action OnSweepDetected;` (line 184) and both `Invoke()` call sites (lines 1936, 2106)
- The sweep block inside `UpdateFoamSpray()` (lines 1925–1938)
- `TestSweepComplete()` context-menu method (line 2099) — repurpose as a debug helper that forces `currentSweepIntensity = 1f` for a few seconds, useful for testing the new continuous behavior in-editor.

**Keep** `lastSweepPosition` (line 107) — still needed as the previous-frame position sample, just consumed differently.

**Add:**

```csharp
[Header("Sweep Detection")]
[SerializeField] private float sweepWindowDuration = 0.6f;   // rolling window length (seconds)
[SerializeField] private float idealSweepSpeed = 1.0f;       // m/s of lateral motion for 100% intensity
[SerializeField] private float sweepIntensitySmoothing = 8f; // MoveTowards rate, higher = snappier

private readonly Queue<(float time, float lateralSpeed)> sweepSamples = new();
private float currentSweepIntensity = 0f; // 0..1, smoothed, read every frame by UpdateFoamSpray
```

**New method**, called once per frame from `UpdateFoamSpray()` while `isSqueezing && currentPassState >= PassStepState.HandleSqueezed`, replacing the old block:

```csharp
private void UpdateSweepIntensity()
{
    Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
    if (cam == null || targetExtinguisher == null) return;

    Vector3 currentPos = targetExtinguisher.transform.position;
    Vector3 delta = currentPos - lastSweepPosition;
    lastSweepPosition = currentPos;

    // Project frame-to-frame movement onto the camera's right axis only — this isolates
    // genuine side-to-side motion from forward approach or incidental depth drift.
    float lateralDistance = Mathf.Abs(Vector3.Dot(delta, cam.transform.right));
    float lateralSpeed = Time.deltaTime > 0f ? lateralDistance / Time.deltaTime : 0f;

    sweepSamples.Enqueue((Time.time, lateralSpeed));
    while (sweepSamples.Count > 0 && Time.time - sweepSamples.Peek().time > sweepWindowDuration)
        sweepSamples.Dequeue();

    // Average absolute lateral speed over the window. Using absolute value (not net
    // displacement) means a real left-right-left sweep counts fully even though its
    // net position change is ~zero.
    float avgLateralSpeed = 0f;
    foreach (var sample in sweepSamples) avgLateralSpeed += sample.lateralSpeed;
    if (sweepSamples.Count > 0) avgLateralSpeed /= sweepSamples.Count;

    float targetIntensity = Mathf.Clamp01(avgLateralSpeed / idealSweepSpeed);
    currentSweepIntensity = Mathf.MoveTowards(currentSweepIntensity, targetIntensity, Time.deltaTime * sweepIntensitySmoothing);
}
```

**Call site change** inside `UpdateFoamSpray()` — call `UpdateSweepIntensity()` near the top (before the fire-suppression loop, so this frame's intensity is current), and change the existing suppression call:

```csharp
// was: fire.ApplyFoamSuppression(fire.transform.position, Time.deltaTime);
fire.ApplyFoamSuppression(fire.transform.position, Time.deltaTime, currentSweepIntensity);
```

Reset `currentSweepIntensity = 0f` and clear `sweepSamples` in `StartSqueezing()` (line 1509) and wherever `sweepAccumulated = 0f` is currently reset (line 2013), so intensity doesn't carry over between grabs or between separate squeeze presses.

### 4.2 Variable suppression rate — `GroundFireController`

**Add** a new tunable alongside the existing `foamPower`:

```csharp
[SerializeField] private float foamPower = 25f;      // existing — base suppression rate (HP/sec) at zero sweep intensity
[SerializeField] private float sweepBonusRate = 50f;  // new — extra suppression (HP/sec) at full (1.0) sweep intensity
```

At `foamPower = 25`, `sweepBonusRate = 50` (2× `foamPower`), full sweeping drains health at `25 + 50 = 75` HP/sec vs. `25` HP/sec standing still — exactly a **3× speed-up** for correct technique (`sweepBonusRate` is set to `2×foamPower` to get a clean 3× total; if the ratio ever needs to change, scale `sweepBonusRate` relative to `foamPower`, not in isolation — total multiplier = `1 + sweepBonusRate/foamPower`).

Target feel: **if a well-swept fire goes out in ~10s, the same fire sprayed without sweeping should take ~30s.** The 3× ratio holds regardless of the absolute numbers; hitting that exact pair specifically is a matter of tuning `maxFireHealth` and `foamPower` together (see §6) — done as a separate pass once this is playable, since it doesn't affect the ratio itself.

**Modify** `ApplyFoamSuppression`:

```csharp
// was: public void ApplyFoamSuppression(Vector3 hitPoint, float deltaTime)
public void ApplyFoamSuppression(Vector3 hitPoint, float deltaTime, float sweepIntensity = 0f)
{
    if (!isFireActive || currentFireHealth <= 0f) return;

    float rate = foamPower + sweepBonusRate * Mathf.Clamp01(sweepIntensity);
    currentFireHealth -= rate * deltaTime;
    currentFireHealth = Mathf.Max(0f, currentFireHealth);

    // ...existing visual scaling / normalizedHealth logic unchanged...

    if (currentFireHealth <= 0f)
    {
        ExtinguishFireInstant();
        OnFireExtinguished?.Invoke();
    }
}
```

The default parameter (`sweepIntensity = 0f`) keeps this source-compatible with any other caller that doesn't care about sweep (falls back to base rate only).

### 4.3 Retiring the old completion event — `FireSafetyModuleManager`

`FireSafetyModuleManager` already subscribes to **both** `FireExtinguisherGrabController.OnSweepDetected` and `GroundFireController.OnFireExtinguished` (lines 84 and 90). Its `HandleFireExtinguished()` handler already does this:

```csharp
private void HandleFireExtinguished()
{
    if (isModuleActive)
    {
        while (isModuleActive && currentStepIndex < totalSteps)
        {
            CompleteCurrentStep();
        }
    }
}
```

This already completes **every remaining step** (including step index 4, "Squeeze & Sweep") the moment the fire goes out — it fully subsumes what `HandleSweepDetected()` was doing. So removal is a clean deletion, no replacement logic needed:

- Delete the `OnSweepDetected += / -=` subscription lines (84, 100).
- Delete the `HandleSweepDetected()` method (lines 316–328) entirely.

### 4.4 UI follow-up — `ARSimulationPageController`

Line 401 has a `switch` case on `PassStepState.SweepComplete` (used to pick instruction text). Since that enum value goes away:

- Delete the `case AR.FireExtinguisherGrabController.PassStepState.SweepComplete:` branch.
- The `HandleSqueezed` case's instruction text (line 397) should be updated to actively coach the new behavior, e.g. *"Keep spraying — sweep side to side at the base of the fire!"* instead of whatever static "squeezing" text it currently shows, since squeezing and sweeping are now simultaneous for the whole discharge rather than sequential steps.

### 4.5 Real-time feedback (recommended, not required for first pass)

Optional but recommended for a training app: expose `currentSweepIntensity` (e.g. via a public getter) and drive a small HUD element in `ARSimulationPageController` — a horizontal gauge or pulsing left-right arrow — so a trainee standing still gets an immediate visual nudge rather than just wondering why the fire isn't shrinking. Reuses the existing alert-banner system already in that controller. Can also be added to the existing `OnGUI` debug fire-health bar (~line 2189) as a quick first pass for internal testing before building real UI.

---

## 5. What this changes for scoring (context, not in scope yet)

Not being implemented now, but worth noting for later: this mechanic quietly produces exactly the data a good "Squeeze & Sweep" score needs, without any separate error-tracking:

- **Time to extinguish** — already a natural byproduct; bad technique (low average intensity) directly produces a slower time.
- **Average sweep intensity** over the discharge — can be accumulated in `FireExtinguisherGrabController` alongside `currentSweepIntensity` for a post-drill quality metric.
- **% of time near-zero intensity** — flags a trainee who mostly stood still, useful for coaching feedback in the results screen.

No design decisions needed here yet — flagged only so the intensity data is captured in a form the later scoring work can reuse.

---

## 6. Open tuning parameters (adjust after first in-hand playtest, not blocking implementation)

| Parameter | Location | Starting value | Purpose |
|---|---|---|---|
| `idealSweepSpeed` | `FireExtinguisherGrabController` | `1.0` m/s | Lateral speed that counts as "full" sweep intensity |
| `sweepWindowDuration` | `FireExtinguisherGrabController` | `0.6` s | Rolling window for smoothing/averaging lateral speed |
| `sweepIntensitySmoothing` | `FireExtinguisherGrabController` | `8` | How fast intensity ramps up/down (higher = snappier, lower = smoother) |
| `foamPower` (base rate) | `GroundFireController` | `25` HP/sec | Suppression rate while squeezing with zero sweep |
| `sweepBonusRate` | `GroundFireController` | `50` HP/sec | Extra suppression rate at full sweep intensity (→ exactly 3× total vs. standing still) |
| `maxFireHealth` | `GroundFireController` | `40` (existing) | Together with `foamPower`, sets the absolute extinguish time (currently ~1.6s unswept — needs a separate tuning pass to land nearer the ~10s/~30s target feel) |

These are Inspector-serialized fields, so they can be tuned live during playtesting without touching code.
