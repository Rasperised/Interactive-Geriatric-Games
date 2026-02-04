using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // =======================
    // Audio Sources
    // =======================
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    // =======================
    // Background Music
    // =======================
    [Header("Background Music")]
    public AudioClip backgroundMusic;

    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    // =======================
    // Win Sound
    // =======================
    [Header("Win Sound")]
    public AudioClip winClip;

    [Range(0f, 1f)]
    public float winVolume = 0.8f;

    // =======================
    // Correct Tile Placement Sound
    // =======================
    [Header("Tile Placement")]
    public AudioClip correctPlaceClip;

    [Range(0f, 1f)]
    public float correctPlaceVolume = 0.7f;

    // =======================
    // Unity Lifecycle
    // =======================
    void Awake()
    {
        // Singleton safety
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        SetupSources();
        PlayMusic();
    }

    void OnValidate()
    {
        // Live Inspector tuning
        if (musicSource != null)
            musicSource.volume = musicVolume;

        if (sfxSource != null)
            sfxSource.volume = winVolume;
    }

    // =======================
    // Setup
    // =======================
    void SetupSources()
    {
        // Music source
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        // SFX source (win sound)
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = winVolume;
        sfxSource.spatialBlend = 0f; // 2D sound
    }

    // =======================
    // Music Control
    // =======================
    void PlayMusic()
    {
        if (backgroundMusic == null) return;

        musicSource.clip = backgroundMusic;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    // =======================
    // SFX Control
    // =======================
    public void PlayWinSound()
    {
        if (winClip == null || sfxSource == null) return;

        sfxSource.PlayOneShot(winClip, winVolume);
    }
    public void PlayPlaceCorrectSound()
    {
        if (correctPlaceClip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(correctPlaceClip, correctPlaceVolume);
    }

}
