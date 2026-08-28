using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MiningSafetyAR.AR
{
    public class AndroidCameraPermissionHelper : MonoBehaviour
    {
        private float checkTimeoutSeconds = 10.0f;

        private void Awake()
        {
            RequestCameraPermission();
        }

        public void RequestCameraPermission()
        {
#if UNITY_ANDROID
            try
            {
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Debug.Log("[WARN] [AndroidCameraPermissionHelper] CAMERA permission NOT yet granted by user. Triggering Android system permission prompt...");
                    Permission.RequestUserPermission(Permission.Camera);
                }
                else
                {
                    Debug.Log("[INFO] [AndroidCameraPermissionHelper] CAMERA permission is already AUTHORIZED.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [AndroidCameraPermissionHelper] Exception checking/requesting CAMERA permission: {ex.Message}");
            }
#else
            Debug.Log("[INFO] [AndroidCameraPermissionHelper] Non-Android platform detected — skipping Android permission check.");
#endif
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID
            float elapsedTime = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                elapsedTime += 0.5f;
                if (elapsedTime >= checkTimeoutSeconds)
                {
                    Debug.LogError("[ERROR] [AndroidCameraPermissionHelper] Timeout waiting for user to grant CAMERA permission! AR Camera stream may fail to initialize.");
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log($"[INFO] [AndroidCameraPermissionHelper] Camera permission CONFIRMED AUTHORIZED after {elapsedTime:F1}s.");
#else
            yield break;
#endif
        }
    }
}
