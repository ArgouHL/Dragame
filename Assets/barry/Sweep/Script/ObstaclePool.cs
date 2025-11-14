using System.Collections.Generic;
using UnityEngine;

public enum ObstacleType
{
    Blackhole
}


public class ObstaclePool : BasePool<ObstacleType, BaseObstacle>
{
    public static ObstaclePool Instance { get; private set; }

    [SerializeField]
    private List<ObstaclePoolEntry> obstacleEntries;

    protected virtual void Awake()
    {
        // 設置 Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化池子
        InitializePool(obstacleEntries);
    }

    /// <summary>
    /// 從池子獲取障礙物，並設定位置與重置狀態
    /// </summary>
    public BaseObstacle GetObstacle(ObstacleType type, Vector3 position)
    {
        BaseObstacle obstacle = Get(type);

        if (obstacle != null)
        {
            obstacle.transform.position = position;
            obstacle.ResetState();
            obstacle.gameObject.SetActive(true);
            return obstacle;
        }

        return obstacle;
    }

    /// <summary>
    /// 歸還障礙物到池子
    /// </summary>
    public void ReturnObstacle(BaseObstacle obstacle)
    {
        if (obstacle == null) return;

        Return(obstacle.obstacleType, obstacle);
    }
    [System.Serializable]
    public class ObstaclePoolEntry : BasePoolEntry<ObstacleType, BaseObstacle> { }

}
