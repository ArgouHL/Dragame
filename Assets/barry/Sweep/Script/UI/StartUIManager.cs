using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class StartUIManager : MonoBehaviour
{
    [Header("=== 主選單 UI ===")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button teachButton;
    [SerializeField] private Button exitButton;

    [Header("=== 說明面板 UI ===")]
    [SerializeField] private GameObject teachPanel;

    [Header("=== 影片播放設定 ===")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoPanel;

    [Header("=== 場景名稱 ===")]
    [SerializeField] private string gameSceneName = "GameScene";

    private bool isPlayingVideo = false;
    private bool isShowingTeachFromMenu = false;

    private void OnEnable()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (teachButton != null) teachButton.onClick.AddListener(OnTeachClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitGame);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartGame);
        if (teachButton != null) teachButton.onClick.RemoveListener(OnTeachClicked);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitGame);

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
        if (videoPanel != null) videoPanel.SetActive(false);
        if (teachPanel != null) teachPanel.SetActive(false);
    }

    private void Update()
    {
        if (isPlayingVideo && Input.GetMouseButtonDown(0))
        {
            SkipVideo();
        }
        else if (isShowingTeachFromMenu && Input.GetMouseButtonDown(0))
        {
            CloseMenuTeachPanel();
        }
    }

    private void OnStartGame()
    {
        if (startPanel != null) startPanel.SetActive(false);

        if (videoPlayer != null && videoPanel != null)
        {
            videoPanel.SetActive(true);
            videoPlayer.Play();
            isPlayingVideo = true;
        }
        else
        {
            LoadGameScene();
        }
    }

    private void OnVideoFinished(VideoPlayer vp) => LoadGameScene();

    private void SkipVideo()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        LoadGameScene();
    }

    private void LoadGameScene()
    {
        isPlayingVideo = false;
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnTeachClicked()
    {
        if (teachPanel != null)
        {
            teachPanel.SetActive(true);
            teachPanel.transform.SetAsLastSibling();
            // 使用協程延遲一幀，避免點擊按鈕的當下立即觸發 Update 裡的關閉邏輯
            StartCoroutine(EnableMenuTeachClosure());
        }
    }

    private IEnumerator EnableMenuTeachClosure()
    {
        yield return null;
        isShowingTeachFromMenu = true;
    }

    private void CloseMenuTeachPanel()
    {
        isShowingTeachFromMenu = false;
        if (teachPanel != null) teachPanel.SetActive(false);
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