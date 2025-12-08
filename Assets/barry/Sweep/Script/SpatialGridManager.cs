using System.Collections.Generic;
using UnityEngine;
public class SpatialGridManager : MonoBehaviour
{
    public static SpatialGridManager Instance { get; private set; }

    [Header("網格設定")]
    public float cellSize = 2.0f;

    private readonly Dictionary<Vector2Int, List<BaseTrash>> grid = new Dictionary<Vector2Int, List<BaseTrash>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateGrid(List<BaseTrash> activeTrashList)
    {
        foreach (var kv in grid) kv.Value.Clear();

        for (int i = 0; i < activeTrashList.Count; i++)
        {
            var trash = activeTrashList[i];
            if (trash == null || trash.IsAbsorbing) continue;

            Vector2Int cellPos = GetCellPos(trash.transform.position);
            if (!grid.TryGetValue(cellPos, out var cellList))
            {
                cellList = new List<BaseTrash>();
                grid[cellPos] = cellList;
            }
            cellList.Add(trash);
        }
    }

    public void GetNearbyTrash(BaseTrash trash, List<BaseTrash> buffer)
    {
        if (trash == null) return;
        GetTrashAroundPosition(trash.transform.position, buffer);
    }

    public void GetTrashAroundPosition(Vector3 position, List<BaseTrash> buffer)
    {
        buffer.Clear();
        Vector2Int centerCell = GetCellPos(position);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int checkPos = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (grid.TryGetValue(checkPos, out var cellContent) && cellContent.Count > 0)
                {
                    buffer.AddRange(cellContent);
                }
            }
        }
    }

    private Vector2Int GetCellPos(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize)
        );
    }
}