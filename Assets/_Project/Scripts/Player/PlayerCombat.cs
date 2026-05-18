using UnityEngine;
using LostSouls.Combat;

namespace LostSouls.Player
{
    /// <summary>
    /// 플레이어의 전투 관련 처리.
    /// Animation Event에서 호출되는 메서드들의 중계 역할 + 콤보 상태 관리.
    ///
    /// 콤보 시스템 (다크소울 Buffered Input 스타일):
    /// - Animator State 머신이 Attack1 → Attack2 → Attack3 직렬 연결
    /// - 각 공격 클립에 OnComboWindowOpen / OnComboWindowClose Animation Event 박혀있음
    /// - 윈도우 안에서 R1 입력 → Combo 트리거 발동 (PlayerController가 IsComboWindowOpen 체크)
    /// - 윈도우 밖에서 R1 입력 → 새 Attack 트리거 (Locomotion → Attack1)
    ///
    /// Motion Value (타별 데미지 차별화):
    /// - Attack1: 1.0배, Attack2: 1.1배, Attack3: 1.8배 (마무리)
    /// - EnableHitbox 호출 시점에 현재 Animator State 이름 보고 WeaponHitbox에 multiplier 설정
    /// - 클립별로 별도 Event 함수 만들지 않아도 자동 적용됨
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponHitbox mainWeapon;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Animator animator;

        [Header("Motion Values")]
        [Tooltip("Attack1 데미지 배율. 1.0이 기본.")]
        [SerializeField] private float attack1Multiplier = 1.0f;
        [Tooltip("Attack2 데미지 배율. 보통 Attack1과 비슷하거나 살짝 높음.")]
        [SerializeField] private float attack2Multiplier = 1.1f;
        [Tooltip("Attack3 데미지 배율. 콤보 마무리라 크게 — 보통 1.7~2.0배.")]
        [SerializeField] private float attack3Multiplier = 1.8f;
        [Tooltip("HeavyAttack (R2) 데미지 배율. R2는 R1 콤보 마무리(Attack3) 수준 또는 그 이상.")]
        [SerializeField] private float heavyAttackMultiplier = 1.7f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = false;

        // 콤보 윈도우 상태. Animation Event로 토글.
        private bool _comboWindowOpen;
        public bool IsComboWindowOpen => _comboWindowOpen;

        // Animator State hash (콤보 타 판별용)
        private static readonly int Attack1StateHash = Animator.StringToHash("Attack1");
        private static readonly int Attack2StateHash = Animator.StringToHash("Attack2");
        private static readonly int Attack3StateHash = Animator.StringToHash("Attack3");
        private static readonly int HeavyAttackStateHash = Animator.StringToHash("HeavyAttack");

        private void Awake()
        {
            if (mainWeapon == null)
                mainWeapon = GetComponentInChildren<WeaponHitbox>();

            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();

            if (animator == null)
                animator = GetComponent<Animator>();
        }

        // === Weapon Hitbox 중계 + Motion Value 적용 ===

        /// <summary>
        /// Animation Event: 공격 히트박스 활성화.
        /// 현재 Animator State에 따라 데미지 배율을 자동 설정.
        /// </summary>
        public void EnableHitbox()
        {
            if (mainWeapon == null) return;

            // 현재 Animator State에 따라 데미지 배율 결정
            float multiplier = GetCurrentAttackMultiplier();
            mainWeapon.SetDamageMultiplier(multiplier);

            mainWeapon.EnableHitbox();

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] EnableHitbox with multiplier {multiplier:F2}");
        }

        public void DisableHitbox()
        {
            if (mainWeapon != null)
                mainWeapon.DisableHitbox();
        }

        /// <summary>
        /// 현재 Animator State 이름으로 콤보 타 판별 + 배율 반환.
        /// 알 수 없는 State (단발 공격 등)면 1.0 반환.
        /// </summary>
        private float GetCurrentAttackMultiplier()
        {
            if (animator == null) return 1.0f;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            int hash = currentState.shortNameHash;

            if (hash == Attack1StateHash) return attack1Multiplier;
            if (hash == Attack2StateHash) return attack2Multiplier;
            if (hash == Attack3StateHash) return attack3Multiplier;
            if (hash == HeavyAttackStateHash) return heavyAttackMultiplier;

            return 1.0f;
        }

        // === Invulnerability (회피 i-frame) 중계 ===

        public void EnableInvulnerability()
        {
            if (playerHealth != null)
                playerHealth.EnableInvulnerability();
        }

        public void DisableInvulnerability()
        {
            if (playerHealth != null)
                playerHealth.DisableInvulnerability();
        }

        // === 콤보 윈도우 (Animation Event 중계) ===

        /// <summary>
        /// Animation Event: 콤보 윈도우 열림.
        /// 클립의 ~70% 지점에 박혀있음. 이때부터 R1 입력이 다음 타로 이어짐.
        /// </summary>
        public void OnComboWindowOpen()
        {
            _comboWindowOpen = true;

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] Combo window OPEN");
        }

        /// <summary>
        /// Animation Event: 콤보 윈도우 닫힘.
        /// 클립의 ~95% 지점. 이후 R1 입력은 새 콤보(Attack1)부터 시작.
        /// </summary>
        public void OnComboWindowClose()
        {
            _comboWindowOpen = false;

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] Combo window CLOSE");
        }

        /// <summary>
        /// 콤보 윈도우 안에서 R1 입력 받았을 때 PlayerController가 호출.
        /// 윈도우를 즉시 닫음 (같은 윈도우에서 두 번 발동 방지).
        /// </summary>
        public void ConsumeComboInput()
        {
            _comboWindowOpen = false;
        }
    }
}