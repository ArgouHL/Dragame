using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera cam;
    private PlayerInput input;
    private Rigidbody2D rb;
    [SerializeField] private DragLine dragLine;

    // -------- 事件：丟給 SkillManager 用 --------
    /// <summary>移動中的小掃：center, radius, moveDir</summary>
    public event Action<Vector2, float, Vector2> OnSweepMove;

    /// <summary>右鍵蓄力過程：holdTime, t(0~1), origin, dir</summary>
    public event Action<float, float, Vector2, Vector2> OnChargedSweepUpdate;

    /// <summary>右鍵蓄力放開：holdTime, t(0~1), origin, dir</summary>
    public event Action<float, float, Vector2, Vector2> OnChargedSweepReleased;

    // Input
    private InputAction pointerPress;
    private InputAction rightPointerPress;
    private InputAction pointerPosition;

    private Vector2 dragStart;
    private Vector2 moveDir;
    private float currentSpeed;

    [Header("移動設定")]
    public float maxSpeed = 12f;
    public float deceleration = 8f;
    public bool isBlocking = false;

    [Header("小掃設定 (左鍵移動掃)")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;

    [Header("右鍵蓄力掃設定")]
    public float maxChargeTime = 1.5f;
    [SerializeField] private float chargeCenterOffset = 0f;

    private float rightPressStartTime;
    public float rightHoldDuration { get; private set; }
    private bool isLeftDown = false;
    private bool isRightDown = false;
    private bool blockBoth = false;

    private void Awake()
    {
        cam = Camera.main;
        input = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
        dragLine = GetComponentInChildren<DragLine>();

        pointerPress = input.actions["PointerPress"];
        rightPointerPress = input.actions["RightPointerPress"];
        pointerPosition = input.actions["PointerPosition"];

        dragLine?.HideLine();
    }

    private void OnEnable()
    {
        pointerPress.started += OnPress;
        pointerPress.canceled += OnRelease;
        rightPointerPress.started += OnRightPress;
        rightPointerPress.canceled += OnRightRelease;
    }

    private void OnDisable()
    {
        pointerPress.started -= OnPress;
        pointerPress.canceled -= OnRelease;
        rightPointerPress.started -= OnRightPress;
        rightPointerPress.canceled -= OnRightRelease;
    }

    private void Update()
    {
        if (pointerPosition == null) return;
        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());

        // 左鍵拖曳顯示線
        if (pointerPress.IsPressed() && !blockBoth)
            dragLine?.ShowLine(dragStart, pointerWorld);

        // 右鍵蓄力：只算 origin / dir / holdTime / t，丟給事件
        if (isRightDown && !blockBoth)
        {
            float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
            float t = Mathf.Clamp01(hold / maxChargeTime);

            Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
            Vector2 approxDir = pointerWorld - baseOrigin;
            approxDir = approxDir.sqrMagnitude > 0.0001f ? approxDir.normalized : Vector2.right;

            Vector2 origin = baseOrigin + approxDir * chargeCenterOffset;
            Vector2 dir = pointerWorld - origin;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

            OnChargedSweepUpdate?.Invoke(hold, t, origin, dir);
        }
    }

    // ----------------------- 左鍵 -----------------------
    private void OnPress(InputAction.CallbackContext ctx)
    {
        isLeftDown = true;
        if (isRightDown) { BlockBoth(); return; }
        StartDrag();
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        isLeftDown = false;
        if (blockBoth)
        {
            if (!isRightDown) UnblockBoth();
            return;
        }
        EndDrag();
    }

    // ----------------------- 右鍵蓄力 -----------------------
    private void OnRightPress(InputAction.CallbackContext ctx)
    {
        isRightDown = true;
        rightPressStartTime = Time.time;
        if (isLeftDown) { BlockBoth(); return; }
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        isRightDown = false;
        if (blockBoth)
        {
            if (!isLeftDown) UnblockBoth();
            return;
        }

        float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
        rightHoldDuration = hold;
        float t = Mathf.Clamp01(hold / maxChargeTime);

        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
        Vector2 approxDir = pointerWorld - baseOrigin;
        if (approxDir.sqrMagnitude < 0.0001f) return;
        approxDir.Normalize();

        Vector2 origin = baseOrigin + approxDir * chargeCenterOffset;
        Vector2 dir = pointerWorld - origin;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        OnChargedSweepReleased?.Invoke(hold, t, origin, dir);
    }

    // ----------------------- 鎖住行為 -----------------------
    private void BlockBoth()
    {
        blockBoth = true;
        dragLine?.HideLine();
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
    }

    private void UnblockBoth() => blockBoth = false;

    // ----------------------- 拖曳移動 -----------------------
    private void StartDrag()
    {
        dragStart = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
        dragLine?.ShowLine(dragStart, dragStart);
    }

    private void EndDrag()
    {
        dragLine?.HideLine();
        Vector2 drag = ScreenToWorld(pointerPosition.ReadValue<Vector2>()) - dragStart;
        if (drag.magnitude < 0.05f) return;

        moveDir = drag.normalized;
        currentSpeed = Mathf.Min(drag.magnitude * 3f, maxSpeed);
    }

    // ----------------------- 移動物理 -----------------------
    private void FixedUpdate()
    {
        if (isBlocking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentSpeed > 0.01f)
        {
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            Vector3 vp = cam.WorldToViewportPoint(nextPos);

            if (vp.x < 0 || vp.x > 1 || vp.y < 0.1f || vp.y > 1f)
            {
                currentSpeed = 0;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = moveDir * currentSpeed;
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, 0);

            // 小掃事件
            Vector2 center = (Vector2)transform.position + sweepOffset;
            OnSweepMove?.Invoke(center, sweepRadius, moveDir);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private Vector2 ScreenToWorld(Vector2 p)
        => cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }
}
