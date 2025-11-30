using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera cam;
    private PlayerInput input;
    private Rigidbody2D rb;
    [SerializeField] private DragLine dragLine;

    // Input
    private InputAction pointerPress;
    private InputAction rightPointerPress;
    private InputAction pointerPosition;

    private Vector2 dragStart;
    private Vector2 moveDir;
    private Vector2 sweepDir;

    private float currentSpeed;
    public float maxSpeed = 12f;
    public float deceleration = 8f;
    public bool isBlocking = false;

    [Header("左鍵移動掃觸發")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;
    public LayerMask trashLayer;

    [Header("右鍵蓄力掃")]
    public float maxChargeTime = 1.5f;
    public float minSweepRadius = 1f;
    public float maxChargedRadius = 4f;
    public float minForceMultiplier = 1f;
    public float maxForceMultiplier = 3f;

    [SerializeField] private Transform chargedSweepRoot;
    [SerializeField] private DynamicSweepMesh sweepMesh;
    [SerializeField] private CapsuleCollider2D sweepCollider;

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

        if (chargedSweepRoot != null)
        {
            if (!sweepMesh) sweepMesh = chargedSweepRoot.GetComponentInChildren<DynamicSweepMesh>(true);
            if (!sweepCollider) sweepCollider = chargedSweepRoot.GetComponentInChildren<CapsuleCollider2D>(true);
            chargedSweepRoot.gameObject.SetActive(false);
        }
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

        // 右鍵蓄力扇形更新
        if (isRightDown && !blockBoth && chargedSweepRoot != null)
        {
            Vector2 origin = (Vector2)transform.position + sweepOffset;
            Vector2 dir = pointerWorld - origin;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

            chargedSweepRoot.position = origin;
            chargedSweepRoot.right = dir;

            float t = Mathf.Clamp01((Time.time - rightPressStartTime) / maxChargeTime);

            sweepMesh?.UpdateShape(t);

            if (sweepCollider)
            {
                float length = Mathf.Lerp(sweepMesh.minLength, sweepMesh.maxLength, t);
                float width = Mathf.Lerp(sweepMesh.headWidth, sweepMesh.tailMaxWidth, t);

                sweepCollider.direction = CapsuleDirection2D.Horizontal;
                sweepCollider.size = new Vector2(length, width);
                sweepCollider.offset = new Vector2(length * 0.5f, 0f);
                sweepCollider.isTrigger = true;
            }

            chargedSweepRoot.gameObject.SetActive(true);
        }
        else if (!isRightDown && chargedSweepRoot != null)
            chargedSweepRoot.gameObject.SetActive(false);
    }

    //----------------------- 左鍵 -----------------------
    private void OnPress(InputAction.CallbackContext ctx)
    {
        isLeftDown = true;
        if (isRightDown) { BlockBoth(); return; }
        StartDrag();
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        isLeftDown = false;
        if (blockBoth) { if (!isRightDown) UnblockBoth(); return; }
        EndDrag();
    }

    //----------------------- 右鍵蓄力 -----------------------
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
            chargedSweepRoot?.gameObject.SetActive(false);
            return;
        }

        rightHoldDuration = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);

        Vector2 origin = (Vector2)transform.position + sweepOffset;
        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 dir = pointerWorld - origin;
        if (dir.sqrMagnitude < 0.0001f) { chargedSweepRoot?.gameObject.SetActive(false); return; }

        sweepDir = dir.normalized;
        DoChargedSweep(rightHoldDuration);
        chargedSweepRoot?.gameObject.SetActive(false);
    }

    //----------------------- 鎖住行為 -----------------------
    private void BlockBoth()
    {
        blockBoth = true;
        dragLine?.HideLine();
        chargedSweepRoot?.gameObject.SetActive(false);
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
    }
    private void UnblockBoth() => blockBoth = false;

    //----------------------- 拖曳移動 -----------------------
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

    //----------------------- 移動物理 -----------------------
    private void FixedUpdate()
    {
        if (isBlocking) { rb.linearVelocity = Vector2.zero; return; }

        if (currentSpeed > 0.01f)
        {
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            Vector3 vp = cam.WorldToViewportPoint(nextPos);

            // 防止飛出鏡頭
            if (vp.x < 0 || vp.x > 1 || vp.y < 0.1f || vp.y > 1f)
            { currentSpeed = 0; rb.linearVelocity = Vector2.zero; return; }

            rb.linearVelocity = moveDir * currentSpeed;
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, 0);
            Sweep();
        }
        else rb.linearVelocity = Vector2.zero;
    }

    //----------------------- 小掃 + 蓄力掃 -----------------------
    private void Sweep()
    {
        Vector2 center = (Vector2)transform.position + sweepOffset;
        foreach (var hit in Physics2D.OverlapCircleAll(center, sweepRadius, trashLayer))
            hit.GetComponent<BaseTrash>()?.ApplyBroomHit(moveDir);
    }

    private void DoChargedSweep(float chargeTime)
    {
        if (!sweepCollider) return;

        float t = Mathf.Clamp01(chargeTime / maxChargeTime);
        float forceMul = Mathf.Lerp(minForceMultiplier, maxForceMultiplier, t);

        ContactFilter2D filter = new() { useLayerMask = true, layerMask = trashLayer, useTriggers = true };
        Collider2D[] results = new Collider2D[32];
        int count = sweepCollider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
            results[i]?.GetComponent<BaseTrash>()?.ApplyBroomHit(sweepDir * forceMul, rightHoldDuration);
    }

    private Vector2 ScreenToWorld(Vector2 p) => cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }
}
