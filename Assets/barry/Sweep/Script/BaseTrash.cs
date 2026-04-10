using System.Collections.Generic;
using UnityEngine;

public class BaseTrash : BasePoolItem, IAbsorbable
{
    [Header("基礎數值")]
    [SerializeField] protected float absorbEffectDuration;
    [SerializeField] protected float rotationSpeed;

    [SerializeField] public TrashType trashType;

    [Tooltip("垃圾的階級/大小。寵物需達到對應的 maxEatTier 才能吃下它")]
    [SerializeField] public int trashTier = 1;

    [SerializeField] protected int scoreValue = 10;

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

    [Header("碰撞檢測 (串珠節點法)")]
    [Tooltip("新增多個節點來拼湊出長條形。如果只有一個點，就是普通的圓形。")]
    [SerializeField] private Vector2[] collisionNodes = { Vector2.zero };
    [SerializeField] private float collisionCheckRadius = 0.45f;
    [SerializeField] private float hitCooldown = 0.2f;
    [SerializeField] private float collisionDamping = 0.8f;
    [SerializeField] private float minCollisionSpeed = 0.1f;

    [Header("睡眠優化")]
    [SerializeField] private float sleepSpeedThreshold;
    [SerializeField] private float sleepTime;

    [Header("Sticky 模式專用")]
    [SerializeField] private float stickSmoothTime = 0.05f;

    [HideInInspector] public bool isDynamicSpawned = false;

    public float AbsorbEffectDuration => absorbEffectDuration;
    public float RotationSpeed => rotationSpeed;
    public bool IsAbsorbing { get; private set; }
    public Vector2 CurrentVelocity => currentVelocity;
    public float Weight => weight;
    public float InvWeight => 1f / weight;
    public int ScoreValue => scoreValue;

    private bool _isRecentlyHit;
    private float _hitCooldownTimer;
    private bool _isSleeping;
    private float _sleepTimer;

    public bool _isStuck;
    private Vector2 _stickVelocitySmooth;

    private readonly List<BaseTrash> _nearbyTrash = new List<BaseTrash>(32);
    private Collider2D[] _colliders;
    private Transform _transform;
    private bool _areCollidersEnabled = true;
    private const int MAX_SUB_STEPS = 8;

    public bool CanBeAbsorbed => !IsAbsorbing && !_isStuck;

    protected virtual void Awake()
    {
        _transform = transform;
        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    public void OnAbsorbStart(BlackHoleObstacle blackHole)
    {
        BeginAbsorb(true, blackHole);
    }

    public void OnEnterBlackHole()
    {
        BeginAbsorb(false, null);
    }

    private void BeginAbsorb(bool registerWithBlackHole, BlackHoleObstacle blackHole)
    {
        if (IsAbsorbing) return;

        IsAbsorbing = true;
        NotifyDynamicConsumption();

        WakeUp();
        currentVelocity = Vector2.zero;
        SetCollidersEnabled(false);
        TrashCounter.MarkCollected(this);

        if (registerWithBlackHole && blackHole != null)
        {
            blackHole.RegisterTrash(this);
        }
    }

    private void NotifyDynamicConsumption()
    {
        if (isDynamicSpawned && DynamicSpawnManager.Instance != null)
        {
            DynamicSpawnManager.Instance.OnDynamicTrashConsumed();
            isDynamicSpawned = false;
        }
    }

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

        Vector2 localPos = _transform.position;
        HandleMovement(ref localPos);
        _transform.position = localPos;

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

        if (_colliders != null && _colliders.Length > 0 && _colliders[0] != null && _colliders[0].enabled)
        {
            SetCollidersEnabled(false);
        }

        Vector2 currentPos = _transform.position;
        float actualSmoothTime = (currentPos - targetPosition).sqrMagnitude > 1f ? stickSmoothTime * 0.5f : stickSmoothTime;
        Vector2 newPos = Vector2.SmoothDamp(currentPos, targetPosition, ref _stickVelocitySmooth, actualSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);

        HandleBoundaryCheck(ref newPos);
        _transform.position = newPos;
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
            HandleBoundaryCheck(ref currentPos);
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
            if (speed <= 0.0001f)
            {
                currentVelocity = Vector2.zero;
                break;
            }

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
                    Vector2 otherPos = hitTrash._transform.position;
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

            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, GetVelocityDamping(stepTime));
            remainingTime -= stepTime;
        }

        ResolveOverlap(ref currentPos);
        HandleBoundaryCheck(ref currentPos);
    }

    private float GetVelocityDamping(float deltaTime)
    {
        float effectiveWeight = Mathf.Max(weight, 0.01f);

        // [重點註釋] 輕物件保留更多慣性、重物件更快收束，避免輕垃圾出現突兀的瞬停感。
        float drag = Mathf.Max(0f, deceleration) * Mathf.Sqrt(effectiveWeight);
        if (drag <= 0f) return 0f;

        return 1f - Mathf.Exp(-drag * deltaTime);
    }

    private bool CheckPathCollision(Vector2 start, Vector2 end, ref float moveDist, out BaseTrash hitTrash)
    {
        hitTrash = null;
        if (collisionNodes == null || collisionNodes.Length == 0) return false;

        SpatialGridManager grid = SpatialGridManager.Instance;
        if (grid == null) return false;

        float r1 = collisionCheckRadius;
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len <= 0.0001f) return false;
        dir /= len;

        float maxTravel = Mathf.Min(len, moveDist);
        float bestDist = maxTravel;
        bool foundHit = false;

        for (int myNodeIdx = 0; myNodeIdx < collisionNodes.Length; myNodeIdx++)
        {
            Vector2 startCenter = start + collisionNodes[myNodeIdx];
            grid.GetTrashAroundPosition(startCenter, _nearbyTrash);

            for (int i = 0; i < _nearbyTrash.Count; i++)
            {
                var trash = _nearbyTrash[i];
                if (trash == null || trash == this || trash.IsAbsorbing || trash._isRecentlyHit || trash._isStuck) continue;
                if (trash.collisionNodes == null || trash.collisionNodes.Length == 0) continue;

                float r2 = trash.collisionCheckRadius;
                float combined = r1 + r2;

                for (int theirNodeIdx = 0; theirNodeIdx < trash.collisionNodes.Length; theirNodeIdx++)
                {
                    Vector2 theirCenter = (Vector2)trash._transform.position + trash.collisionNodes[theirNodeIdx];
                    Vector2 toTrash = theirCenter - startCenter;
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
            }
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
        if (_isStuck || collisionNodes == null || collisionNodes.Length == 0) return;

        SpatialGridManager grid = SpatialGridManager.Instance;
        if (grid == null) return;

        float r1 = collisionCheckRadius;

        for (int myNodeIdx = 0; myNodeIdx < collisionNodes.Length; myNodeIdx++)
        {
            Vector2 myCenter = pos + collisionNodes[myNodeIdx];
            grid.GetTrashAroundPosition(myCenter, _nearbyTrash);

            if (_nearbyTrash.Count == 0) continue;

            for (int i = 0; i < _nearbyTrash.Count; i++)
            {
                var trash = _nearbyTrash[i];
                if (trash == null || trash == this || trash.IsAbsorbing || trash._isStuck) continue;
                if (trash.collisionNodes == null || trash.collisionNodes.Length == 0) continue;

                float r2 = trash.collisionCheckRadius;
                float minDist = r1 + r2;
                float minDistSqr = minDist * minDist;

                for (int theirNodeIdx = 0; theirNodeIdx < trash.collisionNodes.Length; theirNodeIdx++)
                {
                    Vector2 theirCenter = (Vector2)trash._transform.position + trash.collisionNodes[theirNodeIdx];
                    Vector2 delta = myCenter - theirCenter;
                    float sqr = delta.sqrMagnitude;

                    if (sqr <= 0.000001f)
                    {
                        delta = Random.insideUnitCircle.normalized * 0.01f;
                        sqr = delta.sqrMagnitude;
                    }

                    if (sqr >= minDistSqr) continue;

                    float dist = Mathf.Sqrt(sqr);
                    float penetration = minDist - dist;

                    pos += delta / dist * penetration;
                    myCenter = pos + collisionNodes[myNodeIdx];
                }
            }
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
        PlayerController controller = PlayerController.instance;

        if (controller != null)
        {
            Vector2 primaryNode = collisionNodes != null && collisionNodes.Length > 0 ? collisionNodes[0] : Vector2.zero;
            Vector2 hitPoint = ((Vector2)_transform.position + primaryNode) - dir * collisionCheckRadius;
            controller.EmitTrashHit(hitPoint, -dir, trashType);
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
            if (_sleepTimer >= sleepTime)
            {
                currentVelocity = Vector2.zero;
                _isSleeping = true;
            }
        }
        else
        {
            _sleepTimer = 0f;
        }
    }

    private void WakeUp()
    {
        _isSleeping = false;
        _sleepTimer = 0f;
    }

    private void HandleBoundaryCheck(ref Vector2 pos)
    {
        if (WorldBounds2D.Instance == null) return;

        // [重點註釋] 邊界反彈仍交給統一的世界邊界系統，避免各物件各自算邊界造成手感不一致。
        WorldBounds2D.Instance.Bounce(ref pos, ref currentVelocity, viewportPadding, collisionDamping);
    }

    private void StartHitCooldownTimer()
    {
        _isRecentlyHit = true;
        _hitCooldownTimer = hitCooldown;
    }

    public override void ResetState()
    {
        base.ResetState();
        currentVelocity = Vector2.zero;
        IsAbsorbing = false;

        isDynamicSpawned = false;

        _transform.localScale = initialScale;
        _isRecentlyHit = false;
        _hitCooldownTimer = 0f;
        _isStuck = false;
        _stickVelocitySmooth = Vector2.zero;
        WakeUp();
        SetCollidersEnabled(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null) return;
        if (_areCollidersEnabled == enabled) return;

        _areCollidersEnabled = enabled;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null && _colliders[i].enabled != enabled)
            {
                _colliders[i].enabled = enabled;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _isStuck ? Color.green : Color.yellow;
        if (collisionNodes == null || collisionNodes.Length == 0) return;

        foreach (var node in collisionNodes)
        {
            Gizmos.DrawWireSphere((Vector2)transform.position + node, collisionCheckRadius);
        }
    }
#endif
}