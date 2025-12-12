using UnityEngine.Playables;

public class PlayerStateMachine
{
    public PlayerState currentState { get; private set; }
    public void Initialize(PlayerState _currentState)
    {
        currentState = _currentState;
        currentState.Enter();
    }
    public void ChangeState(PlayerState _newState)
    {
        currentState.Exit();
        currentState = _newState;
        currentState.Enter();
    }
}