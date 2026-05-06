using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyDirtPatch : MonoBehaviour
{
    public int clicksToClean = 5;
    public float fadeDuration = 0.35f;

    private int clicks;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private Color baseColor;
    private float targetAlpha;
    private float displayedAlpha;
    private bool ownsRuntimeMaterial;

    public bool IsClean => clicks >= clicksToClean;
    public float CleanPercent => Mathf.Clamp01(clicks / (float)Mathf.Max(1, clicksToClean));

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        EnsureRuntimeMaterial();
        ApplyMaterialAlpha(displayedAlpha);
    }

    private void OnDestroy()
    {
        if (!ownsRuntimeMaterial || runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }
    }

    private void Update()
    {
        EnsureRuntimeMaterial();
        if (runtimeMaterial == null)
        {
            return;
        }

        if (!Mathf.Approximately(displayedAlpha, targetAlpha))
        {
            float fadeSpeed = baseColor.a / Mathf.Max(0.01f, fadeDuration);
            displayedAlpha = Mathf.MoveTowards(displayedAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            ApplyMaterialAlpha(displayedAlpha);
        }

        if (IsClean && displayedAlpha <= 0.01f)
        {
            gameObject.SetActive(false);
        }
    }

    public void CleanStep()
    {
        if (IsClean)
        {
            return;
        }

        EnsureRuntimeMaterial();
        clicks = Mathf.Min(clicksToClean, clicks + 1);
        float remaining = 1f - CleanPercent;
        targetAlpha = Mathf.Lerp(0f, baseColor.a, remaining);

        // Give the click immediate visual feedback, then let Update finish the fade.
        float clickFadeStep = baseColor.a / Mathf.Max(1, clicksToClean) * 0.45f;
        displayedAlpha = Mathf.Max(targetAlpha, displayedAlpha - clickFadeStep);
        ApplyMaterialAlpha(displayedAlpha);
    }

    private void EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return;
        }

        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        Material sourceMaterial = cachedRenderer.sharedMaterial;
        if (sourceMaterial == null)
        {
            return;
        }

        runtimeMaterial = new Material(sourceMaterial);
        runtimeMaterial.name = sourceMaterial.name + " Runtime";
        cachedRenderer.sharedMaterial = runtimeMaterial;
        ownsRuntimeMaterial = true;
        baseColor = runtimeMaterial.color;
        targetAlpha = baseColor.a;
        displayedAlpha = baseColor.a;
    }

    private void ApplyMaterialAlpha(float alpha)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        Color color = runtimeMaterial.color;
        color.a = Mathf.Clamp01(alpha);
        runtimeMaterial.color = color;

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", color);
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }
    }
}
