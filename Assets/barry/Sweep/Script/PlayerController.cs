using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;
   
    private Vector2 startMousePosition;
    private Vector2 endMousePosition;
    private Camera mainCamera; 
   
  
    public Vector2 NormalizedDirection { get; private set; }
    private PlayerInput playerInput;
    private Vector2 currentPointerPosition;


    public float moveSpeed ;
    public Rigidbody2D rb;
    private Vector2 moveDirection;
    private bool isMoving = false;

    [Tooltip("反彈時的隨機角度範圍 (例如 15 度)")]
    [SerializeField] private float randomBounceAngle = 15f;
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
        isMoving = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 

        }
        startMousePosition = GetWorldMousePosition();
    }

    private void OnPointerPressCanceled(InputAction.CallbackContext context)
    {
        endMousePosition = GetWorldMousePosition();
        Debug.Log(startMousePosition);
        Debug.Log(endMousePosition);
       
        Debug.Log(NormalizedDirection);
        NormalizedDirection = (endMousePosition - startMousePosition).normalized;
       

        if (NormalizedDirection != Vector2.zero) // 避免 (0,0) 造成問題
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
        if (isMoving)
        {
            
            rb.linearVelocity = moveDirection * moveSpeed;
        }
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("airWall"))
        {
            
            Vector2 wallNormal = collision.contacts[0].normal;

        
      
            Vector2 reflectedDirection = Vector2.Reflect(moveDirection.normalized, wallNormal);

        
            float randomAngle = Random.Range(-randomBounceAngle, randomBounceAngle);

           
            Quaternion randomRotation = Quaternion.Euler(0, 0, randomAngle);
            Vector2 finalDirection = randomRotation * reflectedDirection;

            moveDirection = finalDirection.normalized;
        }
    }
    }
