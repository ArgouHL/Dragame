using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
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
    [SerializeField] private Button pauseToStartButton; // ✅ 新增：暫停選單回主畫面按鈕

    [Header("=== 結束 UI ===")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private Button endToStartButton; // ✅ 統一命名：結束畫面回主畫面按鈕

    [Header("=== 輸入綁定 ===")]
    [SerializeField] private InputAction pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");

    private InputAction closeTeachAction;
    private bool isPaused;
    private bool isTeaching;
    private bool isGameOver;
    private float remainingTime;
    private int lastDisplaySeconds = -1;

    private void Awake()
    {
        closeTeachAction = new InputAction("CloseTeach", InputActionType.Button);
        closeTeachAction.AddBinding("<Mouse>/leftButton");
        closeTeachAction.AddBinding("<Mouse>/rightButton");
    }

    private void OnEnable()
    {
        TrashCounter.Changed += OnTrashCounterChanged;

        // 按鈕事件綁定
        if (continueButton != null) continueButton.onClick.AddListener(ResumeGame);
        if (teachButton != null) teachButton.onClick.AddListener(OnTeachClicked);
        if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(OnRestartGame);

        // ✅ 兩個面板的「回到主畫面」按鈕指向同一邏輯
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
        isPaused = isTeaching = isGameOver = false;
        remainingTime = gameDuration;

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
            GameOver();
            return;
        }
        UpdateTimerText();
    }

    private void OnTrashCounterChanged(int c, int t) => RefreshTrash(c, t);

    private void RefreshTrash(int collected, int total)
    {
        if (trashCounterText != null) trashCounterText.text = $"{collected}/{total}";
        if (!isGameOver && total > 0 && collected >= total) GameOver();
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
        isPaused = true;
        pausePanel?.SetActive(true);
        pausePanel?.transform.SetAsLastSibling();
        SetGameState(false);
    }

    private void ResumeGame()
    {
        if (isGameOver) return;
        isPaused = isTeaching = false;
        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);
        SetGameState(true);
    }

    private void OnTeachClicked()
    {
        if (isGameOver) return;
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

    private void GameOver()
    {
        if (isGameOver) return;
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Why: 統一處理跳轉場景邏輯。必須強制恢復 TimeScale，否則主選單可能會因為時停導致 UI 動畫或邏輯卡死。
    private void OnReturnToStartMenu()
    {
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
}