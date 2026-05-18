using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Idle: 제자리에 대기.
    /// 플레이어가 aggroRange 안에 들어오면 ChaseState로 전이.
    /// </summary>
    public class BossIdleState : BossStateBase
    {
        public BossIdleState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            // NavMeshAgent 정지
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
            }
        }

        public override void Update()
        {
            if (boss.Player == null) return;

            // 플레이어 사망 시 영구 Idle 유지 — 추격 시작 안 함
            if (boss.IsPlayerDead) return;

            // 어그로 거리 안이면 Chase로
            if (boss.DistanceToPlayer <= boss.AggroRange)
            {
                boss.ChangeState(boss.ChaseState);
            }
        }
    }
}