using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class StartUIManager : MonoBehaviour
{
    [Header("=== 主選單 UI ===")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button rankingButton;
    [SerializeField] private Button pokedexButton;
    [SerializeField] private Button exitButton;

    [Header("=== 影片播放設定 ===")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoPanel;

    [Header("=== 可選跳轉場景名稱 ===")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string rankingSceneName = "";
    [SerializeField] private string pokedexSceneName = "";

    private bool isPlayingVideo = false;

    private void OnEnable()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (rankingButton != null) rankingButton.onClick.AddListener(OnRankingClicked);
        if (pokedexButton != null) pokedexButton.onClick.AddListener(OnPokedexClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitGame);

        // 註冊影片播放結束的事件
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartGame);
        if (rankingButton != null) rankingButton.onClick.RemoveListener(OnRankingClicked);
        if (pokedexButton != null) pokedexButton.onClick.RemoveListener(OnPokedexClicked);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitGame);

        // 取消註冊影片播放結束的事件
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (startPanel != null) startPanel.SetActive(true);
        if (videoPanel != null) videoPanel.SetActive(false); // 確保遊戲開始時影片面板是隱藏的
    }

    private void Update()
    {
        // 偵測是否正在播放影片，且玩家按下了滑鼠左鍵
        if (isPlayingVideo && Input.GetMouseButtonDown(0))
        {
            SkipVideo();
        }
    }

    private void OnStartGame()
    {
        Time.timeScale = 1f;

        // 檢查是否有配置影片播放器
        if (videoPlayer != null && videoPanel != null)
        {
            if (startPanel != null) startPanel.SetActive(false); // 隱藏主選單
            videoPanel.SetActive(true); // 顯示影片畫布
            videoPlayer.Play();
            isPlayingVideo = true;
        }
        else
        {
            // 如果沒有配置影片，直接載入遊戲場景
            LoadGameScene();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // 影片自然播放完畢後觸發
        LoadGameScene();
    }

    private void SkipVideo()
    {
        // 玩家點擊左鍵強制中斷影片
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        LoadGameScene();
    }

    private void LoadGameScene()
    {
        isPlayingVideo = false;
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnRankingClicked()
    {
        if (string.IsNullOrWhiteSpace(rankingSceneName))
        {
            Debug.LogWarning("Ranking 按鈕已按下，但 rankingSceneName 尚未設定。");
            return;
        }

        SceneManager.LoadScene(rankingSceneName);
    }

    private void OnPokedexClicked()
    {
        if (string.IsNullOrWhiteSpace(pokedexSceneName))
        {
            Debug.LogWarning("Pokedex 按鈕已按下，但 pokedexSceneName 尚未設定。");
            return;
        }

        SceneManager.LoadScene(pokedexSceneName);
    }

    private void OnExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}