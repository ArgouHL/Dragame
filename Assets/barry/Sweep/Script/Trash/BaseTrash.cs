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
    [Header("連鎖碰撞設定")]
    [SerializeField] private float collisionCheckRadius;
    [SerializeField] private float hitCooldown;
    [SerializeField] private float collisionDamping;
    [SerializeField] private float minCollisionSpeed;
    [Header("睡眠（停止運算）設定")]
    [SerializeField] private float sleepSpeedThreshold;
    [SerializeField] private float sleepTime;

    public bool IsAbsorbing { get; private set; }
    public Vector2 CurrentVelocity => currentVelocity;
    public float AbsorbEffectDuration => absorbEffectDuration;
    public float RotationSpeed => rotationSpeed;
    public AnimationCurve ScaleCurve => scaleCurve;
    public AnimationCurve MoveCurve => moveCurve;

    private bool _isRecentlyHit;
    private float _hitCooldownTimer;
    private bool _isSleeping;
    private float _sleepTimer;
    private readonly List<BaseTrash> _nearbyTrash = new List<BaseTrash>(32);
    private Collider2D[] _colliders;

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

    private void HandleMovement()
    {
        if (currentVelocity.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector2.zero;
            ResolveOverlap();
            return;
        }

        float remainingTime = Time.fixedDeltaTime;
        float maxStepDist = collisionCheckRadius * 0.5f;
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
            bool hasCollision = CheckPathCollision(currentPos, targetPos, ref moveDist, out BaseTrash hitTrash);

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

                if (relSpeed > minCollisionSpeed)
                {
                    float impulse01 = Mathf.Clamp01((relSpeed - minCollisionSpeed) / minCollisionSpeed);

                    hitTrash.ApplyTrashHit(hitDir, impulse01);

                    Vector2 v = currentVelocity;
                    float proj = Vector2.Dot(v, hitDir);
                    v -= 2f * proj * hitDir;
                    currentVelocity = v * collisionDamping;

                    StartHitCooldownTimer();
                    hitTrash.StartHitCooldownTimer();
                }
            }

            HandleBoundaryCheck();

            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, deceleration * stepTime);
            remainingTime -= stepTime;

            if (currentVelocity.sqrMagnitude < 0.000001f)
            {
                currentVelocity = Vector2.zero;
                break;
            }
        }

        ResolveOverlap();
    }

    private bool CheckPathCollision(Vector2 start, Vector2 end, ref float moveDist, out BaseTrash hitTrash)
    {
        hitTrash = null;

        SpatialGridManager.Instance.GetTrashAroundPosition(start, _nearbyTrash);

        if (_nearbyTrash.Count == 0) return false;

        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len <= 0.0001f) return false;

        dir /= len;
        float maxTravel = Mathf.Min(len, moveDist);
        float radius = collisionCheckRadius;
        float combinedRadius = radius * 2f;
        float combinedRadiusSqr = combinedRadius * combinedRadius;

        float bestDist = maxTravel;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];
            if (trash == null || trash == this || trash.IsAbsorbing || trash._isRecentlyHit) continue;

            Vector2 trashPos = trash.transform.position;
            Vector2 toTrash = trashPos - start;

            float proj = Vector2.Dot(toTrash, dir);
            if (proj < 0f || proj > maxTravel + combinedRadius) continue;

            float sqDistToLine = toTrash.sqrMagnitude - proj * proj;
            if (sqDistToLine > combinedRadiusSqr) continue;

            float offset = Mathf.Sqrt(Mathf.Max(combinedRadiusSqr - sqDistToLine, 0f));
            float hitAlong = proj - offset;
            if (hitAlong < 0f) hitAlong = proj;

            if (hitAlong < 0f || hitAlong > bestDist) continue;

            bestDist = hitAlong;
            hitTrash = trash;
        }

        if (hitTrash == null) return false;

        moveDist = Mathf.Max(0f, bestDist);
        return true;
    }

    private void ResolveOverlap()
    {
        if (SpatialGridManager.Instance == null) return;
        if (collisionCheckRadius <= 0f) return;

        SpatialGridManager.Instance.GetTrashAroundPosition(transform.position, _nearbyTrash);
        if (_nearbyTrash.Count == 0) return;

        Vector2 pos = transform.position;
        float radius = collisionCheckRadius;
        float minDist = radius * 2f;
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            var trash = _nearbyTrash[i];
            if (trash == null || trash == this || trash.IsAbsorbing) continue;

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

    public void ApplyBroomHit(Vector2 hitDirection, float power)
    {
        if (IsAbsorbing) return;
        WakeUp();
        StartHitCooldownTimer();
        currentVelocity = hitDirection.normalized * broomForce * power;
    }

    public void ApplyTrashHit(Vector2 hitDirection, float strength01)
    {
        if (IsAbsorbing) return;
        WakeUp();
        StartHitCooldownTimer();
        float addSpeed = trashForce * Mathf.Clamp01(strength01);
        currentVelocity += hitDirection.normalized * addSpeed;
        currentVelocity *= collisionDamping;
    }

    private void StartHitCooldownTimer()
    {
        _isRecentlyHit = true;
        _hitCooldownTimer = hitCooldown;
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

    public void OnEnterBlackHole()
    {
        if (IsAbsorbing) return;

        IsAbsorbing = true;
        _isSleeping = false;
        currentVelocity = Vector2.zero;
        SetCollidersEnabled(false);
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
        SetCollidersEnabled(true);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collisionCheckRadius);
    }
}
