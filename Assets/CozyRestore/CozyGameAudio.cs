using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyGameAudio : MonoBehaviour
{
    public float ambientVolume = 0.38f;
    public float lobbyMusicVolume = 0.30f;
    public float gothicBirdVolume = 0.20f;
    public float installVolume = 0.82f;

    private AudioSource ambientSource;
    private AudioSource oneShotSource;
    private AudioClip lobbyMusicClip;
    private AudioClip gothicBirdClip;
    private AudioClip modernCityBirdClip;
    private AudioClip woodInstallClip;
    private AudioClip marbleInstallClip;
    private float lastFloorInstallSoundTime = -10f;
    private int rapidFloorInstallSoundCount;

    private const string SoundAssetFolder = "Assets/Resource/Sound";
    private const float RapidInstallSoundWindow = 0.16f;

    private void Awake()
    {
        EnsureReady();
    }

    public void EnsureReady()
    {
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f;
            ambientSource.volume = ambientVolume;
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;
            oneShotSource.volume = installVolume;
        }

        LoadClips();
    }

    public void PlayLobbyMusic()
    {
        PlayAmbient(lobbyMusicClip, lobbyMusicVolume);
    }

    public void PlayLevelAmbience(bool modernTheme)
    {
        PlayAmbient(modernTheme ? modernCityBirdClip : gothicBirdClip, modernTheme ? ambientVolume : gothicBirdVolume);
    }

    public void PlayFloorFinishInstall(bool modernTheme)
    {
        EnsureReady();
        AudioClip clip = modernTheme ? marbleInstallClip : woodInstallClip;
        if (oneShotSource == null || clip == null)
        {
            return;
        }

        oneShotSource.pitch = Random.Range(0.96f, 1.04f);
        oneShotSource.PlayOneShot(clip, installVolume * GetFloorInstallVolumeScale());
    }

    private float GetFloorInstallVolumeScale()
    {
        float elapsed = Time.time - lastFloorInstallSoundTime;
        if (elapsed <= RapidInstallSoundWindow)
        {
            rapidFloorInstallSoundCount++;
        }
        else
        {
            rapidFloorInstallSoundCount = 0;
        }

        lastFloorInstallSoundTime = Time.time;
        if (rapidFloorInstallSoundCount <= 0)
        {
            return 1f;
        }

        float t = Mathf.Clamp01((rapidFloorInstallSoundCount - 1) / 5f);
        return Mathf.Lerp(0.58f, 0.24f, t);
    }

    private void PlayAmbient(AudioClip clip, float volume)
    {
        EnsureReady();
        if (ambientSource == null || clip == null)
        {
            return;
        }

        ambientSource.volume = volume;
        ambientSource.pitch = 1f;
        if (ambientSource.clip == clip && ambientSource.isPlaying)
        {
            return;
        }

        ambientSource.clip = clip;
        ambientSource.time = 0f;
        ambientSource.Play();
    }

    private void LoadClips()
    {
        if (lobbyMusicClip != null)
        {
            return;
        }

        lobbyMusicClip = LoadSoundClip("\uBC30\uACBD\uC74C");
        gothicBirdClip = LoadSoundClip("\uC0C8\uC18C\uB9AC");
        modernCityBirdClip = LoadSoundClip("\uB3C4\uC2DC\uC0C8\uC18C\uB9AC");
        woodInstallClip = LoadSoundClip("\uB098\uBB34\uD310\uC790\uC124\uCE58\uC18C\uB9AC");
        marbleInstallClip = LoadSoundClip("\uB300\uB9AC\uC11D\uD310\uC790\uC124\uCE58\uC18C\uB9AC");
    }

    private AudioClip LoadSoundClip(string clipName)
    {
#if UNITY_EDITOR
        AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundAssetFolder + "/" + clipName + ".mp3");
        if (editorClip != null)
        {
            return editorClip;
        }
#endif
        return Resources.Load<AudioClip>("Sound/" + clipName);
    }
}
