using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyDecorTemplate : MonoBehaviour
{
    public string displayName;

    public static Quaternion GetPlacementRotation(GameObject decorObject, float yaw)
    {
        CozyDecorTemplate marker = decorObject != null ? decorObject.GetComponent<CozyDecorTemplate>() : null;
        string name = marker != null && !string.IsNullOrEmpty(marker.displayName)
            ? marker.displayName
            : (decorObject != null ? decorObject.name : string.Empty);
        return GetPlacementRotation(name, yaw);
    }

    public static Quaternion GetPlacementRotation(string displayName, float yaw)
    {
        return KeepsOriginalRotation(displayName)
            ? Quaternion.Euler(0f, yaw, 0f)
            : Quaternion.Euler(270f, yaw, 0f);
    }

    public static void SnapBottomToFloor(GameObject decorObject, float floorY)
    {
        if (decorObject == null)
        {
            return;
        }

        Renderer[] renderers = decorObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        decorObject.transform.position += Vector3.up * (floorY - bounds.min.y);
    }

    private static bool KeepsOriginalRotation(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        string normalizedName = displayName.ToLowerInvariant();
        return normalizedName.Contains("lamp") || normalizedName.Contains("rug");
    }
}
