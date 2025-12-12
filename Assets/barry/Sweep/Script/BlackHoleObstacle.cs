using UnityEngine;
using System.Collections.Generic;

public class BlackHoleObstacle : BaseObstacle
{
    [Header("黑洞能力設定")]
    [SerializeField] private float absorbRadius;

    [Header("垃圾吸入設定")]
    [SerializeField] private float trashAbsorbTime;
    [SerializeField] private float trashRotateSpeed;
    [SerializeField] private AnimationCurve trashScaleCurve;
    [SerializeField] private AnimationCurve trashMoveCurve;

    [Header("玩家吸入設定")]
    [SerializeField] private PlayerController player;
    [SerializeField] private float playerAbsorbTime;
    [SerializeField] private float playerVanishTime;
    [SerializeField] private float playerEjectDuration;
    [SerializeField] private float playerEjectSpeed;
    [SerializeField] private float playerRotateSpeed;

    // 添加可视化触发器（用于调试）
    [Header("调试设置")]
    [SerializeField] private bool showTriggerGizmos = true;
    [SerializeField] private Color triggerColor = new Color(1f, 0.5f, 0f, 0.3f); // 橙色半透明

    private readonly List<AbsorbData> _absorbing = new List<AbsorbData>(64);

    private struct AbsorbData
    {
        public BaseTrash trash;
        public Vector3 startPos;
        public Vector3 startScale;
        public Vector3 targetPos;
        public float elapsed;
    }

    private enum PlayerState
    {
        Absorbing,
        Waiting,
        Ejecting
    }

    private struct PlayerAbsorbData
    {
        public PlayerController player;
        public Vector3 startPos;
        public Vector3 startScale;
        public Quaternion startRot;
        public Vector3 targetPos;
        public Vector2 ejectDir;
        public float timer;
        public PlayerState state;
    }

    private bool _hasPlayerAbsorb;
    private PlayerAbsorbData _playerAbsorb;

    // 添加圆形碰撞器引用
    private CircleCollider2D _triggerCollider;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
            
        // 获取或添加圆形碰撞器
        _triggerCollider = GetComponent<CircleCollider2D>();
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<CircleCollider2D>();
            _triggerCollider.isTrigger = true;
        }
        
        // 设置碰撞器半径
        _triggerCollider.radius = absorbRadius;
        
        Debug.Log($"黑洞初始化完成，触发器半径: {absorbRadius}");
    }

    private void FixedUpdate()
    {
        CheckAndAbsorbPlayer();   // 玩家仍然用距離方式
    }

    private void Update()
    {
        UpdateAbsorbAnimation();
        UpdatePlayerAbsorbAnimation();
    }

    // ★★★ 新增：垃圾吸入改用 OnTriggerEnter2D ★★★
    private void OnTriggerEnter2D(Collider2D other)
    {
        BaseTrash trash = other.GetComponent<BaseTrash>();
        if (trash == null) 
        {
            Debug.Log("如果不是垃圾");
            // 如果不是垃圾，忽略
            return;
        }
        
        if (trash.IsAbsorbing) 
        {
            Debug.Log($"{trash.name} 已经在被吸入，忽略");
            return;
        }

        Debug.Log($"黑洞检测到垃圾: {trash.name}");

        trash.OnEnterBlackHole();

        AbsorbData data;
        data.trash = trash;
        data.startPos = trash.transform.position;
        data.startScale = trash.transform.localScale;
        data.targetPos = transform.position;
        data.elapsed = 0f;

        _absorbing.Add(data);
        
        Debug.Log($"开始吸入垃圾: {trash.name}，当前正在吸入的垃圾数量: {_absorbing.Count}");
    }

    // ★★★ 玩家仍然用距離判斷（邏輯不動） ★★★
    private void CheckAndAbsorbPlayer()
    {
        if (player == null) return;
        if (_hasPlayerAbsorb) return;
        if (player.IsBeingAbsorbed) return;

        Vector3 myPos = transform.position;
        float sqrDist = (player.transform.position - myPos).sqrMagnitude;
        if (sqrDist > absorbRadius * absorbRadius) return;

        Debug.Log("开始吸入玩家");
        
        player.EnterBlackHole();

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        PlayerAbsorbData data;
        data.player = player;
        data.startPos = player.transform.position;
        data.startScale = player.transform.localScale;
        data.startRot = player.transform.rotation;
        data.targetPos = myPos;
        data.ejectDir = dir;
        data.timer = 0f;
        data.state = PlayerState.Absorbing;

        _playerAbsorb = data;
        _hasPlayerAbsorb = true;
    }

    private void UpdateAbsorbAnimation()
    {
        if (_absorbing.Count == 0) return;

        for (int i = _absorbing.Count - 1; i >= 0; i--)
        {
            AbsorbData data = _absorbing[i];
            BaseTrash trash = data.trash;
            if (trash == null)
            {
                _absorbing.RemoveAt(i);
                Debug.Log("垃圾对象已销毁，从列表中移除");
                continue;
            }

            float duration = trashAbsorbTime > 0f ? trashAbsorbTime : trash.AbsorbEffectDuration;
            
            if (duration <= 0)
            {
                Debug.LogWarning($"垃圾 {trash.name} 的吸入时间为0，使用默认值1秒");
                duration = 1f;
            }

            data.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(data.elapsed / duration);

            float scaleT = trashScaleCurve != null ? trashScaleCurve.Evaluate(t) : t;
            float moveT = trashMoveCurve != null ? trashMoveCurve.Evaluate(t) : t;

            float rotSpeed = trashRotateSpeed != 0f ? trashRotateSpeed : trash.RotationSpeed;

            Transform tr = trash.transform;
            tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, scaleT);
            tr.position = Vector2.LerpUnclamped(data.startPos, data.targetPos, moveT);

            // 垃圾吸入完成後直接返回對象池，不吐出
            if (data.elapsed >= duration)
            {
                Debug.Log($"垃圾 {trash.name} 吸入完成，返回对象池");
                trash.ResetState();
                
                // 确保对象池存在
                if (TrashPool.Instance != null)
                {
                    TrashPool.Instance.ReturnTrash(trash);
                }
                else
                {
                    Debug.LogError("TrashPool.Instance 为 null!");
                    Destroy(trash.gameObject);
                }
                
                _absorbing.RemoveAt(i);
            }
            else
            {
                _absorbing[i] = data;
            }
        }
    }

    private void UpdatePlayerAbsorbAnimation()
    {
        if (!_hasPlayerAbsorb) return;

        PlayerAbsorbData data = _playerAbsorb;
        PlayerController p = data.player;
        if (p == null)
        {
            _hasPlayerAbsorb = false;
            return;
        }

        Transform tr = p.transform;

        if (data.state == PlayerState.Absorbing)
        {
            float duration = playerAbsorbTime;

            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / duration);

            tr.position = Vector2.LerpUnclamped(data.startPos, data.targetPos, t);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, t);
            if (playerRotateSpeed != 0f)
                tr.Rotate(0f, 0f, playerRotateSpeed * Time.deltaTime);

            if (data.timer >= duration)
            {
                data.timer = 0f;
                data.state = PlayerState.Waiting;
            }

            _playerAbsorb = data;
            return;
        }

        if (data.state == PlayerState.Waiting)
        {
            data.timer += Time.deltaTime;

            if (data.timer >= playerVanishTime)
            {
                data.timer = 0f;
                data.state = PlayerState.Ejecting;
                p.ExitBlackHole(data.ejectDir, playerEjectSpeed);
            }

            _playerAbsorb = data;
            return;
        }

        if (data.state == PlayerState.Ejecting)
        {
            float duration = playerEjectDuration;

            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / duration);

            tr.localScale = Vector3.LerpUnclamped(Vector3.zero, data.startScale, t);

            if (data.timer >= duration)
            {
                _hasPlayerAbsorb = false;
            }
            else
            {
                _playerAbsorb = data;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorbRadius);
        
        // 绘制触发器范围
        if (showTriggerGizmos)
        {
            Gizmos.color = triggerColor;
            Gizmos.DrawSphere(transform.position, absorbRadius);
        }
    }
}