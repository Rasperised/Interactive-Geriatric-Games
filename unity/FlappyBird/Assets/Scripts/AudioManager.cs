using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // ============================================================
    // Singleton
    // ============================================================
    public static AudioManager Instance { get; private set; }

    // ============================================================
    // Audio Clips
    // ============================================================
    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Sound Effects")]
    public AudioClip pointSound;
    public AudioClip hitSound;

    [Header("SFX Volume")]
    [Range(0f, 1f)] public float sfxMasterVolume = 0.8f;

    // ============================================================
    // Audio Sources
    // ============================================================
    private AudioSource sfxSource;
    private AudioSource musicSource;

    void Awake()
    {
        // Ensure only one AudioManager exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- SFX Source ---
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = 1f; // controlled in code

        // --- Music Source ---
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 1f; // controlled in code
    }

    void Start()
    {
        SetupMusic();
    }

    // ============================================================
    // Background Music
    // ============================================================
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

    // ============================================================
    // Sound Effects
    // ============================================================
    public void PlayPointSound()
    {
        PlaySFX(pointSound);
    }

    public void PlayHitSound()
    {
        PlaySFX(hitSound);
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;

        sfxSource.PlayOneShot(clip, sfxMasterVolume);
    }

    // ============================================================
    // Inspector Live Update
    // ============================================================
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyMusicVolume();
        }
    }
}
