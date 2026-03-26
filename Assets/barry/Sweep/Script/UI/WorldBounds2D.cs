using System.Collections.Generic;
using UnityEngine;

public class WorldBounds2D : MonoBehaviour
{
    public static WorldBounds2D Instance { get; private set; }

    [Header("世界空間範圍 (絕對座標，當多邊形未啟用時使用)")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -5f;
    public float maxY = 5f;

    [Header("自訂多邊形邊界 (世界空間座標)")]
    public List<Vector2> worldPolygon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (worldPolygon == null)
            worldPolygon = new List<Vector2>();
    }

    /// <summary>
    /// 檢查世界座標點是不是在邊界外面
    /// </summary>
    public bool IsOutside(Vector2 worldPos)
    {
        if (UsePolygon())
        {
            return !IsPointInPolygon(worldPos, worldPolygon);
        }
        else
        {
            return worldPos.x < minX || worldPos.x > maxX || worldPos.y < minY || worldPos.y > maxY;
        }
    }

    /// <summary>
    /// 會把物件「彈回」邊界內，並根據碰到哪一邊反轉速度
    /// padding：可以讓物件比玩家稍早撞牆（垃圾用），僅矩形模式有效
    /// </summary>
    public void Bounce(ref Vector2 worldPos, ref Vector2 velocity, float padding = 0f)
    {
        if (UsePolygon())
        {
            if (IsPointInPolygon(worldPos, worldPolygon))
                return;

            Vector2 closest;
            Vector2 normal;
            FindClosestPointAndNormal(worldPos, worldPolygon, out closest, out normal);

            worldPos = closest;

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

            if (worldPos.x < minXPad)
            {
                worldPos.x = minXPad;
                bounceX = true;
            }
            else if (worldPos.x > maxXPad)
            {
                worldPos.x = maxXPad;
                bounceX = true;
            }

            if (worldPos.y < minYPad)
            {
                worldPos.y = minYPad;
                bounceY = true;
            }
            else if (worldPos.y > maxYPad)
            {
                worldPos.y = maxYPad;
                bounceY = true;
            }

            if (bounceX || bounceY)
            {
                if (bounceX) velocity.x *= -1f;
                if (bounceY) velocity.y *= -1f;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        if (worldPolygon != null && worldPolygon.Count >= 3)
        {
            int count = worldPolygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = worldPolygon[i];
                Vector2 b = worldPolygon[(i + 1) % count];
                // 為了在 3D 空間中畫線，Z 軸補 0
                Gizmos.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
            }
        }
        else
        {
            Vector3 bl = new Vector3(minX, minY, 0);
            Vector3 br = new Vector3(maxX, minY, 0);
            Vector3 tr = new Vector3(maxX, maxY, 0);
            Vector3 tl = new Vector3(minX, maxY, 0);

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }

    public Rect GetWorldRect()
    {
        if (UsePolygon())
        {
            if (worldPolygon == null || worldPolygon.Count == 0)
                return new Rect();

            float minWx = float.PositiveInfinity;
            float maxWx = float.NegativeInfinity;
            float minWy = float.PositiveInfinity;
            float maxWy = float.NegativeInfinity;

            for (int i = 0; i < worldPolygon.Count; i++)
            {
                Vector2 w = worldPolygon[i];
                if (w.x < minWx) minWx = w.x;
                if (w.x > maxWx) maxWx = w.x;
                if (w.y < minWy) minWy = w.y;
                if (w.y > maxWy) maxWy = w.y;
            }

            return Rect.MinMaxRect(minWx, minWy, maxWx, maxWy);
        }
        else
        {
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }

    private bool UsePolygon()
    {
        return worldPolygon != null && worldPolygon.Count >= 3;
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

    public bool TryGetHitPointAndNormalWorld(Vector2 worldPos, out Vector2 hitPoint, out Vector2 hitNormalWorld, float padding = 0f)
    {
        hitPoint = worldPos;
        hitNormalWorld = Vector2.up;

        if (UsePolygon())
        {
            if (IsPointInPolygon(worldPos, worldPolygon)) return false;

            FindClosestPointAndNormal(worldPos, worldPolygon, out hitPoint, out hitNormalWorld);

            Vector2 towardObj = worldPos - hitPoint;
            if (towardObj.sqrMagnitude > 1e-8f)
                hitNormalWorld = towardObj.normalized;

            return true;
        }
        else
        {
            float minXPad = minX + padding;
            float maxXPad = maxX - padding;
            float minYPad = minY + padding;
            float maxYPad = maxY - padding;

            bool outside = (worldPos.x < minXPad || worldPos.x > maxXPad || worldPos.y < minYPad || worldPos.y > maxYPad);
            if (!outside) return false;

            hitPoint = new Vector2(Mathf.Clamp(worldPos.x, minXPad, maxXPad), Mathf.Clamp(worldPos.y, minYPad, maxYPad));
            Vector2 towardObj = worldPos - hitPoint;

            if (towardObj.sqrMagnitude > 1e-8f)
            {
                hitNormalWorld = towardObj.normalized;
            }
            else
            {
                if (worldPos.x < minXPad) hitNormalWorld = Vector2.left;
                else if (worldPos.x > maxXPad) hitNormalWorld = Vector2.right;
                else if (worldPos.y < minYPad) hitNormalWorld = Vector2.down;
                else hitNormalWorld = Vector2.up;
            }

            return true;
        }
    }
}