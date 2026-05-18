using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// 일반 검 공격 패턴.
    /// - Animator 트리거 발동
    /// - Wind-up 동안 플레이어 추적
    /// - 그 이후 회전 묶임
    /// - Animation Event로 히트박스 제어
    ///
    /// 기존 7개 패턴 (Vertical/Horizontal/Low/FastDouble/Spin/SpinLow/DoubleSlashSlam)이 모두 이걸 사용.
    /// 이동 없는 단순 베기. 이동 동반은 RootMotionMeleePattern.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGenericMelee", menuName = "LostSouls/Boss/Pattern - Generic Melee")]
    public class GenericMeleePattern : AttackPattern
    {
        [Header("Animation")]
        [Tooltip("Animator의 Trigger 파라미터 이름. 정확히 일치해야 함.")]
        public string animatorTrigger = "Attack";

        [Header("Hitbox")]
        [Tooltip("사용할 히트박스의 BossController.hitboxes 배열 인덱스. 0=주무기(검), 1=발 등.")]
        public int hitboxIndex = 0;

        [Header("Wind-up Tracking")]
        [Tooltip("패턴 시작 후 이 시간 동안만 플레이어 추적. 이후 회전 묶임.")]
        [Range(0f, 1f)]
        public float windUpDuration = 0.3f;

        [Tooltip("Wind-up 동안의 회전 속도 배율 (0=정지, 1=평소).")]
        [Range(0f, 1f)]
        public float windUpRotationMultiplier = 0.3f;

        public override void Enter(BossController boss)
        {
            // 이동 멈춤 (NavMeshAgent)
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
            }

            // 활성 히트박스 지정 (검/발 등)
            boss.SetActiveHitboxIndex(hitboxIndex);

            // 트리거 발동
            int triggerHash = Animator.StringToHash(animatorTrigger);
            boss.Animator.SetTrigger(triggerHash);
        }

        public override void Update(BossController boss, float stateTimer)
        {
            // Wind-up 동안만 추적
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

            // Animation Event 누락 대비 — 모든 히트박스 강제 끄기 (안전망)
            boss.DisableAllHitboxes();
        }
    }
}