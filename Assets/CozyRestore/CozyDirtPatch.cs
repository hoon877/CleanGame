using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyDirtPatch : MonoBehaviour
{
    public int clicksToClean = 5;

    private int clicks;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private Color baseColor;

    public bool IsClean => clicks >= clicksToClean;
    public float CleanPercent => Mathf.Clamp01(clicks / (float)Mathf.Max(1, clicksToClean));

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            runtimeMaterial = cachedRenderer.material;
            baseColor = runtimeMaterial.color;
        }
    }

    public void CleanStep()
    {
        if (IsClean)
        {
            return;
        }

        clicks = Mathf.Min(clicksToClean, clicks + 1);
        float remaining = 1f - CleanPercent;
        if (runtimeMaterial != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(0f, baseColor.a, remaining);
            runtimeMaterial.color = color;
        }

        if (IsClean)
        {
            gameObject.SetActive(false);
        }
    }
}
