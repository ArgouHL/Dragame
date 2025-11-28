using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera cam;
    private PlayerInput input;
    private Rigidbody2D rb;
    [SerializeField] private DragLine dragLine;

    private InputAction pointerPress;        // 左鍵
    private InputAction rightPointerPress;   // 右鍵
    private InputAction pointerPosition;     // 滑鼠 / 觸控位置

    private Vector2 dragStart;
    private Vector2 moveDir;    // 左鍵移動用
    private Vector2 sweepDir;   // 右鍵蓄力掃用

    private float currentSpeed;
    public float maxSpeed = 12f;
    public float deceleration = 8f;
    public bool isBlocking = false;

    [Header("左鍵移動時的小掃範圍")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;
    public LayerMask trashLayer;

    [Header("右鍵蓄力掃設定")]
    public float maxChargeTime = 1.5f;        // 蓄力上限秒數
    public float minSweepRadius = 1f;         // 最小扇形半徑 (只拿來算 scale)
    public float maxChargedRadius = 4f;       // 最大扇形半徑
    public float minForceMultiplier = 1f;     // 最小力度倍率
    public float maxForceMultiplier = 3f;     // 最大力度倍率

    [Header("右鍵蓄力扇形顯示")]
    [SerializeField] private Transform chargedSweepRoot;        // ChargeConeRoot
    [SerializeField] private SpriteRenderer chargedSweepSprite;      // ChargeCone 的 SpriteRenderer
    [SerializeField] private Collider2D chargedSweepCollider;    // 🔹 ChargeCone 上的 Collider2D
    [Tooltip("當 chargedSweepRoot.localScale = 1 時，扇形圖實際半徑")]
    [SerializeField] private float baseSpriteRadius = 1f;

    // 右鍵按住時間
    private float rightPressStartTime;
    public float rightHoldDuration { get; private set; }

    // 左右鍵排他
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

        if (dragLine != null)
            dragLine.HideLine();

        if (chargedSweepRoot != null)
            chargedSweepRoot.gameObject.SetActive(false);
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
        if (pointerPosition == null)
            return;

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        Vector2 pointerWorld = ScreenToWorld(pointer);

        // ✅ 只有左鍵會顯示拖曳線
        if (pointerPress.IsPressed() && !blockBoth)
        {
            if (dragLine != null)
                dragLine.ShowLine(dragStart, pointerWorld);
        }

        // ✅ 右鍵只顯示扇形 (Sprite + Collider 同步縮放)
        if (isRightDown && !blockBoth && chargedSweepRoot != null)
        {
            Vector2 origin = (Vector2)transform.position + sweepOffset;
            Vector2 dir = pointerWorld - origin;
            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();
            else
                dir = transform.right;

            // 位置 / 朝向
            chargedSweepRoot.position = origin;
            chargedSweepRoot.right = dir;

            // 蓄力 0~1
            float hold = Time.time - rightPressStartTime;
            float t = Mathf.Clamp01(hold / maxChargeTime);

            // 想用「半徑」來控制縮放，所以仍用 min/maxSweepRadius 轉成 scale
            float radius = Mathf.Lerp(minSweepRadius, maxChargedRadius, t);
            float scale = baseSpriteRadius > 0f ? radius / baseSpriteRadius : 1f;
            chargedSweepRoot.localScale = new Vector3(scale, scale, 1f);

            // Sprite 透明度
            if (chargedSweepSprite != null)
            {
                Color c = chargedSweepSprite.color;
                c.a = Mathf.Lerp(0.2f, 0.7f, t);
                chargedSweepSprite.color = c;
            }

            chargedSweepRoot.gameObject.SetActive(true);
        }
        else if (!isRightDown && chargedSweepRoot != null)
        {
            chargedSweepRoot.gameObject.SetActive(false);
        }
    }

    // -------- 左鍵：拖曳＋發射 --------
    private void OnPress(InputAction.CallbackContext ctx)
    {
        isLeftDown = true;

        if (isRightDown)
        {
            BlockBoth();
            return;
        }

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

    // -------- 右鍵：蓄力扇形掃 --------
    private void OnRightPress(InputAction.CallbackContext ctx)
    {
        isRightDown = true;
        rightPressStartTime = Time.time;

        if (isLeftDown)
        {
            BlockBoth();
            return;
        }
        // 右鍵只做蓄力，不畫線
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        isRightDown = false;

        float hold = Time.time - rightPressStartTime;

        if (blockBoth)
        {
            if (!isLeftDown) UnblockBoth();
            if (chargedSweepRoot != null) chargedSweepRoot.gameObject.SetActive(false);
            return;
        }

        rightHoldDuration = Mathf.Clamp(hold, 0f, maxChargeTime);

        // 先算方向 + 打擊，打完再關閉扇形
        Vector2 origin = (Vector2)transform.position + sweepOffset;
        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 dir = pointerWorld - origin;
        if (dir.sqrMagnitude < 0.0001f)
        {
            if (chargedSweepRoot != null)
                chargedSweepRoot.gameObject.SetActive(false);
            return;
        }

        sweepDir = dir.normalized;
        DoChargedSweep(rightHoldDuration);

        if (chargedSweepRoot != null)
            chargedSweepRoot.gameObject.SetActive(false);
    }

    // -------- 左右鍵同時按：全部取消 --------
    private void BlockBoth()
    {
        blockBoth = true;

        if (dragLine != null)
            dragLine.HideLine();
        if (chargedSweepRoot != null)
            chargedSweepRoot.gameObject.SetActive(false);

        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
    }

    private void UnblockBoth()
    {
        blockBoth = false;
    }

    // -------- 左鍵拖曳邏輯 --------
    private void StartDrag()
    {
        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        dragStart = ScreenToWorld(pointer);

        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;

        if (dragLine != null)
            dragLine.ShowLine(dragStart, dragStart);
    }

    private void EndDrag()
    {
        if (dragLine != null)
            dragLine.HideLine();

        Vector2 pointer = pointerPosition.ReadValue<Vector2>();
        Vector2 dragEnd = ScreenToWorld(pointer);
        Vector2 drag = dragEnd - dragStart;

        if (drag.magnitude < 0.05f)
            return;

        moveDir = drag.normalized;
        currentSpeed = Mathf.Min(drag.magnitude * 3f, maxSpeed);
    }

    // -------- 工具函式 --------
    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
    }

    private void FixedUpdate()
    {
        if (isBlocking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 左鍵發射移動
        if (currentSpeed > 0.01f)
        {
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            Vector3 vp = cam.WorldToViewportPoint(nextPos);

            if (vp.x < 0f || vp.x > 1f || vp.y < 0.1f || vp.y > 1f)
            {
                currentSpeed = 0f;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = moveDir * currentSpeed;

            currentSpeed -= deceleration * Time.fixedDeltaTime;
            if (currentSpeed < 0f) currentSpeed = 0f;

            Sweep(); // 移動時的小掃
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // 左鍵移動時的小圓掃
    private void Sweep()
    {
        Vector2 center = (Vector2)transform.position + sweepOffset;
        var hits = Physics2D.OverlapCircleAll(center, sweepRadius, trashLayer);

        foreach (var hit in hits)
            hit.GetComponent<BaseTrash>()?.ApplyBroomHit(moveDir);
    }

    // 右鍵蓄力扇形掃（用 ChargeCone 的 Collider2D）
    private void DoChargedSweep(float chargeTime)
    {
        if (chargedSweepCollider == null)
            return;

        float t = Mathf.Clamp01(chargeTime / maxChargeTime);
        float forceMul = Mathf.Lerp(minForceMultiplier, maxForceMultiplier, t);

        // 用 Collider2D 自身形狀做 Overlap
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = trashLayer;
        filter.useTriggers = true;   // 如果垃圾是 Trigger，就設 true

        Collider2D[] results = new Collider2D[32];
        int count = chargedSweepCollider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            var col = results[i];
            var trash = col.GetComponent<BaseTrash>();
            if (trash == null)
                continue;

            Vector2 forceDir = sweepDir * forceMul;
            trash.ApplyBroomHit(forceDir, rightHoldDuration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }
}
