using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("=== 調試設置 (Debug) ===")]
    [SerializeField, Tooltip("開啟以在 Console 追蹤 UI 與遊戲狀態切換")]
    private bool showDebugLogs = true;

    [Header("=== 分數 UI ===")]
    [SerializeField] private TMP_Text scoreText;

    [Header("=== 垃圾計數 UI ===")]
    [SerializeField] private TMP_Text trashCounterText;

    [Header("=== 倒數計時 UI ===")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField, Min(1f)] private float gameDuration = 90f;

    [Header("=== 技能圖示 UI ===")]
    [SerializeField] private Image skill1Icon;
    [SerializeField] private Image skill2Icon;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.3f;

    [Header("=== 暫停選單 UI ===")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button teachButton;
    [SerializeField] private GameObject teachPanel;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseToStartButton;

    [Header("=== 結束 UI ===")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private Button endToStartButton;

    [Header("=== 輸入綁定 ===")]
    [SerializeField] private InputAction pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");

    private InputAction closeTeachAction;
    private bool isPaused;
    private bool isTeaching;
    private bool isGameOver;
    private float remainingTime;
    private int lastDisplaySeconds = -1;

    // 記錄當前總分
    private int currentScore;

    private void Awake()
    {
        closeTeachAction = new InputAction("CloseTeach", InputActionType.Button);
        closeTeachAction.AddBinding("<Mouse>/leftButton");
        closeTeachAction.AddBinding("<Mouse>/rightButton");
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0, 1, 0);
    }

    private void OnEnable()
    {
        TrashCounter.Changed += OnTrashCounterChanged;

        // [重點註釋] 註冊事件監聽，當黑洞吃掉垃圾發出廣播時，呼叫 AddScore
        BlackHoleObstacle.OnTrashAbsorbedScore += AddScore;

        if (continueButton != null) continueButton.onClick.AddListener(ResumeGame);
        if (teachButton != null) teachButton.onClick.AddListener(OnTeachClicked);
        if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(OnRestartGame);
        if (pauseToStartButton != null) pauseToStartButton.onClick.AddListener(OnReturnToStartMenu);
        if (endToStartButton != null) endToStartButton.onClick.AddListener(OnReturnToStartMenu);

        pauseAction.Enable();
        pauseAction.performed += OnPauseActionTriggered;
        closeTeachAction.Enable();
        closeTeachAction.performed += OnCloseTeachActionTriggered;
    }

    private void OnDisable()
    {
        TrashCounter.Changed -= OnTrashCounterChanged;

        // 解除註冊，防止切換場景時產生 Memory Leak 錯誤
        BlackHoleObstacle.OnTrashAbsorbedScore -= AddScore;

        if (PlayerController.instance != null) PlayerController.instance.OnModeChanged -= OnSkillModeChanged;

        if (continueButton != null) continueButton.onClick.RemoveListener(ResumeGame);
        if (teachButton != null) teachButton.onClick.RemoveListener(OnTeachClicked);
        if (pauseRestartButton != null) pauseRestartButton.onClick.RemoveListener(OnRestartGame);
        if (pauseToStartButton != null) pauseToStartButton.onClick.RemoveListener(OnReturnToStartMenu);
        if (endToStartButton != null) endToStartButton.onClick.RemoveListener(OnReturnToStartMenu);

        pauseAction.Disable();
        closeTeachAction.Disable();
    }

    private void OnDestroy() => closeTeachAction?.Dispose();

    private void Start()
    {
        Log("初始化 UI 系統，遊戲開始。");
        isPaused = isTeaching = isGameOver = false;
        remainingTime = gameDuration;

        // 初始化分數
        currentScore = 0;
        UpdateScoreText();

        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);
        endPanel?.SetActive(false);

        UpdateTimerText();
        RefreshTrash(TrashCounter.Collected, TrashCounter.Total);

        if (PlayerController.instance != null)
        {
            PlayerController.instance.OnModeChanged += OnSkillModeChanged;
            OnSkillModeChanged(PlayerController.instance.currentMode);
        }
        SetGameState(true);
    }

    private void Update()
    {
        if (isPaused || isGameOver) return;
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            UpdateTimerText();
            GameOver("時間耗盡");
            return;
        }
        UpdateTimerText();
    }

    // 接收來自黑洞的分數，加總並更新 UI
    private void AddScore(int scoreToAdd)
    {
        currentScore += scoreToAdd;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
    }

    private void OnTrashCounterChanged(int c, int t) => RefreshTrash(c, t);

    private void RefreshTrash(int collected, int total)
    {
        if (trashCounterText != null) trashCounterText.text = $"{collected}/{total}";
        if (!isGameOver && total > 0 && collected >= total)
        {
            GameOver("已收集所有垃圾");
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
        if (displaySeconds == lastDisplaySeconds) return;

        lastDisplaySeconds = displaySeconds;
        timerText.text = $"{(displaySeconds / 60):00}:{(displaySeconds % 60):00}";
    }

    private void OnSkillModeChanged(BroomMode mode)
    {
        Log($"切換技能模式: {mode}");
        SetImageAlpha(skill1Icon, mode == BroomMode.Impact ? 1f : inactiveAlpha);
        SetImageAlpha(skill2Icon, mode == BroomMode.Sticky ? 1f : inactiveAlpha);
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var c = img.color; c.a = alpha; img.color = c;
    }

    private void OnPauseActionTriggered(InputAction.CallbackContext context)
    {
        if (isGameOver) return;
        if (isTeaching) { CloseTeachPanel(); return; }
        if (isPaused) ResumeGame(); else PauseGame();
    }

    private void PauseGame()
    {
        if (isGameOver) return;
        Log("觸發暫停 (Pause)。");
        isPaused = true;
        pausePanel?.SetActive(true);
        pausePanel?.transform.SetAsLastSibling();
        SetGameState(false);
    }

    private void ResumeGame()
    {
        if (isGameOver) return;
        Log("解除暫停，遊戲繼續 (Resume)。");
        isPaused = isTeaching = false;
        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);
        SetGameState(true);
    }

    private void OnTeachClicked()
    {
        if (isGameOver) return;
        Log("開啟教學面板。");
        isTeaching = true;
        pausePanel?.SetActive(false);
        if (teachPanel != null)
        {
            teachPanel.SetActive(true);
            teachPanel.transform.SetAsLastSibling();
        }
    }

    private void CloseTeachPanel()
    {
        if (isGameOver) return;
        Log("關閉教學面板，返回暫停選單。");
        isTeaching = false;
        teachPanel?.SetActive(false);
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
        }
    }

    private void OnCloseTeachActionTriggered(InputAction.CallbackContext context)
    {
        if (isTeaching) CloseTeachPanel();
    }

    private void GameOver(string reason)
    {
        if (isGameOver) return;
        Log($"遊戲結束 (GameOver)，原因: {reason}");
        isGameOver = true;
        isPaused = isTeaching = false;
        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);
        if (endPanel != null)
        {
            endPanel.SetActive(true);
            endPanel.transform.SetAsLastSibling();
        }
        SetGameState(false);
    }

    private void OnRestartGame()
    {
        Log("重新開始當前關卡。");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnReturnToStartMenu()
    {
        Log("返回主畫面。");
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartMenu");
    }

    private void SetGameState(bool isPlayingGame)
    {
        Time.timeScale = isPlayingGame ? 1f : 0f;
        if (PlayerController.instance != null) PlayerController.instance.enabled = isPlayingGame;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Log(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugLogs)
        {
            Debug.Log($"[UIManager] {message}");
        }
#endif
    }
}