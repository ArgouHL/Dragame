using System.Collections.Generic;
using UnityEngine;

// 垃圾類型
public enum TrashType
{
    Banana,
    Can,
    Paper,
    // 可擴充...
}

// 池子條目
[System.Serializable]
public class TrashPoolEntry : BasePoolEntry<TrashType, BaseTrash> { }


// 垃圾物件池
public class TrashPool : BasePool<TrashType, BaseTrash>
{
    public List<BaseTrash> ActiveTrashList { get; private set; } = new List<BaseTrash>();
    public static TrashPool Instance { get; private set; }

    [SerializeField]
    private List<TrashPoolEntry> trashEntries;

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
        InitializePool(trashEntries);
    }
    private void FixedUpdate()
    {
        // 每幀重建網格
        SpatialGridManager.Instance.UpdateGrid(ActiveTrashList);
    }

    public BaseTrash GetTrash(TrashType type, Vector3 position)
    {
        BaseTrash trash = Get(type);

        if (trash != null)
        {
            trash.transform.position = position;  // 添加這行來設定位置
            trash.gameObject.SetActive(true);
            ActiveTrashList.Add(trash); // <--- 新增：加入活躍清單
            return trash;
        }
        return trash;
    }

    public void ReturnTrash(BaseTrash trash)
    {
        if (trash == null) return;
        ActiveTrashList.Remove(trash); // <--- 新增：移出活躍清單
        Return(trash.trashType, trash);
    }
}
