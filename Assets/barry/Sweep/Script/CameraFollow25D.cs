using UnityEngine;

public class CameraFollow25D : MonoBehaviour
{
    [Header("=== 追隨目標 ===")]
    [Tooltip("請把場景上的 Player 拖曳到這裡")]
    [SerializeField] private Transform target;

    [Header("=== 相機設定 ===")]
    [Tooltip("相機相對於玩家的偏移量。透視相機建議 Y 為正(高度)，Z 為負(拉遠)。例如 (0, 15, -15)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -15f);

    [Tooltip("平滑跟隨的延遲時間，數值越小跟得越緊，數值越大越有攝影機拖曳感")]
    [SerializeField, Range(0f, 1f)] private float smoothTime = 0.15f;

    // 內部阻尼速度暫存，SmoothDamp 必須使用的變數
    private Vector3 _velocity = Vector3.zero;

    private void LateUpdate()
    {
        // 如果沒有設定目標，就不執行，避免報錯
        if (target == null) return;

        // 計算相機應該要到達的終點位置
        Vector3 targetPosition = target.position + offset;

        // [重點註釋] 使用 SmoothDamp 進行平滑插值，且嚴格放置於 LateUpdate 中執行。
        // 確保玩家 (PlayerController) 在 FixedUpdate/Update 的物理位移已完全結束後，相機才跟上，徹底消除畫面撕裂與抖動。
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, smoothTime);
    }
}