using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration; // UnityIntegration helpers (OpenCVMatUtils, OpenCVEnv, OpenCVDebug)
using OpenCVForUnity.UnityUtils;
using System;
using System.Collections;
using System.Collections.Generic;
// using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
//using static UnityEngine.Rendering.DebugUI.Table;

/// <summary>
/// Capture webcam frames at 360, convert to OpenCV Mat for optional processing,
/// then display the (possibly processed) frame on a quad named "PreviewPlane".
/// Uses OpenCVForUnity and OpenCVMatUtils (UnityIntegration) for conversions.
/// Find difference between current and previous frame, and a weighted average of frames.
/// Also finds contours & centroids on the red channel and draws results to imageOut1/imageOut2.
/// </summary>
public class WebcamManager : MonoBehaviour
{
    [Header("Webcam")]
    public int requestedWidth = 320;   // 16:9 = 640x360 
    public int requestedHeight = 180;
    public int requestedFPS = 30;
    public string deviceName = ""; // empty = default device
    public double threshold = 28.0;
    public float K = 0.05f;

    [Header("Contour / Centroid")]
    [Tooltip("Threshold applied to the Red channel (0..255) to create the binary mask")]
    public int contourThreshold = 40;
    [Tooltip("Ignore contours with area smaller than this")]
    public double minContourArea = 3.0;
    [Tooltip("Ignore contours with area larger than this")]
    public double maxContourArea = 5000.0;
    [Tooltip("Centroid rectangle half-size (px)")]
    public int centroidHalfSize = 3;

    [Header("Image Transform")]
    [Tooltip("When true the incoming webcam image will be mirrored horizontally for both preview and processing.")]
    public bool mirrorInput = true;

    private const string previewPlane = "PreviewPlane";
    private DisplayPanels displayPanels;

    WebCamTexture webCamTexture;
    Texture2D previewTexture;   // used to link to preview plane 
    Texture2D imageInTexture;   // used for input image 
    Texture2D imageOutTexture;  // used for output image 
    Texture2D imageOut1Texture; // created for future use
    Texture2D imageOut2Texture;
    Texture2D imageOut3Texture;
    Texture2D imageOut4Texture;

    // Mats (reused) - OpenCV images, need Mat.Dispose()
    Mat imageIn, imageOut, imageOut1, imageOut2, imageOut3, imageOut4;
    Mat prevImageIn, prevImageOut;

    // full-size temporary Mat reused for Texture2D -> Mat conversion (avoid per-frame allocation)
    Mat fullMat;

    // single-channel mats for channels and mask
    Mat rMat, gMat, bMat;
    Mat maskMat;
    Mat hierarchy;

    // contours container
    List<MatOfPoint> contours;

    // small temp Mat reused for per-channel absdiff/threshold to avoid per-frame allocation
    private Mat _tmpSingle;

    // camera runtime state
    private int camWidth = 0;
    private int camHeight = 0;
    private bool cameraReady = false;
    private Coroutine webcamInitCoroutine;

    // Resolution during processing OpenCV Mats and output Textures
    private int procWidth = 320;
    private int procHeight = 180;

    private bool isFirstFrame = true;
    private int skipFramesCounter = 5;

    // contour colors (RGBA order, matching this project's Mat channel order)
    private Scalar[] contourColors;

    Renderer previewRenderer;

    // FIX: Added these variables to handle inactive GameObjects
    GameObject previewPlaneObject;
    bool hasPreviewPlane = false;
    bool previewPlaneInitialized = false;  // Track if PreviewPlane has been initialized

    GameObject displayPanelsObject;
    bool hasDisplayPanels = false;
    bool displayPanelsInitialized = false;  // Track if DisplayPanels has been initialized

    void Start()
    {
        // FIX: Find preview plane even if it's inactive by searching through all objects
        previewPlaneObject = FindInactiveObjectByName(previewPlane);

        if (previewPlaneObject != null)
        {
            previewRenderer = previewPlaneObject.GetComponent<Renderer>();
            if (previewRenderer != null)
            {
                hasPreviewPlane = true;
                Debug.Log("WebcamPreviewOpenCV: PreviewPlane found. Preview will be available when active.");
            }
            else
            {
                Debug.LogWarning("WebcamPreviewOpenCV: PreviewPlane found but does not have a Renderer component. Preview will be disabled.");
                hasPreviewPlane = false;
            }
        }
        else
        {
            Debug.LogWarning($"WebcamPreviewOpenCV: GameObject named \"{previewPlane}\" not found. Preview will be disabled, but webcam will still work.");
            hasPreviewPlane = false;
        }

        // FIX: Find DisplayPanels even if it's inactive
        displayPanelsObject = FindInactiveObjectByName("DisplayPanels");
        if (displayPanelsObject != null)
        {
            displayPanels = displayPanelsObject.GetComponent<DisplayPanels>();
            if (displayPanels != null)
            {
                hasDisplayPanels = true;
                Debug.Log("WebcamPreviewOpenCV: DisplayPanels found. Display panels will be available when active.");
            }
            else
            {
                Debug.LogWarning("WebcamPreviewOpenCV: DisplayPanels GameObject found but DisplayPanels component missing. Display calls will be ignored.");
                hasDisplayPanels = false;
            }
        }
        else
        {
            Debug.LogWarning("WebcamPreviewOpenCV: DisplayPanels GameObject not found. Display calls will be ignored, but webcam will still work.");
            hasDisplayPanels = false;
        }

        // initialize contour colors (RGBA)
        contourColors = new Scalar[]
        {
            new Scalar(255, 0, 0, 255),   // Red
            new Scalar(0, 255, 0, 255),   // Green
            new Scalar(0, 0, 255, 255),   // Blue
            new Scalar(255, 255, 0, 255), // Yellow
            new Scalar(255, 0, 255, 255), // Magenta
            new Scalar(0, 255, 255, 255), // Cyan
        };

        // Start the single camera initialization routine (turn on camera, wait for real size, allocate buffers)
        webcamInitCoroutine = StartCoroutine(InitializeWebcamAndBuffersCoroutine(3.0f)); // 3s timeout
    }

    // FIX: Helper method to find GameObject even if it's inactive
    GameObject FindInactiveObjectByName(string name)
    {
        // First try the normal Find (for active objects)
        GameObject obj = GameObject.Find(name);
        if (obj != null) return obj;

        // If not found, search through all objects including inactive ones
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name == name && go.hideFlags == HideFlags.None)
            {
                // Make sure it's a scene object, not a prefab or asset
                if (go.scene.IsValid())
                {
                    return go;
                }
            }
        }
        return null;
    }

    IEnumerator InitializeWebcamAndBuffersCoroutine(float timeoutSeconds)
    {
        // choose device
        WebCamDevice[] devices = WebCamTexture.devices;
        string usedDevice = deviceName;
        if (string.IsNullOrEmpty(usedDevice) && devices.Length > 0)
            usedDevice = devices[0].name;

        webCamTexture = new WebCamTexture(usedDevice, requestedWidth, requestedHeight, requestedFPS);
        webCamTexture.Play();

        float elapsed = 0f;
        // wait until webcam reports a valid resolution or timeout
        while ((webCamTexture == null || webCamTexture.width <= 16 || webCamTexture.height <= 16) && elapsed < timeoutSeconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (webCamTexture == null)
        {
            Debug.LogError("WebcamPreviewOpenCV: webcam failed to start.");
            cameraReady = false;
            yield break;
        }

        camWidth = webCamTexture.width > 16 ? webCamTexture.width : requestedWidth;
        camHeight = webCamTexture.height > 16 ? webCamTexture.height : requestedHeight;

        Debug.Log($"WebcamPreviewOpenCV: webcam initialized – resolution {camWidth}x{camHeight}");

        // allocate textures with actual size for webcam preview 
        // FIX: Only allocate preview texture if PreviewPlane exists
        if (hasPreviewPlane)
        {
            if (previewTexture != null) Destroy(previewTexture);
            previewTexture = new Texture2D(camWidth, camHeight, TextureFormat.RGBA32, false);

            // attach previewTexture to previewPanel (will be updated each frame)
            // FIX: Only set material if PreviewPlane is currently active
            if (previewPlaneObject.activeInHierarchy && previewRenderer != null)
            {
                previewRenderer.material.mainTexture = previewTexture;

                // If mirroring is requested, flip the preview quad UVs so preview looks mirrored
                if (mirrorInput)
                {
                    // negative X scale mirrors horizontally; offset 1 shifts UV so image stays visible
                    previewRenderer.material.mainTextureScale = new Vector2(-1f, 1f);
                    previewRenderer.material.mainTextureOffset = new Vector2(1f, 0f);
                }
                else
                {
                    previewRenderer.material.mainTextureScale = new Vector2(1f, 1f);
                    previewRenderer.material.mainTextureOffset = new Vector2(0f, 0f);
                }

                previewPlaneInitialized = true;  // Mark as initialized
            }
        }

        // call setupMats to allocate Mats and other Textures
        setupMatsAndDisplayPanels();

        cameraReady = true;
        webcamInitCoroutine = null;
    }

    void setupMatsAndDisplayPanels()
    {

        // allocate textures with actual size for OpenCV processing
        if (imageInTexture != null) Destroy(imageInTexture);
        if (imageOutTexture != null) Destroy(imageOutTexture);
        if (prevImageIn != null) { prevImageIn.Dispose(); prevImageIn = null; }
        if (prevImageOut != null) { prevImageOut.Dispose(); prevImageOut = null; }

        imageInTexture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);
        imageOutTexture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);
        //imageOut1Texture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);
        //imageOut2Texture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);
        //imageOut3Texture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);
        //imageOut4Texture = new Texture2D(procWidth, procHeight, TextureFormat.RGBA32, false);

        // dispose any previous mats before re-creating to avoid leaks
        imageIn?.Dispose(); imageOut?.Dispose(); imageOut1?.Dispose(); imageOut2?.Dispose();
        imageOut3?.Dispose(); imageOut4?.Dispose(); prevImageIn?.Dispose(); prevImageOut?.Dispose();
        _tmpSingle?.Dispose();
        fullMat?.Dispose();
        fullMat = null;

        // dispose any channel/mask mats and contours
        rMat?.Dispose(); rMat = null;
        gMat?.Dispose(); gMat = null;
        bMat?.Dispose(); bMat = null;
        maskMat?.Dispose(); maskMat = null;
        hierarchy?.Dispose(); hierarchy = null;
        if (contours != null)
        {
            foreach (var c in contours) c?.Dispose();
            contours = null;
        }

        // create Mats using 4 channels to match RGBA32 (safe for conversion helpers)
        imageIn = new Mat();
        imageOut = new Mat();
        imageOut1 = new Mat();
        imageOut2 = new Mat();
        imageOut3 = new Mat();
        imageOut4 = new Mat();
        prevImageIn = new Mat();
        prevImageOut = new Mat();

        // create mats with actual size (processing size)
        imageIn.create(procHeight, procWidth, CvType.CV_8UC4);
        imageOut.create(procHeight, procWidth, CvType.CV_8UC4);
        imageOut1.create(procHeight, procWidth, CvType.CV_8UC4);
        imageOut2.create(procHeight, procWidth, CvType.CV_8UC4);
        imageOut3.create(procHeight, procWidth, CvType.CV_8UC1); //CV_8UC1 for mask
        imageOut4.create(procHeight, procWidth, CvType.CV_32FC4);
        prevImageIn.create(procHeight, procWidth, CvType.CV_8UC4);
        prevImageOut.create(procHeight, procWidth, CvType.CV_8UC4);

        // create channel single-channel mats and mask
        rMat = new Mat();
        gMat = new Mat();
        bMat = new Mat();
        maskMat = new Mat();
        rMat.create(procHeight, procWidth, CvType.CV_8UC1);
        gMat.create(procHeight, procWidth, CvType.CV_8UC1);
        bMat.create(procHeight, procWidth, CvType.CV_8UC1);
        maskMat.create(procHeight, procWidth, CvType.CV_8UC1);

        // create hierarchy mat and contours list
        hierarchy = new Mat();
        contours = new List<MatOfPoint>();

        // create small temp single-channel mat reused each frame
        _tmpSingle = new Mat();
        _tmpSingle.create(procHeight, procWidth, CvType.CV_8UC1);
        _tmpSingle.setTo(new Scalar(0));

        // create reusable full-size Mat for Texture2D -> Mat conversion (match previewTexture)
        fullMat = new Mat();
        fullMat.create(camHeight, camWidth, CvType.CV_8UC4);

        // set all pixel values to zero (black), alpha 255s where appropriate
        imageIn.setTo(new Scalar(0, 0, 0, 255));
        imageOut.setTo(new Scalar(0, 0, 0, 255));
        imageOut1.setTo(new Scalar(0, 0, 0, 255));
        imageOut2.setTo(new Scalar(0, 0, 0, 255));
        // imageOut3 is single-channel mask — use single-value scalar
        imageOut3.setTo(new Scalar(0));
        imageOut4.setTo(new Scalar(0, 0, 0, 255));
        prevImageIn.setTo(new Scalar(0, 0, 0, 255));
        prevImageOut.setTo(new Scalar(0, 0, 0, 255));

        // initialize DisplayPanels
        // FIX: Only initialize DisplayPanels if it exists and is active
        if (hasDisplayPanels && displayPanelsObject != null && displayPanelsObject.activeInHierarchy && displayPanels != null)
        {
            // initialize DisplayPanels' textures/materials using proccsing width and height 
            displayPanels.InitDisplayPanels(procWidth, procHeight);

            // initial display to panels (Display0 is full screen, the others are smaller)
            displayPanels.ShowMatOnDisplay(0, imageIn);   // initially all empty images
            displayPanels.ShowMatOnDisplay(1, imageOut1); // 
            displayPanels.ShowMatOnDisplay(2, imageOut2); // 
            displayPanels.ShowMatOnDisplay(3, imageOut3); // 
            displayPanels.ShowMatOnDisplay(4, imageOut4); //

            displayPanelsInitialized = true;  // Mark as initialized
        }
    }

    /// <summary>
    /// In-place version: compute per-channel absolute differences between two Mats, threshold each channel,
    /// then combine via bitwise OR into provided dst (CV_8UC1). Returns true on success. dst is written into,
    /// and must be preallocated (setupMats creates imageOut3 as CV_8UC1).
    /// </summary>
    bool ComputeChannelDiffMask(Mat a, Mat b, double thresh, Mat dst)
    {
        if (a == null || b == null || dst == null)
            return false;

        if (a.cols() != b.cols() || a.rows() != b.rows() || dst.cols() != a.cols() || dst.rows() != a.rows())
        {
            Debug.LogWarning("ComputeChannelDiffMask: input/dst sizes differ.");
            return false;
        }

        int depth = a.depth();
        if (depth != CvType.CV_8U)
        {
            Debug.LogWarning("ComputeChannelDiffMask: only CV_8U Mats supported in this helper.");
            return false;
        }

        int channels = a.channels();
        // If single-channel, do simple absdiff + threshold using reusable _tmpSingle
        if (channels == 1)
        {
            Core.absdiff(a, b, _tmpSingle);
            Imgproc.threshold(_tmpSingle, dst, thresh, 255, Imgproc.THRESH_BINARY);
            return true;
        }

        // For 3 or 4 channels: split and operate on R,G,B channels (ignore alpha)
        List<Mat> chA = new List<Mat>();
        List<Mat> chB = new List<Mat>();
        Mat maskR = null, maskG = null, maskB = null;
        try
        {
            Core.split(a, chA); // expected order: R,G,B,(A)
            Core.split(b, chB);

            if (chA.Count < 3 || chB.Count < 3)
            {
                Debug.LogWarning("ComputeChannelDiffMask: input does not contain 3 color channels.");
                foreach (var m in chA) m.Dispose();
                foreach (var m in chB) m.Dispose();
                return false;
            }

            // reuse _tmpSingle for per-channel diff & threshold, write masks into maskR/G/B then combine into dst
            maskR = new Mat();
            maskG = new Mat();
            maskB = new Mat();

            Core.absdiff(chA[0], chB[0], _tmpSingle);
            Imgproc.threshold(_tmpSingle, maskR, thresh, 255, Imgproc.THRESH_BINARY);

            Core.absdiff(chA[1], chB[1], _tmpSingle);
            Imgproc.threshold(_tmpSingle, maskG, thresh, 255, Imgproc.THRESH_BINARY);

            Core.absdiff(chA[2], chB[2], _tmpSingle);
            Imgproc.threshold(_tmpSingle, maskB, thresh, 255, Imgproc.THRESH_BINARY);

            // combined -> dst
            Core.bitwise_or(maskR, maskG, dst);
            Core.bitwise_or(dst, maskB, dst);

            // cleanup channel Mats
            foreach (var m in chA) m.Dispose();
            foreach (var m in chB) m.Dispose();

            // dispose temporary masks
            maskR.Dispose();
            maskG.Dispose();
            maskB.Dispose();

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("ComputeChannelDiffMask failed: " + ex.Message);
            foreach (var m in chA) m.Dispose();
            foreach (var m in chB) m.Dispose();
            maskR?.Dispose();
            maskG?.Dispose();
            maskB?.Dispose();
            return false;
        }
    }

    // Backwards-compatible convenience that returns a newly allocated Mat (kept for callers that expect it).
    Mat ComputeChannelDiffMask(Mat a, Mat b, double thresh)
    {
        Mat dst = new Mat();
        dst.create(a.rows(), a.cols(), CvType.CV_8UC1);
        if (!ComputeChannelDiffMask(a, b, thresh, dst))
        {
            dst.Dispose();
            return null;
        }
        return dst;
    }

    public Texture2D GetMotionTextureRGBA(bool downscaleTo180x90 = false)
    {
        // Returns the current motion mask (imageOut3) as an RGBA32 Texture2D.
        // If downscaleTo180x90 is true the mask is resized to 180x90 before conversion.
        // Caller owns the returned Texture2D and should Destroy it when finished.
        if (imageOut3 == null)
            return null;

        Mat src = imageOut3;
        Mat tmp = null;
        Mat rgba = null;
        try
        {
            // Optional downscale
            if (downscaleTo180x90)
            {
                tmp = new Mat();
                Imgproc.resize(src, tmp, new Size(180, 90), 0, 0, Imgproc.INTER_AREA);
                src = tmp;
            }

            // Convert single-channel mask to RGBA.
            rgba = new Mat();
            if (src.channels() == 1)
            {
                Imgproc.cvtColor(src, rgba, Imgproc.COLOR_GRAY2RGBA);
            }
            else if (src.channels() == 3)
            {
                Imgproc.cvtColor(src, rgba, Imgproc.COLOR_RGB2RGBA);
            }
            else // already 4 channels or unknown: ensure 4-channel copy
            {
                src.copyTo(rgba);
            }

            int w = rgba.cols();
            int h = rgba.rows();
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            OpenCVMatUtils.MatToTexture2D(rgba, tex);
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogError($"GetMotionTextureRGBA failed: {ex.Message}");
            return null;
        }
        finally
        {
            // Dispose temporary mats but not the returned Texture2D
            rgba?.Dispose();
            tmp?.Dispose();
        }
    }


    // Public helper function to select a single filtered centroid and return its world coordinates.
    // Returns true and sets `worldPos` when exactly one centroid passes the size filter.
    // Returns false and leaves `worldPos` untouched when zero or multiple centroids pass the filter
    // or on any error.
    public bool GetCentroid(out Vector2 worldPos)
    {
        double minArea = minContourArea;
        double maxArea = maxContourArea;

        worldPos = Vector2.zero;

        // Use defaults when caller passes non-positive values
        if (minArea <= 0) minArea = 10.0;
        if (maxArea <= 0) maxArea = 1000000.0f;

        // Basic guards
        if (imageIn == null || imageIn.empty())
            return false;

        try
        {
            // Extract red channel into a temporary Mat
            Mat g = new Mat();
            Core.extractChannel(imageIn, g, 1); // channel 0=R, 1=G, 2=B in this project's convention

            // Threshold red channel to produce binary mask
            Mat mask = new Mat();
            Imgproc.threshold(g, mask, contourThreshold, 255, Imgproc.THRESH_TOZERO); //THRESH_BINARY

            // Find contours on a clone of mask (findContours can modify source)
            Mat maskClone = new Mat();
            mask.copyTo(maskClone);

            List<MatOfPoint> localContours = new List<MatOfPoint>();
            Mat localHierarchy = new Mat();
            Imgproc.findContours(maskClone, localContours, localHierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            // Collect centroids that pass size filtering
            List<Point> validCentroids = new List<Point>();
            for (int i = 0; i < localContours.Count; i++)
            {
                double area = Imgproc.contourArea(localContours[i]);
                Debug.Log($"Contour {i} area: {area}");

                //if (area < minArea || area > maxArea) continue;
                if (area < minArea)
                {
                    Debug.Log($"Contour {i} area: {area} - skipped.");
                    continue;
                }
                Debug.Log($"Contour {i} area: {area}.");

                Moments mu = Imgproc.moments(localContours[i]);
                double m00 = mu.get_m00();
                if (Math.Abs(m00) < Double.Epsilon) continue;

                double cx = mu.get_m10() / m00;
                double cy = mu.get_m01() / m00;
                validCentroids.Add(new Point(cx, cy));
            }

            // Clean up local contour Mats
            foreach (var c in localContours) c?.Dispose();
            localContours.Clear();
            localHierarchy.Dispose();
            maskClone.Dispose();
            mask.Dispose();
            g.Dispose();

            // If exactly one valid centroid found, convert to world coordinates and return it
            if (validCentroids.Count == 1)
            {
                Point c = validCentroids[0];
                Debug.Log($"Valid centroid at image coords: ({c.x}, {c.y})");

                Camera cam = Camera.main;
                if (cam == null)
                    return false;

                // calcualte distance from camera x = -17.15 to Left Padel x position at x = 8.0f-16.51f 
                // (Paddles is at x=8.0f in world space, and PaddleLeft is offset -16.51f from world origin)
                // float distanceToPlane = -cam.transform.position.x + (8.0f-16.51f);

                // first, get distance from camera to world origin (x=0)
                float camX = -cam.transform.position.x;
                // then, get the distance of PaddelLeft from world origin by finding the game objects
                // and find real world paddleLeft x position taking into account paddles' position
                GameObject paddles = GameObject.Find("Paddles");
                GameObject paddleLeft = paddles.transform.Find("PaddleLeft")?.gameObject;
                float paddleLeftX = paddleLeft.transform.position.x;

                float distanceToPlane = camX + paddleLeftX;

                // Map processing-space (procWidth x procHeight) coordinates to screen pixel coordinates,
                // then to world position at the camera plane distance.
                //float screenX = ((float)(c.x + 0.5) / (float)procWidth) * Screen.width;
                //float screenY = ((float)(c.y + 0.5) / (float)procHeight) * Screen.height;
                // Map image coords (top-left origin) to Unity screen coords (bottom-left origin).
                float screenX = ((float)(c.x + 0.5) / (float)procWidth) * Screen.width;
                // Flip Y because image Y=0 is top but Unity screen Y=0 is bottom
                float screenY = Screen.height - (((float)(c.y + 0.5) / (float)procHeight) * Screen.height);
                Vector3 worldP = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, distanceToPlane));
                worldPos = new Vector2(worldP.x, worldP.y);
                return true;
            }

            // zero or multiple valid centroids -> no single centroid to return
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("TryGetFilteredCentroid failed: " + ex.Message);
            return false;
        }
    }

    void Update()
    {
        // Only proceed when camera is ready and frame updated
        // FIX: Removed previewRenderer check so webcam works even without PreviewPlane
        if (!cameraReady || webCamTexture == null || !webCamTexture.isPlaying)
            return;

        if (!webCamTexture.didUpdateThisFrame)
            return;

        // Process, and Display 4 images
        try
        {
            // Get pixels directly from WebCamTexture into managed array
            Color32[] p = webCamTexture.GetPixels32();

            // FIX: Only update preview texture if PreviewPlane exists and is active
            if (hasPreviewPlane && previewTexture != null)
            {
                previewTexture.SetPixels32(p);
                previewTexture.Apply(false); // no mipmaps

                // If PreviewPlane was just activated and not initialized, initialize it now
                if (previewPlaneObject.activeInHierarchy && previewRenderer != null)
                {
                    if (!previewPlaneInitialized)
                    {
                        previewRenderer.material.mainTexture = previewTexture;

                        if (mirrorInput)
                        {
                            previewRenderer.material.mainTextureScale = new Vector2(-1f, 1f);
                            previewRenderer.material.mainTextureOffset = new Vector2(1f, 0f);
                        }
                        else
                        {
                            previewRenderer.material.mainTextureScale = new Vector2(1f, 1f);
                            previewRenderer.material.mainTextureOffset = new Vector2(0f, 0f);
                        }

                        previewPlaneInitialized = true;
                        Debug.Log("WebcamPreviewOpenCV: PreviewPlane initialized after being enabled.");
                    }
                    else if (previewRenderer.material.mainTexture != previewTexture)
                    {
                        // Reattach if somehow disconnected
                        previewRenderer.material.mainTexture = previewTexture;
                    }
                }
                else if (!previewPlaneObject.activeInHierarchy && previewPlaneInitialized)
                {
                    // Reset flag when PreviewPlane is disabled
                    previewPlaneInitialized = false;
                }
            }
            else
            {
                // Create a temporary texture just for processing if preview is disabled
                if (previewTexture == null)
                {
                    previewTexture = new Texture2D(camWidth, camHeight, TextureFormat.RGBA32, false);
                }
                previewTexture.SetPixels32(p);
                previewTexture.Apply(false);
            }

            int width = previewTexture.width;
            int height = previewTexture.height;

            // Do all processing using OpenCV Mats Here
            // convert Texture2D to Mat (Texture2D -> Mat CV_8UC4)
            OpenCVMatUtils.Texture2DToMat(previewTexture, fullMat);

            // Mirror input horizontally if requested (flipCode = 1 horizontally).
            if (mirrorInput)
            {
                Core.flip(fullMat, fullMat, 1);
            }

            Imgproc.resize(fullMat, imageIn, new Size(procWidth, procHeight), 0, 0, Imgproc.INTER_AREA);

            // skip first N frames to allow camera auto-exposure and other settings to settle
            if (skipFramesCounter > 0)
            {
                skipFramesCounter--;
                return;
            }
            if (isFirstFrame)
            {
                // First frame: initialize prevImageIn and prevImageOut
                imageIn.copyTo(prevImageIn);
                imageIn.copyTo(prevImageOut);
                isFirstFrame = false;
                Debug.Log("WebcamPreviewOpenCV: First frame - initialized previous images.");
            }
            else
            {
                // Example interframe processing: simple frame differencing (in-place into imageOut3)
                if (prevImageIn != null && imageOut3 != null)
                {
                    bool ok = ComputeChannelDiffMask(imageIn, prevImageIn, threshold, imageOut3);
                    if (!ok)
                    {
                        // imageOut3 is CV_8UC1 - use single-value scalar
                        imageOut3.setTo(new Scalar(0));
                    }
                }

                // Example interframe processing: weighted average
                // using K*imageIn + (1-K)*prevImageOut
                Core.addWeighted(imageIn, K, prevImageOut, (1.0 - K), 0.0, imageOut4);

                // Store a copy of current frame for next interframe processing
                imageIn.copyTo(prevImageIn);
                imageOut4.copyTo(prevImageOut);
            }

            // TODO: Find Contours and Centroids from imageIn 
            // 1. Filter and Thresholding 
            // 2. Find Contours, plot them on imageOut1 and imageOut2 
            // 3. Calculate Moments and Centroids
            // 4. Draw Centroids on imageOut1 and imageOut2 (as rectangles)
            // 5. Display imageOut1 and imageOut2 on Display2 and Display4

            // Extract R,G,B single-channel mats (project convention: channel order R,G,B,(A))
            Core.extractChannel(imageIn, rMat, 0);
            Core.extractChannel(imageIn, gMat, 1);
            Core.extractChannel(imageIn, bMat, 2);

            // Threshold on red channel into maskMat
            Imgproc.threshold(gMat, maskMat, contourThreshold, 255, Imgproc.THRESH_TOZERO); // or THRESH_BINARY

            // Prepare visualization mats (copy source RGBA into outputs)
            // imageIn.copyTo(imageOut1);
            // duplicate maskMat to imageOut1 for visualization
            Imgproc.cvtColor(maskMat, imageOut1, Imgproc.COLOR_GRAY2RGBA);
            imageIn.copyTo(imageOut2);

            // Find contours on maskMat
            // clear previous contours (dispose elements)
            if (contours != null)
            {
                foreach (var c in contours) c?.Dispose();
                contours.Clear();
            }
            else
            {
                contours = new List<MatOfPoint>();
            }

            Imgproc.findContours(maskMat, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            // Iterate contours, filter by area, draw outlines and centroids
            for (int i = 0; i < contours.Count; i++)
            {
                double area = Imgproc.contourArea(contours[i]);
                if (area < minContourArea) continue;

                // choose color cycling through the 6 basic colors
                Scalar col = contourColors[i % contourColors.Length];
                // draw contour outline on both output mats
                Imgproc.drawContours(imageOut1, contours, i, col, 1);
                Imgproc.drawContours(imageOut2, contours, i, col, 1);

                // compute moments and centroid
                Moments mu = Imgproc.moments(contours[i]);
                double m00 = mu.get_m00();
                if (Math.Abs(m00) < Double.Epsilon) continue;
                double cx = mu.get_m10() / m00;
                double cy = mu.get_m01() / m00;

                // draw centroid as filled rectangle (RGBA) centered at cx,cy
                Scalar centroidColor = new Scalar(0, 0, 255, 255); // white by default for centroid rect
                int hs = Math.Max(1, centroidHalfSize);
                Point topLeft = new Point(cx - hs, cy - hs);
                Point bottomRight = new Point(cx + hs, cy + hs);
                Imgproc.rectangle(imageOut1, topLeft, bottomRight, centroidColor, 1);
                Imgproc.rectangle(imageOut2, topLeft, bottomRight, centroidColor, 1);
            }

            // display output images to panels
            // FIX: Check if DisplayPanels just became active and needs initialization
            if (hasDisplayPanels && displayPanelsObject != null && displayPanelsObject.activeInHierarchy && displayPanels != null)
            {
                // If DisplayPanels exists and is active but hasn't been initialized yet, initialize it now
                if (!displayPanelsInitialized)
                {
                    displayPanels.InitDisplayPanels(procWidth, procHeight);
                    displayPanelsInitialized = true;
                    Debug.Log("WebcamPreviewOpenCV: DisplayPanels initialized after being enabled.");
                }

                displayPanels.HideDisplay(0);
                displayPanels.ShowMatOnDisplay(1, imageIn);
                //displayPanels.ShowMatOnDisplay(2, prevImageIn);
                displayPanels.ShowMatOnDisplay(3, imageOut3);
                // displayPanels.ShowMatOnDisplay(4, imageOut4);

                displayPanels.ShowMatOnDisplay(2, imageOut1);
                displayPanels.ShowMatOnDisplay(4, imageOut2);
            }
            else if (hasDisplayPanels && displayPanelsObject != null && !displayPanelsObject.activeInHierarchy)
            {
                // If DisplayPanels was disabled, reset the initialized flag so it can be re-initialized when enabled again
                if (displayPanelsInitialized)
                {
                    displayPanelsInitialized = false;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("WebcamPreviewOpenCV: error processing frame (method 2): " + ex.Message);
        }

    }

    void OnDisable()
    {
        StopWebcam();
    }

    void OnDestroy()
    {
        StopWebcam();
    }

    void StopWebcam()
    {
        cameraReady = false;

        if (webcamInitCoroutine != null)
        {
            StopCoroutine(webcamInitCoroutine);
            webcamInitCoroutine = null;
        }

        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
                webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }

        // dispose mats (free native memory) — dispose everything allocated in setup
        imageIn?.Dispose(); imageIn = null;
        imageOut?.Dispose(); imageOut = null;
        imageOut1?.Dispose(); imageOut1 = null;
        imageOut2?.Dispose(); imageOut2 = null;
        imageOut3?.Dispose(); imageOut3 = null;
        imageOut4?.Dispose(); imageOut4 = null;
        prevImageIn?.Dispose(); prevImageIn = null;
        prevImageOut?.Dispose(); prevImageOut = null;

        rMat?.Dispose(); rMat = null;
        gMat?.Dispose(); gMat = null;
        bMat?.Dispose(); bMat = null;
        maskMat?.Dispose(); maskMat = null;
        hierarchy?.Dispose(); hierarchy = null;

        if (contours != null)
        {
            foreach (var c in contours) c?.Dispose();
            contours = null;
        }

        _tmpSingle?.Dispose(); _tmpSingle = null;
        fullMat?.Dispose(); fullMat = null;

        if (previewTexture != null)
        {
            Destroy(previewTexture);
            previewTexture = null;
        }

        if (imageOutTexture != null)
        {
            Destroy(imageOutTexture);
            imageOutTexture = null;
        }
    }


}