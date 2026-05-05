using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyWetPatchFx : MonoBehaviour
{
    public Material runtimeMaterial;
    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / 0.7f);
        if (runtimeMaterial != null)
        {
            Color color = runtimeMaterial.color;
            color.a = Mathf.Lerp(0.32f, 0f, t);
            runtimeMaterial.color = color;
        }

        transform.localScale = new Vector3(transform.localScale.x * 0.998f, transform.localScale.y, transform.localScale.z * 0.998f);
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
