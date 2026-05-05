using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyPaintableSurface : MonoBehaviour
{
    public Material oldMaterial;
    public Material freshMaterial;
    public Material grimeMaterial;
    public Material brushMaterial;
    public Vector3 allowedPaintNormalLocal = Vector3.back;
    public Rect[] paintExclusionRectsLocal = new Rect[0];
    public string paintGroupId = string.Empty;
    public float faceInset = 0.0001f;
    public int coverageGridX = 44;
    public int coverageGridY = 30;
    public float completionThreshold = 0.95f;
    public Vector2 rollerContactSize = new Vector2(0.60f, 0.20f);
    private float progress;
    private Renderer cachedRenderer;
    private GameObject grimeOverlay;
    private Material runtimeSurfaceMaterial;
    private RenderTexture dirtMask;
    private RenderTexture dirtMaskScratch;
    private Vector3 lastStrokePoint;
    private Vector3 lastStrokeNormal;
    private Vector3 lastStrokeTangent;
    private bool hasStrokePoint;
    private readonly HashSet<int> coveredCells = new HashSet<int>();
    private int rollerQuarterTurns;

    public bool IsPainted => progress >= 1f;
    public float PaintPercent => progress;
    public bool HasPaintGroup => !string.IsNullOrEmpty(paintGroupId);
    public float DisplayPaintPercent => HasPaintGroup ? GetGroupPaintPercent(paintGroupId) : PaintPercent;
    public bool DisplayIsPainted => HasPaintGroup ? IsGroupPainted(paintGroupId) : IsPainted;

    private void Awake()
    {
        InitializeSurface();
    }

    public void InitializeSurface()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            CozyDirtMaskRenderer.Release(dirtMask);
            CozyDirtMaskRenderer.Release(dirtMaskScratch);
            dirtMask = CozyDirtMaskRenderer.CreateDirtyMask(name + " Dirt Mask");
            dirtMaskScratch = null;
            runtimeSurfaceMaterial = CozyDirtMaskRenderer.CreateMaskedSurfaceMaterial(freshMaterial, oldMaterial != null ? oldMaterial : grimeMaterial, "CR_" + name + "_MaskedPaint");
            runtimeSurfaceMaterial.SetTexture("_DirtMask", dirtMask);
            ConfigureMaskProjection();
            cachedRenderer.material = runtimeSurfaceMaterial;
        }
    }

    private void OnDestroy()
    {
        CozyDirtMaskRenderer.Release(dirtMask);
        CozyDirtMaskRenderer.Release(dirtMaskScratch);
    }

    public void PaintAt(Vector3 worldPoint, Vector3 normal, Vector3 tangent, float amount)
    {
        if (IsPainted)
        {
            return;
        }

        PaintStrokeSegment(worldPoint, normal, tangent);
    }

    public void EndStroke()
    {
        hasStrokePoint = false;
        lastStrokeTangent = Vector3.zero;
    }

    public bool CanAcceptPaintAt(Vector3 worldPoint, Vector3 worldNormal)
    {
        return TryGetSurfaceCoords(worldPoint, worldNormal, out _, out _);
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

    public bool TryProjectRay(Ray ray, out Vector3 worldPoint, out Vector3 worldNormal, out float distance)
    {
        worldPoint = default;
        worldNormal = transform.TransformDirection(GetAllowedNormal()).normalized;
        distance = 0f;

        Vector3 localNormal = GetAllowedNormal();
        Vector3 localPlanePoint = GetPlanePoint(localNormal);
        Plane plane = new Plane(worldNormal, transform.TransformPoint(localPlanePoint));
        if (!plane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 candidatePoint = ray.GetPoint(enter);
        if (!TryGetSurfaceCoords(candidatePoint, worldNormal, out _, out _))
        {
            return false;
        }

        worldPoint = candidatePoint;
        distance = enter;
        return true;
    }

    private void PaintStrokeSegment(Vector3 worldPoint, Vector3 normal, Vector3 tangent)
    {
        Vector3 strokeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : ResolveStrokeTangent(worldPoint, normal);
        if (!hasStrokePoint || Vector3.Distance(lastStrokePoint, worldPoint) > 1.25f || Vector3.Dot(lastStrokeNormal, normal) < 0.92f)
        {
            TryCreateStamp(worldPoint, normal, strokeTangent, rollerContactSize, 0.93f);
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
            TryCreateStamp(point, normal, stepTangent, rollerContactSize, 0.9f);
        }

        lastStrokePoint = worldPoint;
        lastStrokeNormal = normal;
        lastStrokeTangent = strokeTangent;
        hasStrokePoint = true;
    }

    private void TryCreateStamp(Vector3 worldPoint, Vector3 normal, Vector3 tangent, Vector2 stampSize, float alpha)
    {
        if (!TryGetSurfaceCoords(worldPoint, normal, out Vector2 uv, out Vector2 edgeDistancesWorld))
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
            Mathf.Max(1.45f, finalStampSize.x / Mathf.Max(0.001f, rollerContactSize.x) * 2.25f),
            Mathf.Max(1.25f, finalStampSize.y / Mathf.Max(0.001f, rollerContactSize.y) * 2.05f));
        progress = coveredCells.Count / (float)Mathf.Max(1, CountPaintableCells());
        if (newlyCovered > 0)
        {
            UpdateGrimeOpacity();
        }
        StampDirtMask(uv, finalStampSize, tangent, alpha);

        if (HasPaintGroup)
        {
            if (GetGroupPaintPercent(paintGroupId) >= completionThreshold)
            {
                CompletePaintGroup(paintGroupId);
            }
        }
        else if (progress >= completionThreshold)
        {
            CreateCompletionShine();
            progress = 1f;
            CompletePaintMask();
            if (grimeOverlay != null)
            {
                grimeOverlay.SetActive(false);
            }
        }
    }

    private void CreateGrimeOverlay()
    {
        grimeOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        grimeOverlay.name = "Wall Grime Overlay";
        grimeOverlay.transform.SetParent(transform, false);

        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 localPosition = GetPlanePoint(allowedNormal) - allowedNormal * 0.01f;
        grimeOverlay.transform.localPosition = localPosition;
        grimeOverlay.transform.localRotation = Quaternion.LookRotation(-allowedNormal, Vector3.up);
        grimeOverlay.transform.localScale = new Vector3(0.985f, 0.985f, 1f);

        Renderer overlayRenderer = grimeOverlay.GetComponent<Renderer>();
        Material sourceMaterial = grimeMaterial != null ? grimeMaterial : (oldMaterial != null ? oldMaterial : freshMaterial);
        Material overlayMaterial = sourceMaterial != null
            ? new Material(sourceMaterial)
            : new Material(Shader.Find("Standard"));
        Color baseColor = oldMaterial != null ? oldMaterial.color : new Color(0.78f, 0.70f, 0.65f, 1f);
        overlayMaterial.color = new Color(baseColor.r * 0.84f, baseColor.g * 0.80f, baseColor.b * 0.76f, 0.9f);
        overlayRenderer.material = overlayMaterial;

        Collider overlayCollider = grimeOverlay.GetComponent<Collider>();
        if (overlayCollider != null)
        {
            Destroy(overlayCollider);
        }
    }

    private void UpdateGrimeOpacity()
    {
        if (grimeOverlay == null)
        {
            return;
        }

        Renderer overlayRenderer = grimeOverlay.GetComponent<Renderer>();
        if (overlayRenderer == null)
        {
            return;
        }

        Color color = overlayRenderer.material.color;
        color.a = Mathf.Lerp(0.9f, 0.08f, progress);
        overlayRenderer.material.color = color;
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

    private bool TryGetSurfaceCoords(Vector3 worldPoint, Vector3 worldNormal, out Vector2 uv, out Vector2 edgeDistancesWorld)
    {
        uv = default;
        edgeDistancesWorld = Vector2.zero;
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localNormal = transform.InverseTransformDirection(worldNormal).normalized;
        Vector3 allowedNormal = GetAllowedNormal();
        if (Vector3.Dot(localNormal, allowedNormal) < 0.82f)
        {
            return false;
        }

        float depth;
        float a;
        float b;
        Vector3 axisA;
        Vector3 axisB;

        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            float sign = Mathf.Sign(allowedNormal.x);
            depth = Mathf.Abs(localPoint.x - (0.5f * sign));
            a = localPoint.z;
            b = localPoint.y;
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            float sign = Mathf.Sign(allowedNormal.y);
            depth = Mathf.Abs(localPoint.y - (0.5f * sign));
            a = localPoint.x;
            b = localPoint.z;
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            float sign = Mathf.Sign(allowedNormal.z);
            depth = Mathf.Abs(localPoint.z - (0.5f * sign));
            a = localPoint.x;
            b = localPoint.y;
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        if (depth > 0.03f)
        {
            return false;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        if (a < min || a > max || b < min || b > max)
        {
            return false;
        }
        if (IsInExclusionLocal(a, b))
        {
            return false;
        }

        float edgeA = Mathf.Min(a - min, max - a);
        float edgeB = Mathf.Min(b - min, max - b);
        edgeDistancesWorld = new Vector2(LocalDistanceToWorld(axisA, edgeA), LocalDistanceToWorld(axisB, edgeB));
        uv = new Vector2(Mathf.InverseLerp(min, max, a), Mathf.InverseLerp(min, max, b));
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private void StampDirtMask(Vector2 uv, Vector2 stampSizeWorld, Vector3 tangent, float strength)
    {
        Vector2 surfaceSize = GetSurfaceWorldSize();
        bool rotated = rollerQuarterTurns % 2 == 0;
        Vector2 brushSizeUv = rotated
            ? new Vector2(
                stampSizeWorld.y / Mathf.Max(0.001f, surfaceSize.x),
                stampSizeWorld.x / Mathf.Max(0.001f, surfaceSize.y))
            : new Vector2(
                stampSizeWorld.x / Mathf.Max(0.001f, surfaceSize.x),
                stampSizeWorld.y / Mathf.Max(0.001f, surfaceSize.y));
        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, uv, brushSizeUv * 0.62f, Vector2.right, Mathf.Max(0.08f, strength * 0.24f), GetExclusionUvRects());
    }

    private void CreateCompletionShine()
    {
        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 normal = transform.TransformDirection(allowedNormal).normalized;
        Vector3 axisA;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
        }

        Vector2 surfaceSize = GetSurfaceWorldSize();
        CozySurfaceCompletionShineFx.Create(
            "Wall Completion Shine",
            transform.parent,
            transform.TransformPoint(GetPlanePoint(allowedNormal)) + normal * 0.02f,
            BuildStrokeRotation(normal, transform.TransformDirection(axisA).normalized),
            surfaceSize,
            0.42f,
            1.08f,
            0.78f);
    }

    private void CompletePaintMask()
    {
        Rect[] exclusionRectsUv = GetExclusionUvRects();
        if (exclusionRectsUv == null || exclusionRectsUv.Length == 0)
        {
            CozyDirtMaskRenderer.Fill(dirtMask, Color.black);
            return;
        }

        CozyDirtMaskRenderer.Stamp(dirtMask, ref dirtMaskScratch, new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), Vector2.right, 1f, exclusionRectsUv);
    }

    private Rect[] GetExclusionUvRects()
    {
        if (paintExclusionRectsLocal == null || paintExclusionRectsLocal.Length == 0)
        {
            return null;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        Rect[] rects = new Rect[paintExclusionRectsLocal.Length];
        for (int i = 0; i < paintExclusionRectsLocal.Length; i++)
        {
            Rect local = paintExclusionRectsLocal[i];
            float xMin = Mathf.InverseLerp(min, max, local.xMin);
            float yMin = Mathf.InverseLerp(min, max, local.yMin);
            float xMax = Mathf.InverseLerp(min, max, local.xMax);
            float yMax = Mathf.InverseLerp(min, max, local.yMax);
            rects[i] = Rect.MinMaxRect(
                Mathf.Clamp01(Mathf.Min(xMin, xMax)),
                Mathf.Clamp01(Mathf.Min(yMin, yMax)),
                Mathf.Clamp01(Mathf.Max(xMin, xMax)),
                Mathf.Clamp01(Mathf.Max(yMin, yMax)));
        }

        return rects;
    }

    private Vector2 WorldTangentToMaskTangent(Vector3 tangent)
    {
        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        Vector3 worldAxisA = transform.TransformDirection(axisA).normalized;
        Vector3 worldAxisB = transform.TransformDirection(axisB).normalized;
        Vector2 maskTangent = new Vector2(Vector3.Dot(tangent.normalized, worldAxisA), Vector3.Dot(tangent.normalized, worldAxisB));
        return maskTangent.sqrMagnitude > 0.0001f ? maskTangent.normalized : Vector2.right;
    }

    private Vector2 GetSurfaceWorldSize()
    {
        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        float localSize = Mathf.Max(0.001f, 1f - faceInset * 2f);
        return new Vector2(LocalDistanceToWorld(axisA, localSize), LocalDistanceToWorld(axisB, localSize));
    }

    private void ConfigureMaskProjection()
    {
        if (runtimeSurfaceMaterial == null)
        {
            return;
        }

        Vector3 allowedNormal = GetAllowedNormal();
        Vector3 axisA;
        Vector3 axisB;
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            axisA = Vector3.forward;
            axisB = Vector3.up;
        }
        else if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            axisA = Vector3.right;
            axisB = Vector3.forward;
        }
        else
        {
            axisA = Vector3.right;
            axisB = Vector3.up;
        }

        Vector2 surfaceSize = GetSurfaceWorldSize();
        runtimeSurfaceMaterial.SetVector("_MaskOrigin", transform.TransformPoint(GetPlanePoint(allowedNormal)));
        runtimeSurfaceMaterial.SetVector("_MaskAxisA", transform.TransformDirection(axisA).normalized);
        runtimeSurfaceMaterial.SetVector("_MaskAxisB", transform.TransformDirection(axisB).normalized);
        runtimeSurfaceMaterial.SetVector("_MaskSize", new Vector4(surfaceSize.x, surfaceSize.y, 0f, 0f));
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

    private Vector3 GetAllowedNormal()
    {
        return allowedPaintNormalLocal.sqrMagnitude > 0.0001f ? allowedPaintNormalLocal.normalized : Vector3.back;
    }

    private Vector3 GetPlanePoint(Vector3 allowedNormal)
    {
        if (Mathf.Abs(allowedNormal.x) > 0.8f)
        {
            return new Vector3(0.5f * Mathf.Sign(allowedNormal.x), 0f, 0f);
        }

        if (Mathf.Abs(allowedNormal.y) > 0.8f)
        {
            return new Vector3(0f, 0.5f * Mathf.Sign(allowedNormal.y), 0f);
        }

        return new Vector3(0f, 0f, 0.5f * Mathf.Sign(allowedNormal.z));
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
                if (IsExcludedCell(x, y))
                {
                    continue;
                }

                int key = y * coverageGridX + x;
                if (coveredCells.Add(key))
                {
                    added++;
                }
            }
        }

        return added;
    }

    private int CountPaintableCells()
    {
        int count = 0;
        for (int y = 0; y < coverageGridY; y++)
        {
            for (int x = 0; x < coverageGridX; x++)
            {
                if (!IsExcludedCell(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    public int GetPaintableCellCount()
    {
        return CountPaintableCells();
    }

    public int GetCoveredCellCount()
    {
        if (IsPainted)
        {
            return Mathf.Max(1, CountPaintableCells());
        }

        return Mathf.Min(coveredCells.Count, Mathf.Max(1, CountPaintableCells()));
    }

    public void CompletePaintSurface()
    {
        if (!IsPainted)
        {
            CreateCompletionShine();
        }

        progress = 1f;
        CompletePaintMask();
        if (grimeOverlay != null)
        {
            grimeOverlay.SetActive(false);
        }
    }

    public static float GetGroupPaintPercent(string groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return 0f;
        }

        CozyPaintableSurface[] surfaces = FindObjectsOfType<CozyPaintableSurface>(true);
        int covered = 0;
        int total = 0;
        for (int i = 0; i < surfaces.Length; i++)
        {
            CozyPaintableSurface surface = surfaces[i];
            if (surface == null || surface.paintGroupId != groupId)
            {
                continue;
            }

            covered += surface.GetCoveredCellCount();
            total += Mathf.Max(1, surface.GetPaintableCellCount());
        }

        return total > 0 ? Mathf.Clamp01(covered / (float)total) : 0f;
    }

    public static bool IsGroupPainted(string groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return false;
        }

        CozyPaintableSurface[] surfaces = FindObjectsOfType<CozyPaintableSurface>(true);
        bool found = false;
        for (int i = 0; i < surfaces.Length; i++)
        {
            CozyPaintableSurface surface = surfaces[i];
            if (surface == null || surface.paintGroupId != groupId)
            {
                continue;
            }

            found = true;
            if (!surface.IsPainted)
            {
                return false;
            }
        }

        return found;
    }

    private static void CompletePaintGroup(string groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return;
        }

        CozyPaintableSurface[] surfaces = FindObjectsOfType<CozyPaintableSurface>(true);
        bool createdGroupShine = false;
        for (int i = 0; i < surfaces.Length; i++)
        {
            CozyPaintableSurface surface = surfaces[i];
            if (surface != null && surface.paintGroupId == groupId)
            {
                surface.CompletePaintSurface();
                if (!createdGroupShine)
                {
                    CreatePaintGroupCompletionShine(groupId, surfaces);
                    createdGroupShine = true;
                }
            }
        }
    }

    private static void CreatePaintGroupCompletionShine(string groupId, CozyPaintableSurface[] surfaces)
    {
        Bounds groupBounds = default;
        bool hasBounds = false;
        CozyPaintableSurface referenceSurface = null;
        for (int i = 0; i < surfaces.Length; i++)
        {
            CozyPaintableSurface surface = surfaces[i];
            if (surface == null || surface.paintGroupId != groupId)
            {
                continue;
            }

            Renderer renderer = surface.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                groupBounds = renderer.bounds;
                referenceSurface = surface;
                hasBounds = true;
            }
            else
            {
                groupBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds || referenceSurface == null)
        {
            return;
        }

        Vector3 allowedNormal = referenceSurface.GetAllowedNormal();
        Vector3 normal = referenceSurface.transform.TransformDirection(allowedNormal).normalized;
        Vector3 tangent = Mathf.Abs(allowedNormal.x) > 0.8f
            ? referenceSurface.transform.TransformDirection(Vector3.forward).normalized
            : referenceSurface.transform.TransformDirection(Vector3.right).normalized;
        Vector3 up = Vector3.Cross(-normal, tangent).normalized;
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
        }

        Vector2 size = new Vector2(
            Mathf.Max(0.05f, Vector3.ProjectOnPlane(groupBounds.size, up).magnitude),
            Mathf.Max(0.05f, Vector3.Dot(groupBounds.size, up)));
        CozySurfaceCompletionShineFx.Create(
            groupId + " Group Completion Shine",
            referenceSurface.transform.parent,
            groupBounds.center + normal * 0.025f,
            Quaternion.LookRotation(-normal, up),
            size,
            0.48f,
            1.04f,
            0.55f);
    }

    private bool IsExcludedCell(int x, int y)
    {
        if (paintExclusionRectsLocal == null || paintExclusionRectsLocal.Length == 0)
        {
            return false;
        }

        float min = -0.5f + faceInset;
        float max = 0.5f - faceInset;
        float a = Mathf.Lerp(min, max, x / (float)Mathf.Max(1, coverageGridX - 1));
        float b = Mathf.Lerp(min, max, y / (float)Mathf.Max(1, coverageGridY - 1));
        return IsInExclusionLocal(a, b);
    }

    private bool IsInExclusionLocal(float a, float b)
    {
        if (paintExclusionRectsLocal == null)
        {
            return false;
        }

        for (int i = 0; i < paintExclusionRectsLocal.Length; i++)
        {
            Rect rect = paintExclusionRectsLocal[i];
            if (a >= rect.xMin && a <= rect.xMax && b >= rect.yMin && b <= rect.yMax)
            {
                return true;
            }
        }

        return false;
    }
}
