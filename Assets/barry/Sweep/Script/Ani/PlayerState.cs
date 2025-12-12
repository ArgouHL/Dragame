using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;

public class PlayerState
{
    private string animBoolName;
    protected PlayerAnimatorController player;
    protected PlayerStateMachine stateMachine;
    protected float xInput;
    protected float yInput;
    protected float stateTimer;
    public bool triggerCalled = false;

    public PlayerState(string animBoolName, PlayerAnimatorController player, PlayerStateMachine stateMachine)
    {
        this.animBoolName = animBoolName;
        this.player = player;
        this.stateMachine = stateMachine;
    }
    public virtual void Update()
    {
        yInput = UnityEngine.Input.GetAxisRaw("Vertical");
        xInput = UnityEngine.Input.GetAxisRaw("Horizontal");
        
        stateTimer -= Time.deltaTime;
    }
    public virtual void Enter()
    {
        player.anim.SetBool(animBoolName, true);
    }
    public virtual void Exit()
    {
        player.anim.SetBool(animBoolName, false);
    }
    public virtual void AnimationFinishTrigger()
    {
        triggerCalled = true;
    }



}
