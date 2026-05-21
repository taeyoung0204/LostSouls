using UnityEngine;
using LostSouls.Boss;
using LostSouls.Player;
using LostSouls.Audio;

namespace LostSouls.World
{
    /// <summary>
    /// 보스룸 입구에 배치되는 진입 트리거.
    /// 플레이어가 통과하면 보스 AI를 활성화하고 BossHUD를 표시한다.
    ///
    /// 보스 GameObject 자체는 처음부터 활성 — Idle 클립 재생하며 가만히 서있음 (다크소울 정석).
    /// 트리거 통과 시 BossController.Activate() 호출 → State 머신/AI 깨어남.
    ///
    /// 진입 시 플레이어 HP 포션도 자동 풀충전 (임시 — 추후 모닥불/체크포인트로 교체 예정).
    /// 진입 시 보스 전투 BGM 재생 (AudioManager.ClipBank.bossBattleBGM 기준).
    /// 진입과 동시에 entranceBarrier(물리 벽) 활성화 — 보스전 중 도주 방지 (다크소울 안개의 벽).
    /// 보스 사망 시 자동으로 벽 제거 + BGM 페이드아웃.
    ///
    /// 1회만 발동. 사용자가 보스를 죽이고 다시 입장해도 재발동 없음.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossRoomTrigger : MonoBehaviour
    {
        [Header("Activation Targets")]
        [Tooltip("플레이어 진입 시 활성화할 BossController. AI/State 머신만 깨어남 (GameObject는 처음부터 활성).")]
        [SerializeField] private BossController bossController;
        [Tooltip("플레이어 진입 시 활성화할 BossHUD GameObject.")]
        [SerializeField] private GameObject bossHUD;
        [Tooltip("플레이어 진입 시 활성화할 입구 벽 GameObject. 일반 Collider (Is Trigger OFF) " +
                 "라야 물리적으로 막힘. 보스 사망 시 자동 비활성됨. " +
                 "다크소울 '안개의 벽' 역할.")]
        [SerializeField] private GameObject entranceBarrier;

        [Header("Detection")]
        [Tooltip("플레이어 판별용 태그. PlayerLockOn 등에서도 쓰는 동일한 태그.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Potion Refill")]
        [Tooltip("진입 시 포션 풀충전 여부. 임시 — 추후 모닥불 시스템 도입 시 제거 예정.")]
        [SerializeField] private bool refillPotionOnEnter = true;

        [Header("Audio")]
        [Tooltip("보스 사망 시 BGM 페이드아웃 시간 (초). PlayBGM의 페이드인은 AudioManager 기본값 사용.")]
        [SerializeField] private float bgmFadeOutDuration = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = false;

        private bool _activated;

        private void Awake()
        {
            // 시작 시 HUD/벽 둘 다 비활성 (보스 자체는 활성 유지 — Idle 클립으로 가만히 서있음)
            if (bossHUD != null) bossHUD.SetActive(false);
            if (entranceBarrier != null) entranceBarrier.SetActive(false);

            // 자기 Collider가 트리거인지 검증
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[{name}] BossRoomTrigger의 Collider가 IsTrigger=false. 인스펙터에서 체크해라.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_activated) return;
            if (!other.CompareTag(playerTag)) return;

            Activate(other);
        }

        private void Activate(Collider playerCollider)
        {
            _activated = true;

            // BossController에 활성화 신호 — AI/State 머신 깨어남
            if (bossController != null) bossController.Activate();

            // HUD 표시 시작
            if (bossHUD != null) bossHUD.SetActive(true);

            // 입구 벽 활성 — 보스전 중 도주 방지
            if (entranceBarrier != null) entranceBarrier.SetActive(true);

            // 포션 풀충전 (임시)
            if (refillPotionOnEnter)
            {
                var potion = playerCollider.GetComponent<PlayerPotion>();
                if (potion != null)
                {
                    potion.Refill();
                }
                else
                {
                    Debug.LogWarning($"[BossRoom] Player에 PlayerPotion 컴포넌트 없음. 포션 충전 스킵.");
                }
            }

            // 전투 BGM 시작. AudioManager 싱글톤 + ClipBank.bossBattleBGM 둘 다 있어야 작동.
            // 없으면 조용히 무음.
            if (AudioManager.Instance != null && AudioManager.Instance.ClipBank != null)
            {
                AudioClip bgm = AudioManager.Instance.ClipBank.bossBattleBGM;
                if (bgm != null)
                    AudioManager.Instance.PlayBGM(bgm);
            }

            // 보스 사망 이벤트 구독 — 사망 시 벽 자동 제거 + BGM 페이드아웃
            // (BossController.Health는 Awake에서 잡혀있음. Activate가 호출되는 시점엔 이미 준비 완료)
            if (bossController != null && bossController.Health != null)
                bossController.Health.OnDeath += HandleBossDeath;

            Debug.Log($"[BossRoom] Player entered. Boss + HUD + Barrier + BGM activated. Potion refilled: {refillPotionOnEnter}");

            // 트리거 자체는 비활성. 다시 발동되거나 OnTriggerExit가 호출되지 않도록.
            // gameObject.SetActive(false) 하면 이 컴포넌트 자체도 죽어 OnDeath 콜백 못 받음 →
            // 트리거 Collider만 비활성하여 GameObject + 이벤트 구독은 살려둠.
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        private void HandleBossDeath()
        {
            // 보스 사망 → 입구 벽 제거. 플레이어가 자유롭게 둘러볼 수 있음.
            if (entranceBarrier != null) entranceBarrier.SetActive(false);

            // BGM 페이드아웃
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopBGM(bgmFadeOutDuration);

            // 이벤트 구독 해제 (안전망 — BossHealth가 OnDeath 1회만 발행하므로 사실상 불필요)
            if (bossController != null && bossController.Health != null)
                bossController.Health.OnDeath -= HandleBossDeath;

            Debug.Log($"[BossRoom] Boss defeated. Barrier removed. BGM fading out.");
        }

        private void OnDestroy()
        {
            // 안전망 — 씬 전환 등으로 이 컴포넌트가 파괴될 때 구독 해제
            if (bossController != null && bossController.Health != null)
                bossController.Health.OnDeath -= HandleBossDeath;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugInfo) return;

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.color = _activated ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 1f, 0f, 0.3f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = _activated ? Color.green : Color.yellow;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}