using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    public bool occupied;

    private void Awake()
    {
        // Cover points are gameplay markers for ranged-enemy AI. Keep their
        // transforms and state active, but hide the orange debug spheres at runtime.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer pointRenderer in renderers)
            pointRenderer.enabled = false;
    }

    public void SetOccupied(bool occupied) => this.occupied = occupied;
}
