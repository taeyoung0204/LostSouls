using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Chase: 플레이어를 추적한다.
    /// - NavMeshAgent로 길찾기 (SetDestination)
    /// - 매 프레임 부드러운 회전
    /// - 어그로 범위 밖으로 나가면 Idle로 복귀
    ///
    /// 다음 단계에서 "사거리 안이면 AttackState로 전이" 분기가 추가될 것.
    /// </summary>
    public class BossChaseState : BossStateBase
    {
        // 다음 SetDestination 호출 시각
        private float _nextDestinationUpdate;

        public BossChaseState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = false;
            }

            // 즉시 첫 갱신
            _nextDestinationUpdate = 0f;
        }

        public override void Update()
        {
            if (boss.Player == null) return;

            // 플레이어 사망 시 Idle로 복귀. 다시 빠져나갈 일 없음 (IdleState도 가드).
            if (boss.IsPlayerDead)
            {
                boss.ChangeState(boss.IdleState);
                return;
            }

            float distance = boss.DistanceToPlayer;

            // 어그로 밖으로 나갔으면 Idle 복귀
            if (distance > boss.AggroRange)
            {
                boss.ChangeState(boss.IdleState);
                return;
            }

            // 발동 가능한 패턴이 있는지에 따라 동적으로 stopping distance 조정.
            // - 후보 있음: attackRange에서 멈춤, AttackState로 전이
            // - 후보 없음 (전부 쿨다운 중): minChaseDistance까지 더 다가감 (공격 시도 안 함)
            bool hasPattern = boss.HasAvailablePattern();
            float targetStop = hasPattern ? boss.AttackRange : boss.MinChaseDistance;

            if (boss.Agent.isOnNavMesh)
                boss.Agent.stoppingDistance = targetStop;

            // 공격 전이 조건: 사거리 안 + 글로벌 쿨다운 끝 + 발동 가능 패턴 있음
            if (distance <= boss.AttackRange && boss.CanAttack && hasPattern)
            {
                boss.ChangeState(boss.AttackState);
                return;
            }

            // 목적지 갱신 (주기적으로만)
            if (Time.time >= _nextDestinationUpdate && boss.Agent.isOnNavMesh)
            {
                boss.Agent.SetDestination(boss.Player.position);
                _nextDestinationUpdate = Time.time + boss.DestinationUpdateInterval;
            }

            // 회전은 매 프레임 부드럽게
            boss.RotateTowardsPlayer();
        }

        public override void Exit()
        {
            // 다음 state로 넘어갈 때 NavMeshAgent 정지
            if (boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = true;
                boss.Agent.ResetPath();
            }
        }
    }
}