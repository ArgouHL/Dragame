using System.Collections.Generic;
using UnityEngine;

// 強制綁定 AudioEmitter，確保黑洞一定能發出聲音
[RequireComponent(typeof(AudioEmitter))]
public class BlackHoleObstacle : BaseObstacle
{
    // [重點註釋] 發送帶有分數參數的事件廣播
    public static event System.Action<int> OnTrashAbsorbedScore;

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

    [Header("內建音效設定")]
    [SerializeField] private AudioEmitter audioEmitter;
    [SerializeField] private string absorbSound = "Absorb";
    [SerializeField] private string ejectSound = "Eject";
    [SerializeField, Tooltip("防連發機制：短時間內吸入大量垃圾時，只播一次音效")]
    private float absorbCooldown = 0.1f;

    [Header("調試設置")]
    [SerializeField] private bool showTriggerGizmos = true;
    [SerializeField] private Color triggerColor = new Color(1f, 0.5f, 0f, 0.3f);

    private readonly List<AbsorbData> _absorbing = new List<AbsorbData>(64);
    private PlayerAbsorbData _playerAbsorb;
    private bool _hasPlayerAbsorb;
    private CircleCollider2D _triggerCollider;
    private float _lastAbsorbTime = -1f;

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
        if (!TryGetComponent(out _triggerCollider))
        {
            _triggerCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        _triggerCollider.isTrigger = true;
        _triggerCollider.offset = centerOffset;
        _triggerCollider.radius = absorbRadius;

        if (audioEmitter == null) audioEmitter = GetComponent<AudioEmitter>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<IAbsorbable>(out var target) && target.CanBeAbsorbed)
        {
            target.OnAbsorbStart(this);
        }
    }

    private void PlayAbsorbSound()
    {
        if (audioEmitter != null && Time.time - _lastAbsorbTime > absorbCooldown)
        {
            audioEmitter.PlayOneShot(absorbSound);
            _lastAbsorbTime = Time.time;
        }
    }

    public void RegisterTrash(BaseTrash trash)
    {
        PlayAbsorbSound();

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

        PlayAbsorbSound();

        Vector2 dir = Vector2.right;

        if (WorldBounds2D.Instance != null)
        {
            // 修正處：配合簡化後的地圖邊界，直接取用 GetCenter()
            Vector2 safeCenter = WorldBounds2D.Instance.GetCenter();
            Vector2 dirToCenter = (safeCenter - (Vector2)CenterPos).normalized;
            if (dirToCenter == Vector2.zero) dirToCenter = Random.insideUnitCircle.normalized;

            Vector2 randomOffset = Random.insideUnitCircle * 0.15f;
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
        UpdateTrashAbsorbAnimation();
        UpdatePlayerAbsorbAnimation();
    }

    private void UpdateTrashAbsorbAnimation()
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
                // [重點註釋] 吸收完成時，讀取這顆垃圾的 ScoreValue 並廣播出去
                OnTrashAbsorbedScore?.Invoke(data.trash.ScoreValue);

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
        if (data.player == null)
        {
            _hasPlayerAbsorb = false;
            return;
        }

        Transform tr = data.player.transform;
        float dt = Time.deltaTime;

        switch (data.state)
        {
            case PlayerState.Absorbing:
                data.timer += dt;
                float tAbsorb = Mathf.Clamp01(data.timer / playerAbsorbTime);
                tr.position = Vector2.LerpUnclamped(data.startPos, CenterPos, tAbsorb);
                tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, tAbsorb);
                tr.Rotate(0f, 0f, playerRotateSpeed * dt);

                if (data.timer >= playerAbsorbTime)
                {
                    data.timer = 0f;
                    data.state = PlayerState.Waiting;
                }
                break;

            case PlayerState.Waiting:
                data.timer += dt;
                if (data.timer >= playerVanishTime)
                {
                    data.timer = 0f;
                    data.state = PlayerState.Ejecting;

                    if (audioEmitter != null) audioEmitter.PlayOneShot(ejectSound);

                    data.player.ExitBlackHole(data.ejectDir, playerEjectSpeed);
                }
                break;

            case PlayerState.Ejecting:
                data.timer += dt;
                float tEject = Mathf.Clamp01(data.timer / playerEjectDuration);
                tr.localScale = Vector3.LerpUnclamped(Vector3.zero, data.startScale, tEject);

                if (data.timer >= playerEjectDuration)
                {
                    _hasPlayerAbsorb = false;
                    return;
                }
                break;
        }

        _playerAbsorb = data;
    }

#if UNITY_EDITOR
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
#endif
}