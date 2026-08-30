using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy sample script — disabled in favor of ARPlacementManager architecture.
/// </summary>
public class ARPlaceCube : MonoBehaviour
{
    private void Awake()
    {
        // ARPlacementManager handles all plane tap placements cleanly.
        enabled = false;
    }
}
