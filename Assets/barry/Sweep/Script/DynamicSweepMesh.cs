using UnityEngine;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicSweepMesh : MonoBehaviour
{
    private Mesh mesh;
    [Header("頭尾設定")]
    public float headWidth; // 頭總寬
    public float minLength; // 圓心最短距離
    public float maxLength; // 圓心最長距離
    public float tailMaxWidth; // 尾端寬度（直徑）
    [Header("尾巴圓弧 Segments")]
    [Range(2, 24)]
    public int tailSegments;
    [Header("扇形角度設定")]
    public float minArcAngle; // t=0 時整個扇形角度（度）
    public float maxArcAngle; // t=1 時整個扇形角度（度）
    public float CurrentLength { get; private set; }
    public float CurrentHeadHalfWidth { get; private set; }
    public float CurrentTailHalfWidth { get; private set; }
    public Vector2[] CurrentPath2D { get; private set; }
    /// <summary> t = 0~1 </summary>
    public void UpdateShape(float t)
    {
        if (mesh == null) return;
        t = Mathf.Clamp01(t);
        float length = Mathf.Lerp(minLength, maxLength, t); // 圓心 X
        float headHalf = headWidth * 0.5f;
        float tailHalf = Mathf.Lerp(headWidth * 0.5f, tailMaxWidth * 0.5f, t);
        // 角度（轉成 rad，半角）
        float arcDeg = Mathf.Lerp(minArcAngle, maxArcAngle, t);
        float halfRad = arcDeg * Mathf.Deg2Rad * 0.5f;
        CurrentLength = length;
        CurrentHeadHalfWidth = headHalf;
        CurrentTailHalfWidth = tailHalf;
        int seg = Mathf.Max(2, tailSegments);
        // 頂點：0 頭下、1 頭上、2.. 尾巴圓弧（由上到下）
        int vertexCount = 2 + (seg + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] path2D = new Vector2[vertexCount];
        vertices[0] = new Vector3(0f, -headHalf, 0f);
        vertices[1] = new Vector3(0f, headHalf, 0f);
        path2D[0] = new Vector2(0f, -headHalf);
        path2D[1] = new Vector2(0f, headHalf);
        // 圓心在 (length, 0)
        for (int i = 0; i <= seg; i++)
        {
            float tt = i / (float)seg;
            float angle = Mathf.Lerp(halfRad, -halfRad, tt); // 上到下
            float x = length + Mathf.Cos(angle) * tailHalf;
            float y = Mathf.Sin(angle) * tailHalf;
            int idx = 2 + i;
            vertices[idx] = new Vector3(x, y, 0f);
            path2D[idx] = new Vector2(x, y);
        }
        CurrentPath2D = path2D;
        // 扇形三角形 fan
        int triCount = vertexCount - 2;
        int indexCount = triCount * 3;
        int[] triangles = new int[indexCount];
        int ti = 0;
        for (int i = 1; i < vertexCount - 1; i++)
        {
            triangles[ti++] = 0;
            triangles[ti++] = i;
            triangles[ti++] = i + 1;
        }
        // UV
        Vector2[] uvs = new Vector2[vertexCount];
        float minX = 0f;
        float maxX = length + tailHalf;
        float minY = -Mathf.Max(headHalf, tailHalf);
        float maxY = Mathf.Max(headHalf, tailHalf);
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