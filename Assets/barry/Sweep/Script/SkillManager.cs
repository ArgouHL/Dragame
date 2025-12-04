using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class SkillManager : MonoBehaviour
{
    [Header("小掃設定")]
    [SerializeField] private LayerMask trashLayer;

    [Header("右鍵蓄力掃視覺")]
    [SerializeField] private Transform chargedSweepRoot;
    [SerializeField] private DynamicSweepMesh sweepMesh;
    [SerializeField] private PolygonCollider2D sweepCollider;

    [Header("右鍵蓄力掃參數")]
    [SerializeField] private float minForceMultiplier = 1f;
    [SerializeField] private float maxForceMultiplier = 3f;

    private PlayerController player;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        if (chargedSweepRoot != null)
        {
            if (!sweepMesh)
                sweepMesh = chargedSweepRoot.GetComponentInChildren<DynamicSweepMesh>(true);
            if (!sweepCollider)
                sweepCollider = chargedSweepRoot.GetComponentInChildren<PolygonCollider2D>(true);

            if (sweepCollider != null)
                sweepCollider.isTrigger = true;

            chargedSweepRoot.gameObject.SetActive(true);
        }
    }

    private void OnEnable()
    {
        player.OnSweepMove += HandleSweepMove;
        player.OnChargedSweepUpdate += HandleChargedSweepUpdate;
        player.OnChargedSweepReleased += HandleChargedSweepReleased;
    }

    private void OnDisable()
    {
        player.OnSweepMove -= HandleSweepMove;
        player.OnChargedSweepUpdate -= HandleChargedSweepUpdate;
        player.OnChargedSweepReleased -= HandleChargedSweepReleased;
    }

    // ------------------- 小掃處理 -------------------
    private void HandleSweepMove(Vector2 center, float radius, Vector2 moveDir)
    {
        var hits = Physics2D.OverlapCircleAll(center, radius, trashLayer);
        foreach (var hit in hits)
        {
            var trash = hit.GetComponent<BaseTrash>();
            if (trash == null) continue;
            trash.ApplyBroomHit(moveDir);
        }
    }

    // ------------------- 蓄力掃視覺更新 -------------------
    private void HandleChargedSweepUpdate(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (chargedSweepRoot == null || sweepMesh == null || sweepCollider == null)
            return;

        chargedSweepRoot.position = origin;
        chargedSweepRoot.right = dir;

        sweepMesh.UpdateShape(t);
        Vector2[] path = sweepMesh.CurrentPath2D;
        if (path == null || path.Length < 3) return;

        sweepCollider.pathCount = 1;
        sweepCollider.SetPath(0, path);
        sweepCollider.isTrigger = true;

        chargedSweepRoot.gameObject.SetActive(true);
    }

    // ------------------- 蓄力掃實際打擊 -------------------
    private void HandleChargedSweepReleased(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (sweepCollider == null)
            return;

        float forceMul = Mathf.Lerp(minForceMultiplier, maxForceMultiplier, t);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = trashLayer,
            useTriggers = true
        };

        Collider2D[] results = new Collider2D[32];
        int count = sweepCollider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            if (results[i] == null) continue;
            var trash = results[i].GetComponent<BaseTrash>();
            if (trash == null) continue;

            Vector2 itemPos = results[i].transform.position;
            Vector2 radialDir = itemPos - origin;
            if (radialDir.sqrMagnitude < 0.0001f) continue;
            radialDir.Normalize();

            // 第二個參數如果你本來就拿來當「蓄力秒數」用，可以直接用 holdTime
            trash.ApplyBroomHit(radialDir * forceMul, holdTime);
        }

        if (chargedSweepRoot != null)
            chargedSweepRoot.gameObject.SetActive(false);
    }
}
