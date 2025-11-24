using UnityEngine;
using System.Collections.Generic;

public class BlackObstacle : BaseObstacle
{
    [Header("黑洞能力設定")]
    [Tooltip("黑洞能吸入垃圾的半徑範圍")]
    [SerializeField] private float absorbRadius = 3.0f;

    // 用於效能優化的變數 (半徑的平方)
    private float _sqrAbsorbRadius;

    protected void Awake()
    {
     
        // 預先計算平方值，避免在 FixedUpdate 迴圈中重複計算
        _sqrAbsorbRadius = absorbRadius * absorbRadius;
    }

    private void FixedUpdate()
    {
        CheckAndAbsorbTrash();
    }

    private void CheckAndAbsorbTrash()
    {
        // 1. 向網格系統索取周圍的垃圾
        // 這裡回傳的是九宮格內所有的垃圾
        List<BaseTrash> nearbyTrash = SpatialGridManager.Instance.GetTrashAroundPosition(transform.position);

        if (nearbyTrash.Count == 0) return;

        Vector3 myPos = transform.position;

        // 2. 遍歷這些垃圾，判斷距離
        for (int i = 0; i < nearbyTrash.Count; i++)
        {
            BaseTrash trash = nearbyTrash[i];

            // 雙重確認：確保垃圾存在且沒有正在被吸
            // (雖然 GridManager 已經濾過 IsAbsorbing，但多判斷一次成本極低且更安全)
            if (trash == null || trash.IsAbsorbing) continue;

            // 3. 使用 sqrMagnitude 進行純數學距離比對 (比 Vector2.Distance 快很多)
            float sqrDist = (trash.transform.position - myPos).sqrMagnitude;

            if (sqrDist <= _sqrAbsorbRadius)
            {
                // 4. 觸發垃圾的被吸入效果
                // 將黑洞的位置傳給垃圾，讓它往黑洞中心飛
                trash.OnEnterBlackHole(myPos);
            }
        }
    }

    // 在編輯器中畫出吸入範圍，方便調整
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorbRadius);
    }
}