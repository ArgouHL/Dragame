using UnityEngine;
using System;
using System.Collections.Generic;

// 定義結構，讓你在面板可以自由設定「哪個材質，對應哪個音檔名稱」
[Serializable]
public struct TrashSoundMapping
{
    public TrashType type;
    public string soundName;
}

[RequireComponent(typeof(PlayerController), typeof(AudioEmitter))]
public class PlayerAudioController : MonoBehaviour
{
    private PlayerController player;
    private AudioEmitter emitter;

    [Header("除錯設定")]
    [Tooltip("打勾時，會在控制台印出所有被觸發的音效事件，發布遊戲前請取消勾選。")]
    public bool enableDebugLog = true;

    [Header("對應音檔名稱 (需與 AudioEmitter 面板設定完全一致)")]
    public string switchSkillSound = "SwitchSkill";
    public string leftPressSound = "LeftPress";
    public string leftReleaseSound = "LeftRelease";
    public string chargingSound = "Charging";
    public string chargeShootSound = "ChargeShoot";
    public string wallHitSound = "WallHit";
    public string moveLoopSound = "Moving";

    [Header("垃圾打擊設定 (支援多材質)")]
    [Tooltip("將各種垃圾材質對應到 AudioEmitter 裡的名稱")]
    public TrashSoundMapping[] trashHitMappings;
    [Tooltip("獨立材質防連發機制：例如 0.05 秒內不會重複播放同一個鋁罐聲，但可以同時播放鋁罐跟玻璃聲")]
    public float trashHitCooldown = 0.05f;

    // 利用字典儲存「每一種材質」上次發出聲音的時間
    private Dictionary<TrashType, float> _hitCooldowns = new Dictionary<TrashType, float>();

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        emitter = GetComponent<AudioEmitter>();
    }

    private void OnEnable()
    {
        player.OnModeChanged += HandleModeChanged;
        player.OnLeftPressAction += HandleLeftPress;
        player.OnLeftReleaseAction += HandleLeftRelease;
        player.OnRightPressStart += HandleRightPress;
        player.OnChargedSweepUpdate += HandleChargeUpdate;
        player.OnChargedSweepReleased += HandleChargeRelease;
        player.OnWallHitEvent += HandleWallHit;

        // 註冊帶有參數的垃圾擊中事件
        player.OnTrashHitEvent += HandleTrashHit;
    }

    private void OnDisable()
    {
        player.OnModeChanged -= HandleModeChanged;
        player.OnLeftPressAction -= HandleLeftPress;
        player.OnLeftReleaseAction -= HandleLeftRelease;
        player.OnRightPressStart -= HandleRightPress;
        player.OnChargedSweepUpdate -= HandleChargeUpdate;
        player.OnChargedSweepReleased -= HandleChargeRelease;
        player.OnWallHitEvent -= HandleWallHit;

        player.OnTrashHitEvent -= HandleTrashHit;
    }

    private void Update()
    {
        if (player.CurrentSpeed > 0.1f && !player.isBeingAbsorbed && !player.isBlocking)
        {
            float vol = Mathf.Clamp01(player.CurrentSpeed / player.maxSpeed);
            emitter.PlayLocalLoop(moveLoopSound, 1f, vol);
        }
        else if (emitter.IsPlayingLocalLoop(moveLoopSound))
        {
            emitter.StopLocalLoop();
        }
    }

    private void LogTrace(string eventName)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[PlayerAudioController] 接收到事件：{eventName}，準備通知 Emitter 播放。");
        }
    }

    private void HandleModeChanged(BroomMode mode)
    {
        LogTrace("切換技能 (ModeChanged)");
        emitter.PlayOneShot(switchSkillSound);
    }

    private void HandleLeftPress()
    {
        LogTrace("左鍵按下 (LeftPress)");
        emitter.PlayOneShot(leftPressSound);
    }

    private void HandleLeftRelease()
    {
        LogTrace("左鍵放開 (LeftRelease)");
        emitter.PlayOneShot(leftReleaseSound);
    }

    private void HandleWallHit()
    {
        LogTrace("撞到空氣牆 (WallHit)");
        emitter.PlayOneShot(wallHitSound);
    }

    private void HandleRightPress()
    {
        LogTrace("右鍵開始蓄力 (RightPressStart)");
        emitter.PlayLocalLoop(chargingSound, 0.8f);
    }

    private void HandleChargeUpdate(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        float pitch = Mathf.Lerp(0.8f, 1.5f, t);
        emitter.PlayLocalLoop(chargingSound, pitch);
    }

    private void HandleChargeRelease(float holdTime, float t, Vector2 origin, Vector2 dir)
    {
        LogTrace("右鍵放開釋放 (ChargeRelease)");
        if (emitter.IsPlayingLocalLoop(chargingSound))
        {
            emitter.StopLocalLoop();
        }
        emitter.PlayOneShot(chargeShootSound);
    }

    // [重點註釋] 高階材質混音邏輯：透過字典找出該材質上次播放的時間，進行獨立冷卻
    private void HandleTrashHit(TrashType type)
    {
        if (!_hitCooldowns.ContainsKey(type))
        {
            _hitCooldowns[type] = -1f;
        }

        if (Time.time - _hitCooldowns[type] > trashHitCooldown)
        {
            string soundToPlay = GetTrashSoundName(type);

            if (!string.IsNullOrEmpty(soundToPlay))
            {
                LogTrace($"擊中垃圾：材質 {type} (對應音檔名稱：{soundToPlay})");
                emitter.PlayOneShot(soundToPlay);
                _hitCooldowns[type] = Time.time;
            }
            else
            {
                // 防呆機制：提醒你哪個材質忘記設定了
                Debug.LogWarning($"[PlayerAudioController] 警告：接收到擊中 {type} 材質，但你在 Trash Hit Mappings 中沒有為它設定對應名稱！");
            }
        }
    }

    // 輔助函式：從設定好的清單中尋找對應的字串
    private string GetTrashSoundName(TrashType type)
    {
        for (int i = 0; i < trashHitMappings.Length; i++)
        {
            if (trashHitMappings[i].type == type)
            {
                return trashHitMappings[i].soundName;
            }
        }
        return null;
    }
}