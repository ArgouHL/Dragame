using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class WitchController : MonoBehaviour
{
    [Header("移動參數")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float wallPadding = 0.5f; // 魔女身體半徑，避免穿幫

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;
        _rb.gravityScale = 0f;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // 1. 計算目標速度
        Vector2 targetVelocity = _moveInput * moveSpeed;
        _rb.linearVelocity = targetVelocity;

        // 2. 邊界檢查 (手動干涉物理)
        if (WorldBounds2D.Instance != null)
        {
            // 預測下一幀的位置
            Vector2 nextPos = _rb.position + _rb.linearVelocity * Time.fixedDeltaTime;

            // 檢查是否出界
            if (WorldBounds2D.Instance.IsOutside(nextPos))
            {
                // 使用 Bounce 修正位置與速度 (雖然魔女通常不需要反彈，但這能確保她在界內)
                // 這裡傳入 wallPadding 讓魔女在碰到邊界前就停下，保留身體空間
                Vector2 correctedPos = nextPos;
                Vector2 correctedVel = _rb.linearVelocity;

                WorldBounds2D.Instance.Bounce(ref correctedPos, ref correctedVel, wallPadding);

                // 強制修正位置，並歸零撞牆方向的速度 (避免黏牆抖動)
                _rb.position = correctedPos;

                // 如果是簡單的阻擋，可以直接把速度歸零，或者保留切線速度
                // 這裡簡單處理：如果修正後的反彈速度很大，代表撞牆了，我們把那分量殺掉
                _rb.linearVelocity = Vector2.zero; // 最簡單：撞牆就停
            }
        }
    }
}