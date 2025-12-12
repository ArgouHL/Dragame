using UnityEngine;

public class RightClickState : PlayerState
{
    public RightClickState(string animBoolName, PlayerAnimatorController player, PlayerStateMachine stateMachine) : base(animBoolName, player, stateMachine)
    {
    }
    public override void Enter()
    {
        base.Enter();
        PlayerController.instance.rb.linearVelocity = Vector2.zero;
    }
    public override void Exit()
    {
        base.Exit();
    }
    public override void Update()
    {

    }
}
