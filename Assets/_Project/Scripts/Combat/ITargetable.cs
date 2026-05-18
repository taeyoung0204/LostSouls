using UnityEngine;

namespace LostSouls.Combat
{
    /// <summary>
    /// 락온 가능한 모든 대상이 구현해야 하는 인터페이스.
    /// 적, 보스, 파괴 가능한 오브젝트 등.
    /// </summary>
    public interface ITargetable
    {
        /// <summary>
        /// 락온 시 카메라/캐릭터가 바라볼 위치.
        /// 보통 캐릭터 중심 (가슴~머리 사이) 정도가 자연스러움.
        /// </summary>
        Transform LockOnPoint { get; }

        /// <summary>
        /// 현재 락온 가능한 상태인지.
        /// 죽었거나 비활성 상태면 false.
        /// </summary>
        bool IsTargetable { get; }

        /// <summary>
        /// 위치 정보 (거리 계산 등에 사용).
        /// 보통 transform.position과 같지만 별도로 둘 수 있음.
        /// </summary>
        Vector3 Position { get; }
    }
}