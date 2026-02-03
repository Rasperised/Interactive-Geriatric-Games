using UnityEngine;

/// <summary>
/// Centralised audio manager for Pong.
/// Handles background music and sound effects with inspector-based volume control.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    // --------------------------------------------------
    // Background Music
    // --------------------------------------------------
    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // --------------------------------------------------
    // Sound Effect Clips
    // --------------------------------------------------
    [Header("Sound Effect Clips")]
    public AudioClip paddleHit;
    public AudioClip wallHit;
    public AudioClip score;

    // --------------------------------------------------
    // Volume Controls
    // --------------------------------------------------
    [Header("SFX Volumes")]
    [Range(0f, 1f)] public float sfxMasterVolume = 0.8f;
    [Range(0f, 1f)] public float paddleHitVolume = 0.7f;
    [Range(0f, 1f)] public float wallHitVolume = 0.6f;
    [Range(0f, 1f)] public float scoreVolume = 0.8f;

    // --------------------------------------------------
    // Internal Audio Sources
    // --------------------------------------------------
    private AudioSource sfxSource;
    private AudioSource musicSource;

    // --------------------------------------------------
    // Unity Lifecycle
    // --------------------------------------------------
    void Awake()
    {
        // Singleton enforcement
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- SFX source ---
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;

        // --- Music source ---
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 1f; // controlled in code
    }

    void Start()
    {
        SetupMusic();
    }

    // --------------------------------------------------
    // Background Music Logic
    // --------------------------------------------------
    void SetupMusic()
    {
        if (backgroundMusic == null) return;

        musicSource.clip = backgroundMusic;
        musicSource.Play();
        ApplyMusicVolume();
    }

    void ApplyMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void PlayMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
            musicSource.Play();
    }

    // --------------------------------------------------
    // Public Audio API (Used by GameController / Ball)
    // --------------------------------------------------
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;

        float volume = 1f;

        if (clip == paddleHit) volume = paddleHitVolume;
        else if (clip == wallHit) volume = wallHitVolume;
        else if (clip == score) volume = scoreVolume;

        volume *= sfxMasterVolume;
        sfxSource.PlayOneShot(clip, volume);
    }

    // --------------------------------------------------
    // Inspector Live Update
    // --------------------------------------------------
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyMusicVolume();
        }
    }
}
