using System.Collections.Generic;
using UnityEngine;

public class WorldBounds2D : MonoBehaviour
{
    public static WorldBounds2D Instance { get; private set; }

    [Header("Viewport 範圍 (0~1，當多邊形未啟用時使用)")]
    [Range(0f, 1f)] public float minX = 0f;
    [Range(0f, 1f)] public float maxX = 1f;
    [Range(0f, 1f)] public float minY = 0.1f;
    [Range(0f, 1f)] public float maxY = 1f;

    [Header("用哪一顆 Camera 算")]
    public Camera cam;

    [Header("自訂多邊形邊界 (Viewport 空間 0~1)")]
    public List<Vector2> viewportPolygon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!cam) cam = Camera.main;

        if (viewportPolygon == null)
            viewportPolygon = new List<Vector2>();
    }

    /// <summary>
    /// 檢查世界座標點是不是在邊界外面
    /// </summary>
    public bool IsOutside(Vector2 worldPos)
    {
        if (!cam) return false;

        Vector3 vp3 = cam.WorldToViewportPoint(worldPos);
        Vector2 vp = new Vector2(vp3.x, vp3.y);

        if (UsePolygon())
        {
            return !IsPointInPolygon(vp, viewportPolygon);
        }
        else
        {
            return vp.x < minX || vp.x > maxX || vp.y < minY || vp.y > maxY;
        }
    }

    /// <summary>
    /// 會把物件「彈回」邊界內，並根據碰到哪一邊反轉速度
    /// padding：可以讓物件比玩家稍早撞牆（垃圾用），僅矩形模式有效
    /// </summary>
    public void Bounce(ref Vector2 worldPos, ref Vector2 velocity, float padding = 0f)
    {
        if (!cam) return;

        Vector3 vp3 = cam.WorldToViewportPoint(worldPos);
        Vector2 vp = new Vector2(vp3.x, vp3.y);

        if (UsePolygon())
        {
            if (IsPointInPolygon(vp, viewportPolygon))
                return;

            Vector2 closest;
            Vector2 normal;
            FindClosestPointAndNormal(vp, viewportPolygon, out closest, out normal);

            Vector3 newVp3 = new Vector3(closest.x, closest.y, vp3.z);
            Vector3 world = cam.ViewportToWorldPoint(newVp3);
            worldPos = new Vector2(world.x, world.y);

            Vector2 v = velocity;
            Vector2 n = normal.normalized;
            float d = Vector2.Dot(v, n);
            velocity = v - 2f * d * n;
        }
        else
        {
            float minXPad = minX + padding;
            float maxXPad = maxX - padding;
            float minYPad = minY + padding;
            float maxYPad = maxY - padding;

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
                Vector3 world = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, vp3.z));
                worldPos = new Vector2(world.x, world.y);

                if (bounceX) velocity.x *= -1f;
                if (bounceY) velocity.y *= -1f;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Camera c = cam != null ? cam : Camera.main;
        if (!c) return;

        if (viewportPolygon != null && viewportPolygon.Count >= 3)
        {
            Gizmos.color = Color.cyan;

            float depth = -c.transform.position.z;

            int count = viewportPolygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 aVp = viewportPolygon[i];
                Vector2 bVp = viewportPolygon[(i + 1) % count];

                Vector3 a = c.ViewportToWorldPoint(new Vector3(aVp.x, aVp.y, depth));
                Vector3 b = c.ViewportToWorldPoint(new Vector3(bVp.x, bVp.y, depth));

                Gizmos.DrawLine(a, b);
            }
        }
        else
        {
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

    public Rect GetWorldRect()
    {
        if (!cam) cam = Camera.main;
        if (!cam)
        {
            Debug.LogWarning("WorldBounds2D: 找不到 Camera，GetWorldRect 回傳空矩形。");
            return new Rect();
        }

        float depth = -cam.transform.position.z;

        if (UsePolygon())
        {
            if (viewportPolygon == null || viewportPolygon.Count == 0)
                return new Rect();

            float minWx = float.PositiveInfinity;
            float maxWx = float.NegativeInfinity;
            float minWy = float.PositiveInfinity;
            float maxWy = float.NegativeInfinity;

            for (int i = 0; i < viewportPolygon.Count; i++)
            {
                Vector2 vp = viewportPolygon[i];
                Vector3 w = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, depth));

                if (w.x < minWx) minWx = w.x;
                if (w.x > maxWx) maxWx = w.x;
                if (w.y < minWy) minWy = w.y;
                if (w.y > maxWy) maxWy = w.y;
            }

            return Rect.MinMaxRect(minWx, minWy, maxWx, maxWy);
        }
        else
        {
            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(minX, minY, depth));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(maxX, maxY, depth));
            return Rect.MinMaxRect(bl.x, bl.y, tr.x, tr.y);
        }
    }

    private bool UsePolygon()
    {
        return viewportPolygon != null && viewportPolygon.Count >= 3;
    }

    private bool IsPointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int count = poly.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];

            bool intersect =
                ((pi.y > p.y) != (pj.y > p.y)) &&
                (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);

            if (intersect)
                inside = !inside;
        }
        return inside;
    }

    private void FindClosestPointAndNormal(Vector2 p, List<Vector2> poly, out Vector2 closest, out Vector2 normal)
    {
        closest = p;
        normal = Vector2.up;

        float bestSqr = float.PositiveInfinity;
        int count = poly.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];

            Vector2 ab = b - a;
            float abLenSqr = Vector2.SqrMagnitude(ab);
            if (abLenSqr <= Mathf.Epsilon)
                continue;

            float t = Vector2.Dot(p - a, ab) / abLenSqr;
            t = Mathf.Clamp01(t);

            Vector2 proj = a + ab * t;
            float sqr = Vector2.SqrMagnitude(p - proj);

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                closest = proj;

                Vector2 edge = b - a;
                normal = new Vector2(-edge.y, edge.x).normalized;
            }
        }
    }
}
