using UnityEngine;
using MiningSafetyAR.UI;
using MiningSafetyAR.Modules;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Attached to the spawned Alarm Button model. Detects a tap/click on the button's
    /// collider and toggles the red edge alert overlay, plays SFX, turns on a 3D red alarm
    /// warning light, depresses the button model, and notifies the fire safety module.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class AlarmButtonInteractable : MonoBehaviour
    {
        [SerializeField] private AudioClip alarmSfx;
        private AudioSource audioSource;
        private Light statusLight;
        private Vector3 initialLocalPos;
        private bool isAlarmActive = false;

        private void Start()
        {
            initialLocalPos = transform.localPosition;

            // Ensure a BoxCollider is attached for touch raycasting
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
                Debug.Log($"[ALARM_DIAG] ➕ Added missing BoxCollider to AlarmButton '{gameObject.name}'");
            }

            // Ensure a 3D red warning light is attached
            statusLight = GetComponent<Light>();
            if (statusLight == null)
            {
                statusLight = gameObject.AddComponent<Light>();
                Debug.Log($"[ALARM_DIAG] 💡 Attached 3D Light component to AlarmButton '{gameObject.name}'");
            }

            if (statusLight != null)
            {
                statusLight.type = LightType.Point;
                statusLight.color = new Color(1f, 0.1f, 0.1f);
                statusLight.range = 1.5f;
                statusLight.intensity = 3.5f;
                statusLight.enabled = false;
            }

            Debug.Log($"[ALARM_DIAG] 🚨 ALARM BUTTON INITIALIZED! Name='{name}' | WorldPos={transform.position} | Layer={gameObject.layer} | LightAttached={(statusLight != null)} | ColliderCenter={col.center} | ColliderSize={col.size}");
        }

        private void OnMouseDown()
        {
            Debug.Log($"[ALARM_DIAG] 🖱️ OnMouseDown event triggered on '{gameObject.name}'");
            ToggleAlarmState();
        }

        public void ToggleAlarmState()
        {
            isAlarmActive = !isAlarmActive;

            // 1. Toggle Fullscreen Red Edge Alert UI Overlay
            if (ScreenEdgeAlertUI.Instance != null)
            {
                ScreenEdgeAlertUI.Instance.SetAlertActive(isAlarmActive);
            }
            else
            {
                Debug.LogWarning("[ALARM_DIAG] ⚠️ ScreenEdgeAlertUI.Instance is null when toggling alarm!");
            }

            // 2. Toggle 3D Red Warning Light on the button
            if (statusLight != null)
            {
                statusLight.enabled = isAlarmActive;
            }

            // 3. Visual button depress feedback (shift button backward when active)
            transform.localPosition = isAlarmActive ? initialLocalPos - transform.forward * 0.02f : initialLocalPos;

            // 4. Notify Fire Safety Module Step 0 completion & Play SFX
            if (isAlarmActive)
            {
                if (FireSafetyModuleManager.Instance != null)
                {
                    FireSafetyModuleManager.Instance.NotifyAlarmActivated();
                }

                if (alarmSfx != null)
                {
                    if (audioSource == null)
                    {
                        audioSource = gameObject.AddComponent<AudioSource>();
                        audioSource.playOnAwake = false;
                        audioSource.spatialBlend = 1f;
                    }
                    audioSource.PlayOneShot(alarmSfx);
                }
            }

            Debug.Log($"[ALARM_DIAG] 🚨 ALARM TOGGLED! Active={isAlarmActive} | 3D Red Light Enabled={(statusLight != null && statusLight.enabled)} | Color={statusLight?.color} | Intensity={statusLight?.intensity:F2} | LocalPos={transform.localPosition}");
        }

        private int lastProcessedFrame = -1;

        private void Update()
        {
            // Pulse the 3D red warning light intensity while alarm is active
            if (isAlarmActive && statusLight != null)
            {
                float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                statusLight.intensity = Mathf.Lerp(2.0f, 5.0f, pulse);
            }

            CheckTouchInput();
        }

        private void CheckTouchInput()
        {
            if (Time.frameCount == lastProcessedFrame) return;

            Vector2 tapPosition = Vector2.zero;
            bool tapDetected = false;

            // Mobile Enhanced Touch
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.screenPosition;
                    tapDetected = true;
                }
            }
            // New Input System Pointer / Mouse Click
            else if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            {
                tapPosition = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
                tapDetected = true;
            }

            if (tapDetected)
            {
                Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(tapPosition);
                    Debug.Log($"[ALARM_DIAG] 👆 TAP DETECTED at ScreenPos=({tapPosition.x:F1}, {tapPosition.y:F1}) | RayOrigin={ray.origin} | RayDir={ray.direction}");

                    if (Physics.Raycast(ray, out RaycastHit hit, 10.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        Debug.Log($"[ALARM_DIAG] 🎯 RAYCAST HIT object='{hit.transform.name}' (Root='{hit.transform.root.name}') at WorldPos={hit.point} | Dist={hit.distance:F2}m");
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                        {
                            Debug.Log($"[ALARM_DIAG] ✅ RAYCAST MATCHED ALARM BUTTON! Toggling Alarm State...");
                            lastProcessedFrame = Time.frameCount;
                            ToggleAlarmState();
                        }
                        else
                        {
                            Debug.Log($"[ALARM_DIAG] ❌ RAYCAST HIT OTHER OBJECT: '{hit.transform.name}', NOT Alarm Button '{name}'");
                        }
                    }
                    else
                    {
                        Debug.Log($"[ALARM_DIAG] ⚠️ RAYCAST MISSED ALL COLLIDERS in scene from ScreenPos=({tapPosition.x:F1}, {tapPosition.y:F1})");
                    }
                }
            }
        }
    }
}
