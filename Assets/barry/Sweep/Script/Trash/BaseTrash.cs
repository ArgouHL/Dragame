using System.Collections.Generic;
using UnityEngine;

public class BaseTrash : BasePoolItem, IAbsorbable
{
    [Header("基礎數值")]
    [SerializeField] protected float absorbEffectDuration;
    [SerializeField] protected float rotationSpeed;
    [SerializeField] public TrashType trashType;

    [Header("動畫曲線")]
    [SerializeField] protected AnimationCurve scaleCurve;
    [SerializeField] protected AnimationCurve moveCurve;

    [Header("物理屬性")]
    [SerializeField] private Vector2 currentVelocity;
    [SerializeField] private float deceleration;
    [SerializeField] private float viewportPadding;

    [Header("受力設定")]
    [SerializeField] private float broomForce;
    [SerializeField] private float trashForce;
    [SerializeField, Min(0.01f)] private float weight = 1f;

    [Header("碰撞檢測")]
    [SerializeField] private float collisionCheckRadius;
    [SerializeField] private float hitCooldown;
    [SerializeField] private float collisionDamping;
    [SerializeField] private float minCollisionSpeed;

    [Header("睡眠優化")]
    [SerializeField] private float sleepSpeedThreshold;
    [SerializeField] private float sleepTime;

    [Header("Sticky 模式專用")]
    [SerializeField, Tooltip("吸附時的平滑時間")] private float stickSmoothTime = 0.05f;

    public float AbsorbEffectDuration => absorbEffectDuration;
    public float RotationSpeed => rotationSpeed;
    public bool IsAbsorbing { get; private set; }
    public Vector2 CurrentVelocity => currentVelocity;
    public float Weight => weight;
    public float InvWeight => 1f / weight;

    // 內部變數
    private bool _isRecentlyHit;
    private float _hitCooldownTimer;
    private bool _isSleeping;
    private float _sleepTimer;

    // Sticky 狀態
    public bool _isStuck;
    private Vector2 _stickVelocitySmooth;

    private readonly List<BaseTrash> _nearbyTrash = new List<BaseTrash>(32);
    private Collider2D[] _colliders;
    private const int MAX_SUB_STEPS = 8;

    // --- 介面實作開始 ---
    public bool CanBeAbsorbed => !IsAbsorbing && !_isStuck;

    public void OnAbsorbStart(BlackHoleObstacle blackHole)
    {
        if (IsAbsorbing) return;

        IsAbsorbing = true;
        WakeUp();
        currentVelocity = Vector2.zero;
        SetCollidersEnabled(false);
        TrashCounter.MarkCollected(this);

        // 將自己註冊給黑洞進行動畫處理
        blackHole.RegisterTrash(this);
    }
    // --- 介面實作結束 ---

    protected virtual void FixedUpdate()
    {
        if (IsAbsorbing) return;

        if (_isStuck)
        {
            currentVelocity = Vector2.zero;
            return;
        }

        if (_isRecentlyHit)
        {
            _hitCooldownTimer -= Time.fixedDeltaTime;
            if (_hitCooldownTimer <= 0f)
            {
                _hitCooldownTimer = 0f;
                _isRecentlyHit = false;
            }
        }

        if (_isSleeping) return;

        Vector2 localPos = transform.position;
        HandleMovement(ref localPos);
        transform.position = localPos;

        HandleSleepCheck();
    }

    public void ApplyMagnetHold(Vector2 targetPosition, Vector2 hostVelocity)
    {
        if (IsAbsorbing) return;

        if (!_isStuck)
        {
            _isStuck = true;
            _isSleeping = false;
            _stickVelocitySmooth = Vector2.zero;
            currentVelocity = Vector2.zero;
            SetCollidersEnabled(false);
        }

        if (_colliders != null && _colliders.Length > 0 && _colliders[0].enabled) SetCollidersEnabled(false);

        Vector2 currentPos = transform.position;
        float actualSmoothTime = Vector2.Distance(currentPos, targetPosition) > 1f ? stickSmoothTime * 0.5f : stickSmoothTime;
        Vector2 newPos = Vector2.SmoothDamp(currentPos, targetPosition, ref _stickVelocitySmooth, actualSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);

        HandleBoundaryCheck(ref newPos);
        transform.position = newPos;
    }

    public void ReleaseMagnetHold()
    {
        if (!_isStuck) return;
        _isStuck = false;
        SetCollidersEnabled(true);
        currentVelocity = _stickVelocitySmooth;
    }

    private void HandleMovement(ref Vector2 currentPos)
    {
        if (currentVelocity.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector2.zero;
            ResolveOverlap(ref currentPos);
            return;
        }

        float remainingTime = Time.fixedDeltaTime;
        float radius = collisionCheckRadius;
        float maxStepDist = radius * 0.5f;
        int safetyLoopCount = 0;

        while (remainingTime > 0.00001f && safetyLoopCount < MAX_SUB_STEPS)
        {
            safetyLoopCount++;
            float speed = currentVelocity.magnitude;
            if (speed <= 0.0001f) { currentVelocity = Vector2.zero; break; }

            float stepTime = remainingTime;
            float stepDist = speed * stepTime;

            if (stepDist > maxStepDist)
            {
                stepTime = maxStepDist / speed;
                stepDist = maxStepDist;
            }

            Vector2 stepDelta = currentVelocity * stepTime;
            Vector2 targetPos = currentPos + stepDelta;
            float moveDist = stepDist;

            bool hasCollision = CheckPathCollision(currentPos, targetPos, ref moveDist, out BaseTrash hitTrash);

            if (moveDist > 0f)
            {
                currentPos += (stepDelta / stepDist) * moveDist;
            }

            if (hasCollision && hitTrash != null && !hitTrash._isRecentlyHit)
            {
                if (!hitTrash._isStuck)
                {
                    Vector2 otherPos = hitTrash.transform.position;
                    Vector2 hitDir = otherPos - currentPos;
                    if (hitDir.sqrMagnitude < 0.0001f) hitDir = Random.insideUnitCircle;
                    hitDir.Normalize();

                    Vector2 relVel = currentVelocity - hitTrash.CurrentVelocity;
                    float relSpeed = Vector2.Dot(relVel, hitDir);

                    if (relSpeed > minCollisionSpeed)
                    {
                        ResolveTrashCollision(hitTrash, hitDir);
                        StartHitCooldownTimer();
                        hitTrash.StartHitCooldownTimer();
                    }
                }
            }

            HandleBoundaryCheck(ref currentPos);

            float t = Mathf.Clamp01((deceleration * stepTime) * InvWeight);
            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, t);

            remainingTime -= stepTime;
        }

        if (safetyLoopCount >= MAX_SUB_STEPS) remainingTime = 0f;
        ResolveOverlap(ref currentPos);
    }

    private bool CheckPathCollision(Vector2 start, Vector2 end, ref float moveDist, out BaseTrash hitTrash)
    {
        hitTrash = null;
        SpatialGridManager.Instance.GetTrashAroundPosition(start, _nearbyTrash);
        if (_nearbyTrash.Count == 0) return false;

        float r1 = collisionCheckRadius;
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len <= 0.0001f) return false;
        dir /= len;

        float maxTravel = Mathf.Min(len, moveDist);
        float bestDist = maxTravel;
        bool foundHit = false;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];
            if (trash == null || trash == this || trash.IsAbsorbing || trash._isRecentlyHit || trash._isStuck) continue;

            float r2 = trash.collisionCheckRadius;
            float combined = r1 + r2;
            Vector2 toTrash = (Vector2)trash.transform.position - start;
            float proj = Vector2.Dot(toTrash, dir);

            if (proj < 0f || proj > maxTravel + combined) continue;
            float sqLine = toTrash.sqrMagnitude - proj * proj;
            if (sqLine > combined * combined) continue;

            float offset = Mathf.Sqrt(Mathf.Max(combined * combined - sqLine, 0f));
            float hitAlong = proj - offset;

            if (hitAlong < 0f) hitAlong = proj;
            if (hitAlong < 0f || hitAlong > bestDist) continue;

            bestDist = hitAlong;
            hitTrash = trash;
            foundHit = true;
        }

        if (foundHit)
        {
            moveDist = Mathf.Max(0f, bestDist);
            return true;
        }
        return false;
    }

    private void ResolveTrashCollision(BaseTrash other, Vector2 normal)
    {
        Vector2 v1 = currentVelocity;
        Vector2 v2 = other.currentVelocity;
        Vector2 relVel = v1 - v2;
        float relSpeedN = Vector2.Dot(relVel, normal);

        if (relSpeedN <= minCollisionSpeed) return;

        float e = Mathf.Clamp01(collisionDamping);
        float invSum = InvWeight + other.InvWeight;
        if (invSum <= 0f) return;

        float j = (1f + e) * relSpeedN / invSum;
        Vector2 impulse = j * normal;

        currentVelocity -= impulse * InvWeight;
        other.currentVelocity += impulse * other.InvWeight;
    }

    private void ResolveOverlap(ref Vector2 pos)
    {
        if (_isStuck) return;

        float r1 = collisionCheckRadius;
        SpatialGridManager.Instance.GetTrashAroundPosition(pos, _nearbyTrash);

        if (_nearbyTrash.Count == 0) return;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];
            if (trash == null || trash == this || trash.IsAbsorbing || trash._isStuck) continue;

            float r2 = trash.collisionCheckRadius;
            float minDist = r1 + r2;
            Vector2 delta = pos - (Vector2)trash.transform.position;
            float sqr = delta.sqrMagnitude;

            if (sqr <= 0.000001f || sqr >= minDist * minDist) continue;

            float dist = Mathf.Sqrt(sqr);
            float penetration = minDist - dist;
            pos += delta / dist * penetration;
        }
    }

    private void ApplyImpulse(Vector2 impulse)
    {
        currentVelocity += impulse * InvWeight;
    }

    public void ApplyBroomHit(Vector2 hitDirection, float power)
    {
        if (IsAbsorbing || _isStuck) return;

        if (PlayerController.instance != null && PlayerController.instance.currentMode == BroomMode.Sticky) return;

        WakeUp();
        if (_isRecentlyHit) return;

        StartHitCooldownTimer();

        Vector2 dir = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.right;

        if (PlayerController.instance != null)
        {
            Vector2 hitPoint = (Vector2)transform.position - dir * collisionCheckRadius;
            PlayerController.instance.EmitTrashHit(hitPoint, -dir);
        }

        Vector2 impulse = dir * (broomForce * power);
        ApplyImpulse(impulse);
    }

    public void ApplyTrashHit(Vector2 hitDirection, float strength01)
    {
        if (IsAbsorbing || _isStuck) return;
        WakeUp();
        StartHitCooldownTimer();
        Vector2 dir = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.right;
        float impulseMag = trashForce * Mathf.Clamp01(strength01);
        ApplyImpulse(dir * impulseMag);
        currentVelocity *= collisionDamping;
    }

    private void HandleSleepCheck()
    {
        float speed = currentVelocity.magnitude;
        if (speed < sleepSpeedThreshold && speed > 0f)
        {
            _sleepTimer += Time.fixedDeltaTime;
            if (_sleepTimer >= sleepTime) { currentVelocity = Vector2.zero; _isSleeping = true; }
        }
        else { _sleepTimer = 0f; }
    }

    private void WakeUp() { _isSleeping = false; _sleepTimer = 0f; }

    private void HandleBoundaryCheck(ref Vector2 pos)
    {
        if (WorldBounds2D.Instance == null) return;
        WorldBounds2D.Instance.Bounce(ref pos, ref currentVelocity, viewportPadding);
    }

    private void StartHitCooldownTimer()
    {
        _isRecentlyHit = true;
        _hitCooldownTimer = hitCooldown;
    }

    // 保留舊方法但標記為過時，或直接導向新邏輯
    public void OnEnterBlackHole()
    {
        if (IsAbsorbing) return;
        IsAbsorbing = true;
        WakeUp();
        currentVelocity = Vector2.zero;
        SetCollidersEnabled(false);
        TrashCounter.MarkCollected(this);
    }

    public override void ResetState()
    {
        base.ResetState();
        currentVelocity = Vector2.zero;
        IsAbsorbing = false;
        transform.localScale = initialScale;
        _isRecentlyHit = false;
        _hitCooldownTimer = 0f;
        _isStuck = false;
        _stickVelocitySmooth = Vector2.zero;
        WakeUp();
        SetCollidersEnabled(true);
    }

    private void EnsureColliders()
    {
        if (_colliders != null) return;
        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        EnsureColliders();
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null && _colliders[i].enabled != enabled)
                _colliders[i].enabled = enabled;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _isStuck ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collisionCheckRadius);
    }
#endif
}