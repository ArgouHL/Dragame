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

    [Header("=== 體驗優化 (防黏滯與疊音) ===")]
    [SerializeField, Tooltip("被黑洞吐出後的無敵冷卻時間 (秒)")]
    private float absorbInvincibleTime = 1.5f;
    [SerializeField, Tooltip("全局特效與音效防疊加冷卻 (秒)")]
    private float vfxHitCooldown = 0.1f;
    private float _vfxCooldownTimer = 0f;

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
    public event Action<BlackHoleObstacle> OnAbsorbedByBlackHole;
    public event Action OnLeftPressAction;
    public event Action OnLeftReleaseAction;
    public event Action OnRightPressStart;
    public event Action OnWallHitEvent;
    public event Action<TrashType> OnTrashHitEvent;
    public event Action<float> OnScaleChanged;

    // [重點註釋] 新增冷卻更新事件供 UI 訂閱
    public event Action<float, float> OnRightSkillCooldownUpdate;

    // Input Actions
    private InputAction pointerPress;
    private InputAction rightPointerPress;
    private InputAction pointerPosition;
    private InputAction switchModeAction;

    private InputAction scrollYAction;
    private InputAction middleClickAction;

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
    private float _absorbCooldown = 0f;
    private Collider2D _currentBlackHoleCollider;

    [Header("=== 模式與冷卻設定 ===")]
    public BroomMode currentMode = BroomMode.Impact;
    [SerializeField, Tooltip("切換技能的冷卻時間，防止滾輪一次滾動觸發多次切換造成狂閃")]
    private float switchCooldown = 0.2f;
    private float _lastSwitchTime = 0f;

    [SerializeField, Tooltip("右鍵蓄力技能的冷卻時間(秒)")]
    private float rightSkillCooldown = 5f;
    private float _currentRightSkillCooldown = 0f;

    [Header("=== 移動參數 ===")]
    public float maxSpeed = 12f;
    public float deceleration = 8f;

    [Header("=== Sticky Weight Settings (重量系統) ===")]
    [SerializeField, Tooltip("每單位重量減少多少速度比例")]
    private float weightPenaltyFactor = 0.5f;
    [SerializeField, Tooltip("就算再重，速度也不會低於此值")]
    private float minStickySpeed = 2f;
    private float _currentStickyLoad = 0f;

    [Header("=== 基礎判定參數 ===")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;
    public float maxChargeTime = 1.5f;
    [SerializeField] private float chargeCenterOffset = 0f;

    [Header("=== 判定容錯設定 (解決靠牆死角) ===")]
    [SerializeField] public float sweepForgivenessMultiplier = 1.25f;
    [SerializeField] private float forwardReachOffset = 0.5f;

    [Header("=== 碰撞反應參數 (物理反饋) ===")]
    [SerializeField, Min(0.01f)] private float wieght = 1f;
    [SerializeField, Range(0f, 1f)] private float sweepRestitution = 0f;
    [SerializeField] private bool allowBounceBack = false;
    [SerializeField] private bool scaleHitBySweepPower = true;
    [SerializeField, Range(0f, 1f)] private float minHitPower = 0.25f;
    [SerializeField, Min(0f)] private float hitWeightScale = 1f;

    [Header("=== 成長與相機連動 ===")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 15f, -15f);
    [SerializeField] private float growthSyncRatio = 1f;
    [SerializeField] private float growthLerpSpeed = 3f;
    [SerializeField, Min(0f), Tooltip("鏡頭跟隨的平滑時間。越小越靈敏，越大越穩。")]
    private float cameraFollowSmoothTime = 0.18f;
    [SerializeField, Min(0f), Tooltip("鏡頭前視偏移的平滑時間。")]
    private float cameraLookAheadSmoothTime = 0.12f;
    [SerializeField, Min(0f), Tooltip("鏡頭前視偏移距離。")]
    private float cameraLookAheadDistance = 1.5f;
    [SerializeField, Min(0f), Tooltip("低於這個速度時，不再產生前視偏移，避免抖動。")]
    private float cameraLookAheadSpeedThreshold = 0.15f;

    private Vector3 _baseCameraOffset;
    private Vector3 _baseScale;

    private float _baseMaxSpeed;
    private float _baseSweepRadius;
    private Vector2 _baseSweepOffset;
    private float _baseWeight;
    private float _baseChargeCenterOffset;
    private float _baseForwardReachOffset;

    private Vector3 _targetScale;
    private Vector3 _targetCameraOffset;
    private Vector3 _cameraFollowVelocity;
    private Vector3 _cameraLookAheadOffset;
    private Vector3 _cameraLookAheadVelocity;

    public float CurrentSpeed => currentSpeed;
    public float Wieght => wieght;
    private Collider2D[] colliders;
    private bool _areCollidersEnabled = true;

    public Vector2 GetSweepCenter() => (Vector2)transform.position + sweepOffset;
    public bool CanBeAbsorbed => !isBeingAbsorbed && _absorbCooldown <= 0f;

    public float GetEffectiveSweepRadius() => sweepRadius * sweepForgivenessMultiplier;

    public Vector2 GetDynamicSweepCenter()
    {
        Vector2 baseCenter = GetSweepCenter();
        if (currentSpeed > 0.01f && currentMode != BroomMode.Sticky)
        {
            baseCenter += moveDir * forwardReachOffset;
        }
        return baseCenter;
    }

    public void OnAbsorbStart(BlackHoleObstacle blackHole)
    {
        if (isBeingAbsorbed || _absorbCooldown > 0f) return;
        Collider2D incomingCollider = blackHole.GetComponent<Collider2D>();
        if (incomingCollider == _currentBlackHoleCollider) return;

        _currentBlackHoleCollider = incomingCollider;
        isBeingAbsorbed = true;
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
        ResetCameraMotion();
        SetCollidersEnabled(false);
        blackHole.RegisterPlayer(this);
        OnAbsorbedByBlackHole?.Invoke(blackHole);
    }

    public void SetStickyLoad(float totalWeight)
    {
        _currentStickyLoad = totalWeight;
    }

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
        input = GetComponent<PlayerInput>();
        if (input == null) { Debug.LogError("Missing PlayerInput!"); return; }

        pointerPress = input.actions["PointerPress"];
        rightPointerPress = input.actions["RightPointerPress"];
        pointerPosition = input.actions["PointerPosition"];
        switchModeAction = input.actions.FindAction("SwitchMode");

        scrollYAction = new InputAction("ScrollY", binding: "<Mouse>/scroll/y");
        middleClickAction = new InputAction("MiddleClick", binding: "<Mouse>/middleButton");

        colliders = GetComponentsInChildren<Collider2D>(true);

        _baseCameraOffset = cameraOffset;
        _baseScale = transform.localScale;

        _baseMaxSpeed = maxSpeed;
        _baseSweepRadius = sweepRadius;
        _baseSweepOffset = sweepOffset;
        _baseWeight = wieght;
        _baseChargeCenterOffset = chargeCenterOffset;
        _baseForwardReachOffset = forwardReachOffset;

        _targetScale = _baseScale;
        _targetCameraOffset = _baseCameraOffset;
        _cameraFollowVelocity = Vector3.zero;
        _cameraLookAheadOffset = Vector3.zero;
        _cameraLookAheadVelocity = Vector3.zero;
    }

    private void OnEnable()
    {
        if (input == null) return;
        pointerPress.started += OnPress;
        pointerPress.canceled += OnRelease;
        rightPointerPress.started += OnRightPress;
        rightPointerPress.canceled += OnRightRelease;

        if (switchModeAction != null) switchModeAction.performed += OnSwitchModeInput;

        scrollYAction.Enable();
        middleClickAction.Enable();
        middleClickAction.performed += OnSwitchModeInput;

        PetAI.OnPetScaleChanged += HandlePetGrowth;
    }

    private void OnDisable()
    {
        if (input == null) return;
        pointerPress.started -= OnPress;
        pointerPress.canceled -= OnRelease;
        rightPointerPress.started -= OnRightPress;
        rightPointerPress.canceled -= OnRightRelease;

        if (switchModeAction != null) switchModeAction.performed -= OnSwitchModeInput;

        scrollYAction.Disable();
        middleClickAction.Disable();
        middleClickAction.performed -= OnSwitchModeInput;

        PetAI.OnPetScaleChanged -= HandlePetGrowth;
    }

    private void HandlePetGrowth(float petScaleMultiplier)
    {
        float actualMultiplier = 1f + ((petScaleMultiplier - 1f) * growthSyncRatio);

        _targetScale = _baseScale * actualMultiplier;
        _targetCameraOffset = _baseCameraOffset * actualMultiplier;

        maxSpeed = _baseMaxSpeed * actualMultiplier;
        sweepRadius = _baseSweepRadius * actualMultiplier;
        sweepOffset = _baseSweepOffset * actualMultiplier;
        wieght = _baseWeight * actualMultiplier;
        chargeCenterOffset = _baseChargeCenterOffset * actualMultiplier;
        forwardReachOffset = _baseForwardReachOffset * actualMultiplier;

        OnScaleChanged?.Invoke(actualMultiplier);
        effectManager?.ScaleEffects(actualMultiplier);
    }

    private void Update()
    {
        if (isBeingAbsorbed || pointerPosition == null) return;

        // [重點註釋] 右鍵技能冷卻計算與 UI 廣播
        if (_currentRightSkillCooldown > 0f)
        {
            _currentRightSkillCooldown -= Time.deltaTime;
            if (_currentRightSkillCooldown <= 0f) _currentRightSkillCooldown = 0f;
            OnRightSkillCooldownUpdate?.Invoke(_currentRightSkillCooldown, rightSkillCooldown);
        }

        float scrollValue = scrollYAction.ReadValue<float>();
        if (Mathf.Abs(scrollValue) > 0.1f)
        {
            ToggleSkillMode();
        }

        if (_vfxCooldownTimer > 0f) _vfxCooldownTimer -= Time.deltaTime;
        if (_absorbCooldown > 0f) _absorbCooldown -= Time.deltaTime;

        if (transform.localScale != _targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * growthLerpSpeed);
        }

        if (cameraOffset != _targetCameraOffset)
        {
            cameraOffset = Vector3.Lerp(cameraOffset, _targetCameraOffset, Time.deltaTime * growthLerpSpeed);
        }

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
            return;
        }

        Vector2 center = GetSweepCenter();
        Vector2 velocityVector = Vector2.zero;
        float sweepPower = 0f;

        if (currentSpeed > 0.01f)
        {
            velocityVector = moveDir * currentSpeed;
            Vector2 nextPos = rb.position + velocityVector * Time.fixedDeltaTime;

            if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(nextPos))
            {
                Vector2 safePos = nextPos;
                Vector2 tempVel = velocityVector;
                WorldBounds2D.Instance.Bounce(ref safePos, ref tempVel);

                rb.position = safePos;
                rb.linearVelocity = Vector2.zero;

                if (TryGetWallHitFromWorldBounds(nextPos, moveDir, out var hitPoint, out var hitNormal))
                    effectManager.PlayWallHit(hitPoint, hitNormal);
                else
                    effectManager.PlayWallHit(nextPos, -moveDir);

                OnWallHitEvent?.Invoke();
                currentSpeed = 0f;
                effectManager.StopTrail();
                return;
            }

            rb.linearVelocity = velocityVector;

            float damping = 1f - (deceleration * Time.fixedDeltaTime);
            currentSpeed *= Mathf.Clamp01(damping);
            if (currentSpeed < 0.05f) currentSpeed = 0f;

            float effectiveMax = GetEffectiveMaxSpeed();
            sweepPower = Mathf.Clamp01(currentSpeed / effectiveMax);

            effectManager.UpdateTrail(center, moveDir, sweepPower, true);
            OnSweepMove?.Invoke(GetDynamicSweepCenter(), GetEffectiveSweepRadius(), moveDir, sweepPower);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            effectManager.StopTrail();
        }
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        UpdateCameraFollow();
    }

    private void UpdateCameraFollow()
    {
        Vector3 lookAheadTarget = GetCameraLookAheadTarget();

        if (cameraLookAheadSmoothTime <= 0f)
        {
            _cameraLookAheadOffset = lookAheadTarget;
            _cameraLookAheadVelocity = Vector3.zero;
        }
        else
        {
            _cameraLookAheadOffset = Vector3.SmoothDamp(
                _cameraLookAheadOffset,
                lookAheadTarget,
                ref _cameraLookAheadVelocity,
                cameraLookAheadSmoothTime
            );
        }

        Vector3 desiredPosition = transform.position + cameraOffset + _cameraLookAheadOffset;

        if (cameraFollowSmoothTime <= 0f)
        {
            cam.transform.position = desiredPosition;
            _cameraFollowVelocity = Vector3.zero;
            return;
        }

        cam.transform.position = Vector3.SmoothDamp(
            cam.transform.position,
            desiredPosition,
            ref _cameraFollowVelocity,
            cameraFollowSmoothTime
        );
    }

    private Vector3 GetCameraLookAheadTarget()
    {
        if (isBeingAbsorbed || isBlocking || currentSpeed <= cameraLookAheadSpeedThreshold || moveDir.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float effectiveMax = Mathf.Max(GetEffectiveMaxSpeed(), 0.01f);
        float speedRatio = Mathf.Clamp01(currentSpeed / effectiveMax);
        Vector2 offset = moveDir * (cameraLookAheadDistance * speedRatio);
        return new Vector3(offset.x, offset.y, 0f);
    }

    private void ResetCameraMotion()
    {
        _cameraFollowVelocity = Vector3.zero;
        _cameraLookAheadOffset = Vector3.zero;
        _cameraLookAheadVelocity = Vector3.zero;
    }

    private void OnSwitchModeInput(InputAction.CallbackContext ctx)
    {
        ToggleSkillMode();
    }

    private void ToggleSkillMode()
    {
        if (Time.time - _lastSwitchTime < switchCooldown) return;
        _lastSwitchTime = Time.time;

        currentMode = (currentMode == BroomMode.Impact) ? BroomMode.Sticky : BroomMode.Impact;
        _currentStickyLoad = 0f;
        OnModeChanged?.Invoke(currentMode);
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;
        isLeftDown = true;
        OnLeftPressAction?.Invoke();
        if (isRightDown) { BlockBoth(); return; }
        effectManager.StopTrail();
        dragStart = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
        Vector2 c = GetSweepCenter();
        effectManager.UpdateDragLine(c, c);
    }

    // [遺失補回] 左鍵放開邏輯
    private void OnRelease(InputAction.CallbackContext ctx)
    {
        isLeftDown = false;
        OnLeftReleaseAction?.Invoke();
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
        if (isBeingAbsorbed || _currentRightSkillCooldown > 0f) return;

        PlayerAnimatorController.instance?.stateMachine.ChangeState(PlayerAnimatorController.instance.powerState);
        rb.linearVelocity = Vector2.zero;
        isRightDown = true;
        OnRightPressStart?.Invoke();
        rightPressStartTime = Time.time;
        effectManager.StopTrail();
        if (isLeftDown) { BlockBoth(); return; }
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        if (!isRightDown) return;

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

        _currentRightSkillCooldown = rightSkillCooldown;
        OnRightSkillCooldownUpdate?.Invoke(_currentRightSkillCooldown, rightSkillCooldown);
    }

    private void BlockBoth()
    {
        blockBoth = true;
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
        effectManager.StopAllEffects();
        ResetCameraMotion();
    }

    private void UnblockBoth()
    {
        blockBoth = false;
    }

    public void EmitTrashHit(Vector2 hitPoint, Vector2 hitNormal, TrashType type)
    {
        if (_vfxCooldownTimer > 0f) return;

        effectManager.PlayTrashHit(hitPoint, hitNormal);
        OnTrashHitEvent?.Invoke(type);
        _vfxCooldownTimer = vfxHitCooldown;
    }

    public void ExitBlackHole(Vector2 ejectDir, float ejectSpeed)
    {
        _absorbCooldown = absorbInvincibleTime;

        isBeingAbsorbed = false;
        blockBoth = false;
        isBlocking = false;
        isLeftDown = false;
        isRightDown = false;
        _currentStickyLoad = 0f;
        moveDir = ejectDir;
        currentSpeed = ejectSpeed;

        ResetCameraMotion();
        transform.localScale = Vector3.zero;

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
        if (!allowBounceBack)
        {
            currentSpeed *= Mathf.Clamp01(ratio);
        }
        else
        {
            if (ratio < 0f)
            {
                moveDir = -moveDir;
                currentSpeed *= Mathf.Clamp01(-ratio);
            }
            else
            {
                currentSpeed *= Mathf.Clamp01(ratio);
            }
        }

        if (currentSpeed <= 0.01f)
        {
            currentSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            effectManager.StopTrail();
        }
        else
        {
            rb.linearVelocity = moveDir * currentSpeed;
        }
    }

    private bool TryGetWallHitFromWorldBounds(Vector2 worldPos, Vector2 moveDir, out Vector2 hitPoint, out Vector2 hitNormal)
    {
        hitPoint = worldPos;
        hitNormal = -moveDir;
        var wb = WorldBounds2D.Instance;
        if (wb == null) return false;
        return wb.TryGetHitPointAndNormalWorld(worldPos, out hitPoint, out hitNormal);
    }

    private Vector2 ScreenToWorld(Vector2 p)
    {
        if (cam == null) return Vector2.zero;
        Ray ray = cam.ScreenPointToRay(p);
        Plane groundPlane = new Plane(Vector3.back, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return cam.ScreenToWorldPoint(new Vector3(p.x, p.y, Mathf.Abs(cam.transform.position.z)));
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_areCollidersEnabled == enabled) return;
        _areCollidersEnabled = enabled;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled != enabled)
            {
                colliders[i].enabled = enabled;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetSweepCenter(), sweepRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(GetDynamicSweepCenter(), GetEffectiveSweepRadius());
        }
    }
#endif
}