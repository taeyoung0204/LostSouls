using UnityEngine;

namespace LostSouls.Player
{
    /// <summary>
    /// HP 포션 시스템 (엘든링 스타일).
    ///
    /// 사용 흐름 (3-phase):
    /// 1. Wind-up: 팔 올리는 모션. 아직 차감/회복 시작 안 됨.
    ///    이 phase 중 피격 → 모션 캔슬, 포션 미차감 (InterruptDrink 호출).
    /// 2. Active: 마시는 모션. OnPotionDrinkStart Animation Event에서 phase 진입.
    ///    이때 포션 개수 차감 + 회복 시작. 매 프레임 _health.Heal() 호출.
    ///    이 phase 중 피격 → Flinch만 발동, 회복은 계속 진행.
    /// 3. Recovery: 팔 내리는 모션. OnPotionDrinkEnd로 회복 종료.
    ///    OnPotionCancelWindowOpen으로 회피 캔슬 가능 (PlayerController가 체크).
    ///
    /// 리필: BossRoomTrigger 진입 시 Refill() 호출 (임시).
    /// 추후 모닥불/체크포인트 시스템 도입 시 그쪽으로 교체.
    /// </summary>
    public class PlayerPotion : MonoBehaviour
    {
        public enum PotionPhase
        {
            None,       // 사용 중이 아님
            WindUp,     // 팔 올리는 모션 중 (캔슬 가능, 미차감)
            Active,     // 마시는 중 (회복 진행, 차감됨)
            Recovery    // 팔 내리는 모션 중 (회복 완료, 회피 캔슬 가능)
        }

        [Header("Stats")]
        [SerializeField] private int maxCharges = 5;
        [Tooltip("한 번 마실 때 총 회복량 (HP). Active phase 전체에 걸쳐 분산 적용됨.")]
        [SerializeField] private float healAmount = 50f;
        [Tooltip("회복이 분산될 시간 (초). 너무 짧으면 즉시 회복처럼 보이고, 너무 길면 효과 약함.")]
        [SerializeField] private float healDuration = 1.5f;

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Animator animator;
        [Tooltip("마시기 사운드 재생용. 비우면 Awake에서 자동 탐색.")]
        [SerializeField] private PlayerAudio playerAudio;

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = false;

        private int _currentCharges;
        private PotionPhase _phase = PotionPhase.None;
        private bool _cancelWindowOpen;  // Recovery phase 중 회피로 캔슬 가능한지

        // 회복 진행 상태
        private float _healTimer;        // Active phase 진입 후 경과 시간
        private float _healPerSecond;    // 미리 계산 (healAmount / healDuration)

        // Animator
        private static readonly int DrinkHash = Animator.StringToHash("Drink");

        // === Public 접근자 ===
        public int CurrentCharges => _currentCharges;
        public int MaxCharges => maxCharges;
        public PotionPhase CurrentPhase => _phase;
        public bool IsDrinking => _phase != PotionPhase.None;
        /// <summary>다른 행동(이동/공격/회피)이 차단되어야 하는 phase. Recovery는 회피 캔슬 허용이라 false 처리 가능하지만 — PlayerController가 IsCancelWindowOpen으로 별도 분기.</summary>
        public bool BlocksAction => _phase == PotionPhase.WindUp || _phase == PotionPhase.Active || _phase == PotionPhase.Recovery;
        public bool IsCancelWindowOpen => _cancelWindowOpen;

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (playerAudio == null)
                playerAudio = GetComponent<PlayerAudio>();

            // 난이도 따라 시작 포션 개수 적용 (Easy 7, Normal 5, Hard 3 등).
            // GameSettings 없는 경우(테스트용 단일 씬 등)는 인스펙터 maxCharges 그대로.
            var settings = LostSouls.Settings.GameSettings.Instance;
            if (settings != null && settings.CurrentDifficulty != null)
            {
                maxCharges = settings.CurrentDifficulty.startingPotionCharges;
            }

            _currentCharges = maxCharges;
            _healPerSecond = healDuration > 0f ? healAmount / healDuration : healAmount;
        }

        // Start는 더 이상 GameSettings 처리하지 않음.
        // 이유:
        // - GameSettings는 DontDestroyOnLoad 싱글톤이라 TitleScene → GameScene 전환 후에도 존재
        // - GameScene 단독 실행 시에는 GameSettings 자체가 없음 → Awake의 null 체크가 인스펙터 값 유지
        // 둘 다 Awake 시점 처리로 충분.

        private void Update()
        {
            // Active phase일 때만 회복 진행
            if (_phase != PotionPhase.Active) return;

            _healTimer += Time.deltaTime;
            float deltaHeal = _healPerSecond * Time.deltaTime;

            // 의도된 totalHeal 초과 방지 (Animation Event 타이밍이 살짝 어긋나도 안전)
            float remainingTime = Mathf.Max(0f, healDuration - (_healTimer - Time.deltaTime));
            float maxDeltaThisFrame = _healPerSecond * Mathf.Min(Time.deltaTime, remainingTime);

            if (playerHealth != null && deltaHeal > 0f)
                playerHealth.Heal(Mathf.Min(deltaHeal, maxDeltaThisFrame));
        }

        /// <summary>
        /// PlayerController가 R키 입력 시 호출. Wind-up phase 진입.
        /// 실패 조건: 이미 마시는 중 / 잔량 없음 / 사망.
        /// HP 풀이어도 사용 가능 (테스트 편의 + 어차피 모션 동안 묶이는 리스크 있음).
        /// 반환값: 성공하면 true (PlayerController가 다른 입력 처리 막을 수 있게).
        /// </summary>
        public bool TryStartDrink()
        {
            if (IsDrinking) return false;
            if (_currentCharges <= 0) return false;
            if (playerHealth == null || playerHealth.IsDead) return false;

            _phase = PotionPhase.WindUp;
            _cancelWindowOpen = false;
            _healTimer = 0f;

            if (animator != null)
                animator.SetTrigger(DrinkHash);

            if (drawDebugInfo)
                Debug.Log($"[Potion] Drink START (Wind-up) — charges {_currentCharges}/{maxCharges}");

            return true;
        }

        /// <summary>
        /// Animation Event: Wind-up → Active 전환 시점.
        /// 여기서 포션 개수 차감 + 회복 시작.
        /// Wind-up 중 피격으로 캔슬됐다면 이 함수는 호출되지 않음 (Flinch State로 빠져서).
        /// </summary>
        public void OnPotionDrinkStart()
        {
            // Wind-up 단계가 아니면 무시 (안전망 — State가 꼬여서 호출되는 경우)
            if (_phase != PotionPhase.WindUp)
            {
                if (drawDebugInfo)
                    Debug.LogWarning($"[Potion] OnPotionDrinkStart called in unexpected phase: {_phase}");
                return;
            }

            // 포션 차감 (Active 진입의 조건)
            _currentCharges--;
            _phase = PotionPhase.Active;
            _healTimer = 0f;

            // 마시기 사운드 (실제 입에 대는 시점이라 자연스러움)
            if (playerAudio != null) playerAudio.PlayDrink();

            if (drawDebugInfo)
                Debug.Log($"[Potion] Active — charge consumed, charges now {_currentCharges}/{maxCharges}");
        }

        /// <summary>
        /// Animation Event: Active → Recovery 전환 시점.
        /// 회복 종료. 캔슬 윈도우는 아직 열리지 않음 (별도 Event).
        /// </summary>
        public void OnPotionDrinkEnd()
        {
            if (_phase != PotionPhase.Active)
            {
                if (drawDebugInfo)
                    Debug.LogWarning($"[Potion] OnPotionDrinkEnd called in unexpected phase: {_phase}");
                return;
            }

            _phase = PotionPhase.Recovery;

            if (drawDebugInfo)
                Debug.Log($"[Potion] Recovery — heal ended");
        }

        /// <summary>
        /// Animation Event: Recovery phase 중 회피 캔슬 OPEN.
        /// 보통 OnPotionDrinkEnd 직후 ~5~10% 후에 박아두면 자연스러움.
        /// (팔이 살짝 내려간 시점부터 회피 가능)
        /// </summary>
        public void OnPotionCancelWindowOpen()
        {
            _cancelWindowOpen = true;

            if (drawDebugInfo)
                Debug.Log($"[Potion] Cancel window OPEN (Recovery)");
        }

        /// <summary>
        /// Wind-up phase 중 피격 시 PlayerController가 호출. 포션 차감 없이 종료.
        /// Active 이후라면 호출하지 않아야 함 (회복 계속 진행).
        /// </summary>
        public void InterruptDrink()
        {
            if (_phase != PotionPhase.WindUp) return;

            _phase = PotionPhase.None;
            _cancelWindowOpen = false;

            if (drawDebugInfo)
                Debug.Log($"[Potion] Drink INTERRUPTED in Wind-up — no charge consumed");
        }

        /// <summary>
        /// 외부에서 강제 종료 (Drink 클립이 자연 종료될 때 PlayerController가 호출).
        /// State 전환 감지로 자동 호출되도록 PlayerController의 UpdateCombatState에서 처리.
        /// </summary>
        public void EndDrink()
        {
            if (_phase == PotionPhase.None) return;

            _phase = PotionPhase.None;
            _cancelWindowOpen = false;
            _healTimer = 0f;

            if (drawDebugInfo)
                Debug.Log($"[Potion] Drink ENDED (state exit)");
        }

        /// <summary>
        /// 임시: BossRoomTrigger에서 호출. 추후 모닥불/체크포인트로 교체 예정.
        /// </summary>
        public void Refill()
        {
            _currentCharges = maxCharges;

            if (drawDebugInfo)
                Debug.Log($"[Potion] Refilled to {maxCharges}");
        }
    }
}