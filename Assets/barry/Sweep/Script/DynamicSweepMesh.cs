using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicSweepMesh : MonoBehaviour
{
    private Mesh mesh;
    private MeshRenderer meshRenderer;

    [Header("貼圖/材質")]
    [SerializeField] private Texture2D sweepTexture;     // 拖你的 FX(2).png
    [SerializeField] private Material overrideMaterial;  // 可選：你自訂材質
    [SerializeField] private bool createMaterialIfNull = true;

    [Header("渲染排序(2D 常用)")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    [Header("扇形半徑設定")]
    public float minRadius = 1f;
    public float maxRadius = 3f;

    [Header("弧線 Segments")]
    [Range(2, 64)]
    public int arcSegments = 12;

    [Header("扇形角度設定")]
    public float minArcAngle = 30f;
    public float maxArcAngle = 90f;

    [Header("形狀旋轉 (度)")]
    [Tooltip("0=朝上(+Y)。你若想讓扇形預設朝右，就設 -90。")]
    public float shapeRotationDeg = 0f;

    public float CurrentRadius { get; private set; }
    public Vector2[] CurrentPath2D { get; private set; }

    public void UpdateShape(float t)
    {
        if (mesh == null) return;

        t = Mathf.Clamp01(t);
        float radius = Mathf.Lerp(minRadius, maxRadius, t);

        float arcDeg = Mathf.Lerp(minArcAngle, maxArcAngle, t);
        float halfRad = arcDeg * Mathf.Deg2Rad * 0.5f;

        CurrentRadius = radius;

        int seg = Mathf.Max(2, arcSegments);
        int vertexCount = 1 + (seg + 1);

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] path2D = new Vector2[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];

        // Apex
        vertices[0] = Vector3.zero;
        path2D[0] = Vector2.zero;
        uvs[0] = new Vector2(0.5f, 1f); // 貼圖尖端在上方中央

        // 用來把左右邊界正規化到 u=0..1
        float maxX = Mathf.Sin(halfRad) * radius;
        if (maxX < 0.0001f) maxX = 0.0001f;

        float rot = shapeRotationDeg * Mathf.Deg2Rad;
        float cosR = Mathf.Cos(rot);
        float sinR = Mathf.Sin(rot);

        // 弧線點：維持你原本「從上到下」的順序（half -> -half）
        for (int i = 0; i <= seg; i++)
        {
            float tt = i / (float)seg;
            float angle = Mathf.Lerp(halfRad, -halfRad, tt);

            // 讓扇形「預設朝上(+Y)」
            // angle=0 => (0, radius)
            float x = Mathf.Sin(angle) * radius;
            float y = Mathf.Cos(angle) * radius;

            // 再套一個可調旋轉
            float rx = x * cosR - y * sinR;
            float ry = x * sinR + y * cosR;

            int idx = 1 + i;
            vertices[idx] = new Vector3(rx, ry, 0f);
            path2D[idx] = new Vector2(rx, ry);

            // UV：用幾何本身去算（避免弧線被壓成直線）
            // u：左右邊界映射到 0..1
            float u = 0.5f + (rx / (2f * maxX));

            // v：尖端(0) -> 1，外圈(y≈radius) -> 0
            // 這樣外圈的弧形會自然在 UV 裡形成弧形，不會拉扯
            float v = 1f - (ry / radius);

            uvs[idx] = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        CurrentPath2D = path2D;

        int triCount = seg;
        int[] triangles = new int[triCount * 3];
        int ti = 0;
        for (int i = 0; i < seg; i++)
        {
            triangles[ti++] = 0;
            triangles[ti++] = i + 1;
            triangles[ti++] = i + 2;
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
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh { name = "SweepMesh" };
        mesh.MarkDynamic();
        mf.mesh = mesh;

        SetupRendererMaterial();
        UpdateShape(0f);
    }

    private void SetupRendererMaterial()
    {
        if (meshRenderer == null) return;

        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;

        if (sweepTexture != null)
        {
            // 避免 UV clamp 後還因 wrap/repeat 抽到怪邊
            sweepTexture.wrapMode = TextureWrapMode.Clamp;
            sweepTexture.filterMode = FilterMode.Bilinear;
        }

        if (overrideMaterial != null)
        {
            meshRenderer.material = overrideMaterial;
            if (sweepTexture != null) meshRenderer.material.mainTexture = sweepTexture;
            return;
        }

        if (!createMaterialIfNull) return;

        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Transparent");

        var mat = new Material(shader) { name = "SweepMesh_Mat(Auto)" };
        if (sweepTexture != null) mat.mainTexture = sweepTexture;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        meshRenderer.material = mat;
    }
}
