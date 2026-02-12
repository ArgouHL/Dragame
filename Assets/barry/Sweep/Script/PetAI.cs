using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PetAI : MonoBehaviour
{
    [Header("=== 1. 等待狀態 (Idle) ===")]
    [Tooltip("閒置/等待的時間 (10秒)")]
    [SerializeField] private float idleDuration = 10f;

    [Header("=== 2. 飢餓移動 (Walk) ===")]
    [Tooltip("飢餓時移動的距離 (走兩步)")]
    [SerializeField] private float walkDistance = 2f;
    [SerializeField] private float moveSpeed = 3f;

    [Header("=== 3. 飢餓吸食 (Suck) ===")]
    [Tooltip("吸食持續時間 (2秒)")]
    [SerializeField] private float suckDuration = 2f;
    [Tooltip("吸食範圍 (希望大一點)")]
    [SerializeField] private float suckRadius = 6f;
    [SerializeField] private float suckForce = 20f;
    [SerializeField] private LayerMask trashLayer;

    [Header("=== 飽腹嘔吐 ===")]
    [SerializeField] private int maxStomachCapacity = 5;
    [SerializeField] private GameObject trashPrefab;
    [SerializeField] private float vomitForce = 5f;

    // 內部變數
    private float _timer;
    private int _currentStomach;
    private Vector2 _moveTarget;
    private Rigidbody2D _rb;
    private ContactFilter2D _trashFilter;
    private readonly Collider2D[] _suckResults = new Collider2D[16];

    // 狀態定義
    public enum PetState
    {
        Idle,           // 狀態: 等待 10 秒
        Starving_Walk,  // 狀態: 走兩步
        Starving_Suck,  // 狀態: 吸 2 秒
        Vomiting        // 狀態: 嘔吐
    }

    [Header("Debug 資訊")]
    [SerializeField] private PetState currentState;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // 初始化過濾器
        _trashFilter = new ContactFilter2D();
        _trashFilter.SetLayerMask(trashLayer);
        _trashFilter.useLayerMask = true;
        _trashFilter.useTriggers = true;
    }

    private void Start()
    {
        // 初始狀態：直接開始等待 10 秒
        EnterIdleState();
    }

    private void Update()
    {
        // 狀態機邏輯
        switch (currentState)
        {
            case PetState.Idle:
                // 等待 10 秒
                _timer += Time.deltaTime;
                if (_timer >= idleDuration)
                {
                    EnterWalkState();
                }
                break;

            case PetState.Starving_Walk:
                // 移動邏輯
                float dist = Vector2.Distance(transform.position, _moveTarget);
                // 稍微給一點誤差容許值 (0.1f)
                if (dist > 0.1f)
                {
                    Vector2 dir = (_moveTarget - (Vector2)transform.position).normalized;
                    _rb.linearVelocity = dir * moveSpeed;
                }
                else
                {
                    // 到了目的地，開始吸
                    EnterSuckState();
                }
                break;

            case PetState.Starving_Suck:
                // 吸食 2 秒
                _timer += Time.deltaTime;
                if (_timer >= suckDuration)
                {
                    // 吸完沒吃到東西 -> 回到等待循環
                    EnterIdleState();
                }
                break;

            case PetState.Vomiting:
                // 嘔吐由動畫或 Invoke 控制，Update 這裡暫不處理
                break;
        }
    }

    private void FixedUpdate()
    {
        // 只有在吸食狀態下，才會有物理吸力
        if (currentState == PetState.Starving_Suck)
        {
            PerformSuckingPhysics();
        }

        // 額外保險：隨時檢查自己有沒有跑出邊界 (防掉出)
        if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(transform.position))
        {
            // 如果不小心出界了，給一個向回推的力，或者直接彈回來
            Vector2 pos = transform.position;
            Vector2 vel = _rb.linearVelocity;
            WorldBounds2D.Instance.Bounce(ref pos, ref vel, 0.5f);
            transform.position = pos;
            _rb.linearVelocity = vel;
        }
    }

    // --- 狀態進入方法 (Enter Methods) ---

    private void EnterIdleState()
    {
        currentState = PetState.Idle;
        _timer = 0f;
        _rb.linearVelocity = Vector2.zero; // 停止移動
        Debug.Log("[Pet] 進入等待 (10s)...");
    }

    private void EnterWalkState()
    {
        currentState = PetState.Starving_Walk;
        // 設定目標點 (必須在邊界內)
        _moveTarget = PickValidTarget(walkDistance);
        Debug.Log($"[Pet] 餓了，走去 {_moveTarget}");
    }

    private void EnterSuckState()
    {
        currentState = PetState.Starving_Suck;
        _timer = 0f;
        _rb.linearVelocity = Vector2.zero; // 停下來吸
        Debug.Log("[Pet] 開始吸食 (2s)!");
    }

    private void EnterVomitState()
    {
        currentState = PetState.Vomiting;
        _rb.linearVelocity = Vector2.zero;
        PerformVomit();
    }

    // --- 核心功能 ---

    // 尋找一個合法的目標點 (整合 WorldBounds2D)
    private Vector2 PickValidTarget(float distance)
    {
        Vector2 origin = transform.position;
        Vector2 bestTarget = origin;
        bool found = false;

        // 嘗試 10 次找一個不會出界的方向
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector2 candidate = origin + randomDir * distance;

            // 檢查 WorldBounds2D
            if (WorldBounds2D.Instance != null && !WorldBounds2D.Instance.IsOutside(candidate))
            {
                bestTarget = candidate;
                found = true;
                break;
            }
        }

        // 如果運氣太差找 10 次都出界(例如在角落)，就往畫面中心走
        if (!found && WorldBounds2D.Instance != null)
        {
            Rect rect = WorldBounds2D.Instance.GetWorldRect();
            bestTarget = Vector2.MoveTowards(origin, rect.center, distance);
        }

        return bestTarget;
    }

    private void PerformSuckingPhysics()
    {
        int count = Physics2D.OverlapCircle(transform.position, suckRadius, _trashFilter, _suckResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _suckResults[i];
            if (col == null) continue;

            if (col.TryGetComponent<BaseTrash>(out var trash))
            {
                if (trash.IsAbsorbing) continue;

                // 吸力邏輯
                Vector2 dir = (transform.position - trash.transform.position).normalized;
                Rigidbody2D trashRb = trash.GetComponent<Rigidbody2D>();

                if (trashRb != null)
                {
                    trashRb.AddForce(dir * suckForce);
                }
                else
                {
                    trash.transform.position = Vector2.MoveTowards(trash.transform.position, transform.position, suckForce * Time.fixedDeltaTime);
                }

                // 距離夠近就吃掉
                if (Vector2.SqrMagnitude(transform.position - trash.transform.position) < 0.5f)
                {
                    EatTrash(trash);
                }
            }
        }
    }

    // 碰撞偵測 (非吸食狀態下，碰到也吃)
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (currentState == PetState.Vomiting) return;
        if (other.gameObject.TryGetComponent<BaseTrash>(out var trash)) EatTrash(trash);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState == PetState.Vomiting) return;
        if (other.TryGetComponent<BaseTrash>(out var trash)) EatTrash(trash);
    }

    private void EatTrash(BaseTrash trash)
    {
        if (trash == null || !trash.gameObject.activeSelf || trash.IsAbsorbing) return;

        // 銷毀垃圾
        trash.OnEnterBlackHole();

        _currentStomach++;
        Debug.Log($"[Pet] 吃到垃圾! 飽食度: {_currentStomach}/{maxStomachCapacity}");

        if (_currentStomach >= maxStomachCapacity)
        {
            EnterVomitState();
        }
        else
        {
            // 邏輯要求：吃到東西 -> 回到 10秒等待
            // 無論現在是走或是吸，只要吃到就重置
            EnterIdleState();
        }
    }

    private void PerformVomit()
    {
        Debug.Log("[Pet] 嘔吐中...");

        if (trashPrefab != null)
        {
            for (int i = 0; i < _currentStomach; i++)
            {
                Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * 0.5f;
                GameObject vomit = Instantiate(trashPrefab, spawnPos, Quaternion.identity);

                BaseTrash bt = vomit.GetComponent<BaseTrash>();
                if (bt != null) bt.ResetState();

                Rigidbody2D rb = vomit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 force = Random.insideUnitCircle.normalized * vomitForce;
                    rb.AddForce(force, ForceMode2D.Impulse);
                }
            }
        }

        _currentStomach = 0;
        // 吐完後休息 1 秒再開始循環
        Invoke(nameof(EnterIdleState), 1f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 紅圈：吸食範圍
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, suckRadius);

        // 藍線：移動目標
        if (currentState == PetState.Starving_Walk)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _moveTarget);
            Gizmos.DrawWireSphere(_moveTarget, 0.2f);
        }
    }
#endif
}