using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyMoppableFloor : MonoBehaviour
{
    public Material cleanMaterial;
    public Material grimeMaterial;
    public Material brushMaterial;
    public int coverageGridX = 44;
    public int coverageGridY = 34;
    public float completionThreshold = 0.95f;
    public float edgePadding = 0.04f;
    public Vector2 rollerContactSize = new Vector2(0.60f, 0.20f);

    private float coverage;
    private GameObject grimeOverlay;
    private Material runtimeSurfaceMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private readonly HashSet<int> cleanedCells = new HashSet<int>();
    private int rollerQuarterTurns;

    public bool IsClean => coverage >= 1f;
    public float CleanPercent => Mathf.Clamp01(coverage);

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            CozyDirtMaskRenderer.Release(dirtMask);
            CozyDirtMaskRenderer.Release(dirtMaskScratch);
            dirtMask = CozyDirtMaskRenderer.CreateDirtyMask(name + " Dirt Mask");
            dirtMaskScratch = null;
            runtimeSurfaceMaterial = CozyDirtMaskRenderer.CreateMaskedSurfaceMaterial(cleanMaterial, grimeMaterial, "CR_" + name + "_MaskedClean");
            runtimeSurfaceMaterial.SetTexture("_DirtMask", dirtMask);
            ConfigureMaskProjection();
            renderer.material = runtimeSurfaceMaterial;
        }
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

        CreateCleanStroke(worldPoint, normal, tangent);
    }

    private void CreateGrimeOverlay()
    {
        grimeOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grimeOverlay.name = "Floor Grime Overlay";
        grimeOverlay.transform.SetParent(transform, false);
        grimeOverlay.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        grimeOverlay.transform.localScale = new Vector3(0.985f, 0.04f, 0.985f);
        Renderer renderer = grimeOverlay.GetComponent<Renderer>();
        renderer.sharedMaterial = grimeMaterial;
        renderer.material.color = new Color(0.38f, 0.31f, 0.28f, 0.92f);
        Collider collider = grimeOverlay.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private void CreateCleanStroke(Vector3 worldPoint, Vector3 normal, Vector3 tangent)
    {
        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(worldPoint, normal);
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.4f || Vector3.Dot(lastStrokeNormal, normal) < 0.9f)
        {
            TryCreateCleanStamp(worldPoint, normal, strokeTangent, rollerContactSize, 0.98f);
            lastStrokePoint = worldPoint;
            lastStrokeNormal = normal;
            lastStrokeTangent = strokeTangent;
            hasStrokePoint = true;
            return;
        }

        float distance = Vector3.Distance(lastStrokePoint, worldPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.018f));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastStrokePoint, worldPoint, t);
            Vector3 stepTangent = Vector3.Slerp(lastStrokeTangent == Vector3.zero ? strokeTangent : lastStrokeTangent, strokeTangent, t).normalized;
            TryCreateCleanStamp(point, normal, stepTangent, rollerContactSize, 0.96f);
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
        worldNormal = Vector3.up;
        distance = 0f;

        Vector3 planeNormal = transform.up;
        Vector3 planePoint = transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
        Plane plane = new Plane(planeNormal, planePoint);
        if (!plane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 candidatePoint = ray.GetPoint(enter);
        Vector3 localPoint = transform.InverseTransformPoint(candidatePoint);
        float limitX = 0.5f + edgePadding;
        float limitZ = 0.5f + edgePadding;
        if (Mathf.Abs(localPoint.y - 0.5f) > 0.04f || localPoint.x < -limitX || localPoint.x > limitX || localPoint.z < -limitZ || localPoint.z > limitZ)
        {
            return false;
        }

        worldPoint = candidatePoint;
        worldNormal = planeNormal;
        distance = enter;
        return true;
    }

    public void RotateRoller90()
    {
        rollerQuarterTurns = (rollerQuarterTurns + 1) % 4;
        EndStroke();
    }

    public void SetRollerQuarterTurns(int quarterTurns)
    {
        rollerQuarterTurns = ((quarterTurns % 4) + 4) % 4;
    }

    private void TryCreateCleanStamp(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 stampSize, float alpha)
    {
        if (!TryGetFloorCoords(worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld))
        {
            return;
        }

        Vector2 finalStampSize = ClampRectSizeToEdge(stampSize, edgeDistancesWorld);
        if (finalStampSize.x < 0.035f || finalStampSize.y < 0.018f)
        {
            return;
        }

        int newlyCovered = CoverRectCells(
            uv,
            Mathf.Max(1.25f, finalStampSize.x / Mathf.Max(0.001f, rollerContactSize.x) * 1.95f),
            Mathf.Max(1.0f, finalStampSize.y / Mathf.Max(0.001f, rollerContactSize.y) * 1.7f));
        coverage = cleanedCells.Count / (float)Mathf.Max(1, coverageGridX * coverageGridY);
        if (newlyCovered > 0)
        {
            UpdateGrimeOpacity();
        }
        StampDirtMask(uv, finalStampSize, tangent, alpha);

        if (coverage >= completionThreshold)
        {
            CreateCompletionShine();
            coverage = 1f;
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            if (grimeOverlay != null)
            {
                grimeOverlay.SetActive(false);
            }
        }
    }

    private void CreateWetSheen(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 brushSize)
    {
        GameObject wet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wet.name = "Wet Sheen";
        wet.transform.SetParent(transform.parent, true);
        wet.transform.position = worldPoint + normal * 0.01f;
        wet.transform.rotation = BuildStrokeRotation(normal, tangent);
        Vector2 wetSize = brushSize * Random.Range(0.72f, 0.9f);
        wet.transform.localScale = new Vector3(Mathf.Max(0.03f, wetSize.x), Mathf.Max(0.016f, wetSize.y), 1f);
        Renderer wetRenderer = wet.GetComponent<Renderer>();
        Material wetMaterial = new Material(brushMaterial != null ? brushMaterial : cleanMaterial);
        wetMaterial.color = new Color(0.84f, 0.95f, 1f, 0.24f);
        wetRenderer.material = wetMaterial;
        Collider collider = wet.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        CozyWetPatchFx fx = wet.AddComponent<CozyWetPatchFx>();
        fx.runtimeMaterial = wetMaterial;
    }

    private void UpdateGrimeOpacity()
    {
        if (grimeOverlay == null)
        {
            return;
        }

        Renderer renderer = grimeOverlay.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.material.color;
        color.a = Mathf.Lerp(0.92f, 0.18f, CleanPercent);
        renderer.material.color = color;
    }

    private bool TryGetFloorCoords(Vector3 worldPoint, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(localPoint.y) > 0.56f)
        {
            return false;
        }

        float min = -0.5f - edgePadding;
        float max = 0.5f + edgePadding;
        if (localPoint.x < min || localPoint.x > max || localPoint.z < min || localPoint.z > max)
        {
            return false;
        }

        edgeDistancesWorld = new Vector2(
            LocalDistanceToWorld(Vector3.right, Mathf.Min(localPoint.x - min, max - localPoint.x)),
            LocalDistanceToWorld(Vector3.forward, Mathf.Min(localPoint.z - min, max - localPoint.z)));
        uv = new Vector2(Mathf.InverseLerp(min, max, localPoint.x), Mathf.InverseLerp(min, max, localPoint.z));
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector3 floorSize = transform.lossyScale;
        Vector2 brushSizeUv = new Vector2(
            stampSizeWorld.x / Mathf.Max(0.001f, floorSize.x),
            stampSizeWorld.y / Mathf.Max(0.001f, floorSize.z));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.62f, WorldTangentToMaskTangent(tangent), Mathf.Max(0.08f, strength * 0.24f));
    }

    private void CreateCompletionShine()
    {
        Vector3 floorSize = transform.lossyScale;
        Vector2 shineSize = new Vector2(
            Mathf.Max(0.05f, floorSize.x * (1f + edgePadding * 2f)),
            Mathf.Max(0.05f, floorSize.z * (1f + edgePadding * 2f)));
        CozySurfaceCompletionShineFx.Create(
            "Floor Completion Shine",
            transform.parent,
            transform.TransformPoint(new Vector3(0f, 0.5f, 0f)) + transform.up.normalized * 0.02f,
            BuildStrokeRotation(transform.up.normalized, transform.right.normalized),
            shineSize,
            0.42f,
            1.08f,
            0.72f);
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        Vector2 maskTangent = new Vector2(Vector3.Dot(safeTangent, transform.right.normalized), Vector3.Dot(safeTangent, transform.forward.normalized));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeSurfaceMaterial == null)
        {
            return;
        }

        Vector3 axisA = transform.right.normalized;
        Vector3 axisB = transform.forward.normalized;
        Vector3 size = transform.lossyScale;
        float surfaceWidth = Mathf.Max(0.001f, size.x * (1f + edgePadding * 2f));
        float surfaceDepth = Mathf.Max(0.001f, size.z * (1f + edgePadding * 2f));
        runtimeSurfaceMaterial.SetVector("_MaskOrigin", transform.TransformPoint(new Vector3(0f, 0.5f, 0f)));
        runtimeSurfaceMaterial.SetVector("_MaskAxisA", axisA);
        runtimeSurfaceMaterial.SetVector("_MaskAxisB", axisB);
        runtimeSurfaceMaterial.SetVector("_MaskSize", new Vector4(surfaceWidth, surfaceDepth, 0f, 0f));
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

    private Vector3 ResolveStrokeTangent(Vector3 worldPoint, Vector3 normal)
    {
        Vector3 baseAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.8f ? Vector3.right : Vector3.up;
        Vector3 altAxis = Vector3.Cross(normal, baseAxis).normalized;
        if (altAxis.sqrMagnitude < 0.0001f)
        {
            altAxis = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
        }

        return (rollerQuarterTurns % 2 == 0 ? baseAxis : altAxis).normalized;
    }

    private Quaternion BuildStrokeRotation(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(transform.position, normal);
        Vector3 up = Vector3.Cross(-normal, safeTangent).normalized;
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
        }
        return Quaternion.LookRotation(-normal, up);
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
