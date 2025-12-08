using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class SkillManager : MonoBehaviour
{
    [Header("小掃設定")]
    [SerializeField] private LayerMask trashLayer;
    [SerializeField] private float minSweepForce;
    [SerializeField] private float maxSweepForce;

    [Header("右鍵蓄力掃視覺")]
    [SerializeField] private Transform chargedSweepRoot;
    [SerializeField] private DynamicSweepMesh sweepMesh;
    [SerializeField] private PolygonCollider2D sweepCollider;

    [Header("右鍵蓄力掃參數")]
    [SerializeField] private float minForceMultiplier;
    [SerializeField] private float maxForceMultiplier;
    [SerializeField] private float chargedPowerExponent = 1f;

    private readonly Collider2D[] smallSweepResults = new Collider2D[32];
    private readonly HashSet<BaseTrash> sweepHitTrash = new HashSet<BaseTrash>();

    private PlayerController player;
    private ContactFilter2D smallSweepFilter;

    private void Awake()
    {
        smallSweepFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = trashLayer,
            useTriggers = true
        };

        player = GetComponent<PlayerController>();

        if (chargedSweepRoot != null)
        {
            if (!sweepMesh) sweepMesh = chargedSweepRoot.GetComponentInChildren<DynamicSweepMesh>(true);
            if (!sweepCollider) sweepCollider = chargedSweepRoot.GetComponentInChildren<PolygonCollider2D>(true);
            if (sweepCollider) sweepCollider.isTrigger = true;
            chargedSweepRoot.gameObject.SetActive(false);
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

    private void HandleSweepMove(Vector2 center, float radius, Vector2 moveDir, float power01)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;

        float curve = power01 * power01;
        float power = Mathf.Lerp(minSweepForce, maxSweepForce, curve);

        sweepHitTrash.Clear();
        int count = Physics2D.OverlapCircle(center, radius, smallSweepFilter, smallSweepResults);

        for (int i = 0; i < count; i++)
        {
            var col = smallSweepResults[i];
            if (!col) continue;
            var trash = col.GetComponent<BaseTrash>();
            if (trash && sweepHitTrash.Add(trash))
                trash.ApplyBroomHit(moveDir, power);
        }

        if (sweepHitTrash.Count > 0)
            player.ApplyHitSlowdown(sweepHitTrash.Count, power01);
    }

    private void HandleChargedSweepUpdate(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (chargedSweepRoot == null || sweepMesh == null || sweepCollider == null) return;

        chargedSweepRoot.position = origin;
        chargedSweepRoot.right = dir;
        sweepMesh.UpdateShape(t);

        var path = sweepMesh.CurrentPath2D;
        if (path != null && path.Length >= 3)
        {
            sweepCollider.pathCount = 1;
            sweepCollider.SetPath(0, path);
        }

        chargedSweepRoot.gameObject.SetActive(true);
    }

    private void HandleChargedSweepReleased(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (sweepCollider == null) return;

        float curve = Mathf.Pow(t, chargedPowerExponent);
        float forceMul = Mathf.Lerp(minForceMultiplier, maxForceMultiplier, curve);

        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = trashLayer, useTriggers = true };
        Collider2D[] results = new Collider2D[32];
        int count = sweepCollider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            if (!results[i]) continue;
            var trash = results[i].GetComponent<BaseTrash>();
            if (!trash) continue;

            Vector2 radialDir = ((Vector2)results[i].transform.position - origin);
            if (radialDir.sqrMagnitude < 0.0001f) continue;
            radialDir.Normalize();

            trash.ApplyBroomHit(radialDir, forceMul);
        }

        chargedSweepRoot.gameObject.SetActive(false);
    }
}