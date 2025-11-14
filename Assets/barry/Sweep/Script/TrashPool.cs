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


    public BaseTrash GetTrash(TrashType type, Vector3 position)
    {
        BaseTrash trash = Get(type);

        if (trash != null)
        {
            trash.transform.position = position;
            trash.ResetState();
            trash.gameObject.SetActive(true);
            return trash;
        }

        return trash;
    }

    public void ReturnTrash(BaseTrash trash)
    {
        if (trash == null) return;

        Return(trash.trashType, trash);
    }
}
