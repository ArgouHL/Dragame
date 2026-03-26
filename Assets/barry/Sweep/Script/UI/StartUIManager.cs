using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartUIManager : MonoBehaviour
{
    [Header("=== 主選單 UI ===")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button rankingButton;
    [SerializeField] private Button pokedexButton;
    [SerializeField] private Button exitButton;

    [Header("=== 可選跳轉場景名稱 ===")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string rankingSceneName = "";
    [SerializeField] private string pokedexSceneName = "";

    private void OnEnable()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (rankingButton != null) rankingButton.onClick.AddListener(OnRankingClicked);
        if (pokedexButton != null) pokedexButton.onClick.AddListener(OnPokedexClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitGame);
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartGame);
        if (rankingButton != null) rankingButton.onClick.RemoveListener(OnRankingClicked);
        if (pokedexButton != null) pokedexButton.onClick.RemoveListener(OnPokedexClicked);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitGame);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (startPanel != null)
        {
            startPanel.SetActive(true);
        }
    }

    private void OnStartGame()
    {
        Time.timeScale = 1f;
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