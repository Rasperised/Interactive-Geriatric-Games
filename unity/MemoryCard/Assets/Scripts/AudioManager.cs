using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // ============================================================
    // Singleton
    // ============================================================
    // Allows global access via AudioManager.Instance
    public static AudioManager Instance { get; private set; }

    // ============================================================
    // Audio Sources
    // ============================================================
    // sfxSource  : used for all sound effects (PlayOneShot)
    // musicSource: used only for looping background music
    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    // ============================================================
    // Background Music
    // ============================================================
    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // ============================================================
    // SFX Volume Controls
    // ============================================================
    // Master volume affects ALL SFX
    [Header("SFX – Master")]
    [Range(0f, 1f)] public float sfxMasterVolume = 0.8f;

    // Individual per-sound volume multipliers
    [Header("SFX – Individual")]
    [Range(0f, 1f)] public float flipVolume = 0.7f;
    [Range(0f, 1f)] public float matchVolume = 0.9f;
    [Range(0f, 1f)] public float mismatchVolume = 0.6f;
    [Range(0f, 1f)] public float winVolume = 1.0f;

    // ============================================================
    // Audio Clips
    // ============================================================
    [Header("SFX Clips")]
    public AudioClip cardFlip;
    public AudioClip cardMatch;
    public AudioClip cardMismatch;
    public AudioClip winSound;

    // ============================================================
    // Unity Lifecycle
    // ============================================================
    void Awake()
    {
        // Enforce singleton (only one AudioManager allowed)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Persist across scene reloads
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Start background music when the scene starts
        SetupMusic();
    }

    // ============================================================
    // Background Music Logic
    // ============================================================
    void SetupMusic()
    {
        if (musicSource == null || backgroundMusic == null) return;

        musicSource.clip = backgroundMusic;
        musicSource.loop = true;

        // Lock AudioSource volume so only AudioManager controls loudness
        musicSource.volume = 1f;

        musicSource.Play();
        ApplyMusicVolume();
    }

    void ApplyMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    // Public method for UI sliders or code-based control
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

    // ============================================================
    // Sound Effects Logic
    // ============================================================
    public void PlayFlip()
    {
        PlaySFX(cardFlip, flipVolume);
    }

    public void PlayMatch()
    {
        PlaySFX(cardMatch, matchVolume);
    }

    public void PlayMismatch()
    {
        PlaySFX(cardMismatch, mismatchVolume);
    }

    public void PlayWin()
    {
        PlaySFX(winSound, winVolume);
    }

    // Core SFX playback function
    void PlaySFX(AudioClip clip, float individualVolume)
    {
        if (clip == null || sfxSource == null) return;

        float finalVolume = sfxMasterVolume * individualVolume;
        sfxSource.PlayOneShot(clip, finalVolume);
    }

    public void SetSfxMasterVolume(float value)
    {
        sfxMasterVolume = Mathf.Clamp01(value);
    }

    // ============================================================
    // Inspector Live Update (Play Mode Tuning)
    // ============================================================
    void OnValidate()
    {
        // Allows Music Volume slider to update live while playing
        if (Application.isPlaying)
        {
            ApplyMusicVolume();
        }
    }
}
