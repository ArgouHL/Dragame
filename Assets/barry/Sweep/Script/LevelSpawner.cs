using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct TrashSpawnData
{
    public TrashType type;
    public int amount;
}

[System.Serializable]
public struct ObstacleSpawnData
{
    public ObstacleType type;
    public Vector3 position;
}

public class LevelSpawner : MonoBehaviour
{
    [Header("關卡設定檔")]
    [Tooltip("拖曳你要用於這一關的 PreloadConfigSO 資產")]
    [SerializeField]
    private PreloadConfigSO levelConfig;

    [Header("關卡生成設定 (初始)")]
    [Tooltip("要在此關卡中固定生成的障礙物列表 (類型與位置)")]
    [SerializeField]
    private List<ObstacleSpawnData> obstacleLayout = new List<ObstacleSpawnData>();

    [Tooltip("要在此關卡初始時生成的垃圾列表 (類型與數量)")]
    [SerializeField]
    private List<TrashSpawnData> trashSpawnList = new List<TrashSpawnData>();

    [Header("生成範圍設定")]
    [Tooltip("生成區域的中心點 (世界座標)")]
    [SerializeField]
    private Vector2 centerPoint;

    [Tooltip("生成區域的總大小 (寬, 高)")]
    [SerializeField]
    private Vector2 spawnAreaSize;

    [Header("Runtime 階層管理 (父物件)")]
    [Tooltip("在遊戲中生成的「垃圾」要放在哪個物件底下")]
    [SerializeField]
    private Transform trashParentContainer;

    [Tooltip("在遊戲中生成的「障礙物」要放在哪個物件底下")]
    [SerializeField]
    private Transform obstacleParentContainer;

    [Header("垃圾生成安全距離")]
    [Tooltip("生成垃圾時，離其他物件的最小安全距離")]
    [SerializeField]
    public float minSafeDistance ;

    [Tooltip("生成位置的重試次數上限，防止因空間不足導致無限迴圈")]
    [SerializeField]
    private int maxSpawnAttempts = 20;

    // 記錄所有被佔用的位置 (包含障礙物與已生成的垃圾)
    private List<Vector3> allOccupiedPositions = new List<Vector3>();

    public enum SpawnQuadrant
    {
        TopRight,
        TopLeft,
        BottomLeft,
        BottomRight
    }

    private void Start()
    {
        if (TrashPool.Instance == null || ObstaclePool.Instance == null)
        {
            Debug.LogError("LevelSpawner: 物件池 (TrashPool 或 ObstaclePool) 的 Singleton 實例不存在！");
            return;
        }

        if (levelConfig == null)
        {
            Debug.LogError("LevelSpawner: 尚未指定 levelConfig (PreloadConfigSO)！");
            return;
        }

        if (trashParentContainer == null)
            trashParentContainer = new GameObject("--- Trash Container ---").transform;

        if (obstacleParentContainer == null)
            obstacleParentContainer = new GameObject("--- Obstacle Container ---").transform;

        PreloadTrashPool();
        PreloadObstaclePool();

        // 初始關卡生成
        SpawnLevel(obstacleLayout, trashSpawnList);
    }

    // ===== 對外公開的 API（給管理器用）=====

    /// <summary>
    /// 嘗試在指定象限隨機生成一個垃圾。
    /// </summary>
    public bool TrySpawnRandomTrash(TrashType type, SpawnQuadrant quadrant)
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 potentialPos = GetRandomPositionInQuadrant(quadrant);

            if (IsValidSpawnPosition(potentialPos))
            {
                SpawnTrashAtPosition(type, potentialPos);
                allOccupiedPositions.Add(potentialPos);
                return true;
            }
        }

        return false;
    }

   
    public bool TrySpawnTrashAtPosition(TrashType type, Vector3 position)
    {
        if (!IsValidSpawnPosition(position))
            return false;

        SpawnTrashAtPosition(type, position);
        allOccupiedPositions.Add(position);
        return true;
    }

    // ===== 初始關卡生成 =====

    public void SpawnLevel(List<ObstacleSpawnData> obstaclesToSpawn, List<TrashSpawnData> trashToSpawn)
    {
        allOccupiedPositions.Clear();

        // 1. 生成障礙物並記錄位置
        if (obstaclesToSpawn != null)
        {
            foreach (ObstacleSpawnData data in obstaclesToSpawn)
            {
                if (IsValidSpawnPosition(data.position))
                {
                    SpawnObstacleAtPosition(data.type, data.position);
                    allOccupiedPositions.Add(data.position);
                }
                else
                {
                    Debug.LogWarning($"障礙物位置無效或太靠近其他物件: {data.position}");
                }
            }
        }
        Debug.Log($"已生成 {obstaclesToSpawn?.Count ?? 0} 個障礙物。");

        // 2. 準備垃圾資料
        if (trashToSpawn == null || trashToSpawn.Count == 0)
        {
            Debug.LogWarning("無法生成隨機垃圾：'trashSpawnList' 未設定或為空。");
            return;
        }

        List<TrashType> allTrashToSpawn = new List<TrashType>();
        foreach (TrashSpawnData data in trashToSpawn)
        {
            for (int i = 0; i < data.amount; i++)
            {
                allTrashToSpawn.Add(data.type);
            }
        }

        Shuffle(allTrashToSpawn);

        int totalRandomTrash = allTrashToSpawn.Count;
        int trashSpawnedCount = 0;

        // 3. 平均分配象限生成
        for (int i = 0; i < totalRandomTrash; i++)
        {
            SpawnQuadrant quadrant = (SpawnQuadrant)(i % 4);
            TrashType specificType = allTrashToSpawn[i];

            bool success = TrySpawnRandomTrash(specificType, quadrant);

            if (success)
                trashSpawnedCount++;
        }

        Debug.Log($"關卡生成完畢：總共 {totalRandomTrash} 個垃圾需求，成功生成 {trashSpawnedCount} 個。");
    }

    // ===== 工具函式 (內部用) =====

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    public bool IsValidSpawnPosition(Vector3 targetPos)
    {
        foreach (Vector3 occupiedPos in allOccupiedPositions)
        {
            float distance = Vector2.Distance(
                new Vector2(targetPos.x, targetPos.y),
                new Vector2(occupiedPos.x, occupiedPos.y)
            );

            if (distance < minSafeDistance)
                return false;
        }
        return true;
    }

    private void PreloadTrashPool()
    {
        List<BaseTrash> tempTrashList = new List<BaseTrash>();
        foreach (TrashPreloadConfig config in levelConfig.trashToPreload)
        {
            for (int i = 0; i < config.amount; i++)
            {
                BaseTrash trash = TrashPool.Instance.GetTrash(config.type, Vector3.zero);
                if (trash != null)
                    tempTrashList.Add(trash);
            }
        }
        foreach (BaseTrash trash in tempTrashList)
        {
            TrashPool.Instance.ReturnTrash(trash);
        }
    }

    private void PreloadObstaclePool()
    {
        List<BaseObstacle> tempObstacleList = new List<BaseObstacle>();
        foreach (ObstaclePreloadConfig config in levelConfig.obstaclesToPreload)
        {
            for (int i = 0; i < config.amount; i++)
            {
                BaseObstacle obstacle = ObstaclePool.Instance.GetObstacle(config.type, Vector3.zero);
                if (obstacle != null)
                    tempObstacleList.Add(obstacle);
            }
        }
        foreach (BaseObstacle obstacle in tempObstacleList)
        {
            ObstaclePool.Instance.ReturnObstacle(obstacle);
        }
    }

    private void SpawnTrashAtPosition(TrashType type, Vector3 position)
    {
        BaseTrash trash = TrashPool.Instance.GetTrash(type, position);
        if (trash == null)
        {
            Debug.LogWarning($"無法生成垃圾 (類型: {type})：物件池已滿或未預載。");
            return;
        }
        trash.transform.SetParent(trashParentContainer);
    }

    private void SpawnObstacleAtPosition(ObstacleType type, Vector3 position)
    {
        BaseObstacle obstacle = ObstaclePool.Instance.GetObstacle(type, position);
        if (obstacle == null)
        {
            Debug.LogWarning($"無法生成障礙物 (類型: {type})：物件池已滿或未預載。");
            return;
        }
        obstacle.transform.SetParent(obstacleParentContainer);
    }

    public Vector3 GetRandomPositionInQuadrant(SpawnQuadrant quadrant)
    {
        float minX;
        float maxX;
        float minY;
        float maxY;

        // 優先使用 WorldBounds2D 的邊界
        if (WorldBounds2D.Instance != null)
        {
            Rect worldRect = WorldBounds2D.Instance.GetWorldRect();
            minX = worldRect.xMin;
            maxX = worldRect.xMax;
            minY = worldRect.yMin;
            maxY = worldRect.yMax;
        }
        else
        {
            // 沒有 WorldBounds2D 的情況就用原本的設定 (保險用)
            minX = centerPoint.x - spawnAreaSize.x / 2f;
            maxX = centerPoint.x + spawnAreaSize.x / 2f;
            minY = centerPoint.y - spawnAreaSize.y / 2f;
            maxY = centerPoint.y + spawnAreaSize.y / 2f;
        }

        float midX = (minX + maxX) * 0.5f;
        float midY = (minY + maxY) * 0.5f;

        float x = 0f;
        float y = 0f;

        switch (quadrant)
        {
            case SpawnQuadrant.TopRight:
                x = Random.Range(midX, maxX);
                y = Random.Range(midY, maxY);
                break;
            case SpawnQuadrant.TopLeft:
                x = Random.Range(minX, midX);
                y = Random.Range(midY, maxY);
                break;
            case SpawnQuadrant.BottomLeft:
                x = Random.Range(minX, midX);
                y = Random.Range(minY, midY);
                break;
            case SpawnQuadrant.BottomRight:
                x = Random.Range(midX, maxX);
                y = Random.Range(minY, midY);
                break;
        }

        return new Vector3(x, y, 0f);
    }
}
