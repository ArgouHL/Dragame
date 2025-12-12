using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // particleSystems[0]=TrashHit, [1]=Trail(follow player), [2]=WallHit
    private const int FX_TRASH_HIT = 0;
    private const int FX_TRAIL = 1;
    private const int FX_WALL_HIT = 2;
    private ParticleSystem _dragParticleInstance;
    public Vector2 fixForWall;
    private Camera cam;
    private PlayerInput input;
    public Rigidbody2D rb;
    [SerializeField] private DragLine dragLine;
    public ParticleSystem[] particleSystems;

    public event Action<Vector2, float, Vector2, float> OnSweepMove;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepUpdate;
    public event Action<float, float, Vector2, Vector2> OnChargedSweepReleased;

    private InputAction pointerPress;
    private InputAction rightPointerPress;
    private InputAction pointerPosition;

    private Vector2 dragStart;
    private Vector2 moveDir;
    private float currentSpeed;

    [Header("移動設定")]
    public float maxSpeed = 12f;
    public float deceleration = 8f;
    public bool isBlocking = false;

    [Header("小掃設定 (左鍵移動掃)")]
    public float sweepRadius = 1f;
    public Vector2 sweepOffset;

    [Header("右鍵蓄力掃設定")]
    public float maxChargeTime = 1.5f;
    [SerializeField] private float chargeCenterOffset = 0f;

    [Header("玩家重量設定 (越大越不容易被垃圾反推)")]
    [SerializeField, Min(0.01f)] private float wieght = 1f;

    [Header("掃到垃圾的反作用力設定")]
    [SerializeField, Range(0f, 1f)] private float sweepRestitution = 0f;
    [SerializeField] private bool allowBounceBack = false;

    [Header("玩家被垃圾影響的手感")]
    [SerializeField] private bool scaleHitBySweepPower = true;
    [SerializeField, Range(0f, 1f)] private float minHitPower = 0.25f;
    [SerializeField, Min(0f)] private float hitWeightScale = 1f;

    public float CurrentSpeed => currentSpeed;
    public float Wieght => wieght;

    private float rightPressStartTime;
    public float rightHoldDuration { get; private set; }
    private bool isLeftDown = false;
    private bool isRightDown = false;
    private bool blockBoth = false;

    public bool isBeingAbsorbed;
    private Collider2D[] colliders;
    private Vector2 chargedDir;
    public bool IsBeingAbsorbed => isBeingAbsorbed;

    public static PlayerController instance { get; private set; }

    [Header("右鍵蓄力掃視覺")]
    [SerializeField] private DynamicSweepMesh chargedSweepMesh;
    [SerializeField] private float chargedSweepRotationOffset = -90f;

    // ====== FX：Trail（跟著玩家）======
    [Header("FX - Trail（左鍵放開後才開始噴；跟著玩家/掃把中心）")]
    [SerializeField] private float trailRateMax = 60f;        // 速度=滿速 時的噴發率
    [SerializeField] private float trailMinPowerToPlay = 0.01f;
    [SerializeField] private float trailStartBurst = 0f;      // 左鍵放開瞬間噴一下（可 0）

    private ParticleSystem _trailPS;
    private ParticleSystem.EmissionModule _trailEmission;
    private bool _trailReady;
    private bool _trailArmed; // 左鍵放開後才會 true

    // ====== FX：OneShot Pool ======
    [Header("FX - OneShot Pool")]
    [SerializeField] private int poolSizeTrashHit = 12;
    [SerializeField] private int poolSizeWallHit = 12;

    private ParticlePool _trashHitPool;
    private ParticlePool _wallHitPool;
    private Transform _fxRoot;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        cam = Camera.main;
        input = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
        dragLine = GetComponentInChildren<DragLine>();

        pointerPress = input.actions["PointerPress"];
        rightPointerPress = input.actions["RightPointerPress"];
        pointerPosition = input.actions["PointerPosition"];

        dragLine?.HideLine();
        if (chargedSweepMesh != null) chargedSweepMesh.gameObject.SetActive(false);

        SetupFX();
    }

    private void SetupFX()
    {
        _fxRoot = new GameObject("[FX Root]").transform;
        _fxRoot.SetParent(null, true);

        // Trail：應該是「場景內」掛在玩家/子物件上的 ParticleSystem（不是 Project 裡的 prefab asset）
        _trailPS = GetPS(FX_TRAIL);
        if (_trailPS != null && _trailPS.gameObject.scene.IsValid())
        {
            _trailEmission = _trailPS.emission;
            _trailEmission.rateOverTime = 0f;

            _trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _trailReady = true;
        }

        // OneShot Pool：prefab（Loop 必須關）
        var trashPrefab = GetPS(FX_TRASH_HIT);
        if (trashPrefab != null) _trashHitPool = new ParticlePool(trashPrefab, poolSizeTrashHit, _fxRoot, "TrashHitPool");

        var wallPrefab = GetPS(FX_WALL_HIT);
        if (wallPrefab != null) _wallHitPool = new ParticlePool(wallPrefab, poolSizeWallHit, _fxRoot, "WallHitPool");
    }

    private ParticleSystem GetPS(int index)
    {
        if (particleSystems == null) return null;
        if (index < 0 || index >= particleSystems.Length) return null;
        return particleSystems[index];
    }

    private void OnEnable()
    {
        pointerPress.started += OnPress;
        pointerPress.canceled += OnRelease;
        rightPointerPress.started += OnRightPress;
        rightPointerPress.canceled += OnRightRelease;
    }

    private void OnDisable()
    {
        pointerPress.started -= OnPress;
        pointerPress.canceled -= OnRelease;
        rightPointerPress.started -= OnRightPress;
        rightPointerPress.canceled -= OnRightRelease;
    }

    private void Update()
    {
        if (isBeingAbsorbed) return;
        if (pointerPosition == null) return;

        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());

        // 左鍵拖曳線（按住時只畫線，不噴 trail）
        if (pointerPress.IsPressed() && !blockBoth)
        {
            Vector2 drag = pointerWorld - dragStart;
            float len = drag.magnitude;

            Vector2 center = (Vector2)transform.position + sweepOffset;

            if (len > 0.001f)
            {
                Vector2 dir = drag / len;
                dragLine?.ShowLine(center, center + dir * len);
            }
            else
            {
                dragLine?.ShowLine(center, center);
            }
        }

        // 右鍵蓄力（不噴 trail）
        if (isRightDown && !blockBoth)
        {
            float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
            float t = Mathf.Clamp01(hold / maxChargeTime);

            Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
            Vector2 toPointer = pointerWorld - baseOrigin;

            if (toPointer.sqrMagnitude > 0.0001f) chargedDir = toPointer.normalized;
            else if (chargedDir.sqrMagnitude < 0.0001f) chargedDir = Vector2.right;

            Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;

            OnChargedSweepUpdate?.Invoke(hold, t, origin, chargedDir);

            if (chargedSweepMesh != null)
            {
                if (!chargedSweepMesh.gameObject.activeSelf) chargedSweepMesh.gameObject.SetActive(true);

                chargedSweepMesh.transform.position = origin;
                float ang = Mathf.Atan2(chargedDir.y, chargedDir.x) * Mathf.Rad2Deg + chargedSweepRotationOffset;
                chargedSweepMesh.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                chargedSweepMesh.UpdateShape(t);
            }
        }
        else
        {
            if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
                chargedSweepMesh.gameObject.SetActive(false);
        }
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        isLeftDown = true;
        if (isRightDown) { BlockBoth(); return; }

        // 左鍵按下：關閉 trail（只允許放開後開始噴）
        _trailArmed = false;
        StopTrailFX();

        StartDrag();
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        isLeftDown = false;
        if (blockBoth)
        {
            if (!isRightDown) UnblockBoth();
            return;
        }

        EndDrag(); // 左鍵放開：啟動 trail
    }

    private void OnRightPress(InputAction.CallbackContext ctx)
    {
        rb.linearVelocity = Vector2.zero;
        PlayerAnimatorController.instance.anim.SetBool("IsRightClick", true);
        if (isBeingAbsorbed) return;

        isRightDown = true;
        rightPressStartTime = Time.time;

        // 右鍵期間不噴 trail
        _trailArmed = false;
        StopTrailFX();

        if (isLeftDown) { BlockBoth(); return; }

        if (chargedSweepMesh != null && !chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(true);
    }

    private void OnRightRelease(InputAction.CallbackContext ctx)
    {
        if (isBeingAbsorbed) return;

        isRightDown = false;
        if (blockBoth)
        {
            if (!isLeftDown) UnblockBoth();
            return;
        }

        float hold = Mathf.Clamp(Time.time - rightPressStartTime, 0f, maxChargeTime);
        rightHoldDuration = hold;
        float t = Mathf.Clamp01(hold / maxChargeTime);

        Vector2 pointerWorld = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        Vector2 baseOrigin = (Vector2)transform.position + sweepOffset;
        Vector2 toPointer = pointerWorld - baseOrigin;

        PlayerAnimatorController.instance.anim.SetFloat("ClickX", toPointer.x);
        PlayerAnimatorController.instance.anim.SetFloat("ClickY", toPointer.y);
        PlayerAnimatorController.instance.anim.SetBool("IsRightClick", false);

        if (toPointer.sqrMagnitude > 0.0001f) chargedDir = toPointer.normalized;
        else if (chargedDir.sqrMagnitude < 0.0001f) chargedDir = Vector2.right;

        Vector2 origin = baseOrigin + chargedDir * chargeCenterOffset;
        OnChargedSweepReleased?.Invoke(hold, t, origin, chargedDir);

        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    private void BlockBoth()
    {
        blockBoth = true;
        dragLine?.HideLine();
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;

        _trailArmed = false;
        StopTrailFX();

        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    private void UnblockBoth() => blockBoth = false;

    private void StartDrag()
    {
        dragStart = ScreenToWorld(pointerPosition.ReadValue<Vector2>());
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;

        Vector2 center = (Vector2)transform.position + sweepOffset;
        dragLine?.ShowLine(center, center);
    }

    private void EndDrag()
    {
        dragLine?.HideLine();
        Vector2 drag = ScreenToWorld(pointerPosition.ReadValue<Vector2>()) - dragStart;
        float len = drag.magnitude;
        if (len < 0.05f) return;

        moveDir = drag / len;
        currentSpeed = Mathf.Min(len * 3f, maxSpeed);

        // ② 左鍵放開：開始噴 trail（跟著玩家/掃把中心）
        /* _trailArmed = true;
         StartTrailBurstAtCurrent();*/
        StartCoroutine(SpawnOrRestartFollowParticle()); 
    }
    private IEnumerator SpawnOrRestartFollowParticle()
    {
        if (_dragParticleInstance == null)
        {
            _dragParticleInstance = Instantiate(particleSystems[1], transform);
            _dragParticleInstance.transform.localPosition = Vector3.zero;
            _dragParticleInstance.transform.localRotation = Quaternion.identity;
        }

        _dragParticleInstance.Play(true);

        while (_dragParticleInstance != null)
        {
            _dragParticleInstance.transform.position = transform.position;
            yield return null; // ⚠ 必須有
        }
    }
    private void StartTrailBurstAtCurrent()
    {
        if (!_trailReady || !_trailArmed) return;

        Vector2 center = (Vector2)transform.position + sweepOffset;
        _trailPS.transform.position = center;

        if (!_trailPS.isPlaying) _trailPS.Play();

        if (trailStartBurst > 0.01f)
            _trailPS.Emit(Mathf.RoundToInt(trailStartBurst));
    }

    public void ApplyHitSlowdown(float totalTrashWieght, float sweepPower01)
    {
        if (currentSpeed <= 0f) return;
        if (totalTrashWieght <= 0f) return;

        float M = Mathf.Max(0.01f, wieght);

        float p = 1f;
        if (scaleHitBySweepPower)
            p = Mathf.Lerp(minHitPower, 1f, Mathf.Clamp01(sweepPower01));

        float m = Mathf.Max(0f, totalTrashWieght) * hitWeightScale * p;
        if (m <= 0f) return;

        float e = Mathf.Clamp01(sweepRestitution);
        float ratio = (M - e * m) / (M + m);

        if (!allowBounceBack)
        {
            ratio = Mathf.Clamp01(ratio);
            currentSpeed *= ratio;
        }
        else
        {
            if (ratio < 0f)
            {
                moveDir = -moveDir;
                currentSpeed *= Mathf.Clamp01(-ratio);
            }
            else
            {
                currentSpeed *= Mathf.Clamp01(ratio);
            }
        }

        if (currentSpeed <= 0.01f)
        {
            currentSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            StopTrailFX();
        }
        else
        {
            rb.linearVelocity = moveDir * currentSpeed;
        }
    }

    private void FixedUpdate()
    {
        if (isBlocking || isBeingAbsorbed)
        {
            rb.linearVelocity = Vector2.zero;
            StopTrailFX();
            return;
        }

        if (currentSpeed > 0.01f)
        {
            Vector2 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            nextPos += fixForWall;

            // ③ 撞牆：你的牆是 WorldBounds2D（不是 collider），所以在 IsOutside 觸發停止時算撞擊點噴一次
            if (WorldBounds2D.Instance != null && WorldBounds2D.Instance.IsOutside(nextPos))
            {
                if (TryGetWallHitFromWorldBounds(nextPos, moveDir, out var hitPoint, out var hitNormal))
                    EmitWallHit(hitPoint, hitNormal);
                else
                    EmitWallHit(nextPos, -moveDir);

                currentSpeed = 0f;
                rb.linearVelocity = Vector2.zero;

                _trailArmed = false;
                StopTrailFX();
                return;
            }

            rb.linearVelocity = moveDir * currentSpeed;
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, 0f);

            float sweepPower = Mathf.Clamp01(currentSpeed / maxSpeed);
            Vector2 center = (Vector2)transform.position + sweepOffset;

            // ② Trail：跟著玩家/掃把中心，移動時持續噴
            UpdateTrailFX(center, moveDir, sweepPower);

            OnSweepMove?.Invoke(center, sweepRadius, moveDir, sweepPower);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            StopTrailFX();
        }
    }

    // ========= Trail（跟著玩家）=========
    private void UpdateTrailFX(Vector2 center, Vector2 dir, float power01)
    {
        if (!_trailReady || !_trailArmed) return;

        _trailPS.transform.position = center;

        if (dir.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _trailPS.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        _trailEmission.rateOverTime = Mathf.Lerp(0f, trailRateMax, Mathf.Clamp01(power01));

        if (power01 > trailMinPowerToPlay)
        {
            if (!_trailPS.isPlaying) _trailPS.Play();
        }
        else
        {
            StopTrailFX();
        }
    }

    private void StopTrailFX()
    {
        if (!_trailReady) return;

        _trailEmission.rateOverTime = 0f;
        if (_trailPS.isPlaying)
            _trailPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ========= ③ 撞牆：從 WorldBounds2D 估算 hitPoint/hitNormal（支援矩形/多邊形）=========
    private bool TryGetWallHitFromWorldBounds(Vector2 worldPos, Vector2 moveDir, out Vector2 hitPoint, out Vector2 hitNormal)
    {
        hitPoint = worldPos;
        hitNormal = -moveDir;

        var wb = WorldBounds2D.Instance;
        if (wb == null || wb.cam == null) return false;

        Camera c = wb.cam;
        Vector3 vp3 = c.WorldToViewportPoint(worldPos);
        Vector2 vp = new Vector2(vp3.x, vp3.y);

        // 1) 若多邊形邊界
        if (wb.viewportPolygon != null && wb.viewportPolygon.Count >= 3)
        {
            Vector2 closest = vp;
            Vector2 normalVp = Vector2.up;

            float bestSqr = float.PositiveInfinity;
            var poly = wb.viewportPolygon;
            int count = poly.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % count];

                Vector2 ab = b - a;
                float abLenSqr = ab.sqrMagnitude;
                if (abLenSqr <= Mathf.Epsilon) continue;

                float t = Vector2.Dot(vp - a, ab) / abLenSqr;
                t = Mathf.Clamp01(t);

                Vector2 proj = a + ab * t;
                float sqr = (vp - proj).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    closest = proj;

                    // 先取邊法線
                    Vector2 edge = b - a;
                    Vector2 n = new Vector2(-edge.y, edge.x).normalized;

                    // 讓法線朝向物體（vp - closest）
                    Vector2 towardObj = vp - closest;
                    if (Vector2.Dot(n, towardObj) < 0f) n = -n;
                    normalVp = n;
                }
            }

            Vector3 w0 = c.ViewportToWorldPoint(new Vector3(closest.x, closest.y, vp3.z));
            hitPoint = new Vector2(w0.x, w0.y);

            const float eps = 0.01f;
            Vector3 w1 = c.ViewportToWorldPoint(new Vector3(closest.x + normalVp.x * eps, closest.y + normalVp.y * eps, vp3.z));
            Vector2 nW = new Vector2(w1.x - w0.x, w1.y - w0.y);

            if (nW.sqrMagnitude > 1e-8f) hitNormal = nW.normalized;
            return true;
        }

        // 2) 矩形邊界（用 GetWorldRect 近似 hitPoint）
        Rect rect = wb.GetWorldRect();
        if (rect.width <= 0f || rect.height <= 0f) return false;

        float clampedX = Mathf.Clamp(worldPos.x, rect.xMin, rect.xMax);
        float clampedY = Mathf.Clamp(worldPos.y, rect.yMin, rect.yMax);
        hitPoint = new Vector2(clampedX, clampedY);

        Vector2 delta = worldPos - hitPoint;
        if (delta.sqrMagnitude > 1e-8f) hitNormal = delta.normalized;
        else hitNormal = -moveDir;

        return true;
    }

    // ========= OneShot：① 垃圾撞掃把（外部呼叫）=========
    public void EmitTrashHit(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (_trashHitPool == null) return;
        SpawnOneShot(_trashHitPool, hitPoint, hitNormal);
    }

    // ========= OneShot：③ 掃把撞牆（內部呼叫）=========
    private void EmitWallHit(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (_wallHitPool == null) return;
        SpawnOneShot(_wallHitPool, hitPoint, hitNormal);
    }

    private static void SpawnOneShot(ParticlePool pool, Vector2 pos, Vector2 normal)
    {
        ParticleSystem ps = pool.Get();
        if (ps == null) return;

        float ang = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
        ps.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, ang));

        ps.gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    private void EnsureColliders()
    {
        if (colliders != null) return;
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        EnsureColliders();
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = enabled;
    }

    public void EnterBlackHole()
    {
        if (isBeingAbsorbed) return;

        isBeingAbsorbed = true;
        blockBoth = true;
        isLeftDown = false;
        isRightDown = false;
        currentSpeed = 0f;
        rb.linearVelocity = Vector2.zero;
        dragLine?.HideLine();
        SetCollidersEnabled(false);

        _trailArmed = false;
        StopTrailFX();

        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    public void ExitBlackHole(Vector2 ejectDir, float ejectSpeed)
    {
        isBeingAbsorbed = false;
        blockBoth = false;
        moveDir = ejectDir;
        currentSpeed = ejectSpeed;
        SetCollidersEnabled(true);
        rb.linearVelocity = moveDir * currentSpeed;
    }

    private Vector2 ScreenToWorld(Vector2 p)
        => cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + sweepOffset, sweepRadius);
    }

    // ====== Pool（OneShot Prefab 必須 Loop=false 才會 stop callback 回收）======
    private sealed class ParticlePool
    {
        private readonly Queue<ParticleSystem> _idle = new Queue<ParticleSystem>();
        private readonly ParticleSystem _prefab;
        private readonly Transform _root;

        public ParticlePool(ParticleSystem prefab, int prewarm, Transform parentRoot, string name)
        {
            _prefab = prefab;
            _root = new GameObject(name).transform;
            _root.SetParent(parentRoot, false);

            int n = Mathf.Max(0, prewarm);
            for (int i = 0; i < n; i++)
                _idle.Enqueue(CreateInstance());
        }

        public ParticleSystem Get()
        {
            return _idle.Count > 0 ? _idle.Dequeue() : CreateInstance();
        }

        private ParticleSystem CreateInstance()
        {
            ParticleSystem ps = Instantiate(_prefab, _root);
            ps.gameObject.SetActive(false);

            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            var returner = ps.gameObject.GetComponent<ReturnToPool>();
            if (returner == null) returner = ps.gameObject.AddComponent<ReturnToPool>();
            returner.Bind(this, ps);

            return ps;
        }

        private void Return(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            _idle.Enqueue(ps);
        }

        private sealed class ReturnToPool : MonoBehaviour
        {
            private ParticlePool _pool;
            private ParticleSystem _ps;

            public void Bind(ParticlePool pool, ParticleSystem ps)
            {
                _pool = pool;
                _ps = ps;
            }

            private void OnParticleSystemStopped()
            {
                if (_pool != null && _ps != null)
                    _pool.Return(_ps);
            }
        }
    }
}
