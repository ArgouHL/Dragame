using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class WitchController : MonoBehaviour
{
    [Header("移動參數")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float wallPadding = 0.5f;

    [Header("碰撞偏移")]
    [Tooltip("用於修正圖片中心與實際碰撞邊界的偏移量")]
    public Vector2 fixForWall;

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
        Vector2 targetVelocity = _moveInput * moveSpeed;

        if (WorldBounds2D.Instance != null)
        {
            Vector2 currentPos = _rb.position + fixForWall;

            if (targetVelocity.sqrMagnitude > 0f)
            {
                Vector2 nextPos = currentPos + targetVelocity * Time.fixedDeltaTime;

                // WHY: 提早計算下一幀的法線，將速度沿牆面投影（滑牆），取代事後強制覆寫座標造成的物理拉扯與抖動。
                if (WorldBounds2D.Instance.TryGetHitPointAndNormalWorld(nextPos, out _, out Vector2 normal, wallPadding))
                {
                    float velocityIntoWall = Vector2.Dot(targetVelocity, normal);

                    // WHY: 只剔除「朝向牆壁」的速度分量，保留切線速度，讓魔女順滑貼牆移動。
                    if (velocityIntoWall < 0f)
                    {
                        targetVelocity -= normal * velocityIntoWall;
                    }
                }
            }

            // WHY: 獨立的保底機制。若因浮點數誤差或外部碰撞導致微小穿模，才進行位置微調，確保不陷入死角。
            if (WorldBounds2D.Instance.TryGetHitPointAndNormalWorld(currentPos, out Vector2 safePos, out _, wallPadding))
            {
                _rb.position = safePos - fixForWall;
            }
        }

        _rb.linearVelocity = targetVelocity;
    }
}