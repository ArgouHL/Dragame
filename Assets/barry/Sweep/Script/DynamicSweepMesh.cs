using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicSweepMesh : MonoBehaviour
{
    private Mesh mesh;
    [Header("扇形半徑設定")]
    public float minRadius; // t=0 時半徑（取代 minLength）
    public float maxRadius; // t=1 時半徑（取代 maxLength）
    [Header("弧線 Segments")]
    [Range(2, 24)]
    public int arcSegments;
    [Header("扇形角度設定")]
    public float minArcAngle; // t=0 時整個扇形角度（度）
    public float maxArcAngle; // t=1 時整個扇形角度（度）
    public float CurrentRadius { get; private set; }
    public Vector2[] CurrentPath2D { get; private set; }

    /// <summary> t = 0~1 </summary>
    public void UpdateShape(float t)
    {
        if (mesh == null) return;
        t = Mathf.Clamp01(t);
        float radius = Mathf.Lerp(minRadius, maxRadius, t);
        // 角度（轉成 rad，半角）
        float arcDeg = Mathf.Lerp(minArcAngle, maxArcAngle, t);
        float halfRad = arcDeg * Mathf.Deg2Rad * 0.5f;
        CurrentRadius = radius;
        int seg = Mathf.Max(2, arcSegments);
        // 頂點：0 中心點、1..seg+1 弧線點
        int vertexCount = 1 + (seg + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] path2D = new Vector2[vertexCount];
        vertices[0] = Vector3.zero; // 中心點
        path2D[0] = Vector2.zero;
        // 生成弧線點（從上到下）
        for (int i = 0; i <= seg; i++)
        {
            float tt = i / (float)seg;
            float angle = Mathf.Lerp(halfRad, -halfRad, tt); // 上到下
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            int idx = 1 + i;
            vertices[idx] = new Vector3(x, y, 0f);
            path2D[idx] = new Vector2(x, y);
        }
        CurrentPath2D = path2D;
        // 扇形三角形
        int triCount = seg;
        int indexCount = triCount * 3;
        int[] triangles = new int[indexCount];
        int ti = 0;
        for (int i = 0; i < seg; i++)
        {
            triangles[ti++] = 0; // 中心
            triangles[ti++] = i + 1;
            triangles[ti++] = i + 2;
        }
        // UV
        Vector2[] uvs = new Vector2[vertexCount];
        float minX = Mathf.Min(0, -radius);
        float maxX = radius;
        float minY = -radius;
        float maxY = radius;
        float invDX = maxX - minX > 0.0001f ? 1f / (maxX - minX) : 1f;
        float invDY = maxY - minY > 0.0001f ? 1f / (maxY - minY) : 1f;
        for (int i = 0; i < vertexCount; i++)
        {
            float u = (vertices[i].x - minX) * invDX;
            float v = (vertices[i].y - minY) * invDY;
            uvs[i] = new Vector2(u, v);
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void Awake()
    {
        var mf = GetComponent<MeshFilter>();
        mesh = new Mesh { name = "SweepMesh" };
        mf.mesh = mesh;
        UpdateShape(0f);
        mesh.MarkDynamic();
    }
}