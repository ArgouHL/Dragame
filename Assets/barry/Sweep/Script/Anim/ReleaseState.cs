using UnityEngine;
using UnityEngine.InputSystem; 

public class ReleaseState : PlayerState
{
    // 對應 Animator 裡的 "Release" (Bool)
    public ReleaseState(string animBoolName, PlayerAnimatorController player, PlayerStateMachine stateMachine)
        : base(animBoolName, player, stateMachine)
    {
    }


    public override void Enter()
    {
        base.Enter();

        timer = 1;

    }

    public override void Exit()
    {
        base.Exit();


    }

    public override void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            stateMachine.ChangeState(animatorController.idleState);
        }

    }
}