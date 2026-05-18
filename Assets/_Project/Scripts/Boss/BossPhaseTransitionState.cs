using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Phase Transition: 체력 50% 도달 시 1회 발동되는 페이즈 전환 연출.
    /// 포효 애니메이션 + AoE 데미지.
    ///
    /// 작동:
    /// - BossController가 체력 50% 통과 감지 시 강제 전이
    /// - 진행 중에는 다른 모든 상태로 전이 불가 (BossController에서 차단)
    /// - 1회만 발동 (BossController._hasRoared = true)
    /// - Animation Event로 종료 신호 받음
    ///
    /// 향후 (Step 6.5c-5b):
    /// - 무적 처리
    /// - 넉백 시스템
    /// - 페이즈 강화 (공격력 ↑, 이동 속도 ↑ 등)
    /// </summary>
    public class BossPhaseTransitionState : BossStateBase
    {
        private bool _animationEnded;
        private float _stateTimer;

        // 포효 클립은 보통 2~3초. 6초면 충분히 안전한 안전망.
        private const float MaxStateTime = 6f;

        // Animator 트리거 이름. 다른 패턴들처럼 인스펙터에서 받지 않고 고정.
        // 페이즈 전환은 1회성이라 SO 만들기엔 과함.
        private static readonly int RoarTriggerHash = Animator.StringToHash("Roar");

        public BossPhaseTransitionState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            _animationEnded = false;
            _stateTimer = 0f;

            // NavMeshAgent 정지 — 포효 중 이동 없음
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
            }

            // 포이즈 면역 ON — Roar 중엔 무력화 불가.
            // 안 그러면 Roar 중 누적 포이즈가 0이 되어 OnPoiseBroken 발생 → ChangeState가 차단 → break 상태만 영구화되는 버그.
            if (boss.Poise != null)
                boss.Poise.SetImmune(true);

            // 포효 트리거 발동
            boss.Animator.SetTrigger(RoarTriggerHash);

            Debug.Log($"[Boss] === PHASE 2 ENTRY === Roaring!");
        }

        public override void Update()
        {
            _stateTimer += Time.deltaTime;

            // 포효 중에는 회전 없음 (제자리에서 위풍당당)
            // Animation Event로 종료 대기
            if (_animationEnded)
            {
                Debug.Log($"[Boss] Phase 2 transition complete.");
                boss.ChangeState(boss.ChaseState);
                return;
            }

            // 안전망: Animation Event 누락 시 강제 복구
            if (_stateTimer > MaxStateTime)
            {
                Debug.LogError($"[Boss] PhaseTransitionState STUCK for {_stateTimer:F1}s. " +
                               $"Animation Event 'OnAttackAnimationEnd' on Roar clip may be missing. Forcing recovery.");
                boss.ChangeState(boss.ChaseState);
            }
        }

        public override void Exit()
        {
            // 트리거 잔류 방지
            boss.Animator.ResetTrigger(RoarTriggerHash);

            // 모든 히트박스 강제 끄기 (Animation Event 누락 대비)
            boss.DisableAllHitboxes();

            // 포이즈 면역 OFF + 게이지 풀 리셋.
            // 다크소울 정석: 페이즈 전환 = 보스의 새 시작. 게이지도 깨끗한 상태로.
            // 또한 면역 중 시도되었다가 거부된 데미지가 누적되어 있을 일은 없지만 (게이지를 안 깎으니까),
            // ResetPoise로 명시적 풀 리셋해서 다음 페이즈는 무조건 100에서 시작.
            if (boss.Poise != null)
            {
                boss.Poise.SetImmune(false);
                boss.Poise.ResetPoise();
            }
        }

        /// <summary>
        /// Animation Event 중계에서 호출.
        /// </summary>
        public void NotifyAnimationEnd()
        {
            _animationEnded = true;
        }
    }
}