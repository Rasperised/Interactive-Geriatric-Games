using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TouchPhase = UnityEngine.TouchPhase;
using SFB;

/// <summary>
/// Central game controller for the reference memory game.
/// Uses a small state machine and a 4-step per-frame pattern inside each state:
/// 1) Process inputs
/// 2) Update game content
/// 3) Update scoring/status
/// 4) Check transitions / end-stage
/// </summary>
public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    enum GameState
    {
        Initializing,
        StartingGame,
        Playing,
        EndGame
    }

    public enum ImageSourceType
    {
        Food_Indian,
        Food_Malay,
        Food_Chinese,

        Places_Landmarks,
        Places_Historic,
        Places_Neighbourhoods,

        Uploaded
    }

    private ImageSourceType selectedSource;

    private bool startingSequenceRunning = false;
    private bool endingSequenceRunning = false;
    private const float SPRITE_PPU = 100f;
    private const int TARGET_WIDTH = 640;
    private const int TARGET_HEIGHT = 480;

    [Header("Image Folder")]
    public string imageFolderPath = @"D:\Unity\MemoryCardv0.0\Assets\Resources\Cards";
    [Tooltip("If true use LoadFromFolder (editor/testing). If false use Resources.Load.")]
    public bool useFolderLoader = true;

    [Header("UI Panels")]
    public GameObject startMenuPanel;
    public GameObject categoryPanel;
    public GameObject foodPanel;
    public GameObject placesPanel;
    public GameObject imageUploadPanel;

    [Header("UI")]
    public TextMeshProUGUI winMessage;

    [Header("Start UI")]
    public TextMeshProUGUI startMessage;
    public float startUiFade = 0.18f;
    public float readyDisplay = 0.5f;
    public float goDisplay = 0.45f;
    public float startUiPopScale = 1.15f;

    [Header("Buttons / Text (assign in Inspector)")]
    public Button startButton;
    public TextMeshProUGUI uploadStatusText;
    public Button startGameButton;

    [Header("Card Prefab & Grid")]
    public GameObject cardPrefab;
    public int columns = 4;
    public int rows = 3;
    public float margin = 0.1f;

    [Header("Board Padding")]
    public float horizontalPadding = 0.5f;
    public float verticalPadding = 0.5f;

    [Header("Start Sequence")]
    public float memorizeDuration = 2.5f;
    public float flipBackMinDelay = 0.02f;
    public float flipBackMaxDelay = 0.2f;

    [Header("Gameplay")]
    public float revealCheckDelay = 0.75f;

    // Input state (filled by ProcessInputs)
    [HideInInspector] public bool KeyPressedR;
    [HideInInspector] public bool KeyPressedSpace;
    [HideInInspector] public bool KeyPressedESC;
    [HideInInspector] public bool MouseButtonDown;
    [HideInInspector] public bool MouseButtonUp;
    [HideInInspector] public bool MouseClicked;
    [HideInInspector] public float MousePosX;
    [HideInInspector] public float MousePosY;

    // Runtime state
    private List<Card> allCards = new List<Card>();
    private List<Sprite> cardSprites = new List<Sprite>();
    private List<string> uploadErrors = new List<string>();
    private Card firstSelected;
    private Card secondSelected;
    private Transform boardParent;
    private int matchedPairs = 0;
    private bool usingUploadedImages = false;

    private GameState state = GameState.Initializing;
    private bool inputLocked = true;

    void Awake()
    {
        // safe singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameController instances detected — destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ------------------------------------------------------------
    // Start(): SHOW START MENU → gameplay starts only after clicking Start
    // ------------------------------------------------------------
    void Start()
    {
        matchedPairs = 0;
        PrepareUI();

        // show ONLY start menu at boot
        ShowOnly(startMenuPanel);

        // Hook Start button to the NEW UI flow
        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        state = GameState.Initializing;
    }


    void ShowOnly(GameObject panel)
    {
        startMenuPanel.SetActive(false);
        categoryPanel.SetActive(false);
        foodPanel.SetActive(false);
        placesPanel.SetActive(false);
        imageUploadPanel.SetActive(false);

        panel.SetActive(true);
    }


    // Start Menu
    public void OnStartPressed()
    {
        ShowOnly(categoryPanel);
    }

    // Category Panel
    public void OnFoodPressed()
    {
        ShowOnly(foodPanel);
    }

    public void OnFoodIndianPressed()
    {
        selectedSource = ImageSourceType.Food_Indian;
        StartGameFromPreset();
    }

    public void OnFoodMalayPressed()
    {
        selectedSource = ImageSourceType.Food_Malay;
        StartGameFromPreset();
    }

    public void OnFoodChinesePressed()
    {
        selectedSource = ImageSourceType.Food_Chinese;
        StartGameFromPreset();
    }


    public void OnPlacesPressed()
    {
        ShowOnly(placesPanel);
    }

    public void OnPlacesLandmarksPressed()
    {
        selectedSource = ImageSourceType.Places_Landmarks;
        StartGameFromPreset();
    }

    public void OnPlacesHistoricPressed()
    {
        selectedSource = ImageSourceType.Places_Historic;
        StartGameFromPreset();
    }

    public void OnPlacesNeighbourhoodsPressed()
    {
        selectedSource = ImageSourceType.Places_Neighbourhoods;
        StartGameFromPreset();
    }


    public void OnUploadPressed()
    {
        ShowOnly(imageUploadPanel);
    }

    public void OnCategoryBack()
    {
        ShowOnly(startMenuPanel);
    }

    // Food Panel
    public void OnFoodBack()
    {
        ShowOnly(categoryPanel);
    }

    // Places Panel
    public void OnPlacesBack()
    {
        ShowOnly(categoryPanel);
    }

    // Upload Panel
    public void OnUploadBack()
    {
        ShowOnly(categoryPanel);
    }

    Sprite CreateNormalizedSprite(Texture2D sourceTex)
    {
        Texture2D resized = ResizeTexture(sourceTex, TARGET_WIDTH, TARGET_HEIGHT);
        if (resized == null) return null;

        return Sprite.Create(
            resized,
            new Rect(0, 0, TARGET_WIDTH, TARGET_HEIGHT),
            new Vector2(0.5f, 0.5f),
            SPRITE_PPU
        );
    }


    void LoadSpritesBySelection()
    {
        string path = "";

        switch (selectedSource)
        {
            case ImageSourceType.Food_Indian:
                path = "Cards/Food/Indian";
                break;
            case ImageSourceType.Food_Malay:
                path = "Cards/Food/Malay";
                break;
            case ImageSourceType.Food_Chinese:
                path = "Cards/Food/Chinese";
                break;
            case ImageSourceType.Places_Landmarks:
                path = "Cards/Places/Landmarks";
                break;
            case ImageSourceType.Places_Historic:
                path = "Cards/Places/Historic";
                break;
            case ImageSourceType.Places_Neighbourhoods:
                path = "Cards/Places/Neighbourhoods";
                break;
            default:
                Debug.LogError("Invalid image source selected");
                return;
        }

        cardSprites.Clear();

        Texture2D[] textures = Resources.LoadAll<Texture2D>(path);

        if (textures == null || textures.Length == 0)
        {
            Debug.LogError("No textures found at Resources/" + path);
            return;
        }

        foreach (Texture2D tex in textures)
        {
            Sprite sprite = CreateNormalizedSprite(tex);
            if (sprite != null)
                cardSprites.Add(sprite);
        }
    }



    // ------------------------------------------------------------
    // Update state machine
    // ------------------------------------------------------------
    void Update()
    {
        switch (state)
        {
            case GameState.Initializing:
                // Do nothing — waiting for Start button
                break;

            case GameState.StartingGame:
                // ESC does NOT work in this phase (Option 2)
                GetInputs();

                // ❌ R does NOT work here anymore

                // Run start sequence
                if (!startingSequenceRunning)
                {
                    startingSequenceRunning = true;
                    StartCoroutine(RunStartSequence());
                }
                break;

            case GameState.Playing:

                GetInputs();

                // ✔ ESC works here — return to menu
                if (KeyPressedESC)
                {
                    ReturnToMenu();
                    return;
                }

                // ❌ R does NOT work here anymore

                // Handle card clicks
                if (MouseClicked && !inputLocked)
                {
                    Vector2 mouseWorld = new Vector2(MousePosX, MousePosY);
                    Card hit = FindCardUnderPoint(mouseWorld);
                    if (hit != null)
                        HandleCardClick(hit);
                }

                // End condition
                int targetPairs = (columns * rows) / 2;
                if (!endingSequenceRunning && matchedPairs >= targetPairs)
                {
                    endingSequenceRunning = true;
                    inputLocked = true;

                    // 🔊 WIN SOUND (plays once)
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayWin();

                    state = GameState.EndGame;
                    StartCoroutine(EndGameSequence());
                }
                break;

            case GameState.EndGame:

                GetInputs();

                // ✔ ESC works in EndGame too
                if (KeyPressedESC)
                {
                    ReturnToMenu();
                    return;
                }

                // ✔ R ONLY works here
                if (KeyPressedR)
                    Restart();

                break;
        }
    }

    // ------------------------------------------------------------
    // ESC → Return to Menu
    // ------------------------------------------------------------
    void ReturnToMenu()
    {
        StopAllCoroutines();

        startingSequenceRunning = false;
        endingSequenceRunning = false;
        firstSelected = null;
        secondSelected = null;
        inputLocked = true;
        usingUploadedImages = false;
        matchedPairs = 0;

        // Destroy all cards
        if (boardParent != null)
        {
            for (int i = boardParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(boardParent.GetChild(i).gameObject);
        }
        allCards.Clear();

        if (startMessage != null) startMessage.gameObject.SetActive(false);
        if (winMessage != null) winMessage.gameObject.SetActive(false);

        // Show menu again
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);

        state = GameState.Initializing;
    }

    // ------------------------------------------------------------
    // Input handling
    // ------------------------------------------------------------
    void GetInputs()
    {
        // Keyboard
        if (Keyboard.current != null)
        {
            KeyPressedR = Keyboard.current.rKey.wasPressedThisFrame;
            KeyPressedSpace = Keyboard.current.spaceKey.wasPressedThisFrame;
            KeyPressedESC = Keyboard.current.escapeKey.wasPressedThisFrame;
        }
        else
        {
            KeyPressedR = Input.GetKeyDown(KeyCode.R);
            KeyPressedSpace = Input.GetKeyDown(KeyCode.Space);
            KeyPressedESC = Input.GetKeyDown(KeyCode.Escape);
        }

        // Reset click state
        MouseButtonDown = false;
        MouseButtonUp = false;
        MouseClicked = false;

        // --------------------------
        // TOUCH INPUT (NEW)
        // --------------------------
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            float dist = -Camera.main.transform.position.z;
            Vector3 world = Camera.main.ScreenToWorldPoint(
                new Vector3(t.position.x, t.position.y, dist)
            );
            MousePosX = world.x;
            MousePosY = world.y;

            if (t.phase == TouchPhase.Began)
            {
                MouseButtonDown = true;
                MouseClicked = true;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                MouseButtonUp = true;
            }

            return; // IMPORTANT: don't process mouse if touch is active
        }

        // --------------------------
        // MOUSE INPUT (original)
        // --------------------------
        if (Mouse.current != null)
        {
            MouseButtonDown = Mouse.current.leftButton.wasPressedThisFrame;
            MouseButtonUp = Mouse.current.leftButton.wasReleasedThisFrame;
            MouseClicked = MouseButtonDown;

            Vector2 screen = Mouse.current.position.ReadValue();
            float distance = -Camera.main.transform.position.z;
            Vector3 world = Camera.main.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, distance)
            );
            MousePosX = world.x;
            MousePosY = world.y;
        }
        else
        {
            MouseButtonDown = Input.GetMouseButtonDown(0);
            MouseButtonUp = Input.GetMouseButtonUp(0);
            MouseClicked = MouseButtonDown;

            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            MousePosX = world.x;
            MousePosY = world.y;
        }
    }

    // ------------------------------------------------------------
    // Initialization
    // ------------------------------------------------------------
    void InitializeGame()
    {
        CreateBoardParent();

        if (selectedSource != ImageSourceType.Uploaded)
        {
            cardSprites.Clear();
            LoadSpritesBySelection();
        }

        if (cardSprites.Count < 6)
        {
            Debug.LogError("Not enough sprites to start game.");
            return;
        }

        SpawnCards();
        inputLocked = true;
    }


    void PrepareUI()
    {
        if (startMessage != null) startMessage.gameObject.SetActive(false);
        if (winMessage != null) winMessage.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // Load sprite images
    // ------------------------------------------------------------
    void LoadSprites()
    {
        cardSprites.Clear();
        Sprite[] loaded = Resources.LoadAll<Sprite>("Cards");
        cardSprites.AddRange(loaded);
        if (cardSprites.Count == 0)
            Debug.LogWarning("No sprites found in Resources/Cards!");
    }

    void LoadSpritesFromFolder()
    {
        cardSprites.Clear();

        if (!Directory.Exists(imageFolderPath))
        {
            Debug.LogError($"Image folder not found: {imageFolderPath}");
            return;
        }

        string[] files = Directory.GetFiles(imageFolderPath)
            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg"))
            .ToArray();

        foreach (string file in files)
        {
            try
            {
                byte[] data = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;

                    float targetPPU = tex.width;

                    Sprite sprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        SPRITE_PPU
                    );

                    cardSprites.Add(sprite);
                }

            }
            catch { }
        }
    }

    // 🔧 FIX: Create sprites with consistent pixelsPerUnit
    public void UploadSixImages()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Select 6 Images",
            "",
            new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg") },
            true
        );

        if (paths.Length != 6)
        {
            uploadStatusText.text = "Please select exactly 6 images";
            return;
        }

        cardSprites.Clear();
        uploadErrors.Clear();          // 🔧 NEW
        usingUploadedImages = false;   // 🔧 reset first

        foreach (string path in paths)
        {
            string fileName = Path.GetFileName(path); // 🔧 NEW

            try
            {
                byte[] data = File.ReadAllBytes(path);

                Texture2D srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!srcTex.LoadImage(data))
                {
                    uploadErrors.Add(fileName + " (failed to load)");
                    continue;
                }

                Texture2D resized = ResizeTexture(srcTex, TARGET_WIDTH, TARGET_HEIGHT);
                if (resized == null)
                {
                    uploadErrors.Add(fileName + " (resize failed)");
                    continue;
                }

                Sprite sprite = Sprite.Create(
                    resized,
                    new Rect(0, 0, TARGET_WIDTH, TARGET_HEIGHT),
                    new Vector2(0.5f, 0.5f),
                    SPRITE_PPU
                );

                if (sprite == null)
                {
                    uploadErrors.Add(fileName + " (sprite creation failed)");
                    continue;
                }

                cardSprites.Add(sprite);
            }
            catch
            {
                uploadErrors.Add(fileName + " (unexpected error)");
            }
        }

        usingUploadedImages = cardSprites.Count == 6;
        selectedSource = ImageSourceType.Uploaded;

        if (uploadErrors.Count > 0)
        {
            uploadStatusText.text =
                cardSprites.Count + " / 6 images loaded\nSome files may be invalid";
        }
        else
        {
            uploadStatusText.text = "6 / 6 images selected";
        }
    }


    Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;

        return result;
    }

    void StartGameFromPreset()
    {
        // Hide all menu panels
        startMenuPanel.SetActive(false);
        categoryPanel.SetActive(false);
        foodPanel.SetActive(false);
        placesPanel.SetActive(false);
        imageUploadPanel.SetActive(false);

        usingUploadedImages = false; // IMPORTANT: this tells InitializeGame to load from Resources

        InitializeGame();
        state = GameState.StartingGame;
    }

    public void StartGameFromUploadPanel()
    {
        if (uploadErrors.Count > 0)
        {
            uploadStatusText.text =
                "Cannot start game.\nThe following file(s) failed:\n" +
                string.Join("\n", uploadErrors);

            return;
        }

        if (cardSprites.Count < 6)
        {
            uploadStatusText.text = "Please upload 6 valid images first";
            return;
        }

        imageUploadPanel.SetActive(false);
        InitializeGame();
        state = GameState.StartingGame;
    }



    // ------------------------------------------------------------
    // Create parent object for cards
    // ------------------------------------------------------------
    void CreateBoardParent()
    {
        GameObject obj = GameObject.Find("CardObjects");
        if (obj == null)
        {
            obj = new GameObject("CardObjects");
            obj.transform.SetParent(transform, true);
        }
        boardParent = obj.transform;
    }

    // ------------------------------------------------------------
    // Spawning cards
    // ------------------------------------------------------------
    void SpawnCards()
    {
        allCards.Clear();

        Camera cam = Camera.main;
        float screenHeight = cam.orthographicSize * 2f;
        float screenWidth = screenHeight * cam.aspect;

        float usableWidth = screenWidth - 2 * horizontalPadding - (columns - 1) * margin;
        float usableHeight = screenHeight - 2 * verticalPadding - (rows - 1) * margin;

        float cardWidth = usableWidth / columns;
        float cardHeight = usableHeight / rows;

        int total = columns * rows;
        int pairs = total / 2;

        List<int> ids = new List<int>();
        for (int i = 0; i < pairs; i++)
        {
            int idx = i % cardSprites.Count;
            ids.Add(idx);
            ids.Add(idx);
        }

        ShuffleIntList(ids);

        for (int i = 0; i < total; i++)
        {
            GameObject cObj = Instantiate(cardPrefab, boardParent);
            Card card = cObj.GetComponent<Card>();

            int col = i % columns;
            int row = i / columns;

            float totalW = columns * cardWidth + (columns - 1) * margin;
            float totalH = rows * cardHeight + (rows - 1) * margin;

            float startX = -totalW / 2 + cardWidth / 2;
            float startY = totalH / 2 - cardHeight / 2;

            float x = startX + col * (cardWidth + margin);
            float y = startY - row * (cardHeight + margin);

            cObj.transform.position = new Vector3(x, y, 0);

            Sprite front = cardSprites[ids[i]];
            float sw = front.rect.width / front.pixelsPerUnit;
            float sh = front.rect.height / front.pixelsPerUnit;

            cObj.transform.localScale = new Vector3(cardWidth / sw, cardHeight / sh, 1);

            // now initialize visuals
            card.Initialize(front, ids[i]);


            allCards.Add(card);
        }
    }

    void ShuffleIntList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int temp = list[i];
            int r = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }

    // ------------------------------------------------------------
    // Selection handling
    // ------------------------------------------------------------
    Card FindCardUnderPoint(Vector2 point)
    {
        for (int i = allCards.Count - 1; i >= 0; i--)
        {
            if (allCards[i].ContainsPoint(point))
                return allCards[i];
        }
        return null;
    }

    void HandleCardClick(Card clicked)
    {
        if (clicked.IsRevealed()) return;

        // 🔊 Flip sound ONLY on player click/tap
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFlip();

        clicked.ShowFront();

        if (firstSelected == null)
        {
            firstSelected = clicked;
        }
        else if (secondSelected == null && clicked != firstSelected)
        {
            secondSelected = clicked;
            inputLocked = true;
            StartCoroutine(CheckMatchCoroutine());
        }
    }

    IEnumerator CheckMatchCoroutine()
    {
        yield return new WaitForSeconds(revealCheckDelay);

        if (firstSelected != null && secondSelected != null)
        {
            if (firstSelected.cardID == secondSelected.cardID)
            {
                matchedPairs++;

                // 🔊 Match sound
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMatch();

                StartCoroutine(firstSelected.MatchPulse());
                StartCoroutine(secondSelected.MatchPulse());
            }
            else
            {
                // 🔊 Mismatch sound
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMismatch();

                firstSelected.ShowBack();
                secondSelected.ShowBack();
            }

        }

        firstSelected = null;
        secondSelected = null;
        inputLocked = false;
    }

    // ------------------------------------------------------------
    // Start Sequence (Ready → Memorize → Flip → Go)
    // ------------------------------------------------------------
    IEnumerator RunStartSequence()
    {
        inputLocked = true;

        foreach (var c in allCards)
            c.ShowFrontInstant();

        if (startMessage != null)
            StartCoroutine(ShowStartText(startMessage, "Ready?", startUiFade, readyDisplay, startUiPopScale));

        yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(memorizeDuration);

        int left = allCards.Count;
        foreach (var c in allCards)
        {
            float d = Random.Range(flipBackMinDelay, flipBackMaxDelay);
            StartCoroutine(FlipBackWithDelay(c, d, () => left--));
        }

        yield return new WaitUntil(() => left <= 0);

        if (startMessage != null)
            yield return StartCoroutine(ShowStartText(startMessage, "Go!", startUiFade, goDisplay, startUiPopScale));

        startingSequenceRunning = false;
        inputLocked = false;
        state = GameState.Playing;
    }

    IEnumerator EndGameSequence()
    {
        ShowWinMessage();

        foreach (var c in allCards)
            StartCoroutine(c.MatchPulse());

        yield return new WaitForSeconds(3);
    }

    IEnumerator FlipBackWithDelay(Card c, float delay, System.Action done)
    {
        yield return new WaitForSeconds(delay);
        c.ShowBack();
        yield return new WaitForSeconds(0.25f);
        done?.Invoke();
    }

    IEnumerator ShowStartText(TextMeshProUGUI ui, string text,
        float fadeDuration, float displayDuration, float popScale)
    {
        if (ui == null) yield break;

        ui.gameObject.SetActive(true);
        ui.text = text;

        Color col = ui.color;
        Vector3 orig = ui.transform.localScale;

        col.a = 0;
        ui.color = col;
        ui.transform.localScale =
            Vector3.one * Mathf.Max(0.6f, popScale * 0.6f);

        float t = 0;
        float d = Mathf.Max(0.001f, fadeDuration);

        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            col.a = a;
            ui.color = col;
            ui.transform.localScale =
                Vector3.Lerp(Vector3.one * (popScale * 0.6f),
                             Vector3.one * popScale, a);
            yield return null;
        }

        col.a = 1;
        ui.color = col;
        ui.transform.localScale = Vector3.one * popScale;

        yield return new WaitForSeconds(displayDuration);

        t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            float a = 1 - Mathf.Clamp01(t / d);
            col.a = a;
            ui.color = col;
            yield return null;
        }

        col.a = 0;
        ui.color = col;
        ui.transform.localScale = orig;
        ui.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // Win UI
    // ------------------------------------------------------------
    void ShowWinMessage()
    {
        if (winMessage != null)
        {
            winMessage.gameObject.SetActive(true);
            StartCoroutine(FadeMessage(winMessage, 1f, 3f));
        }
    }

    IEnumerator FadeMessage(TextMeshProUGUI msg,
        float fadeDuration, float displayDuration)
    {
        Color col = msg.color;
        col.a = 0;
        msg.color = col;

        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            col.a = Mathf.Lerp(0, 1, t / fadeDuration);
            msg.color = col;
            yield return null;
        }

        col.a = 1;
        msg.color = col;

        yield return new WaitForSeconds(displayDuration);

        t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            col.a = Mathf.Lerp(1, 0, t / fadeDuration);
            msg.color = col;
            yield return null;
        }

        col.a = 0;
        msg.color = col;
        msg.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // Restart (ONLY usable in EndGame)
    // ------------------------------------------------------------
    public void Restart()
    {
        StopAllCoroutines();

        firstSelected = null;
        secondSelected = null;
        matchedPairs = 0;
        endingSequenceRunning = false;   // 🔥 FIX: allow the game to detect ending again
        startingSequenceRunning = false; // (optional but safer)

        inputLocked = true;

        if (boardParent != null)
        {
            for (int i = boardParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(boardParent.GetChild(i).gameObject);
        }

        allCards.Clear();
        SpawnCards();

        if (startMessage != null)
            startMessage.gameObject.SetActive(false);

        if (winMessage != null)
            winMessage.gameObject.SetActive(false);

        state = GameState.StartingGame;
        StartCoroutine(RunStartSequence());
    }

    // ------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------
    void OnValidate()
    {
        if (columns < 1) columns = 1;
        if (rows < 1) rows = 1;

        if (flipBackMaxDelay < flipBackMinDelay)
            flipBackMaxDelay = flipBackMinDelay;

        if ((columns * rows) % 2 != 0)
            Debug.LogWarning("OnValidate: columns * rows is odd — one card will not have a pair.");

        if (cardPrefab == null)
            Debug.LogWarning("OnValidate: cardPrefab is not assigned.");
    }
}