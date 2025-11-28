using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DynamicSpawnGroup
{
    [Tooltip("這一組每幾秒生成一次")]
    public float spawnInterval = 2f;

    [Tooltip("一次生成多少個垃圾")]
    public int spawnAmount = 1;

    [Tooltip("從這一組裡的垃圾隨機挑一個生成")]
    public List<TrashType> trashTypes = new List<TrashType>();

    [Tooltip("是否啟用這一組動態生成")]
    public bool enable = true;

    [HideInInspector]
    public float timer;
}

public class DynamicSpawnManager : MonoBehaviour
{
    [Header("動態生成總開關")]
    [SerializeField] private bool enableDynamicSpawn = true;

    [Header("動態生成組 (一組 = 多種垃圾 + 間隔時間 + 數量)")]
    [SerializeField] private List<DynamicSpawnGroup> spawnGroups = new List<DynamicSpawnGroup>();

    [Header("依賴的生成器")]
    [SerializeField] private LevelSpawner spawner;

    private void Awake()
    {
        if (spawner == null)
            spawner = GetComponent<LevelSpawner>();
    }

    private void Update()
    {
        if (!enableDynamicSpawn) return;
        if (spawner == null) return;

        foreach (var group in spawnGroups)
        {
            if (!group.enable) continue;
            if (group.trashTypes == null || group.trashTypes.Count == 0) continue;
            if (group.spawnInterval <= 0f) continue;

            group.timer += Time.deltaTime;

            if (group.timer >= group.spawnInterval)
            {
                group.timer = 0f;
                SpawnGroupTrash(group);
            }
        }
    }

    /// <summary>
    /// 一次生成一群垃圾：
    /// - 同一群靠近一個中心點（集合感）
    /// - 仍然用 LevelSpawner 的安全距離檢查
    /// </summary>
    private void SpawnGroupTrash(DynamicSpawnGroup group)
    {
        int amount = Mathf.Max(1, group.spawnAmount);

        // 隨機選一個象限
        LevelSpawner.SpawnQuadrant randomQuadrant =
            (LevelSpawner.SpawnQuadrant)Random.Range(0, 4);

        // 這一群的中心點
        Vector3 centerPos = spawner.GetRandomPositionInQuadrant(randomQuadrant);

        // 群聚半徑：略小於 minSafeDistance，讓它們靠近但不重疊
        float clusterRadius = spawner.minSafeDistance * 0.9f;
        if (clusterRadius <= 0f)
            clusterRadius = 0.5f;

        for (int i = 0; i < amount; i++)
        {
            TrashType type = group.trashTypes[Random.Range(0, group.trashTypes.Count)];

            bool spawned = false;

            // 先嘗試在「群聚半徑」內找合法位置
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                Vector3 candidate = centerPos + new Vector3(offset.x, offset.y, 0f);

                if (spawner.IsValidSpawnPosition(candidate))
                {
                    spawner.TrySpawnTrashAtPosition(type, candidate);
                    spawned = true;
                    break;
                }
            }

            // 若附近塞不下，就退回原本的隨機象限生成
            if (!spawned)
            {
                spawner.TrySpawnRandomTrash(type, randomQuadrant);
            }
        }
    }
}
