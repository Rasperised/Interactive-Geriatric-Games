using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameController : MonoBehaviour
{
    // --- OpenCV Webcam ---
    [Header("Webcam Control")]
    public GameObject webcamManagerObject;
    private WebcamManager webcamManager;
    public bool useWebcamControl = false;

    // --- Prefab references ---
    public GameObject BirdPrefab;
    public GameObject PipesPrefab;

    // --- UI references ---
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI gameOverText;
    public GameObject startScreen;
    public Button startButton;
    public GameObject exitButton;

    // --- Fade control ---
    private CanvasGroup startScreenGroup;
    private CanvasGroup exitButtonGroup;
    public float fadeSpeed = 2f;
    private Coroutine gameOverBlinkCoroutine;

    // --- Pipe settings ---
    public float pipeSpawnInterval = 2f;
    public float pipeSpeed = 3f;
    public float pipeSpawnX = 10f;
    public float pipeMinY = -2f;
    public float pipeMaxY = 2f;

    // --- Game state variables ---
    private Bird birdInstance;
    private bool isGameOver = false;
    private bool gameStarted = false;
    private float spawnTimer;
    private int score;
    private static int highScore = 0;

    // --- Lists for tracking pipes ---
    private List<GameObject> pipes = new List<GameObject>();
    private HashSet<GameObject> scoredPipes = new HashSet<GameObject>();

    // --- Input system ---
    private Camera mainCam;
    private M5StickCManager m5Manager;

    // --- Key & mouse states (for supervisor tracking) ---
    public bool keyPressedW;
    public bool keyPressedA;
    public bool keyPressedS;
    public bool keyPressedD;
    public bool keyPressedSpace;
    public bool keyPressedEsc;
    public bool mouseButtonDown;
    public bool mouseButtonUp;
    public bool mouseButtonClick;
    public Vector2 mouseWorldPos;

    // --- Optional: Debug display for input mode ---
    private string currentInputMode = "Mouse";

    void Start()
    {
        mainCam = Camera.main;
        Time.timeScale = 0f;
        score = 0;
        UpdateScoreUI();
        UpdateHighScoreUI();

        // --- Find Webcam Manager ---
        if (webcamManagerObject != null)
        {
            webcamManager = webcamManagerObject.GetComponent<WebcamManager>();
            if (webcamManager == null)
                Debug.LogWarning("GameController: WebcamManager component missing.");
        }

        // --- Find M5StickC Manager ---
        m5Manager = FindFirstObjectByType<M5StickCManager>();

        // --- Setup UI ---
        startScreenGroup = startScreen.GetComponent<CanvasGroup>();
        if (exitButton != null)
            exitButtonGroup = exitButton.GetComponent<CanvasGroup>();

        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (highScoreText != null) highScoreText.gameObject.SetActive(false);

        if (startScreenGroup != null)
            startScreenGroup.alpha = 1f;
        if (exitButtonGroup != null)
            exitButtonGroup.alpha = 1f;
    }

    void Update()
    {
        GetInput(); // optional state tracking

        // Toggle webcam control with T
        if (Input.GetKeyDown(KeyCode.T))
        {
            useWebcamControl = !useWebcamControl;
            Debug.Log($"Webcam control: {useWebcamControl}");
        }

        // --- Restart or Return after Game Over ---
        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGameplay();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToStartMenu();
                return;
            }
        }

        // --- Skip updates if not in gameplay ---
        if (!gameStarted || isGameOver)
            return;

        // --- Handle Bird Input (M5StickC or Mouse) ---
        HandleBirdInput();

        // --- Gameplay Loop ---
        HandlePipeSpawning();
        MoveAndCheckPipes();
    }

    // --- Input Handling (M5StickC + fallback) ---
    void HandleBirdInput()
    {
        if (birdInstance == null)
            return;

        // --- 1) Webcam centroid control ---
        if (useWebcamControl && webcamManager != null)
        {
            if (webcamManager.GetCentroid(out Vector2 centroidWorld))
            {
                Vector3 pos = birdInstance.transform.position;
                pos.y = centroidWorld.y;
                birdInstance.transform.position = pos;
                currentInputMode = "Webcam";
            }
            return; // prevent other inputs from interfering
        }

        // --- 2) M5StickC control ---
        bool useM5 = (m5Manager != null && m5Manager.IsConnected);
        if (useM5)
        {
            float tilt = m5Manager.GetTiltValue();
            Vector3 pos = birdInstance.transform.position;
            pos.y += tilt * Time.deltaTime;
            birdInstance.transform.position = pos;
            currentInputMode = "M5StickC";
            return;
        }

        // --- 3) Mouse fallback ---
        if (mainCam == null) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f;
        Vector3 worldMousePos = mainCam.ScreenToWorldPoint(mousePos);
        birdInstance.MoveTo(worldMousePos);
        currentInputMode = "Mouse";
    }


    // --- Capture key/mouse states (for supervisor framework) ---
    void GetInput()
    {
        keyPressedW = Input.GetKey(KeyCode.W);
        keyPressedA = Input.GetKey(KeyCode.A);
        keyPressedS = Input.GetKey(KeyCode.S);
        keyPressedD = Input.GetKey(KeyCode.D);
        keyPressedSpace = Input.GetKey(KeyCode.Space);
        keyPressedEsc = Input.GetKey(KeyCode.Escape);

        mouseButtonDown = Input.GetMouseButton(0);
        mouseButtonUp = Input.GetMouseButtonUp(0);
        mouseButtonClick = Input.GetMouseButtonDown(0);

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = 10f;
        mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
    }

    // --- Start Game ---
    void StartGame()
    {
        if (gameStarted) return;
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        gameStarted = true;

        if (startScreenGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(startScreenGroup, 1f, 0f, fadeSpeed, false));

        if (exitButtonGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(exitButtonGroup, 1f, 0f, fadeSpeed, false));

        yield return new WaitForSecondsRealtime(0.1f);

        if (scoreText != null) scoreText.gameObject.SetActive(true);
        if (highScoreText != null) highScoreText.gameObject.SetActive(true);

        SpawnBird();
        Time.timeScale = 1f;
    }

    // --- Bird spawning ---
    void SpawnBird()
    {
        Vector3 startPos = new Vector3(-5f, 0f, 0f);
        GameObject b = Instantiate(BirdPrefab, startPos, Quaternion.identity);
        birdInstance = b.GetComponent<Bird>();
        birdInstance.OnBirdHitPipe += TriggerGameOver;
    }

    // --- Pipe handling ---
    void HandlePipeSpawning()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= pipeSpawnInterval)
        {
            spawnTimer = 0f;
            float randomY = Random.Range(pipeMinY, pipeMaxY);
            Vector3 spawnPos = new Vector3(pipeSpawnX, randomY, 0f);
            GameObject newPipe = Instantiate(PipesPrefab, spawnPos, Quaternion.identity);
            pipes.Add(newPipe);
        }
    }

    void MoveAndCheckPipes()
    {
        for (int i = pipes.Count - 1; i >= 0; i--)
        {
            GameObject pipe = pipes[i];
            if (pipe == null)
            {
                pipes.RemoveAt(i);
                continue;
            }

            pipe.transform.Translate(Vector2.left * pipeSpeed * Time.deltaTime);

            if (birdInstance != null &&
                !scoredPipes.Contains(pipe) &&
                pipe.transform.position.x < birdInstance.transform.position.x)
            {
                scoredPipes.Add(pipe);
                AddScore();
            }

            if (pipe.transform.position.x < -12f)
            {
                Destroy(pipe);
                pipes.RemoveAt(i);
                scoredPipes.Remove(pipe);
            }
        }
    }

    // --- Scoring ---
    void AddScore()
    {
        score++;
        UpdateScoreUI();
        AudioManager.Instance.PlayPointSound();

        if (score > highScore)
        {
            highScore = score;
            UpdateHighScoreUI();
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = "High Score: " + highScore.ToString();
    }

    // --- Game Over ---
    void TriggerGameOver()
    {
        isGameOver = true;
        Time.timeScale = 0f;

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            if (gameOverBlinkCoroutine != null)
                StopCoroutine(gameOverBlinkCoroutine);
            gameOverBlinkCoroutine = StartCoroutine(BlinkGameOverText());
        }

        AudioManager.Instance.PlayHitSound();
        Debug.Log("Game Over — Press R to Restart or ESC to return to Start Menu.");
    }

    IEnumerator BlinkGameOverText()
    {
        TextMeshProUGUI text = gameOverText;
        Color c = text.color;

        while (isGameOver)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 2f)
            {
                c.a = Mathf.Lerp(1f, 0.3f, t);
                text.color = c;
                yield return null;
            }

            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 2f)
            {
                c.a = Mathf.Lerp(0.3f, 1f, t);
                text.color = c;
                yield return null;
            }
        }

        c.a = 1f;
        text.color = c;
    }

    // --- ESC → Return to Start Menu ---
    void ReturnToStartMenu()
    {
        Time.timeScale = 0f;
        gameStarted = false;
        isGameOver = false;

        if (gameOverBlinkCoroutine != null)
            StopCoroutine(gameOverBlinkCoroutine);

        foreach (GameObject pipe in pipes)
        {
            if (pipe != null)
                Destroy(pipe);
        }
        pipes.Clear();
        scoredPipes.Clear();

        if (birdInstance != null)
        {
            Destroy(birdInstance.gameObject);
            birdInstance = null;
        }

        score = 0;
        UpdateScoreUI();

        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (highScoreText != null) scoreText.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        if (startScreenGroup != null)
            StartCoroutine(FadeCanvasGroup(startScreenGroup, 0f, 1f, fadeSpeed, true));

        if (exitButtonGroup != null)
            StartCoroutine(FadeCanvasGroup(exitButtonGroup, 0f, 1f, fadeSpeed, true));

        Debug.Log("Returned to Start Menu after Game Over.");
    }

    // --- R → Restart Gameplay Instantly ---
    void RestartGameplay()
    {
        Debug.Log("Restarting Gameplay...");

        if (gameOverBlinkCoroutine != null)
            StopCoroutine(gameOverBlinkCoroutine);

        Time.timeScale = 1f;
        isGameOver = false;
        gameStarted = true;
        score = 0;
        UpdateScoreUI();
        spawnTimer = 0f;

        foreach (GameObject pipe in pipes)
        {
            if (pipe != null)
                Destroy(pipe);
        }
        pipes.Clear();
        scoredPipes.Clear();

        if (birdInstance != null)
        {
            Destroy(birdInstance.gameObject);
            birdInstance = null;
        }

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (scoreText != null) scoreText.gameObject.SetActive(true);
        if (highScoreText != null) highScoreText.gameObject.SetActive(true);

        SpawnBird();
    }

    // --- Exit Game ---
    public void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Exit Game pressed — stopping Play Mode.");
        EditorApplication.isPlaying = false;
#else
        Debug.Log("Exit Game pressed — quitting application.");
        Application.Quit();
#endif
    }

    // --- Fade helper coroutine ---
    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float speed, bool makeInteractable)
    {
        if (group == null) yield break;

        float t = 0f;
        group.interactable = makeInteractable;
        group.blocksRaycasts = makeInteractable;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * speed;
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.interactable = makeInteractable;
        group.blocksRaycasts = makeInteractable;
    }
}
