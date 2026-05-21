using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Hit React: Poise Break 시 진입하는 큰 비틀거림 State.
    /// 보스가 무력화되어 플레이어의 딜타임 윈도우가 열림.
    ///
    /// 작동:
    /// - BossController가 BossPoise.OnPoiseBroken 이벤트 받아서 강제 전이
    /// - 진행 중에는 다른 State로 전이 불가 (BossController.ChangeState가 차단)
    /// - 진행 중에도 체력 데미지는 받음 (BossHealth는 무관)
    /// - 진행 중에는 포이즈 데미지 무시 (BossPoise._isBroken=true)
    /// - Animation Event(OnAttackAnimationEnd)로 종료 신호 받음
    /// - Exit에서 BossPoise.NotifyBreakEnded 호출 → 포이즈 게이지 리셋
    ///
    /// PhaseTransitionState와 매우 비슷한 구조. 차이점:
    /// - 페이즈 전환은 체력 50%에서 1회만, 이건 포이즈 누적 시마다 반복 가능
    /// - 그 외 라이프사이클은 동일
    /// </summary>
    public class BossHitReactState : BossStateBase
    {
        private bool _animationEnded;
        private float _stateTimer;

        // AttackState와 동일한 안전망. HitReact 클립은 보통 1.5초 정도라 5초면 충분히 안전.
        private const float MaxStateTime = 5f;

        // Animator 트리거. AttackPattern들과 달리 SO 안 만들고 고정 (1종류라서).
        private static readonly int BigHitTriggerHash = Animator.StringToHash("BigHit");

        public BossHitReactState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            _animationEnded = false;
            _stateTimer = 0f;

            // NavMeshAgent 정지 — 비틀거리는 동안 이동 없음
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
            }

            // BigHit 트리거 발동 (Any State → HitReact 전환)
            boss.Animator.SetTrigger(BigHitTriggerHash);

            // Poise Break 순간 사운드 2종 동시 재생:
            // - 임팩트 (SFX 채널): "Poise가 깨졌다!"는 게임플레이 신호. 묵직한 슬램.
            // - 신음 (Voice 채널): 보스가 비틀거리는 캐릭터 표현.
            // AudioSource 분리되어 있어 둘 다 끊김 없이 동시에 들림.
            if (boss.Audio != null)
            {
                boss.Audio.PlayPoiseBreak();
                boss.Audio.PlayGroan();
            }

            // 카메라 셰이크 — Poise Break는 가장 임팩트 강한 순간이라 풀강도.
            // 강도/감쇠는 BossController의 ImpulseSource 인스펙터에서 조절.
            boss.ShakeCamera(1f);

            Debug.Log($"[Boss] === POISE BROKEN — Hit Reacting ===");
        }

        public override void Update()
        {
            _stateTimer += Time.deltaTime;

            // 비틀거리는 동안 회전/추적 없음. 완전히 무방비.
            // Animation Event로 종료 신호 대기.
            if (_animationEnded)
            {
                boss.ChangeState(boss.ChaseState);
                return;
            }

            // 안전망: Animation Event 누락 등으로 stuck 시 강제 복구
            if (_stateTimer > MaxStateTime)
            {
                Debug.LogError($"[Boss] HitReactState STUCK for {_stateTimer:F1}s. " +
                               $"Animation Event 'OnAttackAnimationEnd' on HitReact clip may be missing. Forcing recovery.");
                boss.ChangeState(boss.ChaseState);
            }
        }

        public override void Exit()
        {
            // 트리거 잔류 방지
            boss.Animator.ResetTrigger(BigHitTriggerHash);

            // Animation Event 누락 대비 — 모든 히트박스 강제 끄기
            boss.DisableAllHitboxes();

            // 포이즈 게이지 리셋 + break 플래그 해제
            // 이거 안 부르면 BossPoise가 영영 _isBroken=true인 채로 멈춤
            if (boss.Poise != null)
                boss.Poise.NotifyBreakEnded();
        }

        /// <summary>
        /// Animation Event 중계에서 호출. (OnAttackAnimationEnd가 현재 State에 따라 분기)
        /// </summary>
        public void NotifyAnimationEnd()
        {
            _animationEnded = true;
        }
    }
}