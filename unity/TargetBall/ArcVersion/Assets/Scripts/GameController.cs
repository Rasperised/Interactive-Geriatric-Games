using UnityEngine;
using TMPro;
public class GameController : MonoBehaviour
{
    [Header("Game Settings")]
    public int targetCount = 10;
    public float gameTime = 30f;
    [Header("UI")]
    public TextMeshProUGUI countText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI resultText;
    private int currentCount = 0;
    private float timer;
    private bool gameEnded = false;
    private TargetSide lastHitTarget = TargetSide.Any;
    public enum TargetSide
    {
        Any,
        Left,
        Right
    }
    void Start()
    {
        timer = gameTime;
        currentCount = 0;
        gameEnded = false;
        lastHitTarget = TargetSide.Any;
        resultText.gameObject.SetActive(false);
        UpdateUI();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
        }
        if (gameEnded) return;
        timer -= Time.deltaTime;
        UpdateUI();
        if (timer <= 0)
        {
            EndGame(false);
        }
    }
    public bool CanHitTarget(TargetSide hitSide)
    {
        // First hit: can hit any target
        if (lastHitTarget == TargetSide.Any)
            return true;
        // After first hit: must alternate
        return hitSide != lastHitTarget;
    }
    public void RegisterHit(TargetSide hitSide)
    {
        if (gameEnded) return;
        lastHitTarget = hitSide;
        currentCount++;

        UpdateUI(); // UPDATE UI IMMEDIATELY after incrementing

        Debug.Log($"Hit registered: {hitSide}, Count: {currentCount}");
        if (currentCount >= targetCount)
        {
            EndGame(true);
        }
    }
    void EndGame(bool success)
    {
        gameEnded = true;
        resultText.gameObject.SetActive(true);
        resultText.text = success ? "SUCCESS" : "TIME OUT\nTRY AGAIN";
    }
    public bool IsGameEnded()
    {
        return gameEnded;
    }
    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    void UpdateUI()
    {
        countText.text = "Count: " + currentCount;
        timeText.text = "Time: " + Mathf.CeilToInt(timer);
    }
}