using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WorldBounds2D : MonoBehaviour
{
    public enum BoundsType
    {
        Containment, // 世界邊界 (限制在內，出不去)
        Obstacle     // 障礙物 (排斥在外，進不來)
    }

    public static WorldBounds2D Instance { get; private set; }

    [Header("核心設定")]
    [Tooltip("是否為主要的世界邊界？勾選才會被註冊為 Singleton 供 PlayerController 與 LevelSpawner 呼叫")]
    public bool isMainWorldBoundary = true;
    public BoundsType boundsType = BoundsType.Containment;

    [Header("範圍設定 (基於 Transform 動態計算)")]
    public Vector2 centerOffset = Vector2.zero;
    public Vector2 size = new Vector2(20f, 10f);

    // [重點註釋] 改為相對座標，完美支援 Prefab 大量複製、移動與縮放調整
    public Vector2 Min => (Vector2)transform.position + centerOffset - size * 0.5f;
    public Vector2 Max => (Vector2)transform.position + centerOffset + size * 0.5f;

    private void Awake()
    {
        // [重點註釋] 保全既有邏輯：只有主邊界會成為 Instance，避免多個障礙物互相覆蓋導致依賴此 Instance 的系統崩潰
        if (isMainWorldBoundary)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }

    /// <summary>
    /// 取得邊界中心點
    /// </summary>
    public Vector2 GetCenter() => (Vector2)transform.position + centerOffset;

    /// <summary>
    /// 取得完整邊界範圍 (提供給 LevelSpawner 使用)
    /// </summary>
    public Rect GetWorldRect()
    {
        Vector2 min = Min;
        Vector2 max = Max;
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>
    /// 檢查是否違規 (保留原名 IsOutside 確保 PlayerController 不報錯)
    /// 若為邊界模式 = 是否超出邊界；若為障礙物模式 = 是否進入障礙物
    /// </summary>
    public bool IsOutside(Vector2 pos, float padding = 0f)
    {
        Vector2 min = Min;
        Vector2 max = Max;

        if (boundsType == BoundsType.Containment)
        {
            return pos.x < min.x + padding || pos.x > max.x - padding ||
                   pos.y < min.y + padding || pos.y > max.y - padding;
        }

        // 障礙物模式：在內部即視為違規，padding 往外擴張
        return pos.x > min.x - padding && pos.x < max.x + padding &&
               pos.y > min.y - padding && pos.y < max.y + padding;
    }

    /// <summary>
    /// 把位置夾回合法區域，並移除往違規方向的速度分量 (空氣牆式阻擋)
    /// </summary>
    public bool ConstrainToBounds(ref Vector2 pos, ref Vector2 velocity, float padding = 0f)
    {
        Vector2 min = Min;
        Vector2 max = Max;

        float minX = boundsType == BoundsType.Containment ? min.x + padding : min.x - padding;
        float maxX = boundsType == BoundsType.Containment ? max.x - padding : max.x + padding;
        float minY = boundsType == BoundsType.Containment ? min.y + padding : min.y - padding;
        float maxY = boundsType == BoundsType.Containment ? max.y - padding : max.y + padding;

        bool hit = false;

        if (boundsType == BoundsType.Containment)
        {
            if (pos.x < minX) { pos.x = minX; if (velocity.x < 0f) velocity.x = 0f; hit = true; }
            else if (pos.x > maxX) { pos.x = maxX; if (velocity.x > 0f) velocity.x = 0f; hit = true; }

            if (pos.y < minY) { pos.y = minY; if (velocity.y < 0f) velocity.y = 0f; hit = true; }
            else if (pos.y > maxY) { pos.y = maxY; if (velocity.y > 0f) velocity.y = 0f; hit = true; }
        }
        else
        {
            // 障礙物排斥邏輯：尋找最淺穿透軸向將其推出
            if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
            {
                hit = true;
                float dL = pos.x - minX;
                float dR = maxX - pos.x;
                float dB = pos.y - minY;
                float dT = maxY - pos.y;

                float closest = Mathf.Min(dL, dR, dB, dT);

                if (closest == dL) { pos.x = minX; if (velocity.x > 0f) velocity.x = 0f; }
                else if (closest == dR) { pos.x = maxX; if (velocity.x < 0f) velocity.x = 0f; }
                else if (closest == dB) { pos.y = minY; if (velocity.y > 0f) velocity.y = 0f; }
                else { pos.y = maxY; if (velocity.y < 0f) velocity.y = 0f; }
            }
        }

        return hit;
    }

    /// <summary>
    /// 真正的物理反彈，根據傳入的彈力係數 (bounciness) 反轉速度
    /// </summary>
    public void Bounce(ref Vector2 pos, ref Vector2 velocity, float padding = 0f, float bounciness = 1f)
    {
        Vector2 min = Min;
        Vector2 max = Max;

        float minX = boundsType == BoundsType.Containment ? min.x + padding : min.x - padding;
        float maxX = boundsType == BoundsType.Containment ? max.x - padding : max.x + padding;
        float minY = boundsType == BoundsType.Containment ? min.y + padding : min.y - padding;
        float maxY = boundsType == BoundsType.Containment ? max.y - padding : max.y + padding;

        if (boundsType == BoundsType.Containment)
        {
            if (pos.x < minX) { pos.x = minX; if (velocity.x < 0f) velocity.x = -velocity.x * bounciness; }
            else if (pos.x > maxX) { pos.x = maxX; if (velocity.x > 0f) velocity.x = -velocity.x * bounciness; }

            if (pos.y < minY) { pos.y = minY; if (velocity.y < 0f) velocity.y = -velocity.y * bounciness; }
            else if (pos.y > maxY) { pos.y = maxY; if (velocity.y > 0f) velocity.y = -velocity.y * bounciness; }
        }
        else
        {
            if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
            {
                float dL = pos.x - minX;
                float dR = maxX - pos.x;
                float dB = pos.y - minY;
                float dT = maxY - pos.y;

                float closest = Mathf.Min(dL, dR, dB, dT);

                if (closest == dL) { pos.x = minX; if (velocity.x > 0f) velocity.x = -velocity.x * bounciness; }
                else if (closest == dR) { pos.x = maxX; if (velocity.x < 0f) velocity.x = -velocity.x * bounciness; }
                else if (closest == dB) { pos.y = minY; if (velocity.y > 0f) velocity.y = -velocity.y * bounciness; }
                else { pos.y = maxY; if (velocity.y < 0f) velocity.y = -velocity.y * bounciness; }
            }
        }
    }

    /// <summary>
    /// 取得超出(或進入)邊界時的最近碰撞點與反向法線
    /// </summary>
    public bool TryGetHitPointAndNormalWorld(Vector2 pos, out Vector2 hitPoint, out Vector2 normal, float padding = 0f)
    {
        hitPoint = pos;
        normal = Vector2.up;

        Vector2 min = Min;
        Vector2 max = Max;

        float minX = boundsType == BoundsType.Containment ? min.x + padding : min.x - padding;
        float maxX = boundsType == BoundsType.Containment ? max.x - padding : max.x + padding;
        float minY = boundsType == BoundsType.Containment ? min.y + padding : min.y - padding;
        float maxY = boundsType == BoundsType.Containment ? max.y - padding : max.y + padding;

        if (boundsType == BoundsType.Containment)
        {
            if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                return false;

            hitPoint.x = Mathf.Clamp(pos.x, minX, maxX);
            hitPoint.y = Mathf.Clamp(pos.y, minY, maxY);

            Vector2 inward = hitPoint - pos;

            if (inward.sqrMagnitude > 1e-6f)
            {
                normal = inward.normalized;
            }
            else
            {
                float dL = pos.x - minX;
                float dR = maxX - pos.x;
                float dB = pos.y - minY;
                float dT = maxY - pos.y;
                float closest = Mathf.Min(dL, dR, dB, dT);

                if (closest == dL) normal = Vector2.right;
                else if (closest == dR) normal = Vector2.left;
                else if (closest == dB) normal = Vector2.up;
                else normal = Vector2.down;
            }
            return true;
        }
        else
        {
            if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY)
                return false;

            float dL = pos.x - minX;
            float dR = maxX - pos.x;
            float dB = pos.y - minY;
            float dT = maxY - pos.y;
            float closest = Mathf.Min(dL, dR, dB, dT);

            if (closest == dL) { hitPoint.x = minX; normal = Vector2.left; }
            else if (closest == dR) { hitPoint.x = maxX; normal = Vector2.right; }
            else if (closest == dB) { hitPoint.y = minY; normal = Vector2.down; }
            else { hitPoint.y = maxY; normal = Vector2.up; }

            return true;
        }
    }

    private void OnDrawGizmos()
    {
        Vector2 min = Min;
        Vector2 max = Max;

        Gizmos.color = boundsType == BoundsType.Containment ? Color.cyan : Color.red;

        Vector3 bl = new Vector3(min.x, min.y, 0f);
        Vector3 br = new Vector3(max.x, min.y, 0f);
        Vector3 tr = new Vector3(max.x, max.y, 0f);
        Vector3 tl = new Vector3(min.x, max.y, 0f);

        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f);
        Gizmos.DrawCube(GetCenter(), size);
    }
}