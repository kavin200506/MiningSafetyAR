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

            // Ensure a 3D red warning light is attached
            statusLight = GetComponent<Light>();
            if (statusLight == null)
            {
                statusLight = gameObject.AddComponent<Light>();
            }

            if (statusLight != null)
            {
                statusLight.type = LightType.Point;
                statusLight.color = new Color(1f, 0.1f, 0.1f);
                statusLight.range = 1.5f;
                statusLight.intensity = 3.5f;
                statusLight.enabled = false;
            }
        }

        private void OnMouseDown()
        {
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

            Debug.Log($"[AlarmButtonInteractable] 🚨 ALARM TOGGLED! Button '{gameObject.name}' active = {isAlarmActive}");
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
                    if (Physics.Raycast(ray, out RaycastHit hit, 10.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                        {
                            lastProcessedFrame = Time.frameCount;
                            ToggleAlarmState();
                        }
                    }
                }
            }
        }
    }
}
