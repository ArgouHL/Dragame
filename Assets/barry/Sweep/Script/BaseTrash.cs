using UnityEngine;
using System.Collections;

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
    [SerializeField] private float deceleration = 5f;

    [Tooltip("視口邊界的緩衝區 (0.1 = 離邊緣 10% 的地方反彈)")]
    [SerializeField] private float viewportPadding = 0.1f;

    [SerializeField] private float force = 10f;

    [Header("連鎖碰撞設定")]
    [Tooltip("檢查其他垃圾的半徑")]
    [SerializeField] private float collisionCheckRadius = 1.0f;

    [Tooltip("垃圾所在的圖層 (非常重要，避免檢查到玩家或地板)")]
    [SerializeField] private LayerMask trashLayerMask;

    [Tooltip("被擊中後的冷卻時間(秒)，防止重複觸發")]
    [SerializeField] private float hitCooldown = 0.2f;

    [Header("黑洞吸收設定")]
    [Tooltip("檢查黑洞的半徑")]
    [SerializeField] private float blackHoleCheckRadius = 1.5f;

    [Tooltip("黑洞所在的圖層")]
    [SerializeField] private LayerMask blackHoleLayerMask;

    private bool isAbsorbing = false;
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
        if (isAbsorbing) return;

        HandleBlackHoleCheck();

        if (isAbsorbing) return;

        if (currentVelocity.magnitude > 0.01f && !_isRecentlyHit)
        {
            HandleTrashCollisions();
        }

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
        if (isAbsorbing || _isRecentlyHit) return;

        StartHitCooldown();
        currentVelocity = hitDirection.normalized * force;
    }

    /// <summary>
    /// 在 FixedUpdate 中呼叫，主動檢查並觸發與其他垃圾的碰撞
    /// </summary>
    private void HandleTrashCollisions()
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, collisionCheckRadius, trashLayerMask);

        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == this.gameObject)
            {
                continue;
            }

            BaseTrash otherTrash = collider.GetComponent<BaseTrash>();

            if (otherTrash != null && !otherTrash._isRecentlyHit)
            {
                Vector2 hitDirection = (otherTrash.transform.position - this.transform.position).normalized;

                if (hitDirection == Vector2.zero)
                {
                    hitDirection = Random.insideUnitCircle.normalized;
                }

                otherTrash.ApplyBroomHit(hitDirection);
                currentVelocity = -hitDirection * currentVelocity.magnitude;
                StartHitCooldown();

                return; // 成功判斷一次撞擊就退出
            }
        }
    }

    /// <summary>
    /// 在 FixedUpdate 中呼叫，主動檢查是否進入黑洞範圍
    /// </summary>
    private void HandleBlackHoleCheck()
    {
        if (isAbsorbing) return;

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, blackHoleCheckRadius, blackHoleLayerMask);

        if (nearbyColliders.Length > 0)
        {
            Collider2D blackHoleCollider = nearbyColliders[0];
            OnEnterBlackHole(blackHoleCollider.transform.position);
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

    protected virtual void OnEnterBlackHole(Vector3 targetPosition)
    {
        if (!isAbsorbing)
        {
            isAbsorbing = true;
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

        isAbsorbing = false;
        ResetState();
        TrashPool.Instance.ReturnTrash(this);
    }


    public override void ResetState()
    {
        base.ResetState();

        currentVelocity = Vector2.zero;
        isAbsorbing = false;

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