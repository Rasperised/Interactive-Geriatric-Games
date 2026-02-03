using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Central controller for the Pong game.
/// Implements a clear state machine with structured per-frame logic:
/// 1) Process Inputs
/// 2) Update Game Entities
/// 3) Update Score / Status
/// 4) Check Transitions and End Conditions
/// </summary>
public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    // -----------------------
    // ENUMERATIONS
    // -----------------------
    public enum GameState { Menu, Initializing, StartingGame, Playing, EndGame }
    public enum Difficulty { Easy, Medium, Hard }

    [System.Serializable]
    public class DifficultySettings
    {
        [Header("AI Behavior")]
        public float aiFollowSpeed = 4f;
        public float aiUpdateInterval = 0.25f;
        [Range(0f, 1f)] public float aiDistractionChance = 0.15f;

        [Header("Ball Settings")]
        public float ballSpeedMultiplier = 1.05f;
    }

    // -----------------------
    // DIFFICULTY PRESETS
    // -----------------------
    [Header("Difficulty Presets")]
    public DifficultySettings easySettings = new DifficultySettings
    {
        aiFollowSpeed = 3f,
        aiUpdateInterval = 0.35f,
        aiDistractionChance = 0.25f,
        ballSpeedMultiplier = 1.03f
    };

    public DifficultySettings mediumSettings = new DifficultySettings
    {
        aiFollowSpeed = 4.5f,
        aiUpdateInterval = 0.25f,
        aiDistractionChance = 0.15f,
        ballSpeedMultiplier = 1.05f
    };

    public DifficultySettings hardSettings = new DifficultySettings
    {
        aiFollowSpeed = 6f,
        aiUpdateInterval = 0.15f,
        aiDistractionChance = 0.05f,
        ballSpeedMultiplier = 1.08f
    };

    // -----------------------
    // REFERENCES
    // -----------------------
    [Header("References")]
    public GameObject ballPrefab;
    public Transform leftPaddle;
    public Transform rightPaddle;
    public Transform ballSpawnPoint;

    [Header("UI References")]
    public TextMeshProUGUI leftScoreText;
    public TextMeshProUGUI rightScoreText;
    public TextMeshProUGUI restartText;
    public TextMeshProUGUI startMessage;
    public TextMeshProUGUI winMessage;

    [Header("Difficulty Buttons")]
    public CanvasGroup difficultyCanvasGroup;
    public GameObject buttonEasy;
    public GameObject buttonMedium;
    public GameObject buttonHard;
    public GameObject buttonExit; // Exit button in menu

    [Header("Webcam Manager")]
    public GameObject webcamManager; 
    private WebcamManager webcamScript; // Cached reference to WebcamManager component

    // -----------------------
    // GAMEPLAY SETTINGS
    // -----------------------
    [Header("Gameplay Settings")]
    public float paddleSpeed = 5f;
    public float ballSpeed = 6f;
    public int maxScore = 5;
    public float maxBallSpeed = 12f;
    public int hitsBeforeSpeedIncrease = 3;

    // -----------------------
    // PRIVATE RUNTIME STATE
    // -----------------------
    private Difficulty difficulty = Difficulty.Medium;
    private GameObject currentBall;
    private Rigidbody ballRb;
    private int scoreLeft = 0;
    private int scoreRight = 0;
    private bool startingSequenceRunning = false;
    [HideInInspector] public bool inputLocked = true;
    private bool endGameSequenceRunning = false;
    private Coroutine winRestartCo = null;
    private Coroutine menuFadeCo = null;
    [HideInInspector] public GameState state = GameState.Menu;

    [HideInInspector] public float moveInputLeft;
    [HideInInspector] public bool keyPressedR;
    [HideInInspector] public bool keyPressedEsc;

    [HideInInspector] public float aiFollowSpeed;
    [HideInInspector] public float aiUpdateInterval;
    [HideInInspector] public float aiDistractionChance;
    [HideInInspector] public float ballSpeedMultiplier;

    private float aiNextUpdateTime = 0f;
    private float aiTargetY = 0f;
    private bool aiDistracted = false;

    private Vector3 leftPaddleStartPos;
    private Vector3 rightPaddleStartPos;

    // -----------------------
    // UNITY LIFECYCLE
    // -----------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        leftPaddleStartPos = leftPaddle.position;
        rightPaddleStartPos = rightPaddle.position;

        // Setup for Webcam Centroid Tracking 
        // Cache webcam manager component (may be null if not assigned)
        if (webcamManager != null)
        {
            webcamScript = webcamManager.GetComponent<WebcamManager>();
            if (webcamScript == null)
            {
                Debug.LogWarning("GameController: webcamManager GameObject does not have WebcamManager component.");
            }
        }

        // Hide everything at start
        leftPaddle.gameObject.SetActive(false);
        rightPaddle.gameObject.SetActive(false);
        leftScoreText.gameObject.SetActive(false);
        rightScoreText.gameObject.SetActive(false);
        restartText.gameObject.SetActive(false);
        winMessage.gameObject.SetActive(false);

        // Show menu
        ShowDifficultyMenu(true);
        state = GameState.Menu;
    }

    // -----------------------
    // STATE MACHINE LOOP
    // -----------------------
    void Update()
    {
        switch (state)
        {
            case GameState.Menu:
                GetInputs();
                break;

            case GameState.Initializing:
                InitializeGame();
                state = GameState.StartingGame;
                break;

            case GameState.StartingGame:
                GetInputs();
                if (!startingSequenceRunning)
                    StartCoroutine(RunStartSequence());
                break;

            case GameState.Playing:
                GetInputs();
                MovePaddles();
                CheckBallOutOfBounds();
                if (!endGameSequenceRunning && (scoreLeft >= maxScore || scoreRight >= maxScore))
                    StartCoroutine(EndGameSequence());
                break;

            case GameState.EndGame:
                GetInputs();
                if (keyPressedR) RestartSameGame();
                if (keyPressedEsc) ReturnToMenu();
                break;
        }
    }

    private bool WebcamTracking = false;

    // -----------------------
    // INPUT HANDLER
    // -----------------------
    void GetInputs()
    {
        if (Keyboard.current == null) return;

        moveInputLeft = 0f;
        if (!inputLocked)
        {
            if (Keyboard.current.dKey.isPressed) moveInputLeft = 1f;
            if (Keyboard.current.aKey.isPressed) moveInputLeft = -1f;
        }


        // Toggle WebcamTracking when 't' or 'T' is pressed
        if (Input.GetKeyDown(KeyCode.T))
        {
            WebcamTracking = !WebcamTracking;
            Debug.Log($"WebcamTracking: {WebcamTracking}");
        }

 

        keyPressedR = Keyboard.current.rKey.wasPressedThisFrame;
        keyPressedEsc = Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    // -----------------------
    // INITIALIZATION HELPERS
    // -----------------------
    void InitializeGame()
    {
        ApplyDifficultySettings();
        ResetPaddlesToStart();

        if (currentBall != null) Destroy(currentBall);
        currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, Quaternion.identity);
        ballRb = currentBall.GetComponent<Rigidbody>();

        scoreLeft = scoreRight = 0;
        inputLocked = true;
        aiNextUpdateTime = 0f;
        aiDistracted = false;
        aiTargetY = rightPaddle.position.y;
        UpdateScoreUI();

        restartText.gameObject.SetActive(false);
        startMessage.gameObject.SetActive(false);
        winMessage.gameObject.SetActive(false);
    }

    // -----------------------
    // PADDLE & BALL LOGIC
    // -----------------------
    void MovePaddles()
    {
        // --- Player paddle movement ---
        // original code:
        leftPaddle.Translate(Vector3.up * moveInputLeft * paddleSpeed * Time.deltaTime, Space.World);

        // Webcam Centroid Tracking code (this overrides keyboard input moveInputLeft above)
        if (WebcamTracking && webcamScript != null)
        {
            if (webcamScript.GetCentroid(out Vector2 centroidWorld))
            {
                // centroidWorld is in world coordinates returned by webcam manager
                float centX = centroidWorld.x;
                float centY = centroidWorld.y;

                float PosX = leftPaddle.position.x; // original X position
                float PosY = leftPaddle.position.y; // original Y position
                float PosZ = leftPaddle.position.z; // original Z position

                // immediately update leftPaddle visual Y position
                //leftPaddle.Translate(Vector3.up * centY * paddleSpeed * Time.deltaTime, Space.World);
                leftPaddle.transform.position = new Vector3(PosX, centY, PosZ);
                Debug.Log($"Webcam Centroid Y: {centY:F2}");
            }
        }

        // --- Opponent AI paddle movement ---
        if (ballRb != null)
        {
            if (Time.time >= aiNextUpdateTime)
            {
                aiNextUpdateTime = Time.time + aiUpdateInterval;
                aiDistracted = (Random.value < aiDistractionChance);
                if (!aiDistracted)
                    aiTargetY = ballRb.position.y + Random.Range(-0.4f, 0.4f);
            }

            if (!aiDistracted)
            {
                float diff = aiTargetY - rightPaddle.position.y;
                float moveDir = Mathf.Sign(diff);
                rightPaddle.Translate(Vector3.up * moveDir * aiFollowSpeed * Time.deltaTime, Space.World);
            }
        }

        // --- Clamp paddle positions (Y-axis only) ---
        float clampY = 4.2f;
        leftPaddle.position = new Vector3(leftPaddle.position.x, Mathf.Clamp(leftPaddle.position.y, -clampY, clampY), leftPaddle.position.z);
        rightPaddle.position = new Vector3(rightPaddle.position.x, Mathf.Clamp(rightPaddle.position.y, -clampY, clampY), rightPaddle.position.z);
    }

    void LaunchBall()
    {
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();

        // --- Launch ball along X and Y plane ---
        Vector3 direction = new Vector3(Random.Range(0, 2) == 0 ? 1 : -1, Random.Range(-0.4f, 0.4f), 0);
        ballRb.linearVelocity = direction.normalized * ballSpeed;
    }

    void CheckBallOutOfBounds()
    {
        if (ballRb == null) return;

        if (ballRb.position.x < -9f)
        {
            scoreRight++;
            UpdateScoreUI();
            // 🔊 AUDIO: score sound
            AudioManager.Instance.PlaySFX(AudioManager.Instance.score);
            ResetBall();
        }
        else if (ballRb.position.x > 9f)
        {
            scoreLeft++;
            UpdateScoreUI();
            // 🔊 AUDIO: score sound
            AudioManager.Instance.PlaySFX(AudioManager.Instance.score);
            ResetBall();
        }
    }

    void ResetBall()
    {
        // Stop ball motion and reposition it
        ballRb.linearVelocity = Vector3.zero;
        ballRb.position = ballSpawnPoint.position;

        // ✅ Reset internal speed tracker to base
        Ball ballScript = currentBall.GetComponent<Ball>();
        if (ballScript != null)
        {
            ballScript.ResetSpeedToBase();
        }

        // Relaunch after short delay
        StartCoroutine(WaitAndLaunch());
    }

    IEnumerator WaitAndLaunch()
    {
        inputLocked = true;
        yield return new WaitForSeconds(1f);
        if (state == GameState.Playing)
            LaunchBall();
        inputLocked = false;
    }

    // -----------------------
    // START & END SEQUENCES
    // -----------------------
    IEnumerator RunStartSequence()
    {
        startingSequenceRunning = true;
        inputLocked = true;

        yield return StartCoroutine(ShowStartText(startMessage, "Ready?", 0.25f, 0.8f, 1.1f));
        yield return StartCoroutine(ShowStartText(startMessage, "Go!", 0.25f, 0.5f, 1.2f));

        LaunchBall();
        inputLocked = false;
        state = GameState.Playing;
        startingSequenceRunning = false;
    }

    IEnumerator EndGameSequence()
    {
        endGameSequenceRunning = true;
        inputLocked = true;
        state = GameState.EndGame;

        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
        }

        if (currentBall != null)
            currentBall.SetActive(false);

        string winner = (scoreLeft > scoreRight) ? "Blue Player Wins!" : "Red Player Wins!";
        winRestartCo = StartCoroutine(ShowWinMessageHold(winMessage, restartText, winner, 0.4f));
        yield return winRestartCo;
        winRestartCo = null;
        endGameSequenceRunning = false;
    }

    // -----------------------
    // STATE TRANSITIONS
    // -----------------------
    void RestartSameGame()
    {
        if (winRestartCo != null)
        {
            StopCoroutine(winRestartCo);
            winRestartCo = null;
        }

        if (currentBall != null)
        {
            Destroy(currentBall);
            currentBall = null;
            ballRb = null;
        }

        ResetPaddlesToStart();
        leftPaddle.gameObject.SetActive(true);
        rightPaddle.gameObject.SetActive(true);
        state = GameState.Initializing;
    }

    void ReturnToMenu()
    {
        if (winRestartCo != null)
        {
            StopCoroutine(winRestartCo);
            winRestartCo = null;
        }

        // Hide all game-related UI
        winMessage.gameObject.SetActive(false);
        restartText.gameObject.SetActive(false);
        leftScoreText.gameObject.SetActive(false);
        rightScoreText.gameObject.SetActive(false);
        leftPaddle.gameObject.SetActive(false);
        rightPaddle.gameObject.SetActive(false);

        if (currentBall != null)
        {
            Destroy(currentBall);
            currentBall = null;
            ballRb = null;
        }

        ResetPaddlesToStart();
        ShowDifficultyMenu(true);

        state = GameState.Menu;
        endGameSequenceRunning = false;
    }

    // -----------------------
    // EXIT BUTTON
    // -----------------------
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -----------------------
    // DIFFICULTY MANAGEMENT
    // -----------------------
    void ApplyDifficultySettings()
    {
        DifficultySettings selected = difficulty switch
        {
            Difficulty.Easy => easySettings,
            Difficulty.Medium => mediumSettings,
            Difficulty.Hard => hardSettings,
            _ => mediumSettings
        };

        aiFollowSpeed = selected.aiFollowSpeed;
        aiUpdateInterval = selected.aiUpdateInterval;
        aiDistractionChance = selected.aiDistractionChance;
        ballSpeedMultiplier = selected.ballSpeedMultiplier;
    }

    public void SetDifficultyEasy() { difficulty = Difficulty.Easy; StartCoroutine(StartGameTransition()); }
    public void SetDifficultyMedium() { difficulty = Difficulty.Medium; StartCoroutine(StartGameTransition()); }
    public void SetDifficultyHard() { difficulty = Difficulty.Hard; StartCoroutine(StartGameTransition()); }

    IEnumerator StartGameTransition()
    {
        yield return StartCoroutine(FadeDifficultyMenu(false));
        leftPaddle.gameObject.SetActive(true);
        rightPaddle.gameObject.SetActive(true);
        leftScoreText.gameObject.SetActive(true);
        rightScoreText.gameObject.SetActive(true);
        state = GameState.Initializing;
    }

    // -----------------------
    // UI ANIMATIONS & HELPERS
    // -----------------------
    void ShowDifficultyMenu(bool show)
    {
        if (difficultyCanvasGroup == null) return;

        if (menuFadeCo != null)
        {
            StopCoroutine(menuFadeCo);
            menuFadeCo = null;
        }
        menuFadeCo = StartCoroutine(FadeDifficultyMenu(show));
    }

    IEnumerator FadeDifficultyMenu(bool fadeIn)
    {
        float duration = 0.8f;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float time = 0f;

        difficultyCanvasGroup.gameObject.SetActive(true);
        difficultyCanvasGroup.alpha = startAlpha;
        difficultyCanvasGroup.interactable = false;
        difficultyCanvasGroup.blocksRaycasts = false;

        while (time < duration)
        {
            time += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            difficultyCanvasGroup.alpha = a;
            yield return null;
        }

        difficultyCanvasGroup.alpha = endAlpha;
        difficultyCanvasGroup.interactable = fadeIn;
        difficultyCanvasGroup.blocksRaycasts = fadeIn;
        difficultyCanvasGroup.gameObject.SetActive(fadeIn);
    }

    IEnumerator ShowStartText(TextMeshProUGUI ui, string text, float fadeDuration, float displayDuration, float popScale)
    {
        if (ui == null) yield break;
        ui.gameObject.SetActive(true);
        ui.text = text;

        Color color = ui.color;
        Vector3 originalScale = ui.transform.localScale;
        color.a = 0f;
        ui.color = color;
        ui.transform.localScale = Vector3.one * (popScale * 0.6f);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            color.a = a;
            ui.color = color;
            ui.transform.localScale = Vector3.Lerp(Vector3.one * (popScale * 0.6f), Vector3.one * popScale, a);
            yield return null;
        }

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeDuration);
            color.a = a;
            ui.color = color;
            yield return null;
        }

        ui.gameObject.SetActive(false);
        ui.transform.localScale = originalScale;
    }

    IEnumerator ShowWinMessageHold(TextMeshProUGUI winMsg, TextMeshProUGUI restartMsg, string winnerText, float fadeInDuration)
    {
        if (winMsg == null || restartMsg == null) yield break;

        winMsg.text = winnerText;
        restartMsg.text = "Press R to Restart\nEsc for Menu";
        winMsg.gameObject.SetActive(true);
        restartMsg.gameObject.SetActive(true);

        Color winColor = winMsg.color; winColor.a = 0f;
        Color restartColor = restartMsg.color; restartColor.a = 0f;
        winMsg.color = winColor;
        restartMsg.color = restartColor;

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0f, 1f, t / fadeInDuration);
            winColor.a = a; winMsg.color = winColor;
            restartColor.a = a; restartMsg.color = restartColor;
            yield return null;
        }
    }

    void UpdateScoreUI()
    {
        if (leftScoreText != null) leftScoreText.text = scoreLeft.ToString();
        if (rightScoreText != null) rightScoreText.text = scoreRight.ToString();
    }

    void ResetPaddlesToStart()
    {
        leftPaddle.position = leftPaddleStartPos;
        rightPaddle.position = rightPaddleStartPos;

        // --- Do not modify Rigidbody velocities (paddles are kinematic) ---
        // This removes "Setting linear velocity of a kinematic body is not supported" warnings.
    }
}
