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

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    enum GameState
    {
        Menu,
        SelectingDifficulty,
        Initializing,
        StartingGame,
        Playing,
        EndGame
    }

    // =======================
    // Difficulty
    // =======================
    public enum Difficulty
    {
        Easy,
        Medium
    }

    private Difficulty selectedDifficulty = Difficulty.Medium;

    private bool startingSequenceRunning = false;
    private bool endingSequenceRunning = false;

    [Header("Image Source")]
    public string imageFolderPath = @"D:\Unity\Tiles_v0.0\Assets\Resources\Images";
    [Tooltip("If true use folder loader (editor/testing). If false use Resources.LoadAll<Texture2D>(\"Images\").")]
    public bool useFolderLoader = true;

    [Header("UI")]          // UI
    public TextMeshProUGUI winMessage;
    public TextMeshProUGUI startMessage;
    public float startUiFade = 0.18f;
    public float readyDisplay = 0.5f;
    public float goDisplay = 0.45f;
    public float startUiPopScale = 1.15f;

    private bool cameraBusy = false;
    [Header("Camera Capture UI")]       // Camera UI
    public GameObject cameraPanel;
    public RawImage webcamPreview;
    public TextMeshProUGUI countdownText;
    public Button retakeButton;
    public Button usePhotoButton;
    public TextMeshProUGUI cameraErrorText;
    public Button backToPictureButton;

    private WebCamTexture webcamTexture;
    private Texture2D capturedPhoto;
    private Coroutine countdownRoutine;

    [Header("Start Menu")]
    public GameObject startMenuPanel;   // Start menu root panel

    [Header("Picture Select UI")]
    [Tooltip("Panel that shows the picture choices.")]
    public GameObject pictureSelectPanel;          // PictureSelectPanel
    [Tooltip("Parent with GridLayoutGroup where picture buttons are spawned.")]
    public Transform pictureGridParent;            // PictureGrid
    [Tooltip("Button prefab that contains a RawImage and Button component.")]
    public GameObject pictureButtonPrefab;         // PictureButtonPrefab

    [Header("Difficulty Select UI")]
    public GameObject difficultySelectPanel;       // Difficulty Select Panel

    // Stores the path of the image chosen by the player in the picture select screen.
    // We keep this so Restart() always uses the same selected picture.
    private string forcedImagePath = null;

    [Header("Prefabs & Grid")]
    public GameObject tilePrefab;
    public GameObject slotPrefab;
    public int columns = 4;
    public int rows = 3;
    public float margin = 0.1f;

    [Header("Board Padding")]
    public float horizontalPadding = 0.5f;
    public float verticalPadding = 0.5f;

    [Header("Board Layout")]
    [Tooltip("Scale of the board relative to available screen area (0..1).")]
    [Range(0.1f, 1f)]
    public float boardScale = 0.666f; // default to two-thirds of available area  
    [Tooltip("Vertical offset of board center relative to screen center (fraction of screen height). Negative moves board downward.")]
    [Range(-1f, 1f)]
    public float boardVerticalOffsetFraction = -0.25f; // lower-center  

    [Header("Start Sequence")]
    public float memorizeDuration = 2.5f;
    [Tooltip("Max random rotation (degrees) for scattered tiles")]
    public float tileScatterMaxRotation = 10.0f;  // 30f for 30 degrees
    [Tooltip("How far tiles scatter from the board (multiples of board half-extent)")]
    public float scatterDistanceMultiplierMin = 1.2f;
    public float scatterDistanceMultiplierMax = 1.8f;

    [Header("Gameplay")]
    public float snapDistanceMultiplier = 0.6f;

    [Header("Preview / Debug")]
    [Tooltip("Optional: assign the ImageLoaded GameObject in the Inspector. If null GameController will search for a GameObject named 'ImageLoaded'")]
    public GameObject imageLoadedObject;

    // Input state (filled by GetInputs each frame)
    [HideInInspector] public bool KeyPressedR;
    [HideInInspector] public bool KeyPressedSpace;
    [HideInInspector] public bool KeyPressedESC;
    [HideInInspector] public bool MouseButtonDown;
    [HideInInspector] public bool MouseButtonUp;
    [HideInInspector] public bool MouseClicked;
    [HideInInspector] public float MousePosX;
    [HideInInspector] public float MousePosY;

    // Runtime state
    private List<Tile> allTiles = new List<Tile>();
    private List<Slot> allSlots = new List<Slot>();
    private List<Sprite> tileSprites = new List<Sprite>();
    private Transform boardParent;
    private Transform tileParent;
    private Transform slotParent;

    private GameState state = GameState.Menu;
    private bool inputLocked = true;

    // Cached camera values for layout
    private Camera mainCam;

    // computed board center Y in world units (used for spawn and scatter)
    private float boardCenterY = 0f;

    // stored board extents (world units)
    private float boardWorldWidth = 0f;
    private float boardWorldHeight = 0f;

    // Dragging runtime fields
    private Tile grabbedTile = null;
    private bool isDragging = false;

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

    void Start()
    {
        mainCam = Camera.main;
        CreateBoardParents();
        PrepareUI();

        // Show start menu first
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);

        // Hide picture select at the beginning
        if (pictureSelectPanel != null)
            pictureSelectPanel.SetActive(false);

        inputLocked = true;
        state = GameState.Menu;
    }
    // Quit game
    void QuitGame()
    {
        if (state != GameState.Menu)
            return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }


    void Update()
    {
        switch (state)
        {
            case GameState.Menu:
                GetInputs();
                // ESC in menu: optional quit or do nothing. We leave it as no-op.
                if (KeyPressedESC)
                    QuitGame();
                break;

            case GameState.Initializing:
                // Start the Ready/Go + scatter sequence once
                if (!startingSequenceRunning)
                {
                    startingSequenceRunning = true;
                    StartCoroutine(RunStartSequence());
                }
                state = GameState.StartingGame;
                break;

            case GameState.SelectingDifficulty:
                GetInputs();
                if (KeyPressedESC)
                    ReturnToMenu();
                break;

            case GameState.StartingGame:
                GetInputs();
                if (KeyPressedESC) ReturnToMenu();
                if (KeyPressedR) Restart();

                // Wait until RunStartSequence completes
                if (!startingSequenceRunning)
                    state = GameState.Playing;
                break;

            case GameState.Playing:
                GetInputs();
                if (KeyPressedESC) ReturnToMenu();
                if (KeyPressedR) Restart();
                if (!inputLocked)
                    HandlePlayingMouseInput();
                break;

            case GameState.EndGame:
                GetInputs();
                if (KeyPressedESC) ReturnToMenu();
                if (KeyPressedR) Restart();
                break;
        }
    }

    // -----------------------
    // START MENU + PICTURE SELECT FLOW
    // -----------------------

    // Called by Start Button
    public void StartPuzzle()
    {
        // Hide the start menu
        if (startMenuPanel != null)
            startMenuPanel.SetActive(false);

        // Show picture selection panel
        if (pictureSelectPanel != null && pictureGridParent != null && pictureButtonPrefab != null)
        {
            pictureSelectPanel.SetActive(true);
            LoadThumbnails();
        }
        else
        {
            // Fallback: no picture select available
            InitializeGame();
            startingSequenceRunning = false;
            state = GameState.Initializing;
        }

        // 🔒 IMPORTANT: no longer in Menu
        state = GameState.SelectingDifficulty; // or a new PictureSelect state
    }

    // -----------------------
    // CAMERA CAPTURE (WEBCAM)
    // -----------------------

    public void OnTakePhotoClicked()
    {
        state = GameState.SelectingDifficulty; // prevent ESC exit

        if (cameraBusy) return;
        cameraBusy = true;

        if (pictureSelectPanel != null)
            pictureSelectPanel.SetActive(false);

        if (cameraPanel != null)
            cameraPanel.SetActive(true);

        if (backToPictureButton != null)
            backToPictureButton.gameObject.SetActive(true);

        StartWebcam();
    }

    void StartWebcam()
    {
        // 🚫 NO CAMERA DETECTED
        if (WebCamTexture.devices.Length == 0)
        {
            cameraBusy = false;

            if (cameraErrorText != null)
                cameraErrorText.gameObject.SetActive(true);

            // Make sure preview is empty
            if (webcamPreview != null)
                webcamPreview.texture = null;

            return; // ⛔ VERY IMPORTANT
        }

        // ✅ Camera exists
        if (cameraErrorText != null)
            cameraErrorText.gameObject.SetActive(false);

        if (webcamTexture != null)
            webcamTexture.Stop();

        webcamTexture = new WebCamTexture();

        webcamPreview.texture = webcamTexture;
        webcamPreview.material.mainTexture = webcamTexture;

        webcamTexture.Play();

        // Hide buttons until photo is taken
        retakeButton.gameObject.SetActive(false);
        usePhotoButton.gameObject.SetActive(false);

        // ⛔ DO NOT start countdown yet
        StartCoroutine(CheckWebcamAndStartCountdown());
    }

    IEnumerator CheckWebcamAndStartCountdown()
    {
        // Give Unity a short moment to start the stream
        yield return new WaitForSeconds(0.1f);

        // ❌ Webcam failed to start
        if (webcamTexture == null || !webcamTexture.isPlaying || webcamTexture.width <= 16)
        {
            cameraBusy = false;

            if (cameraErrorText != null)
                cameraErrorText.gameObject.SetActive(true);

            if (webcamPreview != null)
                webcamPreview.texture = null;

            yield break;
        }

        // ✅ Webcam is valid
        if (cameraErrorText != null)
            cameraErrorText.gameObject.SetActive(false);

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        countdownRoutine = StartCoroutine(CountdownAndCapture());
    }


    public void OnBackToPictureSelect()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        capturedPhoto = null;
        cameraBusy = false;

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (cameraErrorText != null)
            cameraErrorText.gameObject.SetActive(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            countdownText.text = "";
        }

        if (pictureSelectPanel != null)
            pictureSelectPanel.SetActive(true);
    }

    IEnumerator CountdownAndCapture()
    {
        countdownText.gameObject.SetActive(true);

        for (int i = 5; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        countdownText.text = "Smile!";
        yield return new WaitForSeconds(0.5f);

        CapturePhoto();

        countdownText.gameObject.SetActive(false);

        countdownRoutine = null;
    }

    void CapturePhoto()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            cameraBusy = false;

            if (cameraErrorText != null)
                cameraErrorText.gameObject.SetActive(true);

            return;
        }

        capturedPhoto = new Texture2D(
            webcamTexture.width,
            webcamTexture.height,
            TextureFormat.RGB24,
            false
        );

        capturedPhoto.SetPixels(webcamTexture.GetPixels());
        capturedPhoto.Apply();

        webcamTexture.Stop();

        webcamPreview.texture = capturedPhoto;

        cameraBusy = false;

        retakeButton.gameObject.SetActive(true);
        usePhotoButton.gameObject.SetActive(true);
    }

    public void OnRetakePhoto()
    {
        retakeButton.gameObject.SetActive(false);
        usePhotoButton.gameObject.SetActive(false);

        StartWebcam();
    }

    public void OnUsePhoto()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }

        forcedImagePath = null;
        cameraPanel.SetActive(false);

        if (backToPictureButton != null)
            backToPictureButton.gameObject.SetActive(false);

        if (difficultySelectPanel != null)
            difficultySelectPanel.SetActive(true);

        state = GameState.SelectingDifficulty;
    }

    // =======================
    // Difficulty Selection
    // =======================
    public void SelectEasy()
    {
        selectedDifficulty = Difficulty.Easy;
        ApplyDifficulty();
    }

    public void SelectMedium()
    {
        selectedDifficulty = Difficulty.Medium;
        ApplyDifficulty();
    }

    void ApplyDifficulty()
    {
        if (difficultySelectPanel != null)
            difficultySelectPanel.SetActive(false);

        switch (selectedDifficulty)
        {
            case Difficulty.Easy:
                columns = 4;
                rows = 2;
                break;

            case Difficulty.Medium:
                columns = 4;
                rows = 3;
                break;
        }

        InitializeGame();
        startingSequenceRunning = false;
        state = GameState.Initializing;
    }

    // -----------------------
    // Upload Image (Tiles 2.0)
    // -----------------------
    public void OnUploadImageClicked()
    {
        var extensions = new[]
        {
        new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
    };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select an Image",
            "",
            extensions,
            false
        );

        if (paths.Length > 0 && File.Exists(paths[0]))
        {
            StartPuzzleWithImage(paths[0]);
        }
    }

    // Load thumbnails from folder
    void LoadThumbnails()
    {
        foreach (Transform child in pictureGridParent)
            Destroy(child.gameObject);

        if (!Directory.Exists(imageFolderPath))
        {
            Debug.LogWarning("LoadThumbnails: imageFolderPath does not exist: " + imageFolderPath);
            return;
        }

        string[] files = Directory.GetFiles(imageFolderPath)
            .Where(f =>
                f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0)
        {
            Debug.LogWarning("LoadThumbnails: no image files found in folder: " + imageFolderPath);
            return;
        }

        foreach (string file in files)
        {
            GameObject btn = Instantiate(pictureButtonPrefab, pictureGridParent);

            RawImage rawImg = btn.GetComponentInChildren<RawImage>();
            Image uiImg = (rawImg == null) ? btn.GetComponentInChildren<Image>() : null;

            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    tex.name = Path.GetFileNameWithoutExtension(file);

                    if (rawImg != null)
                    {
                        rawImg.texture = tex;
                        rawImg.SetNativeSize();
                    }
                    else if (uiImg != null)
                    {
                        Sprite sp = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        uiImg.sprite = sp;
                        uiImg.SetNativeSize();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LoadThumbnails: error loading {file}: {ex.Message}");
            }

            string chosenPath = file;
            Button b = btn.GetComponent<Button>();
            if (b != null)
            {
                b.onClick.AddListener(() =>
                {
                    StartPuzzleWithImage(chosenPath);
                });
            }
        }
    }

    // When user chooses a picture
    public void StartPuzzleWithImage(string path)
    {
        forcedImagePath = path;

        if (pictureSelectPanel != null)
            pictureSelectPanel.SetActive(false);

        // Show difficulty selection instead of starting immediately
        if (difficultySelectPanel != null)
            difficultySelectPanel.SetActive(true);

        // IMPORTANT: do NOT let the game initialize yet
        state = GameState.SelectingDifficulty;
    }

    // ESC → return to Menu
    public void ReturnToMenu()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }

        capturedPhoto = null;

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            countdownText.text = "";
        }

        cameraBusy = false;

        StopAllCoroutines();
        startingSequenceRunning = false;
        endingSequenceRunning = false;
        inputLocked = true;

        // Removes "Well Done!" message when R is pressed
        if (winMessage != null)
            winMessage.gameObject.SetActive(false);

        // Force-hide start text (fix stuck "Ready?")
        if (startMessage != null)
        {
            startMessage.StopAllCoroutines();
            startMessage.gameObject.SetActive(false);

            // Optional: reset alpha to be safe
            Color c = startMessage.color;
            c.a = 0f;
            startMessage.color = c;

            startMessage.transform.localScale = Vector3.one;
        }

        // Destroy tiles & slots
        if (slotParent != null)
        {
            for (int i = slotParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(slotParent.GetChild(i).gameObject);
        }
        if (tileParent != null)
        {
            for (int i = tileParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(tileParent.GetChild(i).gameObject);
        }

        allTiles.Clear();
        allSlots.Clear();
        tileSprites.Clear();

        // Show menu again
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);

        if (pictureSelectPanel != null)
            pictureSelectPanel.SetActive(false);

        if (difficultySelectPanel != null)
            difficultySelectPanel.SetActive(false);

        state = GameState.Menu;
    }

    // -----------------------
    // Inputs
    // -----------------------
    void GetInputs()
    {
        // Keyboard (unchanged)
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


        // --------------------------
        // TOUCH SUPPORT (NEW)
        // --------------------------
        MouseButtonDown = false;
        MouseButtonUp = false;
        MouseClicked = false;

        // If touch exists, override mouse
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            // Convert touch to world coords
            float dist = -mainCam.transform.position.z;
            Vector3 world = mainCam.ScreenToWorldPoint(new Vector3(t.position.x, t.position.y, dist));
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

            return; // important: skip mouse processing when touch is active
        }


        // --------------------------
        // MOUSE (existing behaviour)
        // --------------------------
        if (Mouse.current != null)
        {
            MouseButtonDown = Mouse.current.leftButton.wasPressedThisFrame;
            MouseButtonUp = Mouse.current.leftButton.wasReleasedThisFrame;
            MouseClicked = MouseButtonDown;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            float dist = -mainCam.transform.position.z;
            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, dist));
            MousePosX = mouseWorld.x;
            MousePosY = mouseWorld.y;
        }
        else
        {
            MouseButtonDown = Input.GetMouseButtonDown(0);
            MouseButtonUp = Input.GetMouseButtonUp(0);
            MouseClicked = MouseButtonDown;

            Vector3 mw = mainCam.ScreenToWorldPoint(Input.mousePosition);
            MousePosX = mw.x;
            MousePosY = mw.y;
        }
    }

    // -----------------------
    // Initialization
    // -----------------------
    void InitializeGame()
    {
        allTiles.Clear();
        allSlots.Clear();
        tileSprites.Clear();

        Texture2D source = LoadSourceTexture();
        if (source == null)
        {
            Debug.LogError("No source image loaded. Aborting initialization.");
            return;
        }

        DisplayLoadedImage(source);
        SliceTextureIntoSprites(source);
        SpawnSlotsAndTiles();

        inputLocked = true;
    }

    void PrepareUI()
    {
        if (startMessage != null) startMessage.gameObject.SetActive(false);
        if (winMessage != null) winMessage.gameObject.SetActive(false);
    }

    // -----------------------
    // Board Parents
    // -----------------------
    void CreateBoardParents()
    {
        GameObject board = GameObject.Find("Board");
        if (board == null)
        {
            board = new GameObject("Board");
            board.transform.SetParent(transform, worldPositionStays: true);
        }
        boardParent = board.transform;

        GameObject tilesGO = GameObject.Find("TileObjects");
        if (tilesGO == null)
        {
            tilesGO = new GameObject("TileObjects");
            tilesGO.transform.SetParent(boardParent, true);
        }
        tileParent = tilesGO.transform;

        GameObject slotsGO = GameObject.Find("SlotObjects");
        if (slotsGO == null)
        {
            slotsGO = new GameObject("SlotObjects");
            slotsGO.transform.SetParent(boardParent, true);
        }
        slotParent = slotsGO.transform;
    }
    // -----------------------
    // Image loading & slicing
    // -----------------------
    Texture2D LoadSourceTexture()
    {
        if (capturedPhoto != null)
        {
            return capturedPhoto;
        }
        // If a picture was chosen from Picture Select, always use that.
        if (!string.IsNullOrEmpty(forcedImagePath))
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(forcedImagePath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData))
                {
                    tex.name = Path.GetFileNameWithoutExtension(forcedImagePath);
                    return tex; // Do NOT clear forcedImagePath so restart works
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LoadSourceTexture: error loading forced image: {ex.Message}");
            }
        }

        // If no picture selected, fallback to folder loader (v0.1 logic)
        const int targetW = 1620;
        const int targetH = 1080;

        if (useFolderLoader)
        {
            if (!Directory.Exists(imageFolderPath))
            {
                Debug.LogError($"Image folder not found: {imageFolderPath}");
            }
            else
            {
                string[] files = Directory.GetFiles(imageFolderPath)
                    .Where(f => f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (files.Length > 0)
                {
                    int idx = Random.Range(0, files.Length);
                    string file = files[idx];

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(bytes))
                        {
                            tex.name = Path.GetFileNameWithoutExtension(file);

                            Texture2D resized = ResampleTextureCover(tex, targetW, targetH);
                            if (resized != null)
                            {
                                resized.name = tex.name;
                                return resized;
                            }

                            return tex;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Error loading file {file}: {ex.Message}");
                    }
                }
            }
        }

        // Fallback: load from Resources/Images
        Texture2D[] res = Resources.LoadAll<Texture2D>("Images");
        if (res.Length > 0)
        {
            Texture2D src = res[0];
            if (src.isReadable)
            {
                Texture2D resized = ResampleTextureCover(src, targetW, targetH);
                if (resized != null)
                {
                    resized.name = src.name;
                    return resized;
                }
            }
            return src;
        }

        Debug.LogError("No textures found in Resources/Images and folder loader failed.");
        return null;
    }

    // Resample & crop (COVER mode)
    Texture2D ResampleTextureCover(Texture2D src, int targetW, int targetH)
    {
        if (src == null) return null;
        if (targetW <= 0 || targetH <= 0) return src;

        float srcW = src.width;
        float srcH = src.height;
        float srcAspect = srcW / srcH;
        float targetAspect = (float)targetW / targetH;

        float uMin = 0f, vMin = 0f, uMax = 1f, vMax = 1f;

        // decide where to crop
        if (srcAspect > targetAspect)
        {
            float cropWidth = targetAspect / srcAspect;
            uMin = (1f - cropWidth) * 0.5f;
            uMax = uMin + cropWidth;
        }
        else
        {
            float cropHeight = srcAspect / targetAspect;
            vMin = (1f - cropHeight) * 0.5f;
            vMax = vMin + cropHeight;
        }

        // Create dst
        Texture2D dst = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);

        if (!src.isReadable)
        {
            Debug.LogWarning($"Source texture '{src.name}' is not readable.");
            return null;
        }

        for (int y = 0; y < targetH; y++)
        {
            float v = vMin + ((y + 0.5f) / targetH) * (vMax - vMin);
            for (int x = 0; x < targetW; x++)
            {
                float u = uMin + ((x + 0.5f) / targetW) * (uMax - uMin);
                dst.SetPixel(x, y, src.GetPixelBilinear(u, v));
            }
        }

        dst.Apply();
        return dst;
    }

    void SliceTextureIntoSprites(Texture2D tex)
    {
        tileSprites.Clear();
        if (tex == null) return;

        int texW = tex.width;
        int texH = tex.height;
        int tileWpx = texW / columns;
        int tileHpx = texH / rows;
        float ppu = 100f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int x = c * tileWpx;
                int y = (rows - 1 - r) * tileHpx;
                Rect rect = new Rect(x, y, tileWpx, tileHpx);
                Sprite s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu);
                s.name = $"{tex.name}_r{r}_c{c}";
                tileSprites.Add(s);
            }
        }
    }

    // -----------------------
    // Spawn Tiles & Slots
    // -----------------------
    void SpawnSlotsAndTiles()
    {
        for (int i = slotParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(slotParent.GetChild(i).gameObject);

        for (int i = tileParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(tileParent.GetChild(i).gameObject);

        allSlots.Clear();
        allTiles.Clear();

        if (tileSprites.Count == 0)
        {
            Debug.LogWarning("No tile sprites available.");
            return;
        }

        float screenH = 2f * mainCam.orthographicSize;
        float screenW = screenH * mainCam.aspect;

        float usableW = (screenW - 2 * horizontalPadding - (columns - 1) * margin) * boardScale;
        float usableH = (screenH - 2 * verticalPadding - (rows - 1) * margin) * boardScale;

        // preserve true pixel aspect ratios
        Sprite sample = tileSprites[0];
        float spritePPU = sample.pixelsPerUnit;
        float spriteW = sample.rect.width / spritePPU;
        float spriteH = sample.rect.height / spritePPU;

        float gridScaleX = usableW / (spriteW * columns);
        float gridScaleY = usableH / (spriteH * rows);
        float gridScale = Mathf.Min(gridScaleX, gridScaleY);

        float slotW = spriteW * gridScale;
        float slotH = spriteH * gridScale;

        float totalW = columns * slotW + (columns - 1) * margin;
        float totalH = rows * slotH + (rows - 1) * margin;

        boardWorldWidth = totalW;
        boardWorldHeight = totalH;

        float startX = -totalW / 2 + slotW / 2;
        float startY = totalH / 2 - slotH / 2;

        boardCenterY = screenH * boardVerticalOffsetFraction;
        startY += boardCenterY;

        int total = columns * rows;

        for (int i = 0; i < total; i++)
        {
            int col = i % columns;
            int row = i / columns;

            float posX = startX + col * (slotW + margin);
            float posY = startY - row * (slotH + margin);
            Vector3 slotPos = new Vector3(posX, posY, 0f);

            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            slotObj.transform.position = slotPos;

            Slot slot = slotObj.GetComponent<Slot>();
            slot.Initialize(i, new Vector2(slotW, slotH));
            allSlots.Add(slot);

            // Tile
            GameObject tileObj = Instantiate(tilePrefab, tileParent);
            tileObj.transform.position = slotPos;

            Sprite sp = tileSprites[i % tileSprites.Count];
            float scaleX = slotW / (sp.rect.width / spritePPU);
            float scaleY = slotH / (sp.rect.height / spritePPU);
            float uniform = Mathf.Min(scaleX, scaleY);

            tileObj.transform.localScale = new Vector3(uniform, uniform, 1f);

            Tile tile = tileObj.GetComponent<Tile>();
            tile.Initialize(sp, i);
            tile.SetAnchorPosition(slotPos);

            allTiles.Add(tile);
        }
    }
    // -----------------------
    // Start Sequence (Ready → Memorize → Scatter → Go)
    // -----------------------
    IEnumerator RunStartSequence()
    {
        inputLocked = true;

        // Reset rotations before showing assembled preview
        foreach (var t in allTiles)
            t.transform.rotation = Quaternion.identity;

        // Show "Ready?"
        if (startMessage != null)
            StartCoroutine(ShowStartText(startMessage, "Ready?", startUiFade, readyDisplay, startUiPopScale));

        yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(memorizeDuration);

        // Scatter calculation
        List<Vector3> scattered = new List<Vector3>(allTiles.Count);

        Vector2 boardCenter = new Vector2(0f, boardCenterY);

        float boardExtent = Mathf.Max(boardWorldWidth, boardWorldHeight) * 0.5f;

        float camH = 2f * mainCam.orthographicSize;
        float camW = camH * mainCam.aspect;
        Vector2 camCenter = new Vector2(mainCam.transform.position.x, mainCam.transform.position.y);

        float worldLeft = camCenter.x - camW * 0.5f;
        float worldRight = camCenter.x + camW * 0.5f;
        float worldTop = camCenter.y + camH * 0.5f;
        float worldBottom = camCenter.y - camH * 0.5f;

        float tileHalfDiag = 0.5f;
        if (allSlots.Count > 0)
        {
            Vector2 s = allSlots[0].slotSize;
            tileHalfDiag = 0.5f * Mathf.Sqrt(s.x * s.x + s.y * s.y);
        }

        float marginWorld = 0f;
        float safeMinX = worldLeft + tileHalfDiag + marginWorld;
        float safeMaxX = worldRight - tileHalfDiag - marginWorld;
        float safeMinY = worldBottom + tileHalfDiag * 0.75f + marginWorld;
        float safeMaxY = worldTop - tileHalfDiag * 0.75f - marginWorld;

        float boardLeft = float.MaxValue, boardRight = float.MinValue;
        float boardTop = float.MinValue, boardBottom = float.MaxValue;

        foreach (var s in allSlots)
        {
            Vector2 c = s.Center;
            float halfW = s.slotSize.x * 0.5f;
            float halfH = s.slotSize.y * 0.5f;

            boardLeft = Mathf.Min(boardLeft, c.x - halfW);
            boardRight = Mathf.Max(boardRight, c.x + halfW);
            boardTop = Mathf.Max(boardTop, c.y + halfH);
            boardBottom = Mathf.Min(boardBottom, c.y - halfH);
        }

        float forbiddenGap = tileHalfDiag + 0.03f;
        boardLeft -= forbiddenGap;
        boardRight += forbiddenGap;
        boardTop += forbiddenGap;
        boardBottom -= forbiddenGap;

        List<Vector2> anchors = new List<Vector2>();

        float topY = safeMaxY;
        float bottomY = safeMinY;
        float leftX = safeMinX;
        float rightX = safeMaxX;

        // TOP row (4)
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float x = Mathf.Lerp(safeMinX + 2 * tileHalfDiag, safeMaxX - 2 * tileHalfDiag, t);
            anchors.Add(new Vector2(x, topY));
        }

        // RIGHT side (2)
        for (int i = 0; i < 2; i++)
        {
            float t = (i + 1) / 3f;
            float y = Mathf.Lerp(safeMaxY - tileHalfDiag, safeMinY + tileHalfDiag, t);
            anchors.Add(new Vector2(rightX, y));
        }

        // BOTTOM row (4)
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float x = Mathf.Lerp(safeMinX + 2 * tileHalfDiag, safeMaxX - 2 * tileHalfDiag, t);
            anchors.Add(new Vector2(x, bottomY));
        }

        // LEFT side (2)
        for (int i = 0; i < 2; i++)
        {
            float t = (i + 1) / 3f;
            float y = Mathf.Lerp(safeMinY + tileHalfDiag, safeMaxY - tileHalfDiag, t);
            anchors.Add(new Vector2(leftX, y));
        }

        anchors = anchors.OrderBy(a => Random.value).ToList();

        int n = allTiles.Count;

        for (int i = 0; i < n; i++)
        {
            Vector2 basePos =
                (i < anchors.Count)
                ? anchors[i]
                : anchors[i % anchors.Count] + Random.insideUnitCircle * Mathf.Min(tileHalfDiag, 0.15f);

            if (basePos.x >= boardLeft && basePos.x <= boardRight &&
                basePos.y >= boardBottom && basePos.y <= boardTop)
            {
                Vector2 away = (basePos - boardCenter).normalized;
                if (away == Vector2.zero)
                    away = Vector2.right;

                Vector2 candidate = basePos + away * (tileHalfDiag + 0.1f);
                candidate.x = Mathf.Clamp(candidate.x, safeMinX, safeMaxX);
                candidate.y = Mathf.Clamp(candidate.y, safeMinY, safeMaxY);
                basePos = candidate;
            }

            basePos.x = Mathf.Clamp(basePos.x, safeMinX, safeMaxX);
            basePos.y = Mathf.Clamp(basePos.y, safeMinY, safeMaxY);

            scattered.Add(new Vector3(basePos.x, basePos.y, allTiles[i].transform.position.z));
        }

        int remaining = allTiles.Count;

        for (int i = 0; i < allTiles.Count; i++)
        {
            Tile t = allTiles[i];
            Vector3 to = scattered[i];
            float rot = Random.Range(-tileScatterMaxRotation, tileScatterMaxRotation);

            StartCoroutine(MoveAndRotate(t.transform, to, rot, 1.0f, () => remaining--));
        }

        yield return new WaitUntil(() => remaining <= 0);

        foreach (var t in allTiles)
            t.CaptureOriginalTransform();

        // Show "Go!"
        if (startMessage != null)
            yield return StartCoroutine(ShowStartText(startMessage, "Go!", startUiFade, goDisplay, startUiPopScale));

        startingSequenceRunning = false;
        inputLocked = false;
    }

    IEnumerator MoveAndRotate(Transform tr, Vector3 toPos, float targetZRot, float duration, System.Action onComplete)
    {
        Vector3 startPos = tr.position;
        Quaternion startRot = tr.rotation;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetZRot);

        float t = 0f;
        float d = Mathf.Max(0.001f, duration);

        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);

            tr.position = Vector3.Lerp(startPos, toPos, a);
            tr.rotation = Quaternion.Slerp(startRot, targetRot, a);

            yield return null;
        }

        tr.position = toPos;
        tr.rotation = targetRot;

        onComplete?.Invoke();
    }

    // -----------------------
    // UI Animation Helper
    // -----------------------
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
        float fd = Mathf.Max(0.001f, fadeDuration);

        while (t < fd)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fd);

            color.a = a;
            ui.color = color;

            ui.transform.localScale = Vector3.Lerp(Vector3.one * (popScale * 0.6f), Vector3.one * popScale, a);

            yield return null;
        }

        color.a = 1f;
        ui.color = color;

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fd)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fd);

            color.a = a;
            ui.color = color;

            yield return null;
        }

        color.a = 0f;
        ui.color = color;
        ui.transform.localScale = originalScale;
        ui.gameObject.SetActive(false);
    }
    // -----------------------
    // Restart
    // -----------------------
    public void Restart()
    {
        StopAllCoroutines();
        startingSequenceRunning = false;
        endingSequenceRunning = false;
        inputLocked = true;

        // Removes "Well Done!" message when R is pressed
        if (winMessage != null)
            winMessage.gameObject.SetActive(false);

        // Force-hide start text (fix stuck "Go!")
        if (startMessage != null)
        {
            startMessage.StopAllCoroutines();
            startMessage.gameObject.SetActive(false);

            Color c = startMessage.color;
            c.a = 0f;
            startMessage.color = c;

            startMessage.transform.localScale = Vector3.one;
        }

        // Destroy existing tiles and slots
        if (slotParent != null)
        {
            for (int i = slotParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(slotParent.GetChild(i).gameObject);
        }

        if (tileParent != null)
        {
            for (int i = tileParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(tileParent.GetChild(i).gameObject);
        }

        allTiles.Clear();
        allSlots.Clear();
        tileSprites.Clear();

        // Keep forcedImagePath so restart uses the same picture
        InitializeGame();

        // Start Ready/Go sequence again
        startingSequenceRunning = false;
        state = GameState.Initializing;
    }

    void OnValidate()
    {
        if (columns < 1) columns = 1;
        if (rows < 1) rows = 1;

        if (tilePrefab == null)
            Debug.LogWarning("OnValidate: tilePrefab is not assigned.");

        if (slotPrefab == null)
            Debug.LogWarning("OnValidate: slotPrefab is not assigned.");
    }

    // -----------------------
    // Display Loaded Image (debug / preview)
    // -----------------------
    void DisplayLoadedImage(Texture2D tex)
    {
        if (tex == null) return;

        GameObject target = imageLoadedObject;
        if (target == null)
            target = GameObject.Find("ImageLoaded");

        if (target == null)
        {
            Debug.LogWarning("DisplayLoadedImage: ImageLoaded GameObject not found.");
            return;
        }

        // SpriteRenderer
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Sprite preview = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), 100f);
            sr.sprite = preview;
            return;
        }

        // UI RawImage
        RawImage raw = target.GetComponent<RawImage>();
        if (raw != null)
        {
            raw.texture = tex;
            raw.SetNativeSize();
            return;
        }

        // Generic Renderer
        Renderer rend = target.GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            rend.material.mainTexture = tex;
            return;
        }

        Debug.LogWarning("DisplayLoadedImage: No supported renderer found on ImageLoaded object.");
    }

    // -----------------------
    // Gameplay: Drag + Drop
    // -----------------------
    void HandlePlayingMouseInput()
    {
        bool mouseHeld = false;

        // Touch dragging
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            mouseHeld = (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary);
        }
        else
        {
            // Mouse dragging
            mouseHeld = Mouse.current == null
                ? Input.GetMouseButton(0)
                : Mouse.current.leftButton.isPressed;
        }


        // --------------------------
        // PICK UP TILE (mouse down or touch began)
        // --------------------------
        if (MouseButtonDown && !isDragging)
        {
            Tile picked = PickTileUnderMouse();

            if (picked != null && !picked.IsLocked)
            {
                grabbedTile = picked;
                isDragging = true;
                grabbedTile.OnBeginDrag();
            }
        }


        // --------------------------
        // DRAG UPDATE
        // --------------------------
        if (isDragging && grabbedTile != null && mouseHeld)
        {
            grabbedTile.OnDragUpdate(new Vector2(MousePosX, MousePosY));
        }


        // --------------------------
        // RELEASE (mouse up or touch end)
        // --------------------------
        if (MouseButtonUp && isDragging && grabbedTile != null)
        {
            TryPlaceGrabbedTile(grabbedTile);
            grabbedTile = null;
            isDragging = false;
        }
    }


    Tile PickTileUnderMouse()
    {
        Tile best = null;
        int bestOrder = int.MinValue;
        float bestZ = float.MinValue;

        Vector2 m = new Vector2(MousePosX, MousePosY);

        foreach (var t in allTiles)
        {
            if (t == null || t.IsLocked)
                continue;

            if (t.ContainsPoint(m))
            {
                int order = (t.frontRenderer != null) ? t.frontRenderer.sortingOrder : 0;
                float z = t.transform.position.z;

                if (best == null || order > bestOrder || (order == bestOrder && z > bestZ))
                {
                    best = t;
                    bestOrder = order;
                    bestZ = z;
                }
            }
        }

        return best;
    }

    void TryPlaceGrabbedTile(Tile tile)
    {
        if (tile == null) return;

        Slot bestSlot = null;
        float bestDist = float.MaxValue;

        float snapThreshold = 0.5f;
        if (allSlots.Count > 0)
        {
            Vector2 ss = allSlots[0].slotSize;
            float diag = Mathf.Sqrt(ss.x * ss.x + ss.y * ss.y) * 0.5f;
            snapThreshold = diag * snapDistanceMultiplier;
        }

        Vector2 tilePos = tile.transform.position;

        foreach (var s in allSlots)
        {
            float d = s.DistanceTo(tilePos);
            if (d < bestDist)
            {
                bestDist = d;
                bestSlot = s;
            }
        }

        if (bestSlot != null && bestDist <= snapThreshold)
        {
            // Empty slot → snap directly
            if (bestSlot.Occupant == null)
            {
                tile.SnapToSlot(bestSlot, () =>
                {
                    if (tile.TileID == bestSlot.SlotIndex)
                    {
                        tile.LockCorrectlyPlaced();
                    }

                    CheckForWin();
                });

                return;
            }

            // Occupied slot → check lock state
            Tile other = bestSlot.Occupant;
            if (other != null && other != tile)
            {
                // 🚫 Do NOT allow replacing a locked (correct) tile
                if (other.IsLocked)
                {
                    tile.ReturnToOrigin();
                    return;
                }

                // Otherwise, normal swap
                float animDur = 0.35f;
                float swapDelay = 0.05f;

                other.StopAllAnimations();
                tile.StopAllAnimations();

                bestSlot.SetOccupant(tile);

                other.MoveToOriginalPositionAnimated(animDur, null);

                StartCoroutine(DelayedSnap(bestSlot, tile, swapDelay));

                return;
            }
    }

        // Neither: return to scattered origin
        tile.ReturnToOrigin();
    }

    IEnumerator DelayedSnap(Slot slot, Tile tileToSnap, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (slot == null || tileToSnap == null)
            yield break;

        tileToSnap.SnapToSlot(slot, () =>
        {
            if (tileToSnap.TileID == slot.SlotIndex)
            {
                tileToSnap.LockCorrectlyPlaced();
            }

            CheckForWin();
        });
    }

    // -----------------------
    // Win Check
    // -----------------------
    void CheckForWin()
    {
        foreach (var s in allSlots)
        {
            if (s.Occupant == null) return;
            if (s.Occupant.TileID != s.SlotIndex) return;
        }

        StartCoroutine(HandleWin());
    }

    IEnumerator HandleWin()
    {
        inputLocked = true;

        // Play win sound
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWinSound();

        if (winMessage != null)
        {
            winMessage.text = "Well Done!";
            winMessage.gameObject.SetActive(true);
        }

        foreach (var t in allTiles)
            t.Pulse(0.35f);

        yield return new WaitForSeconds(1f);

        state = GameState.EndGame;
    }
}
