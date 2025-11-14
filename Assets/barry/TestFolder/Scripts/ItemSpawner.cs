using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private GameObject hintCirclePrefab;
    [SerializeField] private GameObject biggerHintCirclePrefab;
    [SerializeField] private Vector2 spawnAreaMin ;
    [SerializeField] private Vector2 spawnAreaMax ;
    [SerializeField] private float spawnIntervalDroplet; // 每幾秒生成一次
    [SerializeField] private float spawnIntervalRock; 
    private void Start()
    {
        InvokeRepeating(nameof(SpawnHintCircle), 0f, spawnIntervalDroplet);
        InvokeRepeating(nameof(SpawnBiggerHintCircle), 5f, spawnIntervalRock);
    }

    private void SpawnHintCircle()
    {
        // 在範圍內隨機座標
        Vector2 randomPos = new Vector2(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y)
        );

        Instantiate(hintCirclePrefab, randomPos, Quaternion.identity);
    }
    private void SpawnBiggerHintCircle()
    {
        // 在範圍內隨機座標
        Vector2 randomPos = new Vector2(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y)
        );

        Instantiate(biggerHintCirclePrefab, randomPos, Quaternion.identity);
    }
}