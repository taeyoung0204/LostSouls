using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// 보스 공격 패턴의 추상 베이스.
    ///
    /// 공통 데이터: 이름 / 사거리 / 가중치
    /// 패턴별 행동: Enter / Update / Exit (추상)
    ///
    /// 구현체:
    /// - GenericMeleePattern: 일반 검 공격 (트리거 + Animation Event)
    /// - RootMotionMeleePattern: 이동 동반 검 공격 (예정)
    /// - KickPattern: 발 히트박스 공격 (예정)
    /// - DashAttackPattern: 거리 좁히는 공격 (예정)
    /// - RoarPattern: AoE + 넉백 (예정)
    ///
    /// 이 클래스는 abstract이라 직접 에셋 생성 불가.
    /// 구체 구현체에서 [CreateAssetMenu]로 메뉴 노출.
    /// </summary>
    public abstract class AttackPattern : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("디버그/구분용 이름.")]
        public string patternName = "Attack";

        [Header("Range")]
        [Tooltip("이 거리 이상이어야 발동 가능.")]
        public float minRange = 0f;
        [Tooltip("이 거리 이하여야 발동 가능.")]
        public float maxRange = 3f;

        [Header("Weighting")]
        [Tooltip("기본 가중치. 높을수록 자주 선택됨.")]
        public float baseWeight = 1f;
        [Tooltip("이 거리에서 가장 선호됨. 멀어질수록 가중치 감소.")]
        public float idealRange = 2f;
        [Tooltip("idealRange로부터 거리 차이에 대한 가중치 감쇠율. 0이면 거리 무관.")]
        public float distanceFalloff = 1f;

        [Header("Cooldown")]
        [Tooltip("이 패턴 사용 후 다시 발동 가능해질 때까지 시간 (초). 0이면 쿨다운 없음.")]
        public float cooldownDuration = 0f;

        /// <summary>
        /// 현재 거리에서 이 패턴이 발동 가능한지.
        /// 구체 클래스에서 추가 조건(체력 트리거 등) override 가능.
        /// </summary>
        public virtual bool IsAvailable(BossController boss)
        {
            float distance = boss.DistanceToPlayer;
            return distance >= minRange && distance <= maxRange;
        }

        /// <summary>
        /// 현재 거리에서의 가중치.
        /// </summary>
        public float GetWeight(float distance)
        {
            if (distance < minRange || distance > maxRange) return 0f;

            float distFromIdeal = Mathf.Abs(distance - idealRange);
            float weight = baseWeight - (distFromIdeal * distanceFalloff);
            return Mathf.Max(0.01f, weight);
        }

        // === 패턴별 행동 (추상) ===

        /// <summary>
        /// 패턴 시작 시 1회 호출. 트리거 발동, 이동 제어 등.
        /// </summary>
        public abstract void Enter(BossController boss);

        /// <summary>
        /// 매 프레임 호출. wind-up 회전, 위치 보정 등.
        /// stateTimer: Enter 후 경과 시간 (초).
        /// </summary>
        public abstract void Update(BossController boss, float stateTimer);

        /// <summary>
        /// 패턴 끝날 때 1회 호출. 정리 작업.
        /// </summary>
        public abstract void Exit(BossController boss);
    }
}