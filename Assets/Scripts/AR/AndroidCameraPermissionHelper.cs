using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MiningSafetyAR.AR
{
    public class AndroidCameraPermissionHelper : MonoBehaviour
    {
        private void Awake()
        {
            RequestCameraPermission();
        }

        public void RequestCameraPermission()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[AndroidCameraPermissionHelper] Requesting CAMERA permission from user...");
                Permission.RequestUserPermission(Permission.Camera);
            }
            else
            {
                Debug.Log("[AndroidCameraPermissionHelper] CAMERA permission already granted.");
            }
#endif
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log("[AndroidCameraPermissionHelper] Camera permission confirmed granted.");
#else
            yield break;
#endif
        }
    }
}
