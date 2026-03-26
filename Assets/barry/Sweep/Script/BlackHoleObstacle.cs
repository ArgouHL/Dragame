using UnityEngine;
using System.Collections.Generic;

public class BlackHoleObstacle : BaseObstacle
{
    [Header("黑洞能力設定")]
    [SerializeField] private float absorbRadius = 1f;
    [SerializeField] private Vector2 centerOffset;

    [Header("垃圾吸入設定")]
    [SerializeField] private float trashAbsorbTime = 1f;
    [SerializeField] private float trashRotateSpeed = 360f;
    [SerializeField] private AnimationCurve trashScaleCurve;
    [SerializeField] private AnimationCurve trashMoveCurve;

    [Header("玩家吸入設定")]
    [SerializeField] private float playerAbsorbTime = 0.5f;
    [SerializeField] private float playerVanishTime = 0.5f;
    [SerializeField] private float playerEjectDuration = 0.5f;
    [SerializeField] private float playerEjectSpeed = 15f;
    [SerializeField] private float playerRotateSpeed = 1f;

    [Header("調試設置")]
    [SerializeField] private bool showTriggerGizmos = true;
    [SerializeField] private Color triggerColor = new Color(1f, 0.5f, 0f, 0.3f);

    private readonly List<AbsorbData> _absorbing = new List<AbsorbData>(64);
    private PlayerAbsorbData _playerAbsorb;
    private bool _hasPlayerAbsorb;
    private CircleCollider2D _triggerCollider;

    public Vector3 CenterPos => transform.position + (Vector3)centerOffset;

    private struct AbsorbData
    {
        public BaseTrash trash;
        public Vector3 startPos;
        public Vector3 startScale;
        public float elapsed;
    }

    private enum PlayerState { Absorbing, Waiting, Ejecting }

    private struct PlayerAbsorbData
    {
        public PlayerController player;
        public Vector3 startPos;
        public Vector3 startScale;
        public Vector2 ejectDir;
        public float timer;
        public PlayerState state;
    }

    private void Awake()
    {
        _triggerCollider = GetComponent<CircleCollider2D>();
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<CircleCollider2D>();
            _triggerCollider.isTrigger = true;
        }

        _triggerCollider.offset = centerOffset;
        _triggerCollider.radius = absorbRadius;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<IAbsorbable>(out var target))
        {
            if (target.CanBeAbsorbed)
            {
                target.OnAbsorbStart(this);
            }
        }
    }

    public void RegisterTrash(BaseTrash trash)
    {
        _absorbing.Add(new AbsorbData
        {
            trash = trash,
            startPos = trash.transform.position,
            startScale = trash.transform.localScale,
            elapsed = 0f
        });
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (_hasPlayerAbsorb) return;

        // [重點註釋] 智能噴出方向：計算朝向世界中心的向量，並加上隨機偏移。
        // 這樣可以避免黑洞在牆邊時，無腦將玩家向外發射導致穿模卡死。
        Vector2 dir;
        if (WorldBounds2D.Instance != null)
        {
            Vector2 worldCenter = WorldBounds2D.Instance.GetWorldRect().center;
            Vector2 dirToCenter = (worldCenter - (Vector2)CenterPos).normalized;
            Vector2 randomOffset = Random.insideUnitCircle * 0.4f; // 加上 40% 的隨機干擾
            dir = (dirToCenter + randomOffset).normalized;
        }
        else
        {
            dir = Random.insideUnitCircle.normalized;
        }

        if (dir == Vector2.zero) dir = Vector2.right;

        _playerAbsorb = new PlayerAbsorbData
        {
            player = player,
            startPos = player.transform.position,
            startScale = player.transform.localScale,
            ejectDir = dir,
            timer = 0f,
            state = PlayerState.Absorbing
        };
        _hasPlayerAbsorb = true;
    }

    private void Update()
    {
        UpdateAbsorbAnimation();
        UpdatePlayerAbsorbAnimation();
    }

    private void UpdateAbsorbAnimation()
    {
        int count = _absorbing.Count;
        if (count == 0) return;

        Vector3 targetPos = CenterPos;
        float dt = Time.deltaTime;

        for (int i = count - 1; i >= 0; i--)
        {
            AbsorbData data = _absorbing[i];

            if (data.trash == null || !data.trash.gameObject.activeSelf)
            {
                RemoveAbsorbDataFast(i);
                continue;
            }

            data.elapsed += dt;
            float t = Mathf.Clamp01(data.elapsed / trashAbsorbTime);

            float scaleT = trashScaleCurve != null ? trashScaleCurve.Evaluate(t) : t;
            float moveT = trashMoveCurve != null ? trashMoveCurve.Evaluate(t) : t;

            Transform tr = data.trash.transform;
            tr.Rotate(0f, 0f, trashRotateSpeed * dt);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, scaleT);
            tr.position = Vector2.LerpUnclamped(data.startPos, targetPos, moveT);

            if (data.elapsed >= trashAbsorbTime)
            {
                data.trash.ResetState();
                if (TrashPool.Instance != null) TrashPool.Instance.ReturnTrash(data.trash);
                else Destroy(data.trash.gameObject);

                RemoveAbsorbDataFast(i);
            }
            else
            {
                _absorbing[i] = data;
            }
        }
    }

    private void RemoveAbsorbDataFast(int index)
    {
        int lastIndex = _absorbing.Count - 1;
        if (index != lastIndex)
        {
            _absorbing[index] = _absorbing[lastIndex];
        }
        _absorbing.RemoveAt(lastIndex);
    }

    private void UpdatePlayerAbsorbAnimation()
    {
        if (!_hasPlayerAbsorb) return;

        PlayerAbsorbData data = _playerAbsorb;
        if (data.player == null) { _hasPlayerAbsorb = false; return; }

        Transform tr = data.player.transform;

        if (data.state == PlayerState.Absorbing)
        {
            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / playerAbsorbTime);
            tr.position = Vector2.LerpUnclamped(data.startPos, CenterPos, t);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, t);
            tr.Rotate(0f, 0f, playerRotateSpeed * Time.deltaTime);

            if (data.timer >= playerAbsorbTime)
            {
                data.timer = 0f;
                data.state = PlayerState.Waiting;
            }
        }
        else if (data.state == PlayerState.Waiting)
        {
            data.timer += Time.deltaTime;
            if (data.timer >= playerVanishTime)
            {
                data.timer = 0f;
                data.state = PlayerState.Ejecting;
                data.player.ExitBlackHole(data.ejectDir, playerEjectSpeed);
            }
        }
        else if (data.state == PlayerState.Ejecting)
        {
            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / playerEjectDuration);
            tr.localScale = Vector3.LerpUnclamped(Vector3.zero, data.startScale, t);

            if (data.timer >= playerEjectDuration)
            {
                _hasPlayerAbsorb = false;
                return;
            }
        }
        _playerAbsorb = data;
    }

    private void OnDrawGizmos()
    {
        Vector3 center = CenterPos;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, absorbRadius);
        if (showTriggerGizmos)
        {
            Gizmos.color = triggerColor;
            Gizmos.DrawSphere(center, absorbRadius);
        }
    }
}