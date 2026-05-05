using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyTransientStrokeFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 1.35f;
    public float startAlpha = 1f;

    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
