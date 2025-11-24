using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理「垃圾」物件的行為，包含手動物理、連鎖碰撞和被黑洞吸收。
/// 繼承自 BasePoolItem 以支援物件池。
/// </summary>
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
    private Vector2 currentVelocity;

    [Tooltip("每秒速度衰減的百分比 (例如 5 = 每秒衰減 500% 的速度)")]
    [SerializeField] private float deceleration;

    [Tooltip("視口邊界的緩衝區 (0.1 = 離邊緣 10% 的地方反彈)")]
    [SerializeField] private float viewportPadding;

    [SerializeField] private float force;

    [Header("連鎖碰撞設定")]
    [Tooltip("檢查其他垃圾的半徑")]
    [SerializeField] private float collisionCheckRadius;

    [Tooltip("被擊中後的冷卻時間(秒)，防止重複觸發")]
    [SerializeField] private float hitCooldown;

    // 公開屬性：是否正在被吸入 (由 SpatialGridManager 和 BalckObstacle 讀取)
    public bool IsAbsorbing { get; private set; } = false;

    private Camera mainCamera;
    private bool _isRecentlyHit = false;
    private Coroutine _hitCooldownCoroutine;

    protected virtual void Awake()
    {
        mainCamera = Camera.main;
        initialScale = transform.localScale;
    }

    protected virtual void FixedUpdate()
    {
        // 如果正在被吸入，完全停止物理與碰撞邏輯
        if (IsAbsorbing) return;

        // 1. 處理垃圾與垃圾的碰撞 (使用 Grid)
        if (currentVelocity.magnitude > 0.01f && !_isRecentlyHit)
        {
            HandleTrashCollisions();
        }

        // 2. 處理移動與邊界
        HandleMovement();
        HandleBoundaryCheck();
    }

    /// <summary>
    /// 處理移動和速度衰減
    /// </summary>
    private void HandleMovement()
    {
        if (currentVelocity.magnitude == 0) return;

        // 速度衰減 (模擬摩擦力)
        currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);

        if (currentVelocity.magnitude < 0.01f)
        {
            currentVelocity = Vector2.zero;
            return;
        }

        transform.position += (Vector3)currentVelocity * Time.fixedDeltaTime;
    }

    /// <summary>
    /// 處理視口邊界檢查與反彈
    /// </summary>
    private void HandleBoundaryCheck()
    {
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

    /// <summary>
    /// 外部呼叫，對此垃圾施加一個力 (例如被掃把擊中)
    /// </summary>
    public void ApplyBroomHit(Vector2 hitDirection)
    {
        if (IsAbsorbing || _isRecentlyHit) return;

        StartHitCooldown();
        currentVelocity = hitDirection.normalized * force;
    }

    private void HandleTrashCollisions()
    {
        // 優化關鍵：只向 SpatialGridManager 拿附近的垃圾
        List<BaseTrash> potentialCollisions = SpatialGridManager.Instance.GetNearbyTrash(this);

        float checkRadiusSqr = (collisionCheckRadius * 2) * (collisionCheckRadius * 2); // 預先計算半徑和的平方

        foreach (var otherTrash in potentialCollisions)
        {
            if (otherTrash == this) continue;

            // 使用 sqrMagnitude 取代 Distance 以優化效能
            float distSqr = (this.transform.position - otherTrash.transform.position).sqrMagnitude;

            if (distSqr < checkRadiusSqr)
            {
                if (!otherTrash._isRecentlyHit)
                {
                    Vector2 hitDirection = (otherTrash.transform.position - this.transform.position).normalized;
                    if (hitDirection == Vector2.zero) hitDirection = Random.insideUnitCircle.normalized;

                    otherTrash.ApplyBroomHit(hitDirection);
                    currentVelocity = -hitDirection * currentVelocity.magnitude; // 簡單的反作用力
                    StartHitCooldown();
                    return;
                }
            }
        }
    }

    private void StartHitCooldown()
    {
        _isRecentlyHit = true;
        // 如果上一個冷卻協程還在跑，先停止它
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

    /// <summary>
    /// 被 BalckObstacle 主動呼叫。
    /// </summary>
    public void OnEnterBlackHole(Vector3 targetPosition)
    {
        if (!IsAbsorbing)
        {
            IsAbsorbing = true;
            currentVelocity = Vector2.zero; // 停止手動物理
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

        // 重置冷卻狀態
        if (_hitCooldownCoroutine != null)
        {
            StopCoroutine(_hitCooldownCoroutine);
            _hitCooldownCoroutine = null;
        }
        _isRecentlyHit = false;
    }
}