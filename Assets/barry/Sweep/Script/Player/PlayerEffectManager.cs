using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// 它不參與物理運算，只等著被 Controller 呼叫
public class PlayerEffectManager : MonoBehaviour
{
    [Header("=== 特效組件引用 ===")]
    [SerializeField] private DragLine dragLine; // 拖曳線 (LineRenderer)
    [SerializeField] private DynamicSweepMesh chargedSweepMesh; // 蓄力扇形網格
    [SerializeField] private float chargedSweepRotationOffset = -90f; // 修正扇形預設角度用

    [Header("=== 粒子系統列表 ===")]
    // 陣列索引對應：0=TrashHit(撞垃圾), 1=Trail(拖尾), 2=WallHit(撞牆)
    [SerializeField] private ParticleSystem[] particleSystems;

    [Header("=== Trail (拖尾) 設定 ===")]
    [SerializeField] private float trailRateMax = 60f; // 最快速度時的噴發量
    [SerializeField] private float trailMinPowerToPlay = 0.01f; // 速度低於多少就不噴
    //[SerializeField] private float trailStartBurst = 0f; // 剛開始移動時是否要爆發一下

    [Header("=== Object Pool (物件池) 設定 ===")]
    [SerializeField] private int poolSizeTrashHit = 12; // 預先生成多少個撞擊特效
    [SerializeField] private int poolSizeWallHit = 12;

    // 方便內部使用的索引常數
    private const int FX_TRASH_HIT = 0;
    private const int FX_TRAIL = 1;
    private const int FX_WALL_HIT = 2;

    // 執行時的快取變數 (避免 Update 裡一直 GetComponent)
    private ParticleSystem _trailPS;
    private ParticleSystem.EmissionModule _trailEmission;
    private bool _trailReady;

    // 專門用來跟隨玩家的那個粒子實例
    private ParticleSystem _dragParticleInstance;

    //  使用自定義的物件池，避免一直 Instantiate/Destroy 造成卡頓
    private ParticlePool _trashHitPool;
    private ParticlePool _wallHitPool;
    private Transform _fxRoot; // 特效的父物件，保持 Hierarchy 整潔

    private void Awake()
    {
        // 初始化時先把不該顯示的東西關掉
        dragLine?.HideLine();
        if (chargedSweepMesh != null) chargedSweepMesh.gameObject.SetActive(false);

        SetupFX();
    }

    private void SetupFX()
    {
        // 建立一個乾淨的父物件來裝特效
        _fxRoot = new GameObject("[FX Root]").transform;
        _fxRoot.SetParent(transform.parent); // 保持在場景結構中

        // 1. 設定 Trail (這是持續存在的，不用 Pool)
        _trailPS = GetPS(FX_TRAIL);
        if (_trailPS != null)
        {
            _trailEmission = _trailPS.emission;
            _trailEmission.rateOverTime = 0f; // 預設不噴發
            _trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _trailReady = true;
        }

        // 2. 初始化物件池 (Pools) - 這裡會預先生成特效物件
        var trashPrefab = GetPS(FX_TRASH_HIT);
        if (trashPrefab != null) _trashHitPool = new ParticlePool(trashPrefab, poolSizeTrashHit, _fxRoot, "TrashHitPool");

        var wallPrefab = GetPS(FX_WALL_HIT);
        if (wallPrefab != null) _wallHitPool = new ParticlePool(wallPrefab, poolSizeWallHit, _fxRoot, "WallHitPool");
    }

    // 安全取得粒子系統的工具函式
    private ParticleSystem GetPS(int index)
    {
        if (particleSystems == null) return null;
        if (index < 0 || index >= particleSystems.Length) return null;
        return particleSystems[index];
    }

    // [API 接口區] 這裡的方法都是給 PlayerController 呼叫的

    // 1. 拖曳線控制：只管畫，不管起點終點是怎麼算出來的
    public void UpdateDragLine(Vector2 start, Vector2 end) => dragLine?.ShowLine(start, end);
    public void HideDragLine() => dragLine?.HideLine();

    // 2. 蓄力扇形控制：負責顯示網格變形
    public void ShowChargeSweep(Vector2 position, Vector2 dir, float t)
    {
        if (chargedSweepMesh == null) return;

        if (!chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(true);

        chargedSweepMesh.transform.position = position;
        // 計算旋轉角度 (Atan2) + 修正偏移
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + chargedSweepRotationOffset;
        chargedSweepMesh.transform.rotation = Quaternion.Euler(0f, 0f, ang);

        // 呼叫 Mesh 腳本更新形狀
        chargedSweepMesh.UpdateShape(t);
    }

    public void HideChargeSweep()
    {
        if (chargedSweepMesh != null && chargedSweepMesh.gameObject.activeSelf)
            chargedSweepMesh.gameObject.SetActive(false);
    }

    // 3. Trail (拖尾) 控制：根據 Controller 給的 power (0~1) 調整噴發量
    public void UpdateTrail(Vector2 center, Vector2 dir, float power01, bool armed)
    {
        if (!_trailReady || !armed)
        {
            StopTrail();
            return;
        }

        _trailPS.transform.position = center;
        // 讓粒子發射方向跟著移動方向
        if (dir.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _trailPS.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        // [視覺邏輯] 速度越快，粒子噴越多
        _trailEmission.rateOverTime = Mathf.Lerp(0f, trailRateMax, Mathf.Clamp01(power01));

        if (power01 > trailMinPowerToPlay)
        {
            if (!_trailPS.isPlaying) _trailPS.Play();
        }
        else
        {
            StopTrail();
        }
    }

    public void StopTrail()
    {
        if (!_trailReady) return;
        _trailEmission.rateOverTime = 0f;
        if (_trailPS.isPlaying)
            _trailPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 4. 跟隨粒子 (Coroutine) - 解決粒子跟丟問題
    public void StartFollowParticle(Transform target)
    {
        StartCoroutine(SpawnOrRestartFollowParticle(target));
    }

    private IEnumerator SpawnOrRestartFollowParticle(Transform target)
    {
        if (_dragParticleInstance == null)
        {
            var prefab = GetPS(FX_TRAIL); // 複用 FX_TRAIL 的 Prefab
            if (prefab != null)
            {
                _dragParticleInstance = Instantiate(prefab, target);
                _dragParticleInstance.transform.localPosition = Vector3.zero;
                _dragParticleInstance.transform.localRotation = Quaternion.identity;
            }
        }

        if (_dragParticleInstance != null)
        {
            _dragParticleInstance.Play(true);
            // [重要] 持續跟隨直到粒子銷毀或停止
            while (_dragParticleInstance != null)
            {
                _dragParticleInstance.transform.position = target.position;
                yield return null;
            }
        }
    }

    // 緊急停止所有特效 (例如被黑洞吸入時)
    public void StopAllEffects()
    {
        StopTrail();
        HideDragLine();
        HideChargeSweep();
        if (_dragParticleInstance != null) _dragParticleInstance.Stop();
    }

    // 5. OneShot 特效 (撞擊/撞牆) - 使用物件池
    public void PlayTrashHit(Vector2 pos, Vector2 normal)
    {
        if (_trashHitPool != null) SpawnOneShot(_trashHitPool, pos, normal);
    }

    public void PlayWallHit(Vector2 pos, Vector2 normal)
    {
        if (_wallHitPool != null) SpawnOneShot(_wallHitPool, pos, normal);
    }

    // [內部邏輯] 從池中拿出特效 -> 設定位置 -> 播放 -> 自動回收
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

  //特效池

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
            for (int i = 0; i < Mathf.Max(0, prewarm); i++) _idle.Enqueue(CreateInstance());
        }

        public ParticleSystem Get() => _idle.Count > 0 ? _idle.Dequeue() : CreateInstance();

        private ParticleSystem CreateInstance()
        {
            ParticleSystem ps = Instantiate(_prefab, _root);
            ps.gameObject.SetActive(false);
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback; // 播完自動回調
            var returner = ps.gameObject.GetComponent<ReturnToPool>() ?? ps.gameObject.AddComponent<ReturnToPool>();
            returner.Bind(this, ps);
            return ps;
        }

        public void Return(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            _idle.Enqueue(ps);
        }

        // 輔助腳本：掛在粒子物件上，監聽 "Stop" 事件
        private sealed class ReturnToPool : MonoBehaviour
        {
            private ParticlePool _pool;
            private ParticleSystem _ps;
            public void Bind(ParticlePool pool, ParticleSystem ps) { _pool = pool; _ps = ps; }
            private void OnParticleSystemStopped() { if (_pool != null && _ps != null) _pool.Return(_ps); }
        }
    }
}