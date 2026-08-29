using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using MiningSafetyAR.Modules;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    private bool isPlacing = false;
    private InputAction pressAction;

    private void Awake()
    {
        pressAction = new InputAction("TouchPress", binding: "<Pointer>/press");
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        pressAction.Enable();
        pressAction.performed += OnPressPerformed;
    }

    private void OnDisable()
    {
        pressAction.performed -= OnPressPerformed;
        pressAction.Disable();
        EnhancedTouchSupport.Disable();
    }

    private void OnPressPerformed(InputAction.CallbackContext context)
    {
        if (isPlacing) return;

        Vector2 pointerPosition = Vector2.zero;
        if (Touch.activeTouches.Count > 0)
        {
            pointerPosition = Touch.activeTouches[0].screenPosition;
        }
        else if (Pointer.current != null)
        {
            pointerPosition = Pointer.current.position.ReadValue();
        }
        else
        {
            return;
        }

        PlaceObject(pointerPosition);
    }

    private void PlaceObject(Vector2 touchPosition)
    {
        if (MiningSafetyAR.AR.ARPlacementManager.Instance != null && MiningSafetyAR.AR.ARPlacementManager.Instance.IsPlacementLocked)
        {
            Debug.LogWarning("[ARPlaceCube] Placement attempt blocked — 3-second placement window has expired.");
            return;
        }

        if (raycastManager == null)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
            if (raycastManager == null) return;
        }

        var rayHits = new List<ARRaycastHit>();
        if (raycastManager.Raycast(touchPosition, rayHits, TrackableType.AllTypes | TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds) && rayHits.Count > 0)
        {
            Vector3 hitPosePosition = rayHits[0].pose.position;
            Quaternion hitPoseRotation = rayHits[0].pose.rotation;

            GameObject prefabToSpawn = raycastManager != null ? raycastManager.raycastPrefab : null;
            if (prefabToSpawn == null || prefabToSpawn.name.Contains("Dog"))
            {
#if UNITY_EDITOR
                GameObject firePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_Floor_01_Simple.prefab");
                if (firePrefab != null) prefabToSpawn = firePrefab;
#endif
            }

            if (prefabToSpawn != null)
            {
                GameObject spawnedObj = Instantiate(prefabToSpawn, hitPosePosition, hitPoseRotation);
                spawnedObj.AddComponent<ARAnchor>();

                GroundFireController fireController = spawnedObj.GetComponent<GroundFireController>() ?? spawnedObj.GetComponentInChildren<GroundFireController>();
                if (fireController != null)
                {
                    fireController.IgniteFire();
                }

                Debug.Log($"[ARPlaceCube] Successfully placed object '{spawnedObj.name}' at {hitPosePosition}");
            }
        }

        StartCoroutine(SetIsPlacingToFalseWithDelay());
    }

    private IEnumerator SetIsPlacingToFalseWithDelay()
    {
        isPlacing = true;
        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
    }
}
