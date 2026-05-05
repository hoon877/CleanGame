using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozySurfaceCompletionShineFx : MonoBehaviour
{
    public Material runtimeMaterial;
    public float lifetime = 0.42f;
    public float pulseScale = 1.08f;

    private float elapsed;
    private Vector3 startScale;
    private Color startColor;

    public static void Create(string effectName, Transform parent, Vector3 position, Quaternion rotation, Vector2 size, float lifetime, float pulseScale, float alpha)
    {
        GameObject shine = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shine.name = effectName;
        shine.transform.SetParent(parent, true);
        shine.transform.position = position;
        shine.transform.rotation = rotation;
        shine.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);

        Renderer shineRenderer = shine.GetComponent<Renderer>();
        Material shineMaterial = new Material(Shader.Find("Unlit/Transparent"));
        shineMaterial.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        shineRenderer.material = shineMaterial;

        Collider collider = shine.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        CozySurfaceCompletionShineFx fx = shine.AddComponent<CozySurfaceCompletionShineFx>();
        fx.runtimeMaterial = shineMaterial;
        fx.lifetime = lifetime;
        fx.pulseScale = pulseScale;
    }

    private void Awake()
    {
        startScale = transform.localScale;
        startColor = runtimeMaterial != null ? runtimeMaterial.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        float pulse = Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.Lerp(startScale, startScale * pulseScale, pulse);

        if (runtimeMaterial != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            runtimeMaterial.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
