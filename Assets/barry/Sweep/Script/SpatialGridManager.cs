using UnityEngine;
using System.Collections.Generic;

public class SpatialGridManager : MonoBehaviour
{
    public static SpatialGridManager Instance { get; private set; }

    [Header("網格設定")]
    public float cellSize = 2.0f;
    private List<BaseTrash> _cachedNeighbors = new List<BaseTrash>();
    private Dictionary<Vector2Int, List<BaseTrash>> grid = new Dictionary<Vector2Int, List<BaseTrash>>();
    
    public void UpdateGrid(List<BaseTrash> activeTrashList)
    {
        // 1. 先清空每個格子的 List，而不是 new 新 List
        foreach (var kv in grid)
        {
            kv.Value.Clear();
        }

        // 2. 將垃圾重新放入對應格子
        foreach (var trash in activeTrashList)
        {
            if (trash.IsAbsorbing) continue;

            Vector2Int cellPos = GetCellPos(trash.transform.position);

            if (!grid.TryGetValue(cellPos, out var cellList))
            {
                // 沒有就 new 一次，之後會重用
                cellList = new List<BaseTrash>();
                grid[cellPos] = cellList;
            }

            cellList.Add(trash);
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public List<BaseTrash> GetNearbyTrash(BaseTrash trash)
    {
        return GetTrashAroundPosition(trash.transform.position);
    }

    public List<BaseTrash> GetTrashAroundPosition(Vector3 position)
    {
        _cachedNeighbors.Clear();
        Vector2Int centerCell = GetCellPos(position);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int checkPos = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (grid.TryGetValue(checkPos, out List<BaseTrash> cellContent))
                {
                    _cachedNeighbors.AddRange(cellContent);
                }
            }
        }
        return _cachedNeighbors;
    }

    private Vector2Int GetCellPos(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize)
        );
    }
}
