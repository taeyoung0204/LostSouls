namespace LostSouls.Combat
{
    /// <summary>
    /// 데미지를 받을 수 있는 모든 객체가 구현해야 하는 인터페이스.
    /// 플레이어, 적, 파괴 가능한 오브젝트 등.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}