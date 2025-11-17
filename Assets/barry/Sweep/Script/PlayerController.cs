using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;

    private Vector2 startMousePosition;
    private Vector2 endMousePosition;
    private Camera mainCamera;
    
    Vector2 nextPos;
    Vector3 vp;
    public Vector2 NormalizedDirection { get; private set; }
    private PlayerInput playerInput;
    private Vector2 currentPointerPosition;


    public float moveSpeed;
    public Rigidbody2D rb;
    private Vector2 moveDirection;
    private bool isMoving = false;

    public float sweepRadius ; 
    public LayerMask trashLayer; 
    public Vector2 sweepOffset;
    Vector2 sweepCenter ;


    Collider2D[] hits;

    private void Awake()
    {


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
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerController 找不到 PlayerInput 元件！", this);
        }
    }
    private void Start()
    {
        mainCamera = Camera.main;
        transform.position = new Vector3(-5, 0, 0);
    }
    private void OnEnable()
    {

        playerInput.actions["PointerPress"].started += OnPointerPressStarted;
        playerInput.actions["PointerPress"].canceled += OnPointerPressCanceled;
        playerInput.actions["PointerPosition"].performed += OnPointerPosition;
    }

    private void OnDisable()
    {

        if (playerInput != null)
        {
            playerInput.actions["PointerPress"].started -= OnPointerPressStarted;
            playerInput.actions["PointerPress"].canceled -= OnPointerPressCanceled;
            playerInput.actions["PointerPosition"].performed -= OnPointerPosition;
        }
    }


    private void OnPointerPressStarted(InputAction.CallbackContext context)
    {

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;

        }
        startMousePosition = GetWorldMousePosition();
    }

    private void OnPointerPressCanceled(InputAction.CallbackContext context)
    {
        endMousePosition = GetWorldMousePosition();
      
        NormalizedDirection = (endMousePosition - startMousePosition).normalized;


        if (NormalizedDirection != Vector2.zero) 
        {
            moveDirection = NormalizedDirection;
            isMoving = true;
        }


    }
    public void OnPointerPosition(InputAction.CallbackContext context)
    {

        currentPointerPosition = context.ReadValue<Vector2>();
    }
    private Vector2 GetWorldMousePosition()
    {
        return mainCamera.ScreenToWorldPoint(
                new Vector3(currentPointerPosition.x, currentPointerPosition.y, 10));
    }

    private void FixedUpdate()
    {
  
        HandleMove();
    }
    private void HandleMove()
    {
        if (!isMoving)
            return;

        Sweep();
        nextPos = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;

        vp = mainCamera.WorldToViewportPoint(nextPos);

     
        if (vp.x < 0.1f || vp.x > 0.9f || vp.y < 0.1f || vp.y > 0.9f)
        {
            isMoving = false;
            rb.linearVelocity = Vector2.zero;
            Debug.Log("超出區域，停止移動");
            return;
        }

        
        rb.linearVelocity = moveDirection * moveSpeed;
    }
    void Sweep()
    {
      
         sweepCenter = (Vector2)transform.position + sweepOffset;

    
        hits = Physics2D.OverlapCircleAll(sweepCenter, sweepRadius, trashLayer);

        foreach (Collider2D hit in hits)
        {
            BaseTrash trash = hit.GetComponent<BaseTrash>();
            if (trash != null)
            {
                
                Vector2 direction = moveDirection;

          
                trash.ApplyBroomHit(direction);
            }
        }
    }
    private void OnDrawGizmosSelected()
    {     
        Gizmos.color = Color.yellow;       
        Vector2 gizmoCenter = (Vector2)transform.position + sweepOffset;      
        Gizmos.DrawWireSphere(gizmoCenter, sweepRadius);
    }
}
