using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Vector2 fixForWall;
    private Camera cam;
    private PlayerInput input;
    public Rigidbody2D rb;
    [SerializeField] private DragLine dragLine;

    public event Action<Vector2, float, Vector2, float> OnSweepMove;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepUpdate;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepReleased;

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

    [Header("玩家重量設定 (越大越不容易被垃圾反推)")]
    [SerializeField, Min(0.01f)] private float wieght = 1f;

    [Header("掃到垃圾的反作用力設定")]
    [SerializeField, Range(0f, 1f)] private float sweepRestitution = 0f;
    [SerializeField] private bool allowBounceBack = false;

    [Header("玩家被垃圾影響的手感")]
    [SerializeField] private bool scaleHitBySweepPower = true;          // 是否依速度(掃力)縮放撞擊效果
    [SerializeField, Range(0f, 1f)] private float minHitPower = 0.25f;  // 速度很低時也至少保留多少撞擊量
    [SerializeField, Min(0f)] private float hitWeightScale = 1f;        // 放大/縮小垃圾重量對玩家減速的影響

    public float CurrentSpeed => currentSpeed;
    public float Wieght => wieght;

    private float rightPressStartTime;
    public float rightHoldDuration { get; private set; }
    private bool isLeftDown = false;
    private bool isRightDown = false;
    private bool blockBoth = false;

    public bool isBeingAbsorbed;
    private Collider2D[] colliders;

    private Vector2 chargedDir;

    public bool IsBeingAbsorbed => isBeingAbsorbed;

    public static PlayerController instance { get; private set; }

    [Header("右鍵蓄力掃視覺")]
    [SerializeField] private DynamicSweepMesh chargedSweepMesh;
    [SerializeField] private float chargedSweepRotationOffset = -90f; // DynamicSweepMesh 預設朝上(+Y)，轉成朝右需 -90

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        cam = Camera.main;
        input = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
        dragLine = GetComponentInChildren<DragLine>();

        pointerPress = input.actions["PointerPress"];
        rightPointerPress = input.actions["RightPointerPress"];
        pointerPosition = input.actions["PointerPosition"];

        dragLine?.HideLine();

        if (chargedSweepMesh != null)
            chargedSweepMesh.gameObject.SetActive(false);
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
        if (isBeingAbsorbed) return;
        if (pointerPosition == null) return;

        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());

        // 左鍵拖曳線
        if (pointerPress.IsPressed() && !blockBoth)
        {
            Vector2 drag = pointerWorld - dragStart;
            float len = drag.magnitude;

            Vector2 center = (Vector2)transform.position + sweepOffset;

            if (len > 0.001f)
            {
                Vector2 dir = drag / len;
                dragLine?.ShowLine(center, center + dir * len);
            }
            else
            {
                dragLine?.ShowLine(center, center);
            }
        }

        // 右鍵蓄力：更新方向 + 事件 + 視覺跟隨滑鼠
        if (isRightDown && !blockBoth)
        {
            float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
            float t = Mathf.Clamp01(hold / maxChargeTime);

            Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
            Vector2 toPointer = pointerWorld - baseOrigin;

            if (toPointer.sqrMagnitude > 0.0001f)
            {
                chargedDir = toPointer.normalized;
            }
            else if (chargedDir.sqrMagnitude < 0.0001f)
            {
                chargedDir = Vector2.right;
            }

            Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;

            // 事件：給你的判定/效果系統用
            OnChargedSweepUpdate?.Invoke(hold, t, origin, chargedDir);

            // 視覺：不新增 script，直接在這裡控制 Mesh 物件的 transform
            if (chargedSweepMesh != null)
            {
                if (!chargedSweepMesh.gameObject.activeSelf)
                    chargedSweepMesh.gameObject.SetActive(true);

                chargedSweepMesh.transform.position = origin;

                float ang = Mathf.Atan2(chargedDir.y, chargedDir.x) * Mathf.Rad2Deg + chargedSweepRotationOffset;
                chargedSweepMesh.transform.rotation = Quaternion.Euler(0f, 0f, ang);

                chargedSweepMesh.UpdateShape(t);
            }
        }
        else
        {
            // 沒有右鍵按住時，視覺關掉（避免卡在畫面上）
            if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
                chargedSweepMesh.gameObject.SetActive(false);
        }
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

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
        if (isBeingAbsorbed) return;

        isLeftDown = false;
        if (blockBoth)
        {
            if (!isRightDown) UnblockBoth();
            return;
        }
        EndDrag();
    }

    private void OnRightPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        isRightDown = true;
        rightPressStartTime = Time.time;

        if (isLeftDown)
        {
            BlockBoth();
            return;
        }

        // 右鍵按下當下就先開啟視覺（避免第一幀延遲）
        if (chargedSweepMesh != null && !chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(true);
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

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
        Vector2 toPointer = pointerWorld - baseOrigin;

        if (toPointer.sqrMagnitude > 0.0001f)
        {
            chargedDir = toPointer.normalized;
        }
        else if (chargedDir.sqrMagnitude < 0.0001f)
        {
            chargedDir = Vector2.right;
        }

        Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;

        OnChargedSweepReleased?.Invoke(hold, t, origin, chargedDir);

        // 放開後關掉視覺（如果你想放開後還留著動畫，可改成延遲關閉）
        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    private void BlockBoth()
    {
        blockBoth = true;
        dragLine?.HideLine();
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;

        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    private void UnblockBoth() => blockBoth = false;

    private void StartDrag()
    {
        dragStart = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;

        Vector2 center = (Vector2)transform.position + sweepOffset;
        dragLine?.ShowLine(center, center);
    }

    private void EndDrag()
    {
        dragLine?.HideLine();
        Vector2 drag = ScreenToWorld(pointerPosition.ReadValue<Vector2>()) - dragStart;
        float len = drag.magnitude;
        if (len < 0.05f) return;

        moveDir = drag / len;
        currentSpeed = Mathf.Min(len * 3f, maxSpeed);
    }

    /// <summary>
    /// 讓玩家因為「掃到/撞到」垃圾而產生反作用力（主要是減速）
    /// totalTrashWieght：本次命中的垃圾 wieght 加總
    /// sweepPower01：0~1（可用玩家速度/蓄力強度）
    /// </summary>
    public void ApplyHitSlowdown(float totalTrashWieght, float sweepPower01)
    {
        if (currentSpeed <= 0f) return;
        if (totalTrashWieght <= 0f) return;

        float M = Mathf.Max(0.01f, wieght);

        float p = 1f;
        if (scaleHitBySweepPower)
            p = Mathf.Lerp(minHitPower, 1f, Mathf.Clamp01(sweepPower01));

        float m = Mathf.Max(0f, totalTrashWieght) * hitWeightScale * p;
        if (m <= 0f) return;

        float e = Mathf.Clamp01(sweepRestitution);

        // 1D 碰撞（垃圾初速視為 0）：v' = (M - e*m) / (M + m) * v
        float ratio = (M - e * m) / (M + m);

        if (!allowBounceBack)
        {
            ratio = Mathf.Clamp01(ratio);
            currentSpeed *= ratio;
        }
        else
        {
            if (ratio < 0f)
            {
                moveDir = -moveDir;
                currentSpeed *= Mathf.Clamp01(-ratio);
            }
            else
            {
                currentSpeed *= Mathf.Clamp01(ratio);
            }
        }

        if (currentSpeed <= 0.01f)
        {
            currentSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            rb.linearVelocity = moveDir * currentSpeed;
        }
    }

    private void FixedUpdate()
    {
        if (isBlocking || isBeingAbsorbed)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentSpeed > 0.01f)
        {
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            nextPos += fixForWall;
            if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(nextPos))
            {
                currentSpeed = 0f;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = moveDir * currentSpeed;
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, 0f);

            float sweepPower = Mathf.Clamp01(currentSpeed / maxSpeed);
            Vector2 center = (Vector2)transform.position + sweepOffset;

            OnSweepMove?.Invoke(center, sweepRadius, moveDir, sweepPower);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void EnsureColliders()
    {
        if (colliders != null) return;
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        EnsureColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    public void EnterBlackHole()
    {
        if (isBeingAbsorbed) return;

        isBeingAbsorbed = true;
        blockBoth = true;
        isLeftDown = false;
        isRightDown = false;
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
        dragLine?.HideLine();
        SetCollidersEnabled(false);

        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    public void ExitBlackHole(Vector2 ejectDir, float ejectSpeed)
    {
        isBeingAbsorbed = false;
        blockBoth = false;
        moveDir = ejectDir;
        currentSpeed = ejectSpeed;
        SetCollidersEnabled(true);
        rb.linearVelocity = moveDir * currentSpeed;
    }

    private Vector2 ScreenToWorld(Vector2 p)
        => cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }
}
