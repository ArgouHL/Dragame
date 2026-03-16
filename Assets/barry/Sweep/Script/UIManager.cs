using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("=== 垃圾計數 UI ===")]
    [SerializeField] private TMP_Text trashCounterText;

    [Header("=== 技能圖示 UI ===")]
    [SerializeField] private Image skill1Icon;
    [SerializeField] private Image skill2Icon;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.3f;

    [Header("=== 遊戲流程 UI ===")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject restartPanel;
    [SerializeField] private Button restartButton;

    // [重點註釋] 新增狀態鎖，避免切換場景時因為讀取到未清除的 static 變數（TrashCounter）而瞬間誤觸發通關
    private bool isPlaying = false;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartGame);
    }

    private void OnEnable()
    {
        TrashCounter.Changed += OnTrashCounterChanged;
    }

    private void OnDisable()
    {
        TrashCounter.Changed -= OnTrashCounterChanged;

        if (PlayerController.instance != null)
        {
            PlayerController.instance.OnModeChanged -= OnSkillModeChanged;
        }

        if (startButton != null) startButton.onClick.RemoveListener(OnStartGame);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartGame);
    }

    private void Start()
    {
        // 初始化狀態：遊戲尚未開始
        isPlaying = false;

        // 確保剛載入場景時，只顯示開始畫面
        if (startPanel != null) startPanel.SetActive(true);
        if (startButton != null) startButton.gameObject.SetActive(true);
        if (restartPanel != null) restartPanel.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);

        RefreshTrash(TrashCounter.Collected, TrashCounter.Total);

        if (PlayerController.instance != null)
        {
            PlayerController.instance.OnModeChanged += OnSkillModeChanged;
            OnSkillModeChanged(PlayerController.instance.currentMode);
        }
    }

    private void OnTrashCounterChanged(int collected, int total)
    {
        RefreshTrash(collected, total);
    }

    private void RefreshTrash(int collected, int total)
    {
        if (trashCounterText != null)
        {
            trashCounterText.text = $"{collected}/{total}";
        }

        // [重點註釋] 只有在「遊戲進行中 (isPlaying)」狀態下，才允許觸發通關邏輯
        if (isPlaying && total > 0 && collected >= total)
        {
            isPlaying = false; // 觸發後立刻上鎖，避免重複執行

            if (restartPanel != null) restartPanel.SetActive(true);
            if (restartButton != null) restartButton.gameObject.SetActive(true);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void OnSkillModeChanged(BroomMode mode)
    {
        SetImageAlpha(skill1Icon, mode == BroomMode.Impact ? 1f : inactiveAlpha);
        SetImageAlpha(skill2Icon, mode == BroomMode.Sticky ? 1f : inactiveAlpha);
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var color = img.color;
        color.a = alpha;
        img.color = color;
    }

    private void OnStartGame()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);

        // [重點註釋] 按下開始後，正式進入遊玩狀態
        isPlaying = true;

        // 主動檢查一次是否已經達標（防呆）
        RefreshTrash(TrashCounter.Collected, TrashCounter.Total);
    }

    private void OnRestartGame()
    {
        if (restartPanel != null) restartPanel.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}