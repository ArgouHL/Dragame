using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(string animBoolName, PlayerAnimatorController player, PlayerStateMachine stateMachine) : base(animBoolName, player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        if (PlayerController.instance.rb.linearVelocity!!= Vector2.zero&&!PlayerController.instance.isBeingAbsorbed)
        {

        }
    }
}
