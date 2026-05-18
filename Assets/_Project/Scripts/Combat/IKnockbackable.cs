using UnityEngine;

namespace LostSouls.Combat
{
    /// <summary>
    /// 넉백을 받을 수 있는 객체가 구현하는 인터페이스.
    /// 플레이어, 일부 적 등.
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// 넉백 적용.
        /// </summary>
        /// <param name="direction">넉백 방향 (정규화된 수평 벡터, Y=0 권장).</param>
        /// <param name="force">초기 속도 (m/s).</param>
        /// <param name="duration">지속 시간 (초). 이 시간 동안 감속하며 이동.</param>
        void ApplyKnockback(Vector3 direction, float force, float duration);
    }
}