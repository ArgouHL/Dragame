using System.Collections;
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
    [Tooltip("垃圾的當前速度")]
    [SerializeField] private Vector2 currentVelocity;

    [Tooltip("每秒速度衰減的百分比 (例如 5 = 每秒衰減 500% 的速度)")]
    [SerializeField] private float deceleration = 5f;

    [Tooltip("視口邊界的緩衝區 (0.1 = 離邊緣 10% 的地方反彈)")]
    [SerializeField] private float viewportPadding = 0.05f;

    [Tooltip("玩家掃把給的力道")]
    [SerializeField] private float broomForce = 10f;

    [Tooltip("垃圾互撞給的力道")]
    [SerializeField] private float trashForce = 3f;

    [Header("連鎖碰撞設定")]
    [Tooltip("檢查其他垃圾的半徑")]
    [SerializeField] private float collisionCheckRadius = 0.5f;

    [Tooltip("被擊中後的冷卻時間(秒)，防止重複觸發")]
    [SerializeField] private float hitCooldown = 0.05f;

    [Tooltip("垃圾互撞後保留多少速度（0~1，越小越快慢下來）")]
    [SerializeField] private float collisionDamping = 0.6f;

    [Tooltip("觸發碰撞所需的最小相對速度")]
    [SerializeField] private float minCollisionSpeed = 0.4f;

    [Header("睡眠（停止運算）設定")]
    [Tooltip("低於這個速度就開始計算睡眠時間")]
    [SerializeField] private float sleepSpeedThreshold = 0.1f;

    [Tooltip("速度持續低於門檻多久就進入睡眠狀態")]
    [SerializeField] private float sleepTime = 1.0f;

    public bool IsAbsorbing { get; private set; } = false;

    public Vector2 CurrentVelocity => currentVelocity;

    private Camera mainCamera;
    private bool _isRecentlyHit = false;
    private Coroutine _hitCooldownCoroutine;

    private bool _isSleeping = false;
    private float _sleepTimer = 0f;

    protected virtual void Awake()
    {
        mainCamera = Camera.main;
        initialScale = transform.localScale;
    }

    protected virtual void FixedUpdate()
    {
        if (IsAbsorbing) return;
        if (_isSleeping) return;

        if (currentVelocity.sqrMagnitude > 0.0001f && !_isRecentlyHit)
        {
            HandleTrashCollisions();
        }

        HandleMovement();
        HandleBoundaryCheck();
        HandleSleepCheck();
    }

    private void HandleMovement()
    {
        if (currentVelocity == Vector2.zero) return;

        currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);

        if (currentVelocity.magnitude < 0.001f)
        {
            currentVelocity = Vector2.zero;
            return;
        }

        transform.position += (Vector3)currentVelocity * Time.fixedDeltaTime;
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
        if (mainCamera == null) return;

        Vector3 vp = mainCamera.WorldToViewportPoint(transform.position);
        bool bounceX = false;
        bool bounceY = false;

        if (vp.x < viewportPadding)
        {
            vp.x = viewportPadding;
            bounceX = true;
        }
        else if (vp.x > 1 - viewportPadding)
        {
            vp.x = 1 - viewportPadding;
            bounceX = true;
        }

        if (vp.y < viewportPadding)
        {
            vp.y = viewportPadding;
            bounceY = true;
        }
        else if (vp.y > 1 - viewportPadding)
        {
            vp.y = 1 - viewportPadding;
            bounceY = true;
        }

        if (bounceX || bounceY)
        {
            transform.position = mainCamera.ViewportToWorldPoint(vp);
            if (bounceX) currentVelocity.x *= -1;
            if (bounceY) currentVelocity.y *= -1;
        }
    }

    public void ApplyBroomHit(Vector2 hitDirection)
    {
        if (IsAbsorbing) return;

        WakeUp();
        StartHitCooldown();
        currentVelocity = hitDirection.normalized * broomForce;
    }

    public void ApplyBroomHit(Vector2 hitDirection, float power)
    {
        if (IsAbsorbing) return;

        WakeUp();
        StartHitCooldown();
        currentVelocity = hitDirection.normalized * broomForce * power;
    }

    public void ApplyTrashHit(Vector2 hitDirection)
    {
        if (IsAbsorbing) return;

        WakeUp();
        StartHitCooldown();

        Vector2 impulse = hitDirection.normalized * trashForce;
        currentVelocity += impulse;
        currentVelocity *= collisionDamping;
    }

    private void HandleTrashCollisions()
    {
        List<BaseTrash> potentialCollisions = SpatialGridManager.Instance.GetNearbyTrash(this);

        float checkRadiusSqr = collisionCheckRadius * collisionCheckRadius;
        Vector2 selfPos = transform.position;

        foreach (var otherTrash in potentialCollisions)
        {
            if (otherTrash == null || otherTrash == this) continue;
            if (otherTrash.IsAbsorbing) continue;

            Vector2 otherPos = otherTrash.transform.position;
            float distSqr = (selfPos - otherPos).sqrMagnitude;

            if (distSqr > checkRadiusSqr) continue;

            Vector2 relativeVel = currentVelocity - otherTrash.CurrentVelocity;
            if (relativeVel.sqrMagnitude < minCollisionSpeed * minCollisionSpeed)
                continue;

            if (!otherTrash._isRecentlyHit)
            {
                Vector2 hitDirection = (otherPos - selfPos).normalized;
                if (hitDirection == Vector2.zero)
                    hitDirection = Random.insideUnitCircle.normalized;

                otherTrash.ApplyTrashHit(hitDirection);

                Vector2 selfImpulse = -hitDirection * trashForce;
                currentVelocity += selfImpulse;
                currentVelocity *= collisionDamping;

                StartHitCooldown();
                return;
            }
        }
    }

    private void StartHitCooldown()
    {
        _isRecentlyHit = true;
        if (_hitCooldownCoroutine != null)
        {
            StopCoroutine(_hitCooldownCoroutine);
        }
        _hitCooldownCoroutine = StartCoroutine(HitCooldownCoroutine());
    }

    private IEnumerator HitCooldownCoroutine()
    {
        yield return new WaitForSeconds(hitCooldown);
        _isRecentlyHit = false;
        _hitCooldownCoroutine = null;
    }

    public void OnEnterBlackHole(Vector3 targetPosition)
    {
        if (!IsAbsorbing)
        {
            IsAbsorbing = true;
            _isSleeping = false;
            currentVelocity = Vector2.zero;
            StartCoroutine(AbsorbEffect(targetPosition));
        }
    }

    protected virtual IEnumerator AbsorbEffect(Vector3 target)
    {
        Vector3 initialPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < absorbEffectDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / absorbEffectDuration;
            float scale_t = scaleCurve.Evaluate(t);
            float move_t = moveCurve.Evaluate(t);

            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            transform.localScale = Vector3.LerpUnclamped(initialScale, Vector3.zero, scale_t);
            transform.position = Vector2.LerpUnclamped(initialPosition, target, move_t);

            yield return null;
        }

        IsAbsorbing = false;
        ResetState();
        TrashPool.Instance.ReturnTrash(this);
    }

    public override void ResetState()
    {
        base.ResetState();

        currentVelocity = Vector2.zero;
        IsAbsorbing = false;
        transform.localScale = initialScale;

        if (_hitCooldownCoroutine != null)
        {
            StopCoroutine(_hitCooldownCoroutine);
            _hitCooldownCoroutine = null;
        }
        _isRecentlyHit = false;

        _isSleeping = false;
        _sleepTimer = 0f;
    }
}
