using System;
using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Real-time 3.5ft proximity safety check around the fire hazard.
    /// Monitors camera distance to fire target and triggers warnings/penalties
    /// when the trainee stands too close.
    /// </summary>
    public class ARProximitySafetyValidator : MonoBehaviour
    {
        public static ARProximitySafetyValidator Instance { get; private set; }

        [Header("Safety Configuration")]
        [SerializeField] private float safeDistanceThreshold = 1.0668f;
        [SerializeField] private float hysteresisMargin = 0.15f;
        // Penalty weight now lives in ScoringConstants.ProximityBreachPenalty (via
        // FireSafetyModuleManager.MistakeSeverity.ProximityBreach) — a duplicate local constant
        // here would just be another number that could drift out of sync with it, same class of
        // problem as the four disagreeing pass thresholds this whole rework started from.

        private Transform fireTargetTransform;
        private Camera mainCamera;
        private bool isCurrentlyViolating = false;
        private bool isEnabled = false;

        public bool IsCurrentlyViolating => isCurrentlyViolating;
        public bool IsEnabled => isEnabled;

        public event Action<float> OnProximityBreached;
        public event Action OnProximityResolved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (!isEnabled || fireTargetTransform == null) return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
                if (mainCamera == null) return;
            }

            CheckProximity();
        }

        private void CheckProximity()
        {
            Vector3 camPos = mainCamera.transform.position;
            Vector3 firePos = fireTargetTransform.position;

            float dx = camPos.x - firePos.x;
            float dz = camPos.z - firePos.z;
            float distanceToFire = Mathf.Sqrt(dx * dx + dz * dz);

            if (distanceToFire < safeDistanceThreshold)
            {
                if (!isCurrentlyViolating)
                {
                    isCurrentlyViolating = true;
                    Debug.Log($"[ARProximitySafetyValidator] SAFETY BREACH! Distance={distanceToFire:F2}m (< {safeDistanceThreshold}m)");
                    
                    if (Modules.FireSafetyModuleManager.Instance != null)
                    {
                        Modules.FireSafetyModuleManager.Instance.RegisterMistake(
                            "Step back! Don't stand within 3.5 ft. of the fire.",
                            Modules.FireSafetyModuleManager.MistakeSeverity.ProximityBreach);
                    }

                    OnProximityBreached?.Invoke(distanceToFire);
                }
            }
            else
            {
                if (isCurrentlyViolating && distanceToFire >= safeDistanceThreshold + hysteresisMargin)
                {
                    isCurrentlyViolating = false;
                    Debug.Log($"[ARProximitySafetyValidator] Safety resolved. Distance={distanceToFire:F2}m");
                    OnProximityResolved?.Invoke();
                }
            }
        }

        [ContextMenu("Simulate Proximity Breach Test")]
        public void SimulateProximityBreachTest()
        {
            isCurrentlyViolating = true;
            Debug.Log("[ARProximitySafetyValidator] [TEST] Simulated Proximity Breach (< 3.5 ft)");
            if (Modules.FireSafetyModuleManager.Instance != null)
            {
                Modules.FireSafetyModuleManager.Instance.RegisterMistake(
                    "Step back! Don't stand within 3.5 ft. of the fire.",
                    Modules.FireSafetyModuleManager.MistakeSeverity.ProximityBreach);
            }
            OnProximityBreached?.Invoke(0.8f);
        }

        [ContextMenu("Simulate Proximity Resolved Test")]
        public void SimulateProximityResolvedTest()
        {
            isCurrentlyViolating = false;
            Debug.Log("[ARProximitySafetyValidator] [TEST] Simulated Proximity Resolved (> 3.5 ft)");
            OnProximityResolved?.Invoke();
        }

        /// <summary>
        /// Enable proximity checking around the given fire target transform.
        /// </summary>
        public void EnableChecking(Transform fireTarget)
        {
            fireTargetTransform = fireTarget;
            isEnabled = true;
            isCurrentlyViolating = false;
            mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            Debug.Log($"[ARProximitySafetyValidator] Proximity checking ENABLED. Target: {fireTarget.name}");
        }

        /// <summary>
        /// Disable proximity checking entirely.
        /// </summary>
        public void DisableChecking()
        {
            isEnabled = false;
            isCurrentlyViolating = false;
            fireTargetTransform = null;
            Debug.Log("[ARProximitySafetyValidator] Proximity checking DISABLED.");
        }
    }
}
