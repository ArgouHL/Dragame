using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BroomMode
{
    Impact,
    Sticky
}

public class PlayerController : MonoBehaviour, IAbsorbable
{
    public static PlayerController instance { get; private set; }

    [Header("=== 外部依賴 ===")]
    [SerializeField] public PlayerEffectManager effectManager;
    [SerializeField] private Transform witchAnchor;

    [Header("=== 基礎組件 ===")]
    public Vector2 fixForWall;
    public Rigidbody2D rb;
    private Camera cam;
    private PlayerInput input;

    // Events
    public event Action<Vector2, float, Vector2, float> OnSweepMove;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepUpdate;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepReleased;
    public event Action<BroomMode> OnModeChanged;
    // [優化] 新增玩家被黑洞吸入的專用事件，用於解耦通知其他系統（如 SkillManager 交出垃圾）
    public event Action<BlackHoleObstacle> OnAbsorbedByBlackHole;

    // Input Actions
    private InputAction pointerPress;
    private InputAction rightPointerPress;
    private InputAction pointerPosition;
    private InputAction switchModeAction;

    // State Variables
    private Vector2 dragStart;
    private Vector2 moveDir;
    private float currentSpeed;
    private Vector2 chargedDir;
    private float rightPressStartTime;

    private bool isLeftDown = false;
    private bool isRightDown = false;
    private bool blockBoth = false;
    public bool isBeingAbsorbed;
    public bool isBlocking = false;

    // [Fix] 防止噴出瞬間再次被吸入的冷卻時間
    private float _absorbCooldown = 0f;

    // [New] 記錄當前所在的黑洞 Collider
    private Collider2D _currentBlackHoleCollider;

    [Header("=== 魔女連結設定 ===")]
    [SerializeField] private float maxLeashRadius = 4f;

    [Header("=== 模式設定 ===")]
    public BroomMode currentMode = BroomMode.Impact;

    [Header("=== 移動參數 ===")]
    public float maxSpeed = 12f;
    public float deceleration = 8f;

    [Header("=== Sticky Weight Settings (重量系統) ===")]
    [SerializeField, Tooltip("每單位重量減少多少速度比例 (例如 0.1 代表每 1kg 速度除以 1.1)")]
    private float weightPenaltyFactor = 0.5f;
    [SerializeField, Tooltip("就算再重，速度也不會低於此值")]
    private float minStickySpeed = 2f;

    // 當前黏在掃把上的總重量
    private float _currentStickyLoad = 0f;

    [Header("=== 基礎參數 ===")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;
    public float maxChargeTime = 1.5f;
    [SerializeField] private float chargeCenterOffset = 0f;

    [Header("=== 碰撞反應參數 (物理反饋) ===")]
    [SerializeField, Min(0.01f)] private float wieght = 1f;
    [SerializeField, Range(0f, 1f)] private float sweepRestitution = 0f;
    [SerializeField] private bool allowBounceBack = false;
    [SerializeField] private bool scaleHitBySweepPower = true;
    [SerializeField, Range(0f, 1f)] private float minHitPower = 0.25f;
    [SerializeField, Min(0f)] private float hitWeightScale = 1f;

    public float CurrentSpeed => currentSpeed;
    public float Wieght => wieght;

    private Collider2D[] colliders;

    public Vector2 GetSweepCenter() => (Vector2)transform.position + sweepOffset;

    // --- 介面實作開始 ---
    public bool CanBeAbsorbed => !isBeingAbsorbed && _absorbCooldown <= 0f;

    public void OnAbsorbStart(BlackHoleObstacle blackHole)
    {
        if (isBeingAbsorbed || _absorbCooldown > 0f) return;

        Collider2D incomingCollider = blackHole.GetComponent<Collider2D>();
        if (incomingCollider == _currentBlackHoleCollider) return;

        _currentBlackHoleCollider = incomingCollider;
        isBeingAbsorbed = true;

        // 重置狀態
        isLeftDown = false;
        isRightDown = false;
        _currentStickyLoad = 0f;

        effectManager.HideDragLine();
        effectManager.HideChargeSweep();

        if (PlayerAnimatorController.instance != null)
        {
            PlayerAnimatorController.instance.stateMachine.ChangeState(PlayerAnimatorController.instance.idleState);
        }

        BlockBoth();
        SetCollidersEnabled(false);

        blackHole.RegisterPlayer(this);

        // [核心邏輯] 觸發黑洞吸入事件，通知 SkillManager 轉移身上的垃圾
        OnAbsorbedByBlackHole?.Invoke(blackHole);
    }
    // --- 介面實作結束 ---

    // [New] 由 SkillManager 呼叫，更新當前負重
    public void SetStickyLoad(float totalWeight)
    {
        _currentStickyLoad = totalWeight;
    }

    // [New] 計算負重後的實際最高速度
    private float GetEffectiveMaxSpeed()
    {
        if (currentMode != BroomMode.Sticky || _currentStickyLoad <= 0.001f)
            return maxSpeed;

        float penaltyDivisor = 1f + (_currentStickyLoad * weightPenaltyFactor);
        float penalizedSpeed = maxSpeed / penaltyDivisor;

        return Mathf.Max(penalizedSpeed, minStickySpeed);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_currentBlackHoleCollider != null && other == _currentBlackHoleCollider)
        {
            _currentBlackHoleCollider = null;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        if (effectManager == null) effectManager = GetComponent<PlayerEffectManager>();

        if (witchAnchor != null) input = witchAnchor.GetComponent<PlayerInput>();
        if (input == null) input = GetComponent<PlayerInput>();
        if (input == null) { Debug.LogError("Missing PlayerInput!"); return; }

        pointerPress = input.actions["PointerPress"];
        rightPointerPress = input.actions["RightPointerPress"];
        pointerPosition = input.actions["PointerPosition"];
        if (input.actions.FindAction("SwitchMode") != null) switchModeAction = input.actions["SwitchMode"];
    }

    private void OnEnable()
    {
        if (input == null) return;
        pointerPress.started += OnPress;
        pointerPress.canceled += OnRelease;
        rightPointerPress.started += OnRightPress;
        rightPointerPress.canceled += OnRightRelease;
        if (switchModeAction != null) switchModeAction.performed += OnSwitchModeInput;
    }

    private void OnDisable()
    {
        if (input == null) return;
        pointerPress.started -= OnPress;
        pointerPress.canceled -= OnRelease;
        rightPointerPress.started -= OnRightPress;
        rightPointerPress.canceled -= OnRightRelease;
        if (switchModeAction != null) switchModeAction.performed -= OnSwitchModeInput;
    }

    private void Update()
    {
        if (_absorbCooldown > 0f) _absorbCooldown -= Time.deltaTime;

        if (isBeingAbsorbed || pointerPosition == null) return;

        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 center = GetSweepCenter();

        if (pointerPress.IsPressed() && !blockBoth)
        {
            Vector2 drag = pointerWorld - dragStart;
            float len = drag.magnitude;

            float effectiveMax = GetEffectiveMaxSpeed();
            float maxVisualLen = effectiveMax / 3f;

            Vector2 visualDrag = (len > 0.001f) ? (drag / len) * Mathf.Min(len, maxVisualLen) : Vector2.zero;
            effectManager.UpdateDragLine(center, center + visualDrag);
        }

        if (isRightDown && !blockBoth)
        {
            float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
            float t = Mathf.Clamp01(hold / maxChargeTime);
            Vector2 toPointer = pointerWorld - center;
            if (toPointer.sqrMagnitude > 0.0001f) chargedDir = toPointer.normalized;
            Vector2 origin = center + chargedDir * chargeCenterOffset;

            OnChargedSweepUpdate?.Invoke(hold, t, origin, chargedDir);
            effectManager.ShowChargeSweep(origin, chargedDir, t);
        }
        else
        {
            effectManager.HideChargeSweep();
        }
    }

    private void FixedUpdate()
    {
        if (isBlocking || isBeingAbsorbed)
        {
            rb.linearVelocity = Vector2.zero;
            effectManager.StopTrail();
            ApplyLeashConstraint();
            return;
        }

        Vector2 center = GetSweepCenter();
        Vector2 velocityVector = Vector2.zero;
        float sweepPower = 0f;

        if (currentSpeed > 0.01f)
        {
            velocityVector = moveDir * currentSpeed;
            Vector2 nextPos = rb.position + velocityVector * Time.fixedDeltaTime;
            nextPos += fixForWall;

            if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(nextPos))
            {
                if (TryGetWallHitFromWorldBounds(nextPos, moveDir, out var hitPoint, out var hitNormal))
                    effectManager.PlayWallHit(hitPoint, hitNormal);
                else
                    effectManager.PlayWallHit(nextPos, -moveDir);

                currentSpeed = 0f;
                rb.linearVelocity = Vector2.zero;
                effectManager.StopTrail();
                ApplyLeashConstraint();
                return;
            }

            rb.linearVelocity = velocityVector;

            float damping = 1f - (deceleration * Time.fixedDeltaTime);
            currentSpeed *= Mathf.Clamp01(damping);
            if (currentSpeed < 0.05f) currentSpeed = 0f;

            float effectiveMax = GetEffectiveMaxSpeed();
            sweepPower = Mathf.Clamp01(currentSpeed / effectiveMax);
            effectManager.UpdateTrail(center, moveDir, sweepPower, true);

            OnSweepMove?.Invoke(center, sweepRadius, moveDir, sweepPower);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            effectManager.StopTrail();
        }

        ApplyLeashConstraint();
    }

    private void OnSwitchModeInput(InputAction.CallbackContext ctx)
    {
        currentMode = (currentMode == BroomMode.Impact) ? BroomMode.Sticky : BroomMode.Impact;
        _currentStickyLoad = 0f;
        OnModeChanged?.Invoke(currentMode);
    }

    private void ApplyLeashConstraint()
    {
        if (witchAnchor == null) return;
        Vector2 witchPos = witchAnchor.position;
        Vector2 currentPos = rb.position;
        Vector2 relPos = currentPos - witchPos;
        Vector2 clampedRelPos = Vector2.ClampMagnitude(relPos, maxLeashRadius);
        if (relPos != clampedRelPos) rb.position = witchPos + clampedRelPos;
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        isLeftDown = true;
        if (isRightDown) { BlockBoth(); return; }

        effectManager.StopTrail();
        dragStart = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
        Vector2 c = GetSweepCenter();
        effectManager.UpdateDragLine(c, c);
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        isLeftDown = false;
        if (isBeingAbsorbed) return;

        if (blockBoth)
        {
            if (!isRightDown) UnblockBoth();
            return;
        }

        effectManager.HideDragLine();
        Vector2 drag = ScreenToWorld(pointerPosition.ReadValue<Vector2>()) - dragStart;
        float len = drag.magnitude;
        if (len >= 0.05f)
        {
            moveDir = drag / len;
            float effectiveMax = GetEffectiveMaxSpeed();
            currentSpeed = Mathf.Min(len * 3f, effectiveMax);
            effectManager.StartFollowParticle(transform);
        }
    }

    private void OnRightPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        PlayerAnimatorController.instance?.stateMachine.ChangeState(PlayerAnimatorController.instance.powerState);
        rb.linearVelocity = Vector2.zero;

        isRightDown = true;
        rightPressStartTime = Time.time;
        effectManager.StopTrail();

        if (isLeftDown) { BlockBoth(); return; }
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        isRightDown = false;
        if (isBeingAbsorbed) return;

        if (blockBoth)
        {
            if (!isLeftDown) UnblockBoth();
            return;
        }

        float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
        float t = Mathf.Clamp01(hold / maxChargeTime);
        Vector2 p = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 c = GetSweepCenter();
        Vector2 dir = p - c;

        if (PlayerAnimatorController.instance != null)
        {
            PlayerAnimatorController.instance.anim.SetFloat("ClickX", dir.x);
            PlayerAnimatorController.instance.anim.SetFloat("ClickY", dir.y);
            PlayerAnimatorController.instance.stateMachine.ChangeState(PlayerAnimatorController.instance.releaseState);
        }

        if (dir.sqrMagnitude > 0.0001f) chargedDir = dir.normalized;
        Vector2 origin = c + chargedDir * chargeCenterOffset;

        OnChargedSweepReleased?.Invoke(hold, t, origin, chargedDir);
        effectManager.HideChargeSweep();
    }

    private void BlockBoth()
    {
        blockBoth = true; rb.linearVelocity = Vector2.zero; currentSpeed = 0f; effectManager.StopAllEffects();
    }

    private void UnblockBoth()
    {
        blockBoth = false;
    }

    public void EmitTrashHit(Vector2 hitPoint, Vector2 hitNormal) { effectManager.PlayTrashHit(hitPoint, hitNormal); }

    public void ExitBlackHole(Vector2 ejectDir, float ejectSpeed)
    {
        _absorbCooldown = 1.0f;
        isBeingAbsorbed = false;
        blockBoth = false;
        isBlocking = false;
        isLeftDown = false;
        isRightDown = false;
        _currentStickyLoad = 0f;

        moveDir = ejectDir;
        currentSpeed = ejectSpeed;
        SetCollidersEnabled(true);
        rb.linearVelocity = moveDir * currentSpeed;
        effectManager.HideDragLine();
        effectManager.HideChargeSweep();
    }

    public void ApplyHitSlowdown(float totalTrashWieght, float sweepPower01)
    {
        if (currentSpeed <= 0f || totalTrashWieght <= 0f || currentMode == BroomMode.Sticky) return;
        float M = Mathf.Max(0.01f, wieght);
        float p = scaleHitBySweepPower ? Mathf.Lerp(minHitPower, 1f, Mathf.Clamp01(sweepPower01)) : 1f;
        float m = Mathf.Max(0f, totalTrashWieght) * hitWeightScale * p;
        if (m <= 0f) return;
        float e = Mathf.Clamp01(sweepRestitution);
        float ratio = (M - e * m) / (M + m);
        if (!allowBounceBack) currentSpeed *= Mathf.Clamp01(ratio);
        else { if (ratio < 0f) { moveDir = -moveDir; currentSpeed *= Mathf.Clamp01(-ratio); } else { currentSpeed *= Mathf.Clamp01(ratio); } }
        if (currentSpeed <= 0.01f) { currentSpeed = 0f; rb.linearVelocity = Vector2.zero; effectManager.StopTrail(); } else { rb.linearVelocity = moveDir * currentSpeed; }
    }

    private bool TryGetWallHitFromWorldBounds(Vector2 worldPos, Vector2 moveDir, out Vector2 hitPoint, out Vector2 hitNormal)
    {
        hitPoint = worldPos; hitNormal = -moveDir;
        var wb = WorldBounds2D.Instance;
        if (wb == null) return false;
        return wb.TryGetHitPointAndNormalWorld(worldPos, out hitPoint, out hitNormal);
    }
    private Vector2 ScreenToWorld(Vector2 p) => cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));
    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null) colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = enabled;
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (witchAnchor != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(witchAnchor.position, maxLeashRadius); Gizmos.DrawLine(witchAnchor.position, transform.position); }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetSweepCenter(), sweepRadius);
    }
#endif
}