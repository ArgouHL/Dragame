using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BoundZone
{
    [Header("區域識別名")]
    public string zoneName = "Area";

    [Header("邊界類型")]
    public bool usePolygon = false;

    [Header("矩形設定 (usePolygon = false)")]
    public Vector2 rectMin = new Vector2(-10f, -5f);
    public Vector2 rectMax = new Vector2(10f, 5f);

    [Header("多邊形設定 (usePolygon = true)")]
    public List<Vector2> polygon = new List<Vector2>();

    public bool Contains(Vector2 p, float padding = 0f)
    {
        if (usePolygon)
        {
            if (polygon == null || polygon.Count < 3) return false;
            return IsPointInPolygon(p, polygon);
        }
        else
        {
            return p.x >= (rectMin.x + padding) && p.x <= (rectMax.x - padding) &&
                   p.y >= (rectMin.y + padding) && p.y <= (rectMax.y - padding);
        }
    }

    public Vector2 GetCenter()
    {
        if (!usePolygon) return (rectMin + rectMax) / 2f;
        if (polygon == null || polygon.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < polygon.Count; i++) sum += polygon[i];
        return sum / polygon.Count;
    }

    public Rect GetRect()
    {
        if (!usePolygon)
        {
            return Rect.MinMaxRect(rectMin.x, rectMin.y, rectMax.x, rectMax.y);
        }
        else
        {
            if (polygon == null || polygon.Count == 0) return new Rect();
            float minWx = float.PositiveInfinity;
            float maxWx = float.NegativeInfinity;
            float minWy = float.PositiveInfinity;
            float maxWy = float.NegativeInfinity;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 w = polygon[i];
                if (w.x < minWx) minWx = w.x;
                if (w.x > maxWx) maxWx = w.x;
                if (w.y < minWy) minWy = w.y;
                if (w.y > maxWy) maxWy = w.y;
            }
            return Rect.MinMaxRect(minWx, minWy, maxWx, maxWy);
        }
    }

    public void GetClosestBorder(Vector2 p, float padding, out Vector2 closest, out Vector2 normal)
    {
        if (usePolygon)
        {
            closest = p; normal = Vector2.up;
            if (polygon == null || polygon.Count < 3) return;

            float bestSqr = float.PositiveInfinity;
            int count = polygon.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                Vector2 ab = b - a;
                float abLenSqr = ab.sqrMagnitude;
                if (abLenSqr <= Mathf.Epsilon) continue;

                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSqr);
                Vector2 proj = a + ab * t;
                float sqr = (p - proj).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    closest = proj;
                    Vector2 edge = b - a;
                    normal = new Vector2(-edge.y, edge.x).normalized;

                    if (!IsPointInPolygon(closest + normal * 0.05f, polygon))
                    {
                        normal = -normal;
                    }
                }
            }
        }
        else
        {
            float minX = rectMin.x + padding; float maxX = rectMax.x - padding;
            float minY = rectMin.y + padding; float maxY = rectMax.y - padding;

            if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
            {
                closest = new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minY, maxY));
                Vector2 inward = closest - p;
                if (inward.sqrMagnitude > 1e-6f) normal = inward.normalized;
                else
                {
                    if (p.x < minX) normal = Vector2.right;
                    else if (p.x > maxX) normal = Vector2.left;
                    else if (p.y < minY) normal = Vector2.up;
                    else normal = Vector2.down;
                }
            }
            else
            {
                float dL = p.x - minX; float dR = maxX - p.x;
                float dB = p.y - minY; float dT = maxY - p.y;
                float m = Mathf.Min(dL, dR, dB, dT);
                closest = p;
                if (m == dL) { closest.x = minX; normal = Vector2.right; }
                else if (m == dR) { closest.x = maxX; normal = Vector2.left; }
                else if (m == dB) { closest.y = minY; normal = Vector2.up; }
                else { closest.y = maxY; normal = Vector2.down; }
            }
        }
    }

    private bool IsPointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int count = poly.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    public void DrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (usePolygon && polygon != null && polygon.Count >= 3)
        {
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                Gizmos.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
            }
        }
        else
        {
            Vector3 bl = new Vector3(rectMin.x, rectMin.y, 0);
            Vector3 br = new Vector3(rectMax.x, rectMin.y, 0);
            Vector3 tr = new Vector3(rectMax.x, rectMax.y, 0);
            Vector3 tl = new Vector3(rectMin.x, rectMax.y, 0);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}

public class WorldBounds2D : MonoBehaviour
{
    public static WorldBounds2D Instance { get; private set; }

    [Header("=== 多重空氣牆設定 ===")]
    [Tooltip("只要物件處於其中任何一個區域內，即視為合法。")]
    public List<BoundZone> zones = new List<BoundZone>();

    // [重點註釋] 快取全域邊界，避免每幀重複計算導致效能浪費
    private Rect _cachedWorldRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (zones.Count == 0)
        {
            Debug.LogWarning("WorldBounds2D 未設定任何區域，將預設產生一個矩形邊界。");
            zones.Add(new BoundZone());
        }

        CacheWorldRect();
    }

    /// <summary>
    /// 初始化時計算一次最大外接矩形並快取。
    /// </summary>
    private void CacheWorldRect()
    {
        if (zones == null || zones.Count == 0)
        {
            _cachedWorldRect = new Rect();
            return;
        }

        float minWx = float.PositiveInfinity;
        float maxWx = float.NegativeInfinity;
        float minWy = float.PositiveInfinity;
        float maxWy = float.NegativeInfinity;

        for (int i = 0; i < zones.Count; i++)
        {
            Rect r = zones[i].GetRect();
            if (r.xMin < minWx) minWx = r.xMin;
            if (r.yMin < minWy) minWy = r.yMin;
            if (r.xMax > maxWx) maxWx = r.xMax;
            if (r.yMax > maxWy) maxWy = r.yMax;
        }

        if (float.IsInfinity(minWx)) _cachedWorldRect = new Rect();
        else _cachedWorldRect = Rect.MinMaxRect(minWx, minWy, maxWx, maxWy);
    }

    /// <summary>
    /// O(1) 取用：直接回傳快取的最大外接矩形。
    /// </summary>
    public Rect GetWorldRect()
    {
        return _cachedWorldRect;
    }

    public bool IsOutside(Vector2 worldPos, float padding = 0f)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].Contains(worldPos, padding)) return false;
        }
        return true;
    }

    public Vector2 GetSafeCenter(Vector2 worldPos)
    {
        BoundZone bestZone = GetNearestZone(worldPos, 0f);
        return bestZone != null ? bestZone.GetCenter() : Vector2.zero;
    }

    public void Bounce(ref Vector2 worldPos, ref Vector2 velocity, float padding = 0f)
    {
        if (!IsOutside(worldPos, padding)) return;

        BoundZone bestZone = GetNearestZone(worldPos, padding);
        if (bestZone == null) return;

        bestZone.GetClosestBorder(worldPos, padding, out Vector2 closest, out Vector2 normal);

        worldPos = closest;

        float d = Vector2.Dot(velocity, normal);
        if (d < 0)
        {
            velocity -= 2f * d * normal;
        }
    }

    public bool TryGetHitPointAndNormalWorld(Vector2 worldPos, out Vector2 hitPoint, out Vector2 hitNormalWorld, float padding = 0f)
    {
        hitPoint = worldPos;
        hitNormalWorld = Vector2.up;

        if (!IsOutside(worldPos, padding)) return false;

        BoundZone bestZone = GetNearestZone(worldPos, padding);
        if (bestZone == null) return false;

        bestZone.GetClosestBorder(worldPos, padding, out hitPoint, out hitNormalWorld);
        return true;
    }

    private BoundZone GetNearestZone(Vector2 pos, float padding)
    {
        if (zones.Count == 0) return null;
        if (zones.Count == 1) return zones[0];

        BoundZone nearest = zones[0];
        float minDist = float.PositiveInfinity;

        for (int i = 0; i < zones.Count; i++)
        {
            zones[i].GetClosestBorder(pos, padding, out Vector2 closest, out _);
            float sqr = (pos - closest).sqrMagnitude;
            if (sqr < minDist)
            {
                minDist = sqr;
                nearest = zones[i];
            }
        }
        return nearest;
    }

    private void OnDrawGizmos()
    {
        if (zones == null) return;
        for (int i = 0; i < zones.Count; i++)
        {
            zones[i].DrawGizmos();
        }
    }
}