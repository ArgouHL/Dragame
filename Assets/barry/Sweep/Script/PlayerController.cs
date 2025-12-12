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

    public float CurrentSpeed => currentSpeed;

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

        if (isRightDown && !blockBoth)
        {
            float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
            float t = Mathf.Clamp01(hold / maxChargeTime);

            Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
            Vector2 toPointer = pointerWorld - baseOrigin;
            float sqrMag = toPointer.sqrMagnitude;

            if (sqrMag > 0.0001f)
            {
                float mag = Mathf.Sqrt(sqrMag);
                chargedDir = toPointer / mag;
            }
            else if (chargedDir.sqrMagnitude < 0.0001f)
            {
                chargedDir = Vector2.right;
            }

            Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;

            OnChargedSweepUpdate?.Invoke(hold, t, origin, chargedDir);
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
        float sqrMag = toPointer.sqrMagnitude;

        float mag = Mathf.Sqrt(sqrMag);
        chargedDir = toPointer / mag;

        Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;

        OnChargedSweepReleased?.Invoke(hold, t, origin, chargedDir);
    }

    private void BlockBoth()
    {
        blockBoth = true;
        dragLine?.HideLine();
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
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

    public void ApplyHitSlowdown(float totalWeight, float sweepPower01)
    {
        if (totalWeight <= 0f || currentSpeed <= 0f)
            return;

        float w = Mathf.Max(0f, totalWeight) * Mathf.Clamp01(sweepPower01);
        if (w <= 0f)
            return;

        float factor = 1f / (1f + w);
        currentSpeed *= factor;

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
