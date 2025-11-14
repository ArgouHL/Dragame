using System;
using System.Collections.Generic;
using UnityEngine;

public class BasePoolEntry<TKey, TValue>
{
    public TKey key;
    public TValue prefab;
}

public abstract class BasePool<TKey, TValue> : MonoBehaviour where TValue : Component
{
    [SerializeField]
    private itemData item; // 唯一參照

    // 緩存池主體
    protected Dictionary<TKey, Queue<TValue>> poolDict = new Dictionary<TKey, Queue<TValue>>();

    // 註冊 Prefab，用來在池為空時動態生成
    protected Dictionary<TKey, TValue> prefabDict = new Dictionary<TKey, TValue>();

    // 所有未使用物件的父節點
    public Transform poolParent;


    protected void InitializePool(IEnumerable<BasePoolEntry<TKey, TValue>> entries)
    {
        foreach (var entry in entries)
        {
            // 1. 註冊 Prefab
            if (!prefabDict.ContainsKey(entry.key))
            {
                prefabDict.Add(entry.key, entry.prefab);
            }
            else
            {
                Debug.LogWarning($"鍵 {entry.key} 重複。", this);
                continue;
            }

            // 2. 為每個鍵建立佇列
            if (!poolDict.ContainsKey(entry.key))
            {
                poolDict.Add(entry.key, new Queue<TValue>());
            }
        }
    }

    public virtual TValue Get(TKey key)
    {
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogError($"池中不存在鍵 {key}。", this);
            return null;
        }

        Queue<TValue> pool = poolDict[key];
        TValue item;

        if (pool.Count == 0)
        {
            // 池空 → 動態生成
            if (!prefabDict.ContainsKey(key))
            {
                Debug.LogError($"Prefab 字典中沒有鍵 {key} 的資料。", this);
                return null;
            }

            item = Instantiate(prefabDict[key]);
        }
        else
        {
            // 從佇列取出
            item = pool.Dequeue();
        }

        // 啟用 & 移出父節點
        item.transform.SetParent(null);
        item.gameObject.SetActive(true);

        return item;
    }

    public virtual void Return(TKey key, TValue item)
    {
        if (item == null) return;

        if (!poolDict.ContainsKey(key))
        {
            Debug.LogWarning($"試圖歸還鍵 {key} 到不存在的池中。物件將被銷毀。", this);
            Destroy(item.gameObject);
            return;
        }

        // 關閉並放到父層
        item.gameObject.SetActive(false);
        item.transform.SetParent(poolParent);

        // 放回池子
        poolDict[key].Enqueue(item);
    }
}
