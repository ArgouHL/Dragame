using UnityEngine;

public class WorldBounds2D : MonoBehaviour
{
    public static WorldBounds2D Instance { get; private set; }

    [Header("Viewport 範圍 (0~1)")]
    [Range(0f, 1f)] public float minX = 0f;
    [Range(0f, 1f)] public float maxX = 1f;
    [Range(0f, 1f)] public float minY = 0.1f;  // 對應你原本的 0.1f
    [Range(0f, 1f)] public float maxY = 1f;

    [Header("用哪一顆 Camera 算")]
    public Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!cam) cam = Camera.main;
    }

    /// <summary>
    /// 檢查世界座標點是不是在邊界外面
    /// </summary>
    public bool IsOutside(Vector2 worldPos)
    {
        if (!cam) return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.x < minX || vp.x > maxX || vp.y < minY || vp.y > maxY;
    }

    /// <summary>
    /// 會把物件「彈回」邊界內，並根據碰到哪一邊反轉速度
    /// padding：可以讓物件比玩家稍早撞牆（垃圾用）
    /// </summary>
    public void Bounce(ref Vector2 worldPos, ref Vector2 velocity, float padding = 0f)
    {
        if (!cam) return;

        float minXPad = minX + padding;
        float maxXPad = maxX - padding;
        float minYPad = minY + padding;
        float maxYPad = maxY - padding;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        bool bounceX = false;
        bool bounceY = false;

        if (vp.x < minXPad)
        {
            vp.x = minXPad;
            bounceX = true;
        }
        else if (vp.x > maxXPad)
        {
            vp.x = maxXPad;
            bounceX = true;
        }

        if (vp.y < minYPad)
        {
            vp.y = minYPad;
            bounceY = true;
        }
        else if (vp.y > maxYPad)
        {
            vp.y = maxYPad;
            bounceY = true;
        }

        if (bounceX || bounceY)
        {
            Vector3 world = cam.ViewportToWorldPoint(vp);
            worldPos = new Vector2(world.x, world.y);

            if (bounceX) velocity.x *= -1;
            if (bounceY) velocity.y *= -1;
        }
    }

    private void OnDrawGizmos()
    {
        Camera c = cam != null ? cam : Camera.main;
        if (!c) return;

        float depth = -c.transform.position.z;

        Vector3 bl = c.ViewportToWorldPoint(new Vector3(minX, minY, depth));
        Vector3 br = c.ViewportToWorldPoint(new Vector3(maxX, minY, depth));
        Vector3 tr = c.ViewportToWorldPoint(new Vector3(maxX, maxY, depth));
        Vector3 tl = c.ViewportToWorldPoint(new Vector3(minX, maxY, depth));

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}
