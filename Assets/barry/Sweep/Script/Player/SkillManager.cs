using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class SkillManager : MonoBehaviour
{
    [Header("=== Impact Mode (小掃) ===")]
    [SerializeField] private LayerMask trashLayer;
    [SerializeField] private float minSweepForce;
    [SerializeField] private float maxSweepForce;

    [Header("=== Sticky Mode (磁鐵) ===")]
    [SerializeField, Tooltip("磁鐵模式下的判定範圍倍率")] private float magnetRadiusMultiplier = 1.2f;

    [Header("=== Charged Mode (右鍵蓄力) ===")]
    [SerializeField] private Transform chargedSweepRoot;
    [SerializeField] private DynamicSweepMesh sweepMesh;
    [SerializeField] private PolygonCollider2D sweepCollider;
    [SerializeField] private float minForceMultiplier;
    [SerializeField] private float maxForceMultiplier;
    [SerializeField] private float chargedPowerExponent = 1f;

    private readonly Collider2D[] smallSweepResults = new Collider2D[32];
    private readonly Collider2D[] chargedSweepResults = new Collider2D[64];
    private readonly HashSet<BaseTrash> sweepHitTrash = new HashSet<BaseTrash>();

    private readonly HashSet<BaseTrash> capturedTrash = new HashSet<BaseTrash>();
    private readonly List<BaseTrash> trashToRemove = new List<BaseTrash>();
    private readonly List<BaseTrash> nearbyTrashBuffer = new List<BaseTrash>();

    private PlayerController player;
    private ContactFilter2D trashFilter;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        trashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = trashLayer,
            useTriggers = true
        };
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
        if (player == null) return;
        player.OnSweepMove += HandleSweepMove;
        player.OnChargedSweepUpdate += HandleChargedSweepUpdate;
        player.OnChargedSweepReleased += HandleChargedSweepReleased;
        player.OnModeChanged += HandleModeChanged;
        player.OnAbsorbedByBlackHole += HandleAbsorbedByBlackHole;
    }

    private void OnDisable()
    {
        if (player == null) return;
        player.OnSweepMove -= HandleSweepMove;
        player.OnChargedSweepUpdate -= HandleChargedSweepUpdate;
        player.OnChargedSweepReleased -= HandleChargedSweepReleased;
        player.OnModeChanged -= HandleModeChanged;
        player.OnAbsorbedByBlackHole -= HandleAbsorbedByBlackHole;
        ReleaseAllCapturedTrash();
    }

    private void FixedUpdate()
    {
        if (player == null || player.isBeingAbsorbed) return;

        if (player.currentMode == BroomMode.Sticky)
        {
            Vector2 center = player.GetSweepCenter();
            ScanForNewStickyTrash(center);
            Vector2 velocity = player.rb.linearVelocity;
            UpdateCapturedTrashPosition(center, velocity);
        }
    }

    private void HandleAbsorbedByBlackHole(BlackHoleObstacle blackHole)
    {
        if (capturedTrash.Count == 0) return;

        foreach (var trash in capturedTrash)
        {
            if (trash != null && trash.gameObject.activeInHierarchy)
            {
                trash.ReleaseMagnetHold();
                blackHole.RegisterTrash(trash);
            }
        }

        capturedTrash.Clear();
        player.SetStickyLoad(0f);
    }

    private void HandleSweepMove(Vector2 center, float radius, Vector2 moveDir, float power01)
    {
        if (moveDir.sqrMagnitude < 0.0001f || player.currentMode == BroomMode.Sticky) return;

        float curve = power01 * power01;
        float power = Mathf.Lerp(minSweepForce, maxSweepForce, curve);
        sweepHitTrash.Clear();

        float totalWeight = 0f;
        int count = Physics2D.OverlapCircle(center, radius, trashFilter, smallSweepResults);

        for (int i = 0; i < count; i++)
        {
            var col = smallSweepResults[i];
            if (!col) continue;
            if (col.TryGetComponent(out BaseTrash trash))
            {
                if (sweepHitTrash.Add(trash))
                {
                    trash.ApplyBroomHit(moveDir, power);
                    totalWeight += trash.Weight;
                }
            }
        }
        if (totalWeight > 0f) player.ApplyHitSlowdown(totalWeight, power01);
    }

    private void ScanForNewStickyTrash(Vector2 center)
    {
        nearbyTrashBuffer.Clear();
        float catchRadius = player.sweepRadius * magnetRadiusMultiplier;
        float sqrCatchRadius = catchRadius * catchRadius;
        SpatialGridManager.Instance.GetTrashAroundPosition(center, nearbyTrashBuffer);

        for (int i = 0; i < nearbyTrashBuffer.Count; i++)
        {
            var trash = nearbyTrashBuffer[i];
            if (trash == null || trash.IsAbsorbing) continue;
            if (capturedTrash.Contains(trash)) continue;
            if (((Vector2)trash.transform.position - center).sqrMagnitude <= sqrCatchRadius)
            {
                capturedTrash.Add(trash);
                trash.ApplyMagnetHold(center, Vector2.zero);
            }
        }
    }

    private void UpdateCapturedTrashPosition(Vector2 center, Vector2 velocity)
    {
        float currentTotalWeight = 0f;

        if (capturedTrash.Count == 0)
        {
            player.SetStickyLoad(0f);
            return;
        }

        trashToRemove.Clear();
        foreach (var trash in capturedTrash)
        {
            if (trash == null || !trash.gameObject.activeInHierarchy || trash.IsAbsorbing)
            {
                trashToRemove.Add(trash);
                continue;
            }
            trash.ApplyMagnetHold(center, velocity);
            currentTotalWeight += trash.Weight;
        }

        if (trashToRemove.Count > 0)
        {
            foreach (var t in trashToRemove) capturedTrash.Remove(t);
        }

        player.SetStickyLoad(currentTotalWeight);
    }

    private void ReleaseAllCapturedTrash()
    {
        foreach (var trash in capturedTrash)
        {
            if (trash != null) trash.ReleaseMagnetHold();
        }
        capturedTrash.Clear();
        if (player != null) player.SetStickyLoad(0f);
    }

    private void HandleModeChanged(BroomMode newMode)
    {
        if (newMode != BroomMode.Sticky) ReleaseAllCapturedTrash();
    }

    private void HandleChargedSweepUpdate(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (chargedSweepRoot == null) return;
        chargedSweepRoot.gameObject.SetActive(true);
        chargedSweepRoot.position = origin;
        chargedSweepRoot.right = dir;
        if (sweepMesh) sweepMesh.UpdateShape(t);
        if (sweepMesh && sweepCollider)
        {
            var path = sweepMesh.CurrentPath2D;
            if (path != null && path.Length >= 3)
            {
                sweepCollider.pathCount = 1;
                sweepCollider.SetPath(0, path);
            }
        }
    }

    private void HandleChargedSweepReleased(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        if (sweepCollider == null) return;

        int count = sweepCollider.Overlap(trashFilter, chargedSweepResults);
        if (chargedSweepRoot != null) chargedSweepRoot.gameObject.SetActive(false);

        // [重點註釋] 根據當前模式切分邏輯，確保右鍵蓄力在不同技能下有對應的效果
        if (player.currentMode == BroomMode.Impact)
        {
            // 原有邏輯：打飛敵人
            float curve = Mathf.Pow(t, chargedPowerExponent);
            float forceMul = Mathf.Lerp(minForceMultiplier, maxForceMultiplier, curve);

            for (int i = 0; i < count; i++)
            {
                var col = chargedSweepResults[i];
                if (!col) continue;

                if (col.TryGetComponent(out BaseTrash trash))
                {
                    Vector2 radialDir = ((Vector2)trash.transform.position - origin);
                    if (radialDir.sqrMagnitude < 0.0001f) radialDir = dir;
                    else radialDir.Normalize();

                    trash.ApplyBroomHit(radialDir, forceMul);
                }
            }
        }
        else if (player.currentMode == BroomMode.Sticky)
        {
            // [重點註釋] 磁鐵模式邏輯：強制將蓄力範圍內的垃圾納入吸附列隊，實現大範圍吸取
            Vector2 sweepCenter = player.GetSweepCenter();

            for (int i = 0; i < count; i++)
            {
                var col = chargedSweepResults[i];
                if (!col) continue;

                if (col.TryGetComponent(out BaseTrash trash))
                {
                    // 排除無效、正在被黑洞吸、或已經在吸附名單中的垃圾
                    if (trash == null || trash.IsAbsorbing || capturedTrash.Contains(trash)) continue;

                    capturedTrash.Add(trash);
                    trash.ApplyMagnetHold(sweepCenter, Vector2.zero);
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (player != null && player.currentMode == BroomMode.Sticky)
        {
            Gizmos.color = Color.green;
            float radius = player.sweepRadius * magnetRadiusMultiplier;
            Gizmos.DrawWireSphere(player.GetSweepCenter(), radius);
        }
    }
#endif
}