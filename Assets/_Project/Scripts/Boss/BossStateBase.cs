namespace LostSouls.Boss
{
    /// <summary>
    /// 보스 FSM의 모든 State가 상속받는 추상 베이스.
    /// Enter / Update / Exit 3단 구조.
    ///
    /// State는 BossController에 대한 참조를 통해
    /// 보스의 컴포넌트들(Animator, NavMeshAgent, Stats 등)에 접근한다.
    /// </summary>
    public abstract class BossStateBase
    {
        protected readonly BossController boss;

        protected BossStateBase(BossController boss)
        {
            this.boss = boss;
        }

        /// <summary>
        /// State 진입 시 1회 호출.
        /// 애니메이션 트리거 발동, 타이머 초기화 등.
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// 매 프레임 호출. 상태 전이 판정도 여기서.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// State 이탈 시 1회 호출.
        /// 정리 작업 (히트박스 끄기, 변수 리셋 등).
        /// </summary>
        public virtual void Exit() { }
    }
}