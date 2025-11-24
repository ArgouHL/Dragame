using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DynamicSpawnGroup
{
   

    [Tooltip("這一組每幾秒生成一次")]
    public float spawnInterval ;

    [Tooltip("從這一組裡的垃圾隨機挑一個生成")]
    public List<TrashType> trashTypes = new List<TrashType>();

    [Tooltip("是否啟用這一組動態生成")]
    public bool enable ;

    [HideInInspector] public float timer;   // runtime 用，不顯示在 Inspector
}

public class DynamicSpawnManager : MonoBehaviour
{
    [Header("動態生成總開關")]
    [SerializeField] private bool enableDynamicSpawn;

    [Header("動態生成組 (一組 = 多種垃圾 + 間隔時間)")]
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
            // 這組關閉或沒東西就跳過
            if (!group.enable) continue;
            if (group.trashTypes == null || group.trashTypes.Count == 0) continue;

            group.timer += Time.deltaTime;

            if (group.timer >= group.spawnInterval)
            {
                group.timer = 0f;
                TrySpawnFromGroup(group);
            }
        }
    }

    private void TrySpawnFromGroup(DynamicSpawnGroup group)
    {
        // 從這一組裡隨機挑一種垃圾
        TrashType randomType =
            group.trashTypes[Random.Range(0, group.trashTypes.Count)];

        // 隨機選一個象限
        LevelSpawner.SpawnQuadrant randomQuadrant =
            (LevelSpawner.SpawnQuadrant)Random.Range(0, 4);

        bool success = spawner.TrySpawnRandomTrash(randomType, randomQuadrant);

        
    }
}
