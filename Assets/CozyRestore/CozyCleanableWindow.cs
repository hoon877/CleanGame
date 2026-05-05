using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyCleanableWindow : MonoBehaviour
{
    public int coverageGridX = 36;
    public int coverageGridY = 24;
    public float completionThreshold = 0.95f;
    public Vector2 wiperContactSize = new Vector2(0.55f, 0.18f);

    private float coverage;
    private Material runtimeMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private bool playedCleanFx;
    private readonly HashSet<int> cleanedCells = new HashSet<int>();

    public bool IsClean => coverage >= 1f;
    public float CleanPercent => Mathf.Clamp01(coverage);

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
        dirtMask = CozyDirtMaskRenderer.CreateDirtyMask("CR_" + name + "_WindowMask");
        dirtMaskScratch = null;

        runtimeMaterial = CozyDirtMaskRenderer.CreateMaskedGlassMaterial("CR_" + name + "_MaskedGlass");
        runtimeMaterial.SetTexture("_DirtMask", dirtMask);
        ConfigureMaskProjection();
        renderer.material = runtimeMaterial;
    }

    private void OnDestroy()
    {
        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
    }

    public void CleanAt(Vector3 worldPoint, Vector3 normal, Vector3 tangent, float amount)
    {
        if (IsClean)
        {
            return;
        }

        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.1f || Vector3.Dot(lastStrokeNormal, normal) < 0.9f)
        {
            TryCreateCleanStamp(worldPoint, strokeTangent, 0.95f);
            lastStrokePoint = worldPoint;
            lastStrokeNormal = normal;
            lastStrokeTangent = strokeTangent;
            hasStrokePoint = true;
            return;
        }

        float distance = Vector3.Distance(lastStrokePoint, worldPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.05f));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastStrokePoint, worldPoint, t);
            Vector3 stepTangent = Vector3.Slerp(lastStrokeTangent == Vector3.zero ? strokeTangent : lastStrokeTangent, strokeTangent, t).normalized;
            TryCreateCleanStamp(point, stepTangent, 0.92f);
        }

        lastStrokePoint = worldPoint;
        lastStrokeNormal = normal;
        lastStrokeTangent = strokeTangent;
        hasStrokePoint = true;
    }

    public void EndStroke()
    {
        hasStrokePoint = false;
        lastStrokeTangent = Vector3.zero;
    }

    public bool TryProjectRay(Ray ray, out Vector3 worldPoint, out Vector3 worldNormal, out float distance)
    {
        worldPoint = default;
        worldNormal = transform.forward.normalized;
        distance = 0f;

        Vector3 planeNormal = transform.forward.normalized;
        Plane plane = new Plane(planeNormal, transform.position);
        if (!plane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 candidatePoint = ray.GetPoint(enter);
        if (!TryGetWindowCoords(candidatePoint, out _, out _))
        {
            return false;
        }

        worldPoint = candidatePoint;
        worldNormal = Vector3.Dot(planeNormal, -ray.direction) >= 0f ? planeNormal : -planeNormal;
        distance = enter;
        return true;
    }

    private void TryCreateCleanStamp(Vector3 worldPoint, Vector3 tangent, float strength)
    {
        if (!TryGetWindowCoords(worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld))
        {
            return;
        }

        Vector2 finalStampSize = ClampRectSizeToEdge(wiperContactSize, edgeDistancesWorld);
        if (finalStampSize.x < 0.035f || finalStampSize.y < 0.018f)
        {
            return;
        }

        int newlyCovered = CoverRectCells(
            uv,
            Mathf.Max(2.15f, finalStampSize.x / Mathf.Max(0.001f, wiperContactSize.x) * 3.6f),
            Mathf.Max(1.75f, finalStampSize.y / Mathf.Max(0.001f, wiperContactSize.y) * 3.0f));
        coverage = cleanedCells.Count / (float)Mathf.Max(1, coverageGridX * coverageGridY);
        StampDirtMask(uv, finalStampSize, tangent, strength);

        if (coverage >= completionThreshold)
        {
            coverage = 1f;
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            PlayCleanCompleteFx();
        }
    }

    private void PlayCleanCompleteFx()
    {
        if (playedCleanFx)
        {
            return;
        }

        playedCleanFx = true;
        Vector3 normal = transform.forward.normalized;
        Vector2 windowSize = new Vector2(
            LocalDistanceToWorld(Vector3.right, 1f) * 1.04f,
            LocalDistanceToWorld(Vector3.up, 1f) * 1.04f);
        CozySurfaceCompletionShineFx.Create(
            "Window Completion Shine Front",
            transform.parent,
            transform.position + normal * 0.045f,
            Quaternion.LookRotation(normal, transform.up),
            windowSize,
            0.48f,
            1.06f,
            0.68f);
        CozySurfaceCompletionShineFx.Create(
            "Window Completion Shine Back",
            transform.parent,
            transform.position - normal * 0.045f,
            Quaternion.LookRotation(-normal, transform.up),
            windowSize,
            0.48f,
            1.06f,
            0.68f);
    }

    private bool TryGetWindowCoords(Vector3 worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(localPoint.z) > 0.58f)
        {
            return false;
        }

        float min = -0.5f;
        float max = 0.5f;
        if (localPoint.x < min || localPoint.x > max || localPoint.y < min || localPoint.y > max)
        {
            return false;
        }

        edgeDistancesWorld = new Vector2(
            LocalDistanceToWorld(Vector3.right, Mathf.Min(localPoint.x - min, max - localPoint.x)),
            LocalDistanceToWorld(Vector3.up, Mathf.Min(localPoint.y - min, max - localPoint.y)));
        uv = new Vector2(Mathf.InverseLerp(min, max, localPoint.x), Mathf.InverseLerp(min, max, localPoint.y));
        return true;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector2 windowSize = new Vector2(LocalDistanceToWorld(Vector3.right, 1f), LocalDistanceToWorld(Vector3.up, 1f));
        Vector2 brushSizeUv = new Vector2(
            stampSizeWorld.x / Mathf.Max(0.001f, windowSize.x),
            stampSizeWorld.y / Mathf.Max(0.001f, windowSize.y));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.68f, WorldTangentToMaskTangent(tangent), Mathf.Max(0.08f, strength * 0.24f));
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        Vector2 maskTangent = new Vector2(Vector3.Dot(safeTangent, transform.right.normalized), Vector3.Dot(safeTangent, transform.up.normalized));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetVector("_MaskOrigin", transform.position);
        runtimeMaterial.SetVector("_MaskAxisA", transform.right.normalized);
        runtimeMaterial.SetVector("_MaskAxisB", transform.up.normalized);
        runtimeMaterial.SetVector("_MaskSize", new Vector4(LocalDistanceToWorld(Vector3.right, 1f), LocalDistanceToWorld(Vector3.up, 1f), 0f, 0f));
    }

    private int CoverRectCells(Vector2 uv, float halfWidthCells, float halfHeightCells)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (coverageGridX - 1)), 0, coverageGridX - 1);
        int centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (coverageGridY - 1)), 0, coverageGridY - 1);
        int radiusX = Mathf.Max(1, Mathf.CeilToInt(halfWidthCells));
        int radiusY = Mathf.Max(1, Mathf.CeilToInt(halfHeightCells));
        int added = 0;

        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            if (y < 0 || y >= coverageGridY) continue;
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || x >= coverageGridX) continue;
                float dx = x - centerX;
                float dy = y - centerY;
                if (Mathf.Abs(dx) > halfWidthCells || Mathf.Abs(dy) > halfHeightCells)
                {
                    continue;
                }

                int key = y * coverageGridX + x;
                if (cleanedCells.Add(key))
                {
                    added++;
                }
            }
        }

        return added;
    }

    private Vector2 ClampRectSizeToEdge(Vector2 desiredSize, Vector2 edgeDistancesWorld)
    {
        return new Vector2(
            Mathf.Min(desiredSize.x, edgeDistancesWorld.x * 2f * 0.92f),
            Mathf.Min(desiredSize.y, edgeDistancesWorld.y * 2f * 0.92f));
    }

    private float LocalDistanceToWorld(Vector3 localAxis, float localDistance)
    {
        return transform.TransformVector(localAxis.normalized * Mathf.Max(0f, localDistance)).magnitude;
    }
}

public static class CozyDirtMaskRenderer
{
    public const int MaskResolution = 512;
    private static Material brushMaterial;

    public static RenderTexture CreateDirtyMask(string maskName)
    {
        RenderTexture mask = new RenderTexture(MaskResolution, MaskResolution, 0, RenderTextureFormat.R8);
        mask.name = maskName;
        mask.wrapMode = TextureWrapMode.Clamp;
        mask.filterMode = FilterMode.Bilinear;
        mask.Create();
        Fill(mask, Color.white);
        return mask;
    }

    public static Material CreateMaskedSurfaceMaterial(Material cleanMaterial, Material dirtyMaterial, string materialName)
    {
        Shader shader = Shader.Find("CozyRestore/MaskedSurface");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = materialName;

        Color cleanColor = cleanMaterial != null ? cleanMaterial.color : Color.white;
        Color dirtyColor = dirtyMaterial != null ? dirtyMaterial.color : new Color(0.24f, 0.19f, 0.16f, 1f);
        dirtyColor.a = 1f;

        material.SetColor("_CleanColor", cleanColor);
        material.SetColor("_DirtyColor", dirtyColor);
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", cleanMaterial != null ? cleanMaterial.GetFloat("_Glossiness") : 0.15f);
        }

        return material;
    }

    public static Material CreateMaskedGlassMaterial(string materialName)
    {
        Shader shader = Shader.Find("CozyRestore/MaskedGlass");
        Material material = new Material(shader != null ? shader : Shader.Find("Transparent/Diffuse"));
        material.name = materialName;
        material.SetColor("_CleanColor", new Color(0.80f, 0.94f, 1f, 0.14f));
        material.SetColor("_DirtyColor", new Color(0.42f, 0.46f, 0.42f, 0.76f));
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.88f);
        }
        return material;
    }

    public static void Stamp(RenderTexture mask, ref RenderTexture scratch, Vector2 uv, Vector2 brushSizeUv, Vector2 brushTangentUv, float strength, Rect[] exclusionRectsUv = null)
    {
        if (mask == null)
        {
            return;
        }

        EnsureScratch(mask, ref scratch);
        EnsureBrushMaterial();
        if (brushMaterial == null)
        {
            return;
        }

        brushMaterial.SetVector("_BrushCenter", new Vector4(uv.x, uv.y, 0f, 0f));
        brushMaterial.SetVector("_BrushSize", new Vector4(Mathf.Max(0.001f, brushSizeUv.x), Mathf.Max(0.001f, brushSizeUv.y), 0f, 0f));
        brushMaterial.SetVector("_BrushTangent", new Vector4(brushTangentUv.x, brushTangentUv.y, 0f, 0f));
        brushMaterial.SetFloat("_BrushSoftness", 0.38f);
        brushMaterial.SetFloat("_BrushStrength", Mathf.Clamp01(strength));
        SetExclusionRects(exclusionRectsUv);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(mask, scratch, brushMaterial);
        Graphics.Blit(scratch, mask);
        RenderTexture.active = previous;
    }

    public static void Fill(RenderTexture target, Color color)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, color);
        RenderTexture.active = previous;
    }

    public static void Release(RenderTexture target)
    {
        if (target == null)
        {
            return;
        }

        if (RenderTexture.active == target)
        {
            RenderTexture.active = null;
        }
        target.Release();
        Object.Destroy(target);
    }

    private static void SetExclusionRects(Rect[] exclusionRectsUv)
    {
        int count = exclusionRectsUv != null ? Mathf.Min(4, exclusionRectsUv.Length) : 0;
        Vector4[] rectVectors = new Vector4[4];
        for (int i = 0; i < count; i++)
        {
            Rect rect = exclusionRectsUv[i];
            rectVectors[i] = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
        }

        brushMaterial.SetInt("_ExclusionCount", count);
        brushMaterial.SetVectorArray("_ExclusionRects", rectVectors);
    }

    private static void EnsureScratch(RenderTexture mask, ref RenderTexture scratch)
    {
        if (scratch != null && scratch.width == mask.width && scratch.height == mask.height)
        {
            return;
        }

        Release(scratch);
        scratch = new RenderTexture(mask.width, mask.height, 0, mask.format);
        scratch.name = mask.name + " Scratch";
        scratch.wrapMode = TextureWrapMode.Clamp;
        scratch.filterMode = FilterMode.Bilinear;
        scratch.Create();
    }

    private static void EnsureBrushMaterial()
    {
        if (brushMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/CozyRestore/MaskBrush");
        if (shader != null)
        {
            brushMaterial = new Material(shader);
        }
    }
}
