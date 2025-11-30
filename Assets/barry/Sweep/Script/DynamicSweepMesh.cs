using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicSweepMesh : MonoBehaviour
{
    private Mesh mesh;

    [Header("頭尾設定")]
    public float headWidth = 1f;       // 頭的總寬度（上下）
    public float minLength = 0.5f;     // 初始長度
    public float maxLength = 4f;       // 蓄滿最大長度
    public float tailMaxWidth = 3f;    // 蓄滿時屁股的總寬度

    /// <summary>
    /// t = 0 ~ 1，依蓄力程度更新形狀
    /// </summary>
    public void UpdateShape(float t)
    {
        if (mesh == null) return;

        t = Mathf.Clamp01(t);

        float length = Mathf.Lerp(minLength, maxLength, t);
        float headHalf = headWidth * 0.5f;
        float tailHalf = Mathf.Lerp(headWidth * 0.5f, tailMaxWidth * 0.5f, t);

        // 四個頂點
        Vector3 v0 = new Vector3(0f, -headHalf, 0f);
        Vector3 v1 = new Vector3(0f, headHalf, 0f);
        Vector3 v2 = new Vector3(length, tailHalf, 0f);
        Vector3 v3 = new Vector3(length, -tailHalf, 0f);

        var vertices = new Vector3[]
        {
            v0, v1, v2, v3
        };

        var triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3
        };

        // UV 隨便拉一個矩形，之後要做貼圖再調也行
        var uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };

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
        mesh = new Mesh();
        mesh.name = "SweepMesh";
        mf.mesh = mesh;

        // 一開始給個最小形狀
        UpdateShape(0f);
    }
}
