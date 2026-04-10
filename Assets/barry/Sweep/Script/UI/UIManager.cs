using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("=== 調試設置 (Debug) ===")]
    [SerializeField, Tooltip("開啟以在 Console 追蹤 UI 與遊戲狀態切換")]
    private bool showDebugLogs = true;

    [Header("=== 分數 UI ===")]
    [SerializeField] private TMP_Text scoreText;

    [Header("=== 分數動態視覺 (Juice) ===")]
    [SerializeField, Tooltip("加分時放大的最大倍率")]
    private float punchScaleMultiplier = 1.5f;
    [SerializeField, Tooltip("動效演出時間(秒)")]
    private float punchDuration = 0.2f;
    [SerializeField, Tooltip("加分瞬間的高亮顏色")]
    private Color punchColor = new Color(1f, 0.8f, 0f, 1f);

    [Header("=== 垃圾計數 UI ===")]
    [SerializeField] private TMP_Text trashCounterText;

    [Header("=== 倒數計時 UI ===")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField, Min(1f)] private float gameDuration = 90f;

    [Header("=== 技能圖示 UI ===")]
    [SerializeField] private Image skill1Icon;
    [SerializeField] private Image skill2Icon;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.3f;

    [Header("=== 右鍵技能 UI ===")]
    [SerializeField] private Image rightSkillIcon;
    [SerializeField] private TMP_Text rightSkillCooldownText;
    [SerializeField, Tooltip("冷卻時的圖標顏色(模擬黑白/暗化)")]
    private Color onCooldownColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("=== 黑洞等級 UI ===")]
    [SerializeField] private Image blackHoleLevelIcon;
    [SerializeField, Tooltip("請依序放入LV1~LV5的圖片 (Index 0 = LV1)")]
    private Sprite[] levelSprites;

    [Header("=== 暫停選單 UI ===")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button teachButton;
    [SerializeField] private GameObject teachPanel;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseToStartButton;

    [Header("=== 結束 UI ===")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endScoreText; // 新增這行來綁定結算分數文字
    [SerializeField] private Button endToStartButton;

    [Header("=== 輸入綁定 ===")]
    [SerializeField] private InputAction pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");

    private InputAction closeTeachAction;
    private bool isPaused;
    private bool isTeaching;
    private bool isGameOver;
    private float remainingTime;
    private int lastDisplaySeconds = -1;

    // 記錄當前總分與動效狀態
    private int currentScore;
    private Vector3 _originalScoreScale;
    private Color _originalScoreColor;
    private Coroutine _scorePunchRoutine;

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

        BlackHoleObstacle.OnTrashAbsorbedScore += AddScore;
        PetAI.OnVomitPenalty += AddScore;
        PetAI.OnPetLevelChanged += UpdateBlackHoleLevelUI;

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

        BlackHoleObstacle.OnTrashAbsorbedScore -= AddScore;
        PetAI.OnVomitPenalty -= AddScore;
        PetAI.OnPetLevelChanged -= UpdateBlackHoleLevelUI;

        if (PlayerController.instance != null)
        {
            PlayerController.instance.OnModeChanged -= OnSkillModeChanged;
            PlayerController.instance.OnRightSkillCooldownUpdate -= UpdateRightSkillCooldownUI;
        }

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

        if (scoreText != null)
        {
            _originalScoreScale = scoreText.transform.localScale;
            _originalScoreColor = scoreText.color;
        }

        currentScore = 0;
        UpdateScoreText(false);

        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);
        endPanel?.SetActive(false);

        UpdateTimerText();
        RefreshTrash(TrashCounter.Collected, TrashCounter.Total);

        if (PlayerController.instance != null)
        {
            PlayerController.instance.OnModeChanged += OnSkillModeChanged;
            OnSkillModeChanged(PlayerController.instance.currentMode);

            // 訂閱冷卻事件並手動刷新一次初始狀態
            PlayerController.instance.OnRightSkillCooldownUpdate += UpdateRightSkillCooldownUI;
            UpdateRightSkillCooldownUI(0f, 1f);
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

    private void AddScore(int scoreToAdd)
    {
        currentScore += scoreToAdd;
        UpdateScoreText(true);
    }

    private void UpdateScoreText(bool playAnimation)
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();

            if (playAnimation)
            {
                TriggerScorePunchAnim();
            }
        }
    }

    private void TriggerScorePunchAnim()
    {
        if (_scorePunchRoutine != null)
        {
            StopCoroutine(_scorePunchRoutine);
        }
        _scorePunchRoutine = StartCoroutine(ScorePunchCoroutine());
    }

    private IEnumerator ScorePunchCoroutine()
    {
        float halfDuration = punchDuration * 0.5f;
        float elapsed = 0f;

        Vector3 targetScale = _originalScoreScale * punchScaleMultiplier;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            float easeT = t * (2f - t);

            scoreText.transform.localScale = Vector3.Lerp(_originalScoreScale, targetScale, easeT);
            scoreText.color = Color.Lerp(_originalScoreColor, punchColor, easeT);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            float easeT = t * t;

            scoreText.transform.localScale = Vector3.Lerp(targetScale, _originalScoreScale, easeT);
            scoreText.color = Color.Lerp(punchColor, _originalScoreColor, easeT);
            yield return null;
        }

        scoreText.transform.localScale = _originalScoreScale;
        scoreText.color = _originalScoreColor;
        _scorePunchRoutine = null;
    }

    private void OnTrashCounterChanged(int c, int t) => RefreshTrash(c, t);

    private void RefreshTrash(int collected, int total)
    {
        if (trashCounterText != null) trashCounterText.text = $"{collected}/{total}";
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

    // [重點註釋] 接收冷卻廣播，更新圖標顏色與文字狀態
    private void UpdateRightSkillCooldownUI(float currentCooldown, float maxCooldown)
    {
        if (rightSkillIcon == null) return;

        if (currentCooldown > 0f)
        {
            rightSkillIcon.color = onCooldownColor;
            if (rightSkillCooldownText != null)
            {
                rightSkillCooldownText.gameObject.SetActive(true);
                // 使用 CeilToInt，這樣 4.2 秒會顯示 5，0.1 秒會顯示 1，符合玩家直覺
                rightSkillCooldownText.text = Mathf.CeilToInt(currentCooldown).ToString();
            }
        }
        else
        {
            rightSkillIcon.color = Color.white;
            if (rightSkillCooldownText != null)
            {
                rightSkillCooldownText.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateBlackHoleLevelUI(int petLevel)
    {
        if (blackHoleLevelIcon == null || levelSprites == null || levelSprites.Length == 0) return;

        int safeIndex = Mathf.Clamp(petLevel, 0, levelSprites.Length - 1);
        blackHoleLevelIcon.sprite = levelSprites[safeIndex];
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

        // 新增這區塊：更新結算畫面的分數
        if (endScoreText != null)
        {
            endScoreText.text = currentScore.ToString();
        }

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