using UnityEngine;

public enum CozyCleaningLoopSound
{
    FloorMop,
    PaintRoller,
    WindowWiper
}

public enum CozyCleaningOneShotSound
{
    DustClean,
    ObstacleRemove
}

public sealed class CozyCleaningAudio : MonoBehaviour
{
    public float masterVolume = 0.62f;
    public float loopFadeSpeed = 12f;

    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private AudioClip floorMopClip;
    private AudioClip paintRollerClip;
    private AudioClip windowWiperClip;
    private AudioClip dustCleanClip;
    private AudioClip obstacleRemoveClip;
    private float loopRefreshTime = -10f;
    private bool hasActiveLoop;

    private const int SampleRate = 22050;
    private const float LoopKeepAlive = 0.08f;

    private void Awake()
    {
        EnsureReady();
    }

    private void Update()
    {
        if (loopSource == null)
        {
            return;
        }

        bool shouldPlay = hasActiveLoop && Time.time - loopRefreshTime <= LoopKeepAlive;
        float targetVolume = shouldPlay ? masterVolume : 0f;
        loopSource.volume = Mathf.MoveTowards(loopSource.volume, targetVolume, loopFadeSpeed * Time.deltaTime);

        if (!shouldPlay && loopSource.volume <= 0.001f && loopSource.isPlaying)
        {
            loopSource.Stop();
            hasActiveLoop = false;
        }
    }

    public void PlayLoop(CozyCleaningLoopSound loopSound)
    {
        EnsureReady();
        if (loopSource == null)
        {
            return;
        }

        AudioClip clip = GetLoopClip(loopSound);
        if (clip == null)
        {
            return;
        }

        if (loopSource.clip != clip)
        {
            loopSource.clip = clip;
            loopSource.volume = 0f;
            loopSource.Play();
        }
        else if (!loopSource.isPlaying)
        {
            loopSource.Play();
        }

        hasActiveLoop = true;
        loopRefreshTime = Time.time;
        loopSource.pitch = GetLoopPitch(loopSound);
    }

    public void StopLoop()
    {
        hasActiveLoop = false;
        loopRefreshTime = -10f;
    }

    public void PlayOneShot(CozyCleaningOneShotSound sound)
    {
        EnsureReady();
        if (oneShotSource == null)
        {
            return;
        }

        AudioClip clip = GetOneShotClip(sound);
        if (clip == null)
        {
            return;
        }

        oneShotSource.pitch = GetOneShotPitch(sound);
        oneShotSource.PlayOneShot(clip, masterVolume);
    }

    public void EnsureReady()
    {
        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f;
            loopSource.volume = 0f;
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;
            oneShotSource.volume = masterVolume;
        }

        if (floorMopClip == null)
        {
            floorMopClip = CreateNoiseLoop("CR_FloorMopLoop", 0.62f, 0.22f, 0.08f, 0.95f, 0.14f);
            paintRollerClip = CreateNoiseLoop("CR_PaintRollerLoop", 0.72f, 0.18f, 0.13f, 0.62f, 0.10f);
            windowWiperClip = CreateNoiseLoop("CR_WindowWiperLoop", 0.5f, 0.28f, 0.24f, 1.35f, 0.09f);
            dustCleanClip = CreateBurst("CR_DustClean", 0.16f, 0.95f, 0.20f, 0.26f);
            obstacleRemoveClip = CreateBurst("CR_ObstacleRemove", 0.22f, 0.55f, 0.12f, 0.42f);
        }
    }

    private AudioClip GetLoopClip(CozyCleaningLoopSound loopSound)
    {
        switch (loopSound)
        {
            case CozyCleaningLoopSound.FloorMop:
                return floorMopClip;
            case CozyCleaningLoopSound.PaintRoller:
                return paintRollerClip;
            case CozyCleaningLoopSound.WindowWiper:
                return windowWiperClip;
            default:
                return null;
        }
    }

    private AudioClip GetOneShotClip(CozyCleaningOneShotSound sound)
    {
        switch (sound)
        {
            case CozyCleaningOneShotSound.DustClean:
                return dustCleanClip;
            case CozyCleaningOneShotSound.ObstacleRemove:
                return obstacleRemoveClip;
            default:
                return null;
        }
    }

    private float GetLoopPitch(CozyCleaningLoopSound loopSound)
    {
        switch (loopSound)
        {
            case CozyCleaningLoopSound.FloorMop:
                return 0.86f;
            case CozyCleaningLoopSound.PaintRoller:
                return 0.96f;
            case CozyCleaningLoopSound.WindowWiper:
                return 1.18f;
            default:
                return 1f;
        }
    }

    private float GetOneShotPitch(CozyCleaningOneShotSound sound)
    {
        switch (sound)
        {
            case CozyCleaningOneShotSound.DustClean:
                return Random.Range(0.94f, 1.08f);
            case CozyCleaningOneShotSound.ObstacleRemove:
                return Random.Range(0.88f, 1.02f);
            default:
                return 1f;
        }
    }

    private AudioClip CreateNoiseLoop(string clipName, float seconds, float roughness, float toneAmount, float pulseRate, float gain)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
        float[] data = new float[sampleCount];
        uint seed = 2166136261u;
        float smoothedNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 9) / 8388607f) * 2f - 1f;
            smoothedNoise = Mathf.Lerp(smoothedNoise, noise, roughness);

            float pulse = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * pulseRate * t);
            float tone = Mathf.Sin(2f * Mathf.PI * 120f * t) * toneAmount;
            float envelope = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, sampleCount - 1));
            data[i] = Mathf.Clamp((smoothedNoise + tone) * pulse * Mathf.Lerp(0.75f, 1f, envelope) * gain, -1f, 1f);
        }

        MatchLoopEnds(data);
        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateBurst(string clipName, float seconds, float roughness, float toneAmount, float gain)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
        float[] data = new float[sampleCount];
        uint seed = 2166136261u;
        float smoothedNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
            seed = seed * 1103515245u + 12345u;
            float noise = ((seed >> 9) / 8388607f) * 2f - 1f;
            smoothedNoise = Mathf.Lerp(smoothedNoise, noise, roughness);

            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 90f, normalized) * t) * toneAmount;
            float envelope = Mathf.Exp(-normalized * 6.5f);
            data[i] = Mathf.Clamp((smoothedNoise + tone) * envelope * gain, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void MatchLoopEnds(float[] data)
    {
        int fadeSamples = Mathf.Min(data.Length / 4, Mathf.RoundToInt(SampleRate * 0.04f));
        for (int i = 0; i < fadeSamples; i++)
        {
            float t = i / (float)Mathf.Max(1, fadeSamples - 1);
            float blendedStart = Mathf.Lerp(data[i], data[data.Length - fadeSamples + i], 1f - t);
            float blendedEnd = Mathf.Lerp(data[data.Length - fadeSamples + i], data[i], t);
            data[i] = blendedStart;
            data[data.Length - fadeSamples + i] = blendedEnd;
        }
    }
}
