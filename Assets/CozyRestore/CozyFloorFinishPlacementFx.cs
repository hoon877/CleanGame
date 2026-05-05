using UnityEngine;

public sealed class CozyFloorFinishPlacementFx : MonoBehaviour
{
    private const float DefaultDuration = 0.24f;
    private const float DropHeight = 0.16f;
    private const float StartScale = 0.28f;
    private const float PopScale = 1.08f;

    private float duration = DefaultDuration;
    private float elapsed;
    private Vector3 finalLocalPosition;
    private Vector3 finalLocalScale;
    private Quaternion finalLocalRotation;
    private Quaternion startLocalRotation;

    public static void Play(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        CozyFloorFinishPlacementFx fx = target.GetComponent<CozyFloorFinishPlacementFx>();
        if (fx == null)
        {
            fx = target.AddComponent<CozyFloorFinishPlacementFx>();
        }

        fx.Begin();
    }

    private void Begin()
    {
        elapsed = 0f;
        finalLocalPosition = transform.localPosition;
        finalLocalScale = transform.localScale;
        finalLocalRotation = transform.localRotation;
        startLocalRotation = finalLocalRotation * Quaternion.Euler(-8f, 0f, 0f);

        transform.localPosition = finalLocalPosition + Vector3.up * DropHeight;
        transform.localScale = finalLocalScale * StartScale;
        transform.localRotation = startLocalRotation;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        float settle = EaseOutCubic(t);

        transform.localPosition = Vector3.LerpUnclamped(finalLocalPosition + Vector3.up * DropHeight, finalLocalPosition, settle);
        transform.localRotation = Quaternion.SlerpUnclamped(startLocalRotation, finalLocalRotation, settle);

        float scaleMultiplier = t < 0.72f
            ? Mathf.Lerp(StartScale, PopScale, EaseOutCubic(t / 0.72f))
            : Mathf.Lerp(PopScale, 1f, EaseOutBack((t - 0.72f) / 0.28f));
        transform.localScale = finalLocalScale * scaleMultiplier;

        if (t >= 1f)
        {
            transform.localPosition = finalLocalPosition;
            transform.localScale = finalLocalScale;
            transform.localRotation = finalLocalRotation;
            Destroy(this);
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
