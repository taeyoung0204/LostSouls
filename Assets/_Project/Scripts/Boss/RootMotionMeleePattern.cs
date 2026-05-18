using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Root Motion 기반 검 공격 패턴.
    /// 애니메이션 클립의 root motion이 보스를 이동시킴.
    ///
    /// 예: Slow Double Slash (휘두르며 전진), 대시 공격, 일부 점프 베기.
    ///
    /// 사용 조건:
    /// - Animator의 Apply Root Motion이 ON이어야 함
    /// - 클립의 Bake Into Pose가 OFF여야 함 (XZ는 최소한 OFF)
    /// - 그래야 root motion이 Transform에 전달됨
    ///
    /// GenericMeleePattern과의 차이:
    /// - Enter에서 NavMeshAgent.updatePosition = false (Agent vs Root Motion 충돌 방지)
    /// - Exit에서 Agent 위치를 Transform에 동기화 후 updatePosition 복원
    ///   (안 하면 다음 Chase 시 보스가 옛 Agent 위치로 순간이동)
    /// </summary>
    [CreateAssetMenu(fileName = "NewRootMotionMelee", menuName = "LostSouls/Boss/Pattern - Root Motion Melee")]
    public class RootMotionMeleePattern : AttackPattern
    {
        [Header("Animation")]
        [Tooltip("Animator의 Trigger 파라미터 이름.")]
        public string animatorTrigger = "Attack";

        [Header("Hitbox")]
        [Tooltip("사용할 히트박스의 BossController.hitboxes 배열 인덱스.")]
        public int hitboxIndex = 0;

        [Header("Wind-up Tracking")]
        [Tooltip("패턴 시작 후 이 시간 동안만 플레이어 추적.")]
        [Range(0f, 1f)]
        public float windUpDuration = 0.3f;

        [Tooltip("Wind-up 동안의 회전 속도 배율.")]
        [Range(0f, 1f)]
        public float windUpRotationMultiplier = 0.3f;

        public override void Enter(BossController boss)
        {
            // NavMeshAgent를 멈추고 위치 갱신도 꺼서 Root Motion이 위치를 통제하게 함
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
                boss.Agent.updatePosition = false;
            }

            // 활성 히트박스 지정
            boss.SetActiveHitboxIndex(hitboxIndex);

            // 트리거 발동
            int triggerHash = Animator.StringToHash(animatorTrigger);
            boss.Animator.SetTrigger(triggerHash);
        }

        public override void Update(BossController boss, float stateTimer)
        {
            // Wind-up 동안만 추적 (회전은 코드가, 위치는 root motion이)
            if (stateTimer < windUpDuration)
            {
                boss.RotateTowardsPlayer(windUpRotationMultiplier);
            }
        }

        public override void Exit(BossController boss)
        {
            // 트리거 잔류 방지
            int triggerHash = Animator.StringToHash(animatorTrigger);
            boss.Animator.ResetTrigger(triggerHash);

            // 모든 히트박스 강제 끄기 (안전망)
            boss.DisableAllHitboxes();

            // NavMeshAgent 위치 동기화 → updatePosition 복원.
            // 즉시 Warp 하지 않고 LateUpdate로 미룬다.
            // 이유: 이 Exit이 호출되는 Update 시점엔 이번 프레임의 root motion이 아직 transform에 반영 안 된 상태.
            // Animator는 LateUpdate에서 root motion을 transform에 적용한다.
            // 즉시 Warp하면 "root motion 적용 전 위치"로 Agent가 박혀, 다음 프레임에 Agent가 transform을 그 어긋난 위치로 끌어와
            // 시각적으로 보스가 살짝 뒤로 순간이동하는 현상이 발생.
            // BossController.LateUpdate에서 처리하면 root motion 최종 위치 그대로 동기화됨.
            if (boss.Agent.isOnNavMesh)
            {
                boss.ScheduleAgentWarpToTransform();
            }
        }
    }
}