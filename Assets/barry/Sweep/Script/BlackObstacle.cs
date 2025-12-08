using UnityEngine;
using System.Collections.Generic;

public class BlackObstacle : BaseObstacle
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

    private float _sqrAbsorbRadius;
    private readonly List<BaseTrash> _nearbyTrash = new List<BaseTrash>(64);

    private struct AbsorbData
    {
        public BaseTrash trash;
        public Vector3 startPos;
        public Vector3 startScale;
        public Vector3 targetPos;
        public float elapsed;
    }

    private readonly List<AbsorbData> _absorbing = new List<AbsorbData>(64);

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

    private void Awake()
    {
        _sqrAbsorbRadius = absorbRadius * absorbRadius;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    private void FixedUpdate()
    {
        CheckAndAbsorbTrash();
        CheckAndAbsorbPlayer();
    }

    private void Update()
    {
        UpdateAbsorbAnimation();
        UpdatePlayerAbsorbAnimation();
    }

    private void CheckAndAbsorbTrash()
    {
        if (SpatialGridManager.Instance == null) return;

        SpatialGridManager.Instance.GetTrashAroundPosition(transform.position, _nearbyTrash);
        if (_nearbyTrash.Count == 0) return;

        Vector3 myPos = transform.position;

        for (int i = 0; i < _nearbyTrash.Count; i++)
        {
            BaseTrash trash = _nearbyTrash[i];
            if (trash == null || trash.IsAbsorbing) continue;

            float sqrDist = (trash.transform.position - myPos).sqrMagnitude;
            if (sqrDist > _sqrAbsorbRadius) continue;

            trash.OnEnterBlackHole();

            AbsorbData data;
            data.trash = trash;
            data.startPos = trash.transform.position;
            data.startScale = trash.transform.localScale;
            data.targetPos = myPos;
            data.elapsed = 0f;
            _absorbing.Add(data);
        }
    }

    private void CheckAndAbsorbPlayer()
    {
        if (player == null) return;
        if (_hasPlayerAbsorb) return;
        if (player.IsBeingAbsorbed) return;

        Vector3 myPos = transform.position;
        Vector3 playerPos = player.transform.position;
        float sqrDist = (playerPos - myPos).sqrMagnitude;
        if (sqrDist > _sqrAbsorbRadius) return;

        player.EnterBlackHole();

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        PlayerAbsorbData data;
        data.player = player;
        data.startPos = playerPos;
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
                continue;
            }

            float duration = trashAbsorbTime > 0f ? trashAbsorbTime : trash.AbsorbEffectDuration;
            if (duration <= 0f)
            {
                trash.ResetState();
                TrashPool.Instance.ReturnTrash(trash);
                _absorbing.RemoveAt(i);
                continue;
            }

            data.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(data.elapsed / duration);

            AnimationCurve scaleCurve = trashScaleCurve != null ? trashScaleCurve : trash.ScaleCurve;
            AnimationCurve moveCurve = trashMoveCurve != null ? trashMoveCurve : trash.MoveCurve;

            float scaleT = scaleCurve != null ? scaleCurve.Evaluate(t) : t;
            float moveT = moveCurve != null ? moveCurve.Evaluate(t) : t;

            float rotSpeed = trashRotateSpeed != 0f ? trashRotateSpeed : trash.RotationSpeed;

            Transform tr = trash.transform;
            tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, scaleT);
            tr.position = Vector2.LerpUnclamped(data.startPos, data.targetPos, moveT);

            if (data.elapsed >= duration)
            {
                trash.ResetState();
                TrashPool.Instance.ReturnTrash(trash);
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
            if (duration <= 0f)
            {
                tr.position = data.targetPos;
                tr.localScale = Vector3.zero;
                data.timer = 0f;
                data.state = PlayerState.Waiting;
                _playerAbsorb = data;
                return;
            }

            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / duration);

            tr.position = Vector2.LerpUnclamped(data.startPos, data.targetPos, t);
            tr.localScale = Vector3.LerpUnclamped(data.startScale, Vector3.zero, t);
            if (playerRotateSpeed != 0f)
                tr.Rotate(0f, 0f, playerRotateSpeed * Time.deltaTime);

            if (data.timer >= duration)
            {
                tr.position = data.targetPos;
                tr.localScale = Vector3.zero;
                tr.rotation = data.startRot;
                data.timer = 0f;
                data.state = PlayerState.Waiting;
            }

            _playerAbsorb = data;
            return;
        }

        if (data.state == PlayerState.Waiting)
        {
            float waitTime = playerVanishTime;
            data.timer += Time.deltaTime;

            if (data.timer >= waitTime)
            {
                data.timer = 0f;
                data.state = PlayerState.Ejecting;
                tr.position = data.targetPos;
                tr.localScale = Vector3.zero;
                tr.rotation = data.startRot;
                p.ExitBlackHole(data.ejectDir, playerEjectSpeed);
            }

            _playerAbsorb = data;
            return;
        }

        if (data.state == PlayerState.Ejecting)
        {
            float duration = playerEjectDuration;
            if (duration <= 0f)
            {
                tr.localScale = data.startScale;
                _hasPlayerAbsorb = false;
                return;
            }

            data.timer += Time.deltaTime;
            float t = Mathf.Clamp01(data.timer / duration);

            tr.localScale = Vector3.LerpUnclamped(Vector3.zero, data.startScale, t);

            if (data.timer >= duration)
            {
                tr.localScale = data.startScale;
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
    }
}
