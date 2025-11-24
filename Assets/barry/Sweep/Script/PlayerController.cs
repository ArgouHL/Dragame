using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera cam;
    private PlayerInput input;
    private Rigidbody2D rb;

    private Vector2 dragStart;
    private Vector2 moveDir;

    private float currentSpeed;
    public float maxSpeed = 12f;
    public float deceleration = 8f;
    public bool isBlocking = false;

    [Header("Sweep")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;
    public LayerMask trashLayer;

    private void Awake()
    {
        cam = Camera.main;
        input = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        input.actions["PointerPress"].started += OnPress;
        input.actions["PointerPress"].canceled += OnRelease;
    }

    private void OnDisable()
    {
        input.actions["PointerPress"].started -= OnPress;
        input.actions["PointerPress"].canceled -= OnRelease;
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        dragStart = ScreenToWorld(Input.mousePosition);
        currentSpeed = 0f;               // 拖曳中停止慣性
        rb.linearVelocity = Vector2.zero;
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        Vector2 dragEnd = ScreenToWorld(Input.mousePosition);
        Vector2 drag = dragEnd - dragStart;

        if (drag.magnitude < 0.05f)
            return; // 拖曳太短 → 不移動

        moveDir = drag.normalized;

        // 初速度 = 拖曳長度（限制最大速度）
        currentSpeed = Mathf.Min(drag.magnitude * 3f, maxSpeed);
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
    }

    private void FixedUpdate()
    {
        if (isBlocking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentSpeed > 0.01f)
        {
            // ✅ 先預測下一幀位置，用來做邊界檢查
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            Vector3 vp = cam.WorldToViewportPoint(nextPos);

            // ✅ 超出視窗範圍就直接停下來
            if (vp.x < 0f || vp.x > 1f || vp.y < 0.1f || vp.y > 1f)
            {
                currentSpeed = 0f;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            // 真正移動
            rb.linearVelocity = moveDir * currentSpeed;

            // 慣性減速
            currentSpeed -= deceleration * Time.fixedDeltaTime;
            if (currentSpeed < 0f) currentSpeed = 0f;

            Sweep();
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Sweep()
    {
        Vector2 center = (Vector2)transform.position + sweepOffset;

        var hits = Physics2D.OverlapCircleAll(center, sweepRadius, trashLayer);

        foreach (var hit in hits)
            hit.GetComponent<BaseTrash>()?.ApplyBroomHit(moveDir);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }
}
