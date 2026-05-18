using System;
using UnityEngine;
using LostSouls.Combat;

namespace LostSouls.Boss
{
    /// <summary>
    /// 보스의 포이즈(경직치)를 관리한다.
    ///
    /// 다크소울 정석 동작:
    /// - 평소엔 가득 차있음
    /// - 맞을 때마다 깎임 (공격 중에도 깎임 — 슈퍼 아머와 무관)
    /// - 마지막 피격 후 일정 시간 뒤부터 자동 회복
    /// - 0 이하 도달 시 Poise Break 이벤트 발생 → 컨트롤러가 받아서 State 전환
    /// - Poise Break 후엔 즉시 풀로 리셋되되, 회복 지연이 평소보다 길게
    ///   (브레이크 직후엔 회복 잠시 막힘 — 다음 한 방까지 시간 여유)
    ///
    /// 책임 분리:
    /// - 포이즈 게이지 값만 관리. State 전환은 컨트롤러의 일.
    /// - 슈퍼 아머 판정(현재 공격 중인가)도 여기 책임 아님.
    ///   상위에서 "포이즈 데미지를 적용할지 말지" 결정하면 됨.
    ///   여기는 그냥 받는 대로 깎는다.
    ///
    /// 단, 포이즈 브레이크 진행 중에는 추가 포이즈 데미지를 무시한다
    /// (사용자 결정: 체력 데미지는 받되 포이즈는 안 깎임 → 딜타임 일관성).
    /// </summary>
    public class BossPoise : MonoBehaviour, IPoiseDamageable
    {
        [Header("Stats")]
        [Tooltip("최대 포이즈. 이 값 이상으로는 회복되지 않음.")]
        [SerializeField] private float maxPoise = 100f;

        [Header("Regeneration")]
        [Tooltip("마지막 피격 후 회복 시작까지 지연 (초).")]
        [SerializeField] private float regenDelay = 2f;
        [Tooltip("초당 회복량.")]
        [SerializeField] private float regenRate = 30f;

        [Header("Post-Break Recovery")]
        [Tooltip("Poise Break 직후 회복 지연 (초). 평소 regenDelay보다 길게.")]
        [SerializeField] private float postBreakRegenDelay = 4f;

        [Header("Debug")]
        [Tooltip("매 포이즈 데미지/무시 로그. 필요할 때만 켜라. Break 발생/종료는 항상 출력됨.")]
        [SerializeField] private bool drawDebugInfo = false;

        private float _currentPoise;
        private float _regenTimer;
        private bool _isBroken;          // Poise Break 처리 진행 중인지 (외부에서 SetBroken로 토글)
        private bool _isImmune;          // 외부에서 면역 토글 (Roar 등 무적 연출 중)
        private float _currentRegenDelay;

        // === Public 접근자 ===
        public float CurrentPoise => _currentPoise;
        public float MaxPoise => maxPoise;
        public float Normalized => Mathf.Clamp01(_currentPoise / maxPoise);
        public bool IsBroken => _isBroken;
        public bool IsImmune => _isImmune;

        /// <summary>
        /// Poise가 0 이하로 떨어진 순간 1회 발생.
        /// BossController가 구독해서 HitReactState로 전이.
        /// </summary>
        public event Action OnPoiseBroken;

        private void Awake()
        {
            _currentPoise = maxPoise;
            _regenTimer = 0f;
            _isBroken = false;
            _currentRegenDelay = regenDelay;
        }

        private void Update()
        {
            HandleRegeneration();
        }

        /// <summary>
        /// 포이즈 데미지를 가한다. 0 이하 도달 시 1회만 Break 이벤트 발생.
        /// 이미 Break 진행 중이면 무시 (체력 데미지는 다른 경로로 적용되니 영향 없음).
        /// </summary>
        public void TakePoiseDamage(float amount)
        {
            if (amount <= 0f) return;

            // 외부 면역 (예: Roar 페이즈 전환 중) — 데미지 자체를 무시.
            // 게이지 깎임/회복 타이머 둘 다 안 건드림. Roar 끝나면 외부에서 ResetPoise()로 풀 리셋.
            if (_isImmune)
            {
                if (drawDebugInfo)
                    Debug.Log($"[BossPoise] Poise damage ignored (immune). amount={amount}");
                return;
            }

            // 사용자 결정: Break 중에는 추가 포이즈 데미지 무시
            // (체력은 받되 포이즈는 안 깎임 → 딜타임 일관성)
            if (_isBroken)
            {
                if (drawDebugInfo)
                    Debug.Log($"[BossPoise] Poise damage ignored (already broken). amount={amount}");
                return;
            }

            _currentPoise -= amount;
            _regenTimer = 0f;  // 피격 시 회복 지연 리셋

            if (drawDebugInfo)
                Debug.Log($"[BossPoise] Took {amount} poise damage. Current: {_currentPoise:F1}/{maxPoise}");

            if (_currentPoise <= 0f)
            {
                TriggerBreak();
            }
        }

        /// <summary>
        /// 외부에서 포이즈를 면역/면역해제 상태로 토글.
        /// 예: BossPhaseTransitionState가 Enter에서 true, Exit에서 false로 호출.
        /// 면역 중에는 TakePoiseDamage가 통째로 무시됨 (게이지 깎임/회복 타이머 영향 없음).
        /// </summary>
        public void SetImmune(bool immune)
        {
            _isImmune = immune;
        }

        /// <summary>
        /// 포이즈 게이지를 풀로 리셋하고 break 플래그도 해제.
        /// 페이즈 전환 후 새 페이즈에서 깨끗한 상태로 시작하고 싶을 때 외부에서 호출.
        /// NotifyBreakEnded와 본질은 같지만 의미상 분리 (Break 종료 vs 명시적 리셋).
        /// </summary>
        public void ResetPoise()
        {
            _isBroken = false;
            _currentPoise = maxPoise;
            _regenTimer = 0f;
            _currentRegenDelay = regenDelay;

            if (drawDebugInfo)
                Debug.Log($"[BossPoise] Poise reset to {maxPoise} (external)");
        }

        private void TriggerBreak()
        {
            _isBroken = true;
            _currentPoise = 0f;
            // 회복 지연을 break 직후 전용으로 교체. 컨트롤러가 HitReact 끝낼 때 NotifyBreakEnded 호출하면 풀로 리셋.
            _currentRegenDelay = postBreakRegenDelay;

            // 1회성 중요 이벤트라 항상 출력
            Debug.Log($"[BossPoise] === POISE BROKEN ===");

            OnPoiseBroken?.Invoke();
        }

        /// <summary>
        /// HitReact State가 끝났을 때 컨트롤러가 호출.
        /// 게이지를 풀로 리셋하고 break 플래그 해제.
        /// 회복 지연은 그대로 postBreakRegenDelay 유지 (다음 break까지 시간 벌어줌).
        /// </summary>
        public void NotifyBreakEnded()
        {
            _isBroken = false;
            _currentPoise = maxPoise;
            _regenTimer = 0f;

            // 1회성 중요 이벤트라 항상 출력
            Debug.Log($"[BossPoise] Break ended. Poise reset to {maxPoise}");
        }

        private void HandleRegeneration()
        {
            if (_isBroken) return;
            if (_currentPoise >= maxPoise)
            {
                // 가득 찼으면 회복 지연을 평소로 복귀시켜 둠 (다음 피격 후 적용)
                _currentRegenDelay = regenDelay;
                return;
            }

            _regenTimer += Time.deltaTime;

            if (_regenTimer < _currentRegenDelay) return;

            _currentPoise += regenRate * Time.deltaTime;
            _currentPoise = Mathf.Min(_currentPoise, maxPoise);
        }
    }
}