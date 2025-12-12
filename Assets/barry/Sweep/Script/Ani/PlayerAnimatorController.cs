using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
  
    public Animator anim { get; private set; }
    public PlayerStateMachine stateMachine { get; private set; }
    public PlayerMoveState moveState { get; private set; }

   
    
    public static PlayerAnimatorController instance;
    public PlayerIdleState idleState { get; private set; }

    protected  void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        anim = GetComponent<Animator>();
        stateMachine = new PlayerStateMachine();
       
    }
    protected  void Start()
    {
        idleState = new PlayerIdleState("Idle", this, stateMachine);
        moveState = new PlayerMoveState("Move", this, stateMachine);
        stateMachine.Initialize(idleState);

    }
    protected void Update()
    {
        UpdateLocomotionParameters();
        stateMachine.currentState.Update();

    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationFinishTrigger();

    private void UpdateLocomotionParameters()
    {
        if (PlayerController.instance == null) return;

        if (PlayerController.instance.isBeingAbsorbed)
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        Vector2 vel = PlayerController.instance.rb.linearVelocity;
        float speed = vel.magnitude;

        anim.SetFloat("Speed", speed);

        if (speed > 0.001f)
        {
            Vector2 dir = vel.normalized;
            anim.SetFloat("VelX", dir.x);
            anim.SetFloat("VelY", dir.y);
        }
    }



}
