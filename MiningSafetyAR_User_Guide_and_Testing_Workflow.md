# MiningSafetyAR — User Guide & Testing Workflow

## Quick Setup & Scene Configuration Guide

### 1. Automated 1-Click Scene Setup
Before building or running the project in Unity Editor:
1. Open the project in **Unity 6000.3.23f1 LTS**.
2. In the top Unity menu, click **`Mining Safety AR` -> `Setup Core AR Scene`**.
3. The editor tool will automatically:
   - Configure your scene hierarchy (`AR Session`, `XR Origin`, `AR Camera`, `TrackedPoseDriver`).
   - Assign the **`AR Default Plane.prefab`** with semi-transparent cyan URP material.
   - Assign **`DogPrefab_Parent.prefab`** as the default plane placement model.
   - Programmatically build **`MiningSafetyImageLibrary.asset`** (`FireExtinguisherMarker` & `ExitSignMarker` textures with `0.2m` physical width).
   - Verify URP `ARBackgroundRendererFeature` across all mobile and PC renderer assets.
   - Verify `activeInputHandler = 1` (**Input System Package (New)**) for Android build player compatibility.

---

## Testing Workflows

### Workflow A: AR Plane Detection & Object Placement (3D Dog Model)
1. Open scene **`Assets/Scenes/AR Plane Detection Placement.unity`** (or `SampleScene.unity`).
2. Build and run on your Android device (**File -> Build And Run**).
3. **Scan Environment**: Point the camera towards a flat floor or table surface. Blue semi-transparent AR plane meshes will visualize automatically.
4. **Tap to Place**: Tap anywhere on the detected surface plane.
   - The animated **3D Dog model** (`DogPrefab_Parent.prefab`) will instantiate and lock to the physical surface via an `ARAnchor`.
   - Subsequent taps will smoothly reposition and re-anchor the 3D model.

---

### Workflow B: AR Image Marker Tracking (Fire Extinguisher & Exit Sign)
1. Open scene **`Assets/Scenes/AR Image Tracking.unity`** (or `SampleScene.unity`).
2. **Display Markers**:
   - Open **`Assets/ImageTracking/FireExtinguisherMarker.jpg`** or **`Assets/ImageTracking/ExitSignMarker.jpg`** on a computer/laptop screen, or print them on paper (`0.2m` width).
3. **Point Camera at Marker**:
   - Point your phone camera directly at the image marker on the screen.
4. **Real-Time AR Augmentation**:
   - **`FireExtinguisherMarker`**: Triggers real-time tracking logs and displays a prominent **Vivid Safety Red** 3D equipment model anchored over the marker.
   - **`ExitSignMarker`**: Triggers real-time tracking logs and displays a prominent **Vivid Emerald Green** 3D exit sign model anchored over the marker.
5. **Tracking Recovery**:
   - Cover or point the camera away from the marker -> The app logs a tracking loss warning and hides the 3D model.
   - Point back at the marker -> The 3D model cleanly reappears and resumes smooth tracking.

---

## Live ADB Device Log Streaming

To monitor live diagnostic logs (`[DIAG]`, `[INFO]`, `[WARN]`, `[ERROR]`) from your connected Android phone:

Run the included shell script from your terminal:
```bash
./stream_device_logs.sh
```

This streams live Unity device logs into `device_logs.txt` while keeping the file cleanly capped at the last 500 lines.

### Key Log Diagnostics to Look For:
- `[DIAG] [ARPlacementManager] Initializing official AR Foundation sample architecture`
- `[DIAG] [ARPlacementManager] NEW PLANE DETECTED! ID: ...`
- `[DIAG] [ARPlacementManager] New Input System Tap Detected at (X, Y)`
- `[ARImageTrackingManager] Tracking marker: FireExtinguisherMarker at (X, Y, Z)`
