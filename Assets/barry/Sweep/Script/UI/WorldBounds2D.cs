// WorldBounds2D.cs
using UnityEngine;

public class WorldBounds2D : MonoBehaviour
{
    public static WorldBounds2D Instance { get; private set; }

    [Header("地圖邊界設定")]
    public Vector2 rectMin = new Vector2(-10f, -5f);
    public Vector2 rectMax = new Vector2(10f, 5f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 取得邊界中心點
    /// </summary>
    public Vector2 GetCenter()
    {
        return (rectMin + rectMax) * 0.5f;
    }

    /// <summary>
    /// 取得完整邊界範圍
    /// </summary>
    public Rect GetWorldRect()
    {
        return Rect.MinMaxRect(rectMin.x, rectMin.y, rectMax.x, rectMax.y);
    }

    /// <summary>
    /// 檢查是否超出邊界
    /// </summary>
    public bool IsOutside(Vector2 pos, float padding = 0f)
    {
        return pos.x < rectMin.x + padding ||
               pos.x > rectMax.x - padding ||
               pos.y < rectMin.y + padding ||
               pos.y > rectMax.y - padding;
    }

    /// <summary>
    /// 把位置夾回邊界內，並移除往外的速度分量
    /// 這不是反彈，是「空氣牆」式阻擋
    /// </summary>
    public bool ConstrainToBounds(ref Vector2 pos, ref Vector2 velocity, float padding = 0f)
    {
        float minX = rectMin.x + padding;
        float maxX = rectMax.x - padding;
        float minY = rectMin.y + padding;
        float maxY = rectMax.y - padding;

        bool hit = false;

        if (pos.x < minX)
        {
            pos.x = minX;
            if (velocity.x < 0f) velocity.x = 0f;
            hit = true;
        }
        else if (pos.x > maxX)
        {
            pos.x = maxX;
            if (velocity.x > 0f) velocity.x = 0f;
            hit = true;
        }

        if (pos.y < minY)
        {
            pos.y = minY;
            if (velocity.y < 0f) velocity.y = 0f;
            hit = true;
        }
        else if (pos.y > maxY)
        {
            pos.y = maxY;
            if (velocity.y > 0f) velocity.y = 0f;
            hit = true;
        }

        return hit;
    }

    /// <summary>
    /// 舊方法保留相容：改成空氣牆式限制
    /// </summary>
    public void Bounce(ref Vector2 pos, ref Vector2 velocity, float padding = 0f)
    {
        ConstrainToBounds(ref pos, ref velocity, padding);
    }

    /// <summary>
    /// 取得超出邊界時的最近碰撞點與反向法線
    /// </summary>
    public bool TryGetHitPointAndNormalWorld(Vector2 pos, out Vector2 hitPoint, out Vector2 normal, float padding = 0f)
    {
        hitPoint = pos;
        normal = Vector2.up;

        float minX = rectMin.x + padding;
        float maxX = rectMax.x - padding;
        float minY = rectMin.y + padding;
        float maxY = rectMax.y - padding;

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
            float min = Mathf.Min(dL, dR, dB, dT);

            if (min == dL) normal = Vector2.right;
            else if (min == dR) normal = Vector2.left;
            else if (min == dB) normal = Vector2.up;
            else normal = Vector2.down;
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Vector3 bl = new Vector3(rectMin.x, rectMin.y, 0f);
        Vector3 br = new Vector3(rectMax.x, rectMin.y, 0f);
        Vector3 tr = new Vector3(rectMax.x, rectMax.y, 0f);
        Vector3 tl = new Vector3(rectMin.x, rectMax.y, 0f);

        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}