using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyWindowSparkleFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 0.55f;
    public float pulseScale = 1.6f;

    private float elapsed;
    private Vector3 startScale;
    private Quaternion startRotation;

    private void Awake()
    {
        startScale = transform.localScale;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        float pulse = Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.Lerp(startScale, startScale * pulseScale, pulse);
        transform.rotation = startRotation * Quaternion.AngleAxis(t * 80f, Vector3.forward);

        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(color.a, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
