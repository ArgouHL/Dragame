using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PetAI : MonoBehaviour
{
    [Header("=== 1. 等待狀態 (Idle) ===")]
    [Tooltip("閒置/等待的時間 (秒)")]
    [SerializeField] private float idleDuration = 10f;

    [Header("=== 2. 飢餓移動 (Walk) ===")]
    [Tooltip("飢餓時移動的距離")]
    [SerializeField] private float walkDistance = 2f;
    [SerializeField] private float moveSpeed = 3f;

    [Header("=== 3. 飢餓吸食 (Suck) ===")]
    [Tooltip("吸食持續時間 (秒)")]
    [SerializeField] private float suckDuration = 2f;
    [SerializeField] private float suckRadius = 6f;
    [SerializeField] private float suckForce = 20f;
    [SerializeField] private LayerMask trashLayer;

    [Header("=== 4. 飽腹嘔吐 (Vomit) ===")]
    [SerializeField] private int maxStomachCapacity = 5;
    [SerializeField] private GameObject trashPrefab;
    [SerializeField] private float vomitForce = 5f;

    [Header("Debug 資訊")]
    [SerializeField] private PetState currentState;
    [SerializeField] private int currentStomach;

    // 狀態定義
    public enum PetState { Idle, Starving_Walk, Starving_Suck, Vomiting }

    // 內部依賴
    private Rigidbody2D _rb;
    private float _stateTimer;
    private Vector2 _moveTarget;
    private ContactFilter2D _trashFilter;
    private readonly Collider2D[] _suckResults = new Collider2D[16];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        _trashFilter = new ContactFilter2D
        {
            layerMask = trashLayer,
            useLayerMask = true,
            useTriggers = true
        };
    }

    private void Start() => ChangeState(PetState.Idle);

    private void Update()
    {
        UpdateStateMachine();
    }

    private void FixedUpdate()
    {
        if (currentState == PetState.Starving_Suck)
        {
            PerformSuckingPhysics();
        }

        EnsureWithinBounds();
    }

    #region --- 職責 1: 狀態機大腦 (State Machine) ---

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case PetState.Idle:
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= idleDuration) ChangeState(PetState.Starving_Walk);
                break;

            case PetState.Starving_Walk:
                if (Vector2.Distance(transform.position, _moveTarget) > 0.1f)
                {
                    Vector2 dir = (_moveTarget - (Vector2)transform.position).normalized;
                    _rb.linearVelocity = dir * moveSpeed;
                }
                else
                {
                    ChangeState(PetState.Starving_Suck);
                }
                break;

            case PetState.Starving_Suck:
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= suckDuration) ChangeState(PetState.Idle);
                break;

            case PetState.Vomiting:
                // 嘔吐狀態由 ChangeState 觸發 Invoke 處理，不在此輪詢
                break;
        }
    }

    private void ChangeState(PetState newState)
    {
        currentState = newState;
        _stateTimer = 0f;
        _rb.linearVelocity = Vector2.zero; // 切換狀態時預設先煞車

        switch (newState)
        {
            case PetState.Idle:
                // 等待中
                break;
            case PetState.Starving_Walk:
                _moveTarget = PickValidTarget(walkDistance);
                break;
            case PetState.Starving_Suck:
                // 準備吸食
                break;
            case PetState.Vomiting:
                PerformVomit();
                break;
        }
    }

    #endregion

    #region --- 職責 2: 移動與導航 (Movement) ---

    private Vector2 PickValidTarget(float distance)
    {
        Vector2 origin = transform.position;

        for (int i = 0; i < 10; i++)
        {
            Vector2 candidate = origin + Random.insideUnitCircle.normalized * distance;
            if (WorldBounds2D.Instance != null && !WorldBounds2D.Instance.IsOutside(candidate))
            {
                return candidate;
            }
        }

        // 防呆：若找不到合法點，往中心走
        if (WorldBounds2D.Instance != null)
        {
            return Vector2.MoveTowards(origin, WorldBounds2D.Instance.GetWorldRect().center, distance);
        }
        return origin;
    }

    private void EnsureWithinBounds()
    {
        if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(transform.position))
        {
            Vector2 pos = transform.position;
            Vector2 vel = _rb.linearVelocity;
            WorldBounds2D.Instance.Bounce(ref pos, ref vel, 0.5f);
            transform.position = pos;
            _rb.linearVelocity = vel;
        }
    }

    #endregion

    #region --- 職責 3: 腸胃與互動 (Suck & Stomach) ---

    private void PerformSuckingPhysics()
    {
        int count = Physics2D.OverlapCircle(transform.position, suckRadius, _trashFilter, _suckResults);

        for (int i = 0; i < count; i++)
        {
            if (_suckResults[i] != null && _suckResults[i].TryGetComponent<BaseTrash>(out var trash))
            {
                if (trash.IsAbsorbing) continue;

                Vector2 dir = (transform.position - trash.transform.position).normalized;
                if (trash.TryGetComponent<Rigidbody2D>(out var trashRb))
                {
                    trashRb.AddForce(dir * suckForce);
                }

                // 距離夠近就吃掉
                if (Vector2.SqrMagnitude(transform.position - trash.transform.position) < 0.5f)
                {
                    EatTrash(trash);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState != PetState.Vomiting && other.TryGetComponent<BaseTrash>(out var trash))
        {
            EatTrash(trash);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (currentState != PetState.Vomiting && other.gameObject.TryGetComponent<BaseTrash>(out var trash))
        {
            EatTrash(trash);
        }
    }

    private void EatTrash(BaseTrash trash)
    {
        if (trash == null || !trash.gameObject.activeSelf || trash.IsAbsorbing) return;

        trash.OnEnterBlackHole(); // 依賴原邏輯銷毀垃圾
        currentStomach++;

        if (currentStomach >= maxStomachCapacity)
        {
            ChangeState(PetState.Vomiting);
        }
        else
        {
            ChangeState(PetState.Idle); // 吃到東西重置等待
        }
    }

    private void PerformVomit()
    {
        if (trashPrefab != null)
        {
            for (int i = 0; i < currentStomach; i++)
            {
                Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * 0.5f;
                GameObject vomit = Instantiate(trashPrefab, spawnPos, Quaternion.identity);

                if (vomit.TryGetComponent<BaseTrash>(out var bt)) bt.ResetState();
                if (vomit.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.AddForce(Random.insideUnitCircle.normalized * vomitForce, ForceMode2D.Impulse);
                }
            }
        }

        currentStomach = 0;
        Invoke(nameof(ReturnToIdleAfterVomit), 1f); // 吐完休息 1 秒
    }

    private void ReturnToIdleAfterVomit() => ChangeState(PetState.Idle);

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, suckRadius);

        if (currentState == PetState.Starving_Walk)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _moveTarget);
            Gizmos.DrawWireSphere(_moveTarget, 0.2f);
        }
    }
#endif
}