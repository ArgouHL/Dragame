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


public class LevelManager : MonoBehaviour
{
    [Header("關卡設定檔")]
    [Tooltip("拖曳你要用於這一關的 PreloadConfigSO 資產")]
    [SerializeField]
    private PreloadConfigSO levelConfig;

    [Header("關卡生成設定")]
    [Tooltip("要在此關卡中固定生成的障礙物列表 (類型與位置)")]
    [SerializeField]
    private List<ObstacleSpawnData> obstacleLayout = new List<ObstacleSpawnData>();

    [Tooltip("要在此關卡中生成的垃圾列表 (類型與數量)")]
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
    [Tooltip("生成垃圾時，離障礙物的最小 X 軸安全距離 (你提到的 1,1 中的 x)")]
    [SerializeField]
    private float minSafeDistanceX = 1.0f;

    [Tooltip("生成垃圾時，離障礙物的最小 Y 軸安全距離 (你提到的 1,1 中的 y)")]
    [SerializeField]
    private float minSafeDistanceY = 1.0f;

    [Tooltip("生成位置的重試次數上限，防止因空間不足導致無限迴圈")]
    [SerializeField]
    private int maxSpawnAttempts = 20;

    private List<Vector3> spawnedObstaclePositions = new List<Vector3>();

    public enum SpawnQuadrant
    {
        TopRight,
        TopLeft,
        BottomLeft,
        BottomRight
    }

    void Start()
    {
        if (TrashPool.Instance == null || ObstaclePool.Instance == null)
        {
            Debug.LogError("LevelManager: 物件池 (TrashPool 或 ObstaclePool) 的 Singleton 實例不存在！");
            return;
        }

        if (levelConfig == null)
        {
            Debug.LogError("LevelManager: 尚未指定 levelConfig (PreloadConfigSO)！");
            return;
        }

        if (trashParentContainer == null)
        {
            trashParentContainer = new GameObject("--- Trash Container ---").transform;
        }
        if (obstacleParentContainer == null)
        {
            obstacleParentContainer = new GameObject("--- Obstacle Container ---").transform;
        }

        PreloadTrashPool();
        PreloadObstaclePool();

        SpawnLevel(this.obstacleLayout, this.trashSpawnList);
    }

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

    public void SpawnLevel(List<ObstacleSpawnData> obstaclesToSpawn, List<TrashSpawnData> trashToSpawn)
    {
        spawnedObstaclePositions.Clear();

        if (obstaclesToSpawn != null)
        {
            foreach (ObstacleSpawnData data in obstaclesToSpawn)
            {
                SpawnObstacleAtPosition(data.type, data.position);
                spawnedObstaclePositions.Add(data.position);
            }
        }
        Debug.Log($"已生成 {spawnedObstaclePositions.Count} 個障礙物。");

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

        for (int i = 0; i < totalRandomTrash; i++)
        {
            SpawnQuadrant quadrant = (SpawnQuadrant)(i % 4);

            TrashType specificType = allTrashToSpawn[i];

            int attempts = 0;
            Vector3 spawnPosition;
            bool positionFound = false;

            do
            {
                spawnPosition = GetRandomPositionInQuadrant(quadrant);
                attempts++;

                if (IsValidTrashSpawnPosition(spawnPosition))
                {
                    positionFound = true;
                    break;
                }

                if (attempts >= maxSpawnAttempts)
                {
                    Debug.LogWarning($"在象限 {quadrant} 嘗試 {maxSpawnAttempts} 次後仍無法找到 {specificType} 的安全生成點。此垃圾將不會生成。");
                    break;
                }
            }
            while (attempts < maxSpawnAttempts);

            if (positionFound)
            {
                SpawnTrashAtPosition(specificType, spawnPosition);
                trashSpawnedCount++;
            }
        }

        Debug.Log($"關卡生成完畢：總共 {totalRandomTrash} 個垃圾需求，成功生成 {trashSpawnedCount} 個。");
    }

    private bool IsValidTrashSpawnPosition(Vector3 trashPos)
    {
        foreach (Vector3 obsPos in spawnedObstaclePositions)
        {
            bool tooCloseX = Mathf.Abs(trashPos.x - obsPos.x) < minSafeDistanceX;
            bool tooCloseY = Mathf.Abs(trashPos.y - obsPos.y) < minSafeDistanceY;

            if (tooCloseX && tooCloseY)
            {
                return false;
            }
        }
        return true;
    }

    void PreloadTrashPool()
    {
        List<BaseTrash> tempTrashList = new List<BaseTrash>();
        foreach (TrashPreloadConfig config in levelConfig.trashToPreload)
        {
            for (int i = 0; i < config.amount; i++)
            {
                BaseTrash trash = TrashPool.Instance.GetTrash(config.type, Vector3.zero);
                if (trash != null)
                {
                    tempTrashList.Add(trash);
                }
            }
        }
        foreach (BaseTrash trash in tempTrashList)
        {
            TrashPool.Instance.ReturnTrash(trash);
        }
    }

    void PreloadObstaclePool()
    {
        List<BaseObstacle> tempObstacleList = new List<BaseObstacle>();
        foreach (ObstaclePreloadConfig config in levelConfig.obstaclesToPreload)
        {
            for (int i = 0; i < config.amount; i++)
            {
                BaseObstacle obstacle = ObstaclePool.Instance.GetObstacle(config.type, Vector3.zero);
                if (obstacle != null)
                {
                    tempObstacleList.Add(obstacle);
                }
            }
        }
        foreach (BaseObstacle obstacle in tempObstacleList)
        {
            ObstaclePool.Instance.ReturnObstacle(obstacle);
        }
    }

    public void SpawnTrashAtPosition(TrashType type, Vector3 position)
    {
        BaseTrash trash = TrashPool.Instance.GetTrash(type, position);
        if (trash == null)
        {
            Debug.LogWarning($"無法生成垃圾 (類型: {type})：物件池已滿或未預載。");
            return;
        }
        trash.transform.SetParent(trashParentContainer);
    }

    public void SpawnTrashInQuadrant(TrashType type, SpawnQuadrant quadrant)
    {
        Vector3 spawnPosition = GetRandomPositionInQuadrant(quadrant);
        SpawnTrashAtPosition(type, spawnPosition);
    }

    public void SpawnObstacleAtPosition(ObstacleType type, Vector3 position)
    {
        BaseObstacle obstacle = ObstaclePool.Instance.GetObstacle(type, position);
        if (obstacle == null)
        {
            Debug.LogWarning($"無法生成障礙物 (類型: {type})：物件池已滿或未預載。");
            return;
        }
        obstacle.transform.SetParent(obstacleParentContainer);
    }

    private Vector3 GetRandomPositionInQuadrant(SpawnQuadrant quadrant)
    {
        float minX = centerPoint.x - spawnAreaSize.x / 2;
        float maxX = centerPoint.x + spawnAreaSize.x / 2;
        float minY = centerPoint.y - spawnAreaSize.y / 2;
        float maxY = centerPoint.y + spawnAreaSize.y / 2;
        float midX = centerPoint.x;
        float midY = centerPoint.y;
        float x = 0;
        float y = 0;
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
        return new Vector3(x, y, 0);
    }
}