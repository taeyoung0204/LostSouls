using UnityEngine;

namespace LostSouls.Player
{
    /// <summary>
    /// 플레이어의 스태미나를 관리한다.
    /// 다크소울 방식: 음수 허용. 0 초과면 행동 가능, 음수면 행동 차단.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenRate = 40f;          // 초당 회복량
        [SerializeField] private float regenDelay = 1f;          // 행동 후 회복 지연 (초)
        [SerializeField] private float sprintDrainRate = 10f;    // 달리기 초당 소모량

        [Header("Action Costs")]
        [SerializeField] private float attackCost = 25f;
        [SerializeField] private float heavyAttackCost = 40f;
        [SerializeField] private float rollCost = 25f;

        private float _currentStamina;
        private float _regenTimer;
        private bool _regenBlocked;

        // Public 접근자 (UI 등 외부에서 읽기)
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float Normalized => Mathf.Clamp01(_currentStamina / maxStamina);

        /// <summary>
        /// 행동을 시도할 수 있는지 (0보다 큰가).
        /// 다크소울 방식: 1이라도 남아있으면 시도 가능, 음수면 거부.
        /// </summary>
        public bool CanPerformAction => _currentStamina > 0f;

        private void Awake()
        {
            _currentStamina = maxStamina;
            _regenTimer = 0f;
            _regenBlocked = false;
        }

        private void Update()
        {
            HandleRegeneration();
        }

        /// <summary>
        /// 스태미나를 소모. 음수까지 허용 (다크소울 방식).
        /// 회복 타이머도 리셋.
        /// </summary>
        public void Consume(float amount)
        {
            _currentStamina -= amount;
            _regenTimer = 0f;  // 회복 지연 리셋
        }

        /// <summary>
        /// 회피 시도 - 입력 검증 + 소모.
        /// </summary>
        public bool TryConsumeRoll()
        {
            if (!CanPerformAction) return false;
            Consume(rollCost);
            return true;
        }

        // === 공격용 분리된 체크/소모 API ===
        // 콤보 입력 시점과 실제 모션 시작 시점이 떨어져있는 경우를 위해 분리.
        // 입력 받을 때는 CanAttack/CanHeavyAttack으로 가능 여부만 보고,
        // 실제 State 진입 시 ConsumeAttackCost/ConsumeHeavyAttackCost로 소모.
        // 회피는 입력 = 모션 시작이라 분리 불필요 (TryConsumeRoll 유지).

        public bool CanAttack() => CanPerformAction;
        public bool CanHeavyAttack() => CanPerformAction;

        public void ConsumeAttackCost() => Consume(attackCost);
        public void ConsumeHeavyAttackCost() => Consume(heavyAttackCost);

        /// <summary>
        /// 달리기 중 지속 소모. 매 프레임 호출됨.
        /// </summary>
        public void DrainForSprint()
        {
            _currentStamina -= sprintDrainRate * Time.deltaTime;
            _regenTimer = 0f;  // 달리는 동안에는 계속 회복 지연
        }

        public void SetRegenBlocked(bool blocked)
        {
            _regenBlocked = blocked;

            // 차단되는 순간 타이머도 리셋 (해제 시 다시 1초부터 카운트)
            if (blocked)
                _regenTimer = 0f;
        }

        private void HandleRegeneration()
        {
            // 외부에서 차단 중이면 회복 X, 타이머도 정지
            if (_regenBlocked) return;

            _regenTimer += Time.deltaTime;

            if (_regenTimer < regenDelay) return;
            if (_currentStamina >= maxStamina) return;

            _currentStamina += regenRate * Time.deltaTime;
            _currentStamina = Mathf.Min(_currentStamina, maxStamina);
        }
    }
}