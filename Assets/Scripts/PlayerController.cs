using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{

    [Header("Movement")]
    [SerializeField] private float moveSpeed ;

    [Header("Status")]
    [SerializeField] private float stunDuration;
    private bool isStunned = false;
    private bool isMoving = false;

    private Rigidbody2D rb;
    private InputSystem_Actions inputSystem;
    private Camera mainCamera;

    public static PlayerController instance;
    public int droletCount;
    public TextMeshPro dropletText;

    private void Awake()
    {

        // 單例模式
        if (instance == null)
        {
            instance = this;
           
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;

        // 初始化 Input Actions
        inputSystem = new InputSystem_Actions();

        // 設定 Rigidbody
        rb.gravityScale = 0; // 角色不受重力影響
        rb.freezeRotation = true; // 角色不旋轉
        dropletText.text= "water:" + droletCount;
    }

    private void OnEnable()
    {
        inputSystem.Player.Enable();

        // 訂閱事件
        // 當 PointerPress 動作 "開始" (按下)
        inputSystem.Player.PointerPress.started += OnPointerPressStarted;
        // 當 PointerPress 動作 "取消" (放開)
        inputSystem.Player.PointerPress.canceled += OnPointerPressCanceled;
    }

    private void OnDisable()
    {
        inputSystem.Player.Disable();

        // 取消訂閱
        inputSystem.Player.PointerPress.started -= OnPointerPressStarted;
        inputSystem.Player.PointerPress.canceled -= OnPointerPressCanceled;
    }

    // 按下時
    private void OnPointerPressStarted(InputAction.CallbackContext context)
    {
        if (isStunned) return; // 如果被暈眩，不允許移動
        isMoving = true;
    }

    // 放開時
    private void OnPointerPressCanceled(InputAction.CallbackContext context)
    {
        isMoving = false;
        rb.linearVelocity = Vector2.zero; // 立即停止移動
    }

    // 物理更新使用 FixedUpdate
    private void FixedUpdate()
    {
        // 如果沒有在移動或被暈眩，就什麼都不做
        if (!isMoving || isStunned)
        {
            // 確保停止（以防萬一）
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 1. 獲取螢幕上的點按位置
        Vector2 pointerScreenPos = inputSystem.Player.PointerPosition.ReadValue<Vector2>();

        // 2. 建立一個 Vector3，並手動設定 Z 軸
        //    ( -mainCamera.transform.position.z 就會是攝影機到 Z=0 的距離)
        float zDepth = -mainCamera.transform.position.z;
        Vector3 screenPosWithZ = new Vector3(pointerScreenPos.x, pointerScreenPos.y, zDepth);

        // 3. 將螢幕位置轉換為世界位置 (現在它知道 Z 軸深度了)
        Vector3 pointerWorldPos = mainCamera.ScreenToWorldPoint(screenPosWithZ);

        // 4. 設定目標位置 (X 和 Y 都跟隨滑鼠)
        Vector2 targetPosition = pointerWorldPos;

        // 5. 計算這一幀的移動速度
        // 使用 Vector2.MoveTowards 來確保它不會超過目標點，並以恆定速度移動
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);

        // 6. 使用 Rigidbody.MovePosition 來移動
        rb.MovePosition(newPos);
    }

    public void UpdateDropletCount()
    {
        droletCount++;
        dropletText.text = "water:"+droletCount;
    }

    // 公開方法，給障礙物調用
    public void ApplyStun()
    {
        if (!isStunned) // 避免重複觸發
        {
            StartCoroutine(StunCoroutine());
        }
    }

    private System.Collections.IEnumerator StunCoroutine()
    {
        isStunned = true;
        isMoving = false; // 停止移動標記
        rb.linearVelocity = Vector2.zero; // 立即停止

        // 可以在這裡添加視覺效果，例如改變顏色
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = Color.red;

        yield return new WaitForSeconds(stunDuration);

        if (renderer != null) renderer.color = Color.white;
        isStunned = false;
    }
}