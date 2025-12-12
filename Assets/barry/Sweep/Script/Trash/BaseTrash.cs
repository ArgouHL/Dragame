using System.Collections.Generic;
using UnityEngine;

public class BaseTrash : BasePoolItem
{
    [SerializeField] protected float absorbEffectDuration;
    [SerializeField] protected float rotationSpeed;

    [Header("縮小(消退)速率曲線")]
    [SerializeField] protected AnimationCurve scaleCurve;

    [Header("移動設定速率曲線")]
    [SerializeField] protected AnimationCurve moveCurve;

    [SerializeField] public TrashType trashType;

    [Header("手動物理設定")]
    [SerializeField] private Vector2 currentVelocity;
    [SerializeField] private float deceleration;
    [SerializeField] private float viewportPadding;
    [SerializeField] private float broomForce;
    [SerializeField] private float trashForce;

    [Header("重量設定 (越大越重)")]
    [SerializeField, Min(0.01f)] private float wieght = 1f;   // 按你的要求命名：wieght

    [Header("連鎖碰撞設定")]
    [SerializeField] private float collisionCheckRadius;
    [SerializeField] private float hitCooldown;
    [SerializeField] private float collisionDamping;
    [SerializeField] private float minCollisionSpeed;

    [Header("睡眠（停止運算）設定")]
    [SerializeField] private float sleepSpeedThreshold;
    [SerializeField] private float sleepTime;

    [Header("多邊形碰撞形狀 (local 空間，可選)")]
    [SerializeField] private List<Vector2> collisionPolygon;

    public bool IsAbsorbing { get; private set; }
    public Vector2 CurrentVelocity => currentVelocity;
    public float AbsorbEffectDuration => absorbEffectDuration;
    public float RotationSpeed => rotationSpeed;
    public AnimationCurve ScaleCurve => scaleCurve;
    public AnimationCurve MoveCurve => moveCurve;

    public float Wieght => wieght;
    public float InvWieght => 1f / wieght;

    private bool _isRecentlyHit;
    private float _hitCooldownTimer;
    private bool _isSleeping;
    private float _sleepTimer;

    private readonly List<BaseTrash> _nearbyTrash = new List<BaseTrash>(32);
    private Collider2D[] _colliders;

    private float cachedRadius;

    [Header("调试设置")]
    [SerializeField] private bool debugCollision = true;
    [SerializeField] private bool debugVelocity = true;

    protected virtual void FixedUpdate()
    {
        if (IsAbsorbing || _isSleeping) return;

        if (_isRecentlyHit)
        {
            _hitCooldownTimer -= Time.fixedDeltaTime;
            _isRecentlyHit = _hitCooldownTimer > 0f;
        }

        HandleMovement();
        HandleSleepCheck();
    }

    private float GetCollisionRadius()
    {
        if (cachedRadius > 0f) return cachedRadius;

        if (UsePolygon())
        {
            float max = 0f;
            for (int i = 0; i < collisionPolygon.Count; i++)
            {
                float sq = collisionPolygon[i].sqrMagnitude;
                if (sq > max) max = sq;
            }

            cachedRadius = Mathf.Sqrt(max);
        }
        else
        {
            cachedRadius = collisionCheckRadius;
        }

        return cachedRadius;
    }

    private bool UsePolygon() =>
        collisionPolygon != null && collisionPolygon.Count >= 3;

#if UNITY_EDITOR
    private void OnValidate() => cachedRadius = 0f;
#endif

    private void ApplyImpulse(Vector2 impulse)
    {
        // impulse 視為「衝量」，重量越大（wieght 越大）速度增量越小
        currentVelocity += impulse * InvWieght;
    }

    private void HandleMovement()
    {
        float radius = GetCollisionRadius();

        if (debugVelocity && currentVelocity.sqrMagnitude > 0.01f)
        {
            Debug.Log($"{name} 速度: {currentVelocity.magnitude:F3}, 方向: {currentVelocity.normalized}");
        }

        if (currentVelocity.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector2.zero;
            ResolveOverlap();
            return;
        }

        float remainingTime = Time.fixedDeltaTime;
        float maxStepDist = radius * 0.5f;
        Vector2 currentPos = transform.position;

        while (remainingTime > 0f)
        {
            float stepTime = remainingTime;
            float speed = currentVelocity.magnitude;
            if (speed <= 0f)
            {
                currentVelocity = Vector2.zero;
                break;
            }

            Vector2 stepDelta = currentVelocity * stepTime;
            float stepMag = stepDelta.magnitude;

            if (stepMag > maxStepDist)
            {
                stepTime = maxStepDist / speed;
                stepDelta = currentVelocity * stepTime;
                stepMag = stepDelta.magnitude;
            }

            Vector2 targetPos = currentPos + stepDelta;
            float moveDist = stepMag;

            if (debugCollision)
                Debug.Log($"{name} 检查碰撞: 从 {currentPos} 到 {targetPos}, 距离: {moveDist:F3}");

            bool hasCollision = CheckPathCollision(currentPos, targetPos, ref moveDist, out BaseTrash hitTrash);

            Debug.Log("hascollision  " + hasCollision);

            if (moveDist > 0f)
            {
                Vector2 actualDelta = currentVelocity.normalized * moveDist;
                transform.position = currentPos + actualDelta;
                currentPos = transform.position;
            }

            if (hasCollision && hitTrash != null && !hitTrash._isRecentlyHit)
            {
                Vector2 hitDir = (Vector2)(hitTrash.transform.position - transform.position);
                if (hitDir.sqrMagnitude < 0.0001f) hitDir = Random.insideUnitCircle;
                hitDir.Normalize();

                Vector2 relVel = currentVelocity - hitTrash.CurrentVelocity;
                float relSpeed = Vector2.Dot(relVel, hitDir);

                if (debugCollision)
                {
                    Debug.Log($"{name} 进入垃圾碰撞检测");
                    Debug.Log($"  与 {hitTrash.name} 相对速度: {relSpeed:F3}, minCollisionSpeed: {minCollisionSpeed}");
                    Debug.Log($"  自己速度: {currentVelocity.magnitude:F3}, 对方速度: {hitTrash.CurrentVelocity.magnitude:F3}");
                    Debug.Log($"  自己 wieght: {wieght:F3}, 对方 wieght: {hitTrash.Wieght:F3}");
                }

                if (relSpeed > minCollisionSpeed)
                {
                    if (debugCollision)
                        Debug.Log($"{name} 垃圾碰撞条件满足，处理碰撞(相对速度 + 重量)");

                    ResolveTrashCollision(hitTrash, hitDir);

                    StartHitCooldownTimer();
                    hitTrash.StartHitCooldownTimer();

                    if (debugCollision)
                        Debug.Log($"{name} 碰撞处理完成，新速度: {currentVelocity.magnitude:F3}");
                }
                else
                {
                    if (debugCollision)
                        Debug.Log($"{name} 相对速度不足，跳过碰撞");
                }
            }
            else if (hasCollision && hitTrash != null && hitTrash._isRecentlyHit)
            {
                if (debugCollision)
                    Debug.Log($"{name} 对方 {hitTrash.name} 在冷却中，跳过碰撞");
            }
            else if (!hasCollision && debugCollision && _nearbyTrash.Count > 0)
            {
                Debug.Log($"{name} 有周围垃圾但未检测到碰撞，周围垃圾数: {_nearbyTrash.Count}");
            }

            HandleBoundaryCheck();

            float t = Mathf.Clamp01((deceleration * stepTime) * InvWieght);
            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, t);

            remainingTime -= stepTime;

            if (currentVelocity.sqrMagnitude < 0.000001f)
            {
                currentVelocity = Vector2.zero;
                break;
            }
        }

        ResolveOverlap();
    }

    private void ResolveTrashCollision(BaseTrash other, Vector2 normal)
    {
        normal.Normalize();

        Vector2 v1 = currentVelocity;
        Vector2 v2 = other.currentVelocity;

        Vector2 relVel = v1 - v2;

        float relSpeedN = Vector2.Dot(relVel, normal);
        if (relSpeedN <= minCollisionSpeed) return;

        float e = Mathf.Clamp01(collisionDamping);

        float invSum = InvWieght + other.InvWieght;
        if (invSum <= 0f) return;

        float j = (1f + e) * relSpeedN / invSum;

        currentVelocity = v1 - (j * InvWieght) * normal;
        other.currentVelocity = v2 + (j * other.InvWieght) * normal;
    }

    private bool CheckPathCollision(Vector2 start, Vector2 end, ref float moveDist, out BaseTrash hitTrash)
    {
        hitTrash = null;

        SpatialGridManager.Instance.GetTrashAroundPosition(start, _nearbyTrash);

        Debug.Log($"{name} 检查路径碰撞，周围垃圾数量: {_nearbyTrash.Count}");

        if (_nearbyTrash.Count == 0) return false;

        float r1 = GetCollisionRadius();

        if (debugCollision)
            Debug.Log($"{name} 碰撞半径: {r1}");

        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len <= 0.0001f) return false;

        dir /= len;
        float maxTravel = Mathf.Min(len, moveDist);

        float bestDist = maxTravel;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];

            if (debugCollision && trash != null)
            {
                Debug.Log($"  检查垃圾 {trash.name}: ");
                Debug.Log($"    IsAbsorbing: {trash.IsAbsorbing}");
                Debug.Log($"    _isRecentlyHit: {trash._isRecentlyHit}");
                Debug.Log($"    位置: {trash.transform.position}");
                Debug.Log($"    碰撞半径: {trash.GetCollisionRadius()}");
                Debug.Log($"    wieght: {trash.Wieght}");
            }

            if (trash == null || trash == this || trash.IsAbsorbing || trash._isRecentlyHit)
            {
                if (debugCollision && trash != null && trash != this)
                    Debug.Log($"  跳过垃圾 {trash.name} (条件不满足)");
                continue;
            }

            float r2 = trash.GetCollisionRadius();
            float combined = r1 + r2;
            float combinedSqr = combined * combined;

            Vector2 trashPos = trash.transform.position;
            Vector2 toTrash = trashPos - start;

           
              

            float proj = Vector2.Dot(toTrash, dir);
            if (proj < 0f || proj > maxTravel + combined)
            {
                if (debugCollision)
                   
                continue;
            }

            float sqLine = toTrash.sqrMagnitude - proj * proj;
            if (sqLine > combinedSqr)
            {
                if (debugCollision)
                    Debug.Log($"  垂直距离平方 {sqLine} > 碰撞半径平方 {combinedSqr}");
                continue;
            }

            float offset = Mathf.Sqrt(Mathf.Max(combinedSqr - sqLine, 0f));
            float hitAlong = proj - offset;
            if (hitAlong < 0f) hitAlong = proj;

            if (hitAlong < 0f || hitAlong > bestDist)
            {
                if (debugCollision)
                    Debug.Log($"  碰撞距离 {hitAlong} 不在最佳距离范围内");
                continue;
            }

            bestDist = hitAlong;
            hitTrash = trash;

            if (debugCollision)
                Debug.Log($"  找到碰撞: 与 {hitTrash.name} 在距离 {bestDist} 处");
        }

        if (hitTrash == null)
        {
            if (debugCollision)
                Debug.Log($"  未找到碰撞");
            return false;
        }

        moveDist = Mathf.Max(0f, bestDist);
        return true;
    }

    private void ResolveOverlap()
    {
        float r1 = GetCollisionRadius();

        SpatialGridManager.Instance.GetTrashAroundPosition(transform.position, _nearbyTrash);
        if (_nearbyTrash.Count == 0) return;

        Vector2 pos = transform.position;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];
            if (trash == null || trash == this || trash.IsAbsorbing) continue;

            float r2 = trash.GetCollisionRadius();
            float minDist = r1 + r2;
            float minDistSqr = minDist * minDist;

            Vector2 otherPos = trash.transform.position;
            Vector2 delta = pos - otherPos;
            float sqr = delta.sqrMagnitude;
            if (sqr <= 0.000001f || sqr >= minDistSqr) continue;

            float dist = Mathf.Sqrt(sqr);
            float penetration = minDist - dist;
            Vector2 push = delta / dist * penetration;
            pos += push;
        }

        transform.position = pos;
    }

    public void ApplyBroomHit(Vector2 hitDirection, float power)
    {
        if (IsAbsorbing) return;

        WakeUp();
        StartHitCooldownTimer();

        Vector2 impulse = hitDirection.normalized * (broomForce * power);
        ApplyImpulse(impulse);
    }

    public void ApplyTrashHit(Vector2 hitDirection, float strength01)
    {
        if (IsAbsorbing) return;

        WakeUp();
        StartHitCooldownTimer();

        float impulseMag = trashForce * Mathf.Clamp01(strength01);
        Vector2 impulse = hitDirection.normalized * impulseMag;

        ApplyImpulse(impulse);

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

    private void HandleBoundaryCheck()
    {
        if (WorldBounds2D.Instance == null) return;

        Vector2 pos = transform.position;
        Vector2 vel = currentVelocity;
        WorldBounds2D.Instance.Bounce(ref pos, ref vel, viewportPadding);
        transform.position = pos;
        currentVelocity = vel;
    }

    private void StartHitCooldownTimer()
    {
        _isRecentlyHit = true;
        _hitCooldownTimer = hitCooldown;
    }

    public void OnEnterBlackHole()
    {
        if (IsAbsorbing) return;

        IsAbsorbing = true;
        _isSleeping = false;
        currentVelocity = Vector2.zero;
        SetCollidersEnabled(false);

        // ===== 這行是新增：黑洞吸入時 x++（只會觸發一次，因為上面 IsAbsorbing guard）=====
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
        _isSleeping = false;
        _sleepTimer = 0f;
        cachedRadius = 0f;
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
            if (_colliders[i] != null)
                _colliders[i].enabled = enabled;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        if (UsePolygon())
        {
            int count = collisionPolygon.Count;
            if (count >= 2)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = transform.TransformPoint(collisionPolygon[i]);
                    Vector3 b = transform.TransformPoint(collisionPolygon[(i + 1) % count]);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, collisionCheckRadius);
        }
    }
}
