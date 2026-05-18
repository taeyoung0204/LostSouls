namespace LostSouls.Combat
{
    /// <summary>
    /// 포이즈(경직치) 데미지를 받을 수 있는 객체가 구현하는 인터페이스.
    /// IDamageable과 분리한 이유:
    /// - 모든 적이 포이즈를 가질 필요는 없음 (한 방에 죽는 잡몹은 불필요)
    /// - 보스/엘리트만 선택적으로 구현
    /// - 무기는 IDamageable에 체력 데미지를, IPoiseDamageable에 포이즈 데미지를 각각 적용
    /// </summary>
    public interface IPoiseDamageable
    {
        /// <summary>
        /// 포이즈 데미지를 가한다.
        /// 누적치가 임계치를 넘으면 Poise Break (구현체가 알아서 처리).
        /// </summary>
        void TakePoiseDamage(float amount);
    }
}