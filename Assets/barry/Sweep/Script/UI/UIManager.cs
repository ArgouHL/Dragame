using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [System.Serializable]
    public struct GradeThreshold
    {
        [Tooltip("達到此評級所需的最低分數")]
        public int minScore;
        public Sprite gradeSprite;
    }

    [Header("=== 調試設置 ===")]
    [SerializeField, Tooltip("開啟以在主控台追蹤介面與狀態切換")]
    private bool showDebugLogs = true;

    [Header("=== 游標設置 (UI 軟體游標) ===")]
    [SerializeField, Tooltip("將畫布上代表游標的 UI 圖片 (RectTransform) 拖入此處。務必關閉該 Image 的 Raycast Target！")]
    private RectTransform customCursorUI;
    [SerializeField, Tooltip("點擊時向左旋轉的角度 (Z軸正向)")]
    private float clickRotationAngle = 15f;
    [SerializeField, Tooltip("旋轉動效演出時間")]
    private float clickRotationDuration = 0.15f;

    [Header("=== 分數 UI ===")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField, Tooltip("加分時放大的最大倍率")] private float punchScaleMultiplier = 1.5f;
    [SerializeField, Tooltip("動效演出時間")] private float punchDuration = 0.2f;
    [SerializeField, Tooltip("加分瞬間的高亮顏色")] private Color punchColor = new Color(1f, 0.8f, 0f, 1f);

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
    [SerializeField, Tooltip("冷卻時的圖標顏色")] private Color onCooldownColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("=== 黑洞等級 UI ===")]
    [SerializeField] private Image blackHoleLevelIcon;
    [SerializeField, Tooltip("依序放入各等級的圖片")] private Sprite[] levelSprites;

    [Header("=== 暫停選單 UI ===")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button teachButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseToStartButton;

    [Header("=== 教學 UI ===")]
    [SerializeField, Tooltip("將帶有圖片與按鈕元件的教學面板拖入")]
    private GameObject teachPanel;
    [SerializeField, Tooltip("依序放入教學圖片，將隨點擊切換")]
    private Sprite[] teachSprites;

    [Header("=== 結束 UI ===")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endScoreText;
    [SerializeField] private Button endRestartButton;
    [SerializeField] private Button endToStartButton;

    [Header("=== 結算評級 UI ===")]
    [SerializeField] private Image endGradeImage;
    [SerializeField] private GradeThreshold[] gradeSettings;

    [Header("=== 輸入綁定 ===")]
    [SerializeField] private InputAction pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");

    private bool isPaused;
    private bool isTeaching;
    private bool isGameOver;
    private float remainingTime;
    private int lastDisplaySeconds = -1;
    private int currentScore;
    private int currentTeachIndex;
    private bool blockInputThisFrame = false;

    private Image _teachImage;
    private Vector3 _originalScoreScale;
    private Color _originalScoreColor;
    private Coroutine _scorePunchRoutine;
    private Coroutine _cursorPunchRoutine;
    private PlayerController _cachedPlayerController;

    private void Awake()
    {
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0, 1, 0);

        if (teachPanel != null)
        {
            _teachImage = teachPanel.GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        TrashCounter.Changed += OnTrashCounterChanged;
        BlackHoleObstacle.OnTrashAbsorbedScore += AddScore;
        PetAI.OnVomitPenalty += AddScore;
        PetAI.OnPetLevelChanged += UpdateBlackHoleLevelUI;

        continueButton?.onClick.AddListener(ResumeGame);
        teachButton?.onClick.AddListener(OnTeachClicked);
        pauseRestartButton?.onClick.AddListener(OnRestartGame);
        pauseToStartButton?.onClick.AddListener(OnReturnToStartMenu);
        endRestartButton?.onClick.AddListener(OnRestartGame);
        endToStartButton?.onClick.AddListener(OnReturnToStartMenu);

        pauseAction.Enable();
        pauseAction.performed += OnPauseActionTriggered;
    }

    private void OnDisable()
    {
        TrashCounter.Changed -= OnTrashCounterChanged;
        BlackHoleObstacle.OnTrashAbsorbedScore -= AddScore;
        PetAI.OnVomitPenalty -= AddScore;
        PetAI.OnPetLevelChanged -= UpdateBlackHoleLevelUI;

        continueButton?.onClick.RemoveListener(ResumeGame);
        teachButton?.onClick.RemoveListener(OnTeachClicked);
        pauseRestartButton?.onClick.RemoveListener(OnRestartGame);
        pauseToStartButton?.onClick.RemoveListener(OnReturnToStartMenu);
        endRestartButton?.onClick.RemoveListener(OnRestartGame);
        endToStartButton?.onClick.RemoveListener(OnReturnToStartMenu);

        pauseAction.Disable();
    }

    private void Start()
    {
        Log("初始化介面系統，啟動狀態設定。");

        // 若啟用自定義 UI 游標，隱藏原生系統游標
        if (customCursorUI != null)
        {
            Cursor.visible = false;
        }

        isGameOver = false;
        isPaused = false;
        remainingTime = gameDuration;
        currentScore = 0;

        if (scoreText != null)
        {
            _originalScoreScale = scoreText.transform.localScale;
            _originalScoreColor = scoreText.color;
        }

        UpdateScoreText(false);
        pausePanel?.SetActive(false);
        endPanel?.SetActive(false);

        OpenTeachPanel();
        UpdateTimerText();
        RefreshTrash(TrashCounter.Collected, TrashCounter.Total);

        _cachedPlayerController = PlayerController.instance;
        if (_cachedPlayerController != null)
        {
            _cachedPlayerController.OnModeChanged += OnSkillModeChanged;
            _cachedPlayerController.OnRightSkillCooldownUpdate += UpdateRightSkillCooldownUI;

            OnSkillModeChanged(_cachedPlayerController.currentMode);
            UpdateRightSkillCooldownUI(0f, 1f);
        }

        SetGameState(false);
    }

    private void Update()
    {
        // 處理自定義 UI 游標的跟隨與點擊動畫
        if (customCursorUI != null)
        {
            customCursorUI.position = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                TriggerCursorClickAnim();
            }
        }

        if (Input.GetMouseButtonDown(0) && isTeaching)
        {
            if (!blockInputThisFrame) AdvanceTeachImage();
        }

        if (blockInputThisFrame) blockInputThisFrame = false;

        if (isPaused || isTeaching || isGameOver) return;

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

    private void TriggerCursorClickAnim()
    {
        if (_cursorPunchRoutine != null) StopCoroutine(_cursorPunchRoutine);
        _cursorPunchRoutine = StartCoroutine(CursorPunchCoroutine());
    }

    // 將 UI 游標向左旋轉再回正，使用 unscaledDeltaTime 確保暫停時仍有動畫
    private IEnumerator CursorPunchCoroutine()
    {
        float halfDuration = clickRotationDuration * 0.5f;
        float elapsed = 0f;
        Quaternion targetRot = Quaternion.Euler(0, 0, clickRotationAngle);

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            customCursorUI.rotation = Quaternion.Slerp(Quaternion.identity, targetRot, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            customCursorUI.rotation = Quaternion.Slerp(targetRot, Quaternion.identity, t);
            yield return null;
        }

        customCursorUI.rotation = Quaternion.identity;
        _cursorPunchRoutine = null;
    }

    private void AddScore(int scoreToAdd)
    {
        currentScore += scoreToAdd;
        UpdateScoreText(true);
    }

    private void UpdateScoreText(bool playAnimation)
    {
        if (scoreText == null) return;

        scoreText.text = currentScore.ToString();
        if (playAnimation) TriggerScorePunchAnim();
    }

    private void TriggerScorePunchAnim()
    {
        if (_scorePunchRoutine != null) StopCoroutine(_scorePunchRoutine);
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
            float easeT = t * t * (3f - 2f * t);

            scoreText.transform.localScale = Vector3.Lerp(_originalScoreScale, targetScale, easeT);
            scoreText.color = Color.Lerp(_originalScoreColor, punchColor, easeT);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            float easeT = t * t * (3f - 2f * t);

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

        int displaySeconds = Mathf.CeilToInt(remainingTime);
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
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private void UpdateRightSkillCooldownUI(float currentCooldown, float maxCooldown)
    {
        if (rightSkillIcon == null) return;

        bool isOnCooldown = currentCooldown > 0f;
        rightSkillIcon.color = isOnCooldown ? onCooldownColor : Color.white;

        if (rightSkillCooldownText != null)
        {
            rightSkillCooldownText.gameObject.SetActive(isOnCooldown);
            if (isOnCooldown)
            {
                rightSkillCooldownText.text = Mathf.CeilToInt(currentCooldown).ToString();
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
        if (isTeaching)
        {
            CloseTeachPanel();
            return;
        }
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    private void PauseGame()
    {
        if (isGameOver) return;
        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
        }
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
        OpenTeachPanel();
    }

    private void OpenTeachPanel()
    {
        isTeaching = true;
        currentTeachIndex = 0;
        blockInputThisFrame = true;
        pausePanel?.SetActive(false);

        if (teachPanel != null)
        {
            teachPanel.SetActive(true);
            teachPanel.transform.SetAsLastSibling();
        }

        UpdateTeachDisplay();
    }

    private void AdvanceTeachImage()
    {
        if (!isTeaching) return;

        currentTeachIndex++;

        if (teachSprites != null && currentTeachIndex < teachSprites.Length)
        {
            UpdateTeachDisplay();
        }
        else
        {
            Log("教學圖片播放完畢，關閉面板。");
            CloseTeachPanel();
        }
    }

    private void UpdateTeachDisplay()
    {
        if (_teachImage != null && teachSprites != null && currentTeachIndex < teachSprites.Length)
        {
            _teachImage.sprite = teachSprites[currentTeachIndex];
        }
    }

    private void CloseTeachPanel()
    {
        if (isGameOver) return;
        isTeaching = false;
        teachPanel?.SetActive(false);

        if (isPaused)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                pausePanel.transform.SetAsLastSibling();
            }
        }
        else
        {
            ResumeGame();
        }
    }

    private void GameOver(string reason)
    {
        if (isGameOver) return;
        Log($"遊戲結束，原因: {reason}");
        isGameOver = true;
        isPaused = isTeaching = false;

        pausePanel?.SetActive(false);
        teachPanel?.SetActive(false);

        if (endScoreText != null) endScoreText.text = currentScore.ToString();

        if (endGradeImage != null && gradeSettings != null && gradeSettings.Length > 0)
        {
            Sprite finalGrade = null;
            int maxValidScore = -1;

            for (int i = 0; i < gradeSettings.Length; i++)
            {
                if (currentScore >= gradeSettings[i].minScore && gradeSettings[i].minScore > maxValidScore)
                {
                    maxValidScore = gradeSettings[i].minScore;
                    finalGrade = gradeSettings[i].gradeSprite;
                }
            }

            endGradeImage.gameObject.SetActive(finalGrade != null);
            if (finalGrade != null) endGradeImage.sprite = finalGrade;
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnReturnToStartMenu()
    {
        Time.timeScale = 1f;
        // 離開場景時恢復系統原生游標
        Cursor.visible = true;
        SceneManager.LoadScene("StartMenu");
    }

    private void SetGameState(bool isPlayingGame)
    {
        Time.timeScale = isPlayingGame ? 1f : 0f;
        if (PlayerController.instance != null) PlayerController.instance.enabled = isPlayingGame;

        Cursor.lockState = CursorLockMode.None;
        // 若使用了自定義 UI 游標，則保持原生游標隱藏；反之才顯示原生游標
        Cursor.visible = customCursorUI == null;
    }

    private void OnDestroy()
    {
        if (_cachedPlayerController != null)
        {
            _cachedPlayerController.OnModeChanged -= OnSkillModeChanged;
            _cachedPlayerController.OnRightSkillCooldownUpdate -= UpdateRightSkillCooldownUI;
        }
    }

    private void Log(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugLogs) Debug.Log($"[UIManager] {message}");
#endif
    }
}