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
    /// 회피 캔슬 시스템 (다크소울 정석):
    /// - 히트박스 비활성화(DisableHitbox) 시점부터 캔슬 윈도우 OPEN
    /// - Active(히트박스 ON) 구간에는 캔슬 불가 → 어택 트레이드 리스크 보존
    /// - 별도 Animation Event 추가 없이 기존 EnableHitbox/DisableHitbox 재활용
    /// - State 변경 시 자동 리셋 (PlayerController.UpdateCombatState에서 호출)
    ///
    /// 콤보 윈도우 vs 캔슬 윈도우 (둘은 독립적):
    /// - 콤보 윈도우는 타이밍 여유를 위해 넓게 — EnableHitbox 직후부터 열림 (Active 중에도 OPEN)
    /// - 캔슬 윈도우는 DisableHitbox 이후에만 열림 (Active 끝난 뒤)
    /// - 즉 "콤보로 다음 타 이어가기는 가능하지만 회피로 끊고 빠지는 건 불가"인 구간이 존재
    ///   → 다크소울 정석. 공격을 잇는 것과 끊는 것은 리스크가 달라야 함.
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

        [Header("Audio")]
        [Tooltip("무기에 붙인 AudioSource. 3D 사운드용 — 위치 자동.")]
        [SerializeField] private AudioSource weaponAudioSource;
        [Tooltip("사운드 클립 보관소. AudioManager의 ClipBank와 동일한 에셋 참조.")]
        [SerializeField] private LostSouls.Audio.AudioClipBank clipBank;

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

        // 회피 캔슬 윈도우 상태. DisableHitbox 시점에 자동 OPEN.
        // 콤보 윈도우 ⊆ 캔슬 윈도우 (콤보 가능하면 회피도 가능).
        // State 진입/변경 시 PlayerController가 ResetCancelWindow()로 리셋.
        private bool _cancelWindowOpen;
        public bool IsCancelWindowOpen => _cancelWindowOpen;

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
        /// 이 시점부터 Active 구간 — 캔슬 윈도우는 명시적으로 닫아둔다.
        /// (State 진입 시 PlayerController가 이미 닫아두지만 안전망)
        /// </summary>
        public void EnableHitbox()
        {
            if (mainWeapon == null) return;

            // Active 구간 시작 — 캔슬 불가
            _cancelWindowOpen = false;

            // 현재 Animator State에 따라 데미지 배율 결정
            float multiplier = GetCurrentAttackMultiplier();
            mainWeapon.SetDamageMultiplier(multiplier);

            mainWeapon.EnableHitbox();

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] EnableHitbox with multiplier {multiplier:F2}");
        }

        /// <summary>
        /// Animation Event: 공격 히트박스 비활성화.
        /// Recovery 구간 시작 — 이 시점부터 회피 캔슬 가능 (다크소울 정석).
        /// 콤보 윈도우는 보통 이 뒤(~70%)에 따로 열리지만, 캔슬은 더 일찍 열림.
        /// </summary>
        public void DisableHitbox()
        {
            if (mainWeapon != null)
                mainWeapon.DisableHitbox();

            // Recovery 구간 시작 — 회피 캔슬 OPEN
            _cancelWindowOpen = true;

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] DisableHitbox → Cancel window OPEN");
        }

        // === 사운드 (Animation Event에서 호출) ===
        // 4타 각각 다른 소리. 각 클립의 EnableHitbox 위치에 해당 메서드 박기.
        // SoundSet은 클립 배열 + 카테고리 볼륨 + 피치 랜덤을 함께 보유.

        /// <summary>Animation Event: R1 1타 (Attack1) 휘두름 소리.</summary>
        public void PlaySwingLight1()
        {
            PlaySoundSet(clipBank != null ? clipBank.weaponSwingLight1 : null);
        }

        /// <summary>Animation Event: R1 2타 (Attack2) 휘두름 소리.</summary>
        public void PlaySwingLight2()
        {
            PlaySoundSet(clipBank != null ? clipBank.weaponSwingLight2 : null);
        }

        /// <summary>Animation Event: R1 3타 (Attack3, 마무리) 휘두름 소리.</summary>
        public void PlaySwingLight3()
        {
            PlaySoundSet(clipBank != null ? clipBank.weaponSwingLight3 : null);
        }

        /// <summary>Animation Event: R2 강공격 (HeavyAttack) 휘두름 소리.</summary>
        public void PlaySwingHeavy()
        {
            PlaySoundSet(clipBank != null ? clipBank.weaponSwingHeavy : null);
        }

        /// <summary>
        /// 공통 재생 헬퍼. SoundSet에서 랜덤 클립 + 카테고리 볼륨/피치 적용해 재생.
        /// 동시 발음(Attack 직후 Heavy 등)이 잘리지 않도록 PlayOneShot 사용.
        /// 피치 변경은 AudioSource.pitch에 적용되므로 동시 재생 중에는 모든 사운드가 영향받음 —
        /// PlayOneShot은 피치를 따로 잡지 못해 마지막 SetPitch만 반영됨.
        /// 휘두름은 동시 다발 가능성 거의 없어서 무방. 동시 발음 중요한 사운드는 별도 AudioSource 두기.
        /// </summary>
        private void PlaySoundSet(LostSouls.Audio.SoundSet set)
        {
            if (set == null || weaponAudioSource == null) return;
            AudioClip clip = set.PickRandomClip();
            if (clip == null) return;

            weaponAudioSource.pitch = set.GetRandomPitch();
            weaponAudioSource.PlayOneShot(clip, set.SafeVolume);
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

        /// <summary>
        /// State 전환 시 PlayerController가 호출.
        /// 새 공격 State 진입 또는 비공격 State로 빠지면 캔슬 윈도우는 항상 닫힘.
        /// (다음 EnableHitbox/DisableHitbox 사이클이 다시 열어준다)
        /// 콤보 윈도우도 함께 리셋 — 클립 자체의 Animation Event가 다시 열어주므로 안전.
        /// </summary>
        public void ResetWindows()
        {
            _cancelWindowOpen = false;
            _comboWindowOpen = false;

            if (drawDebugInfo)
                Debug.Log($"[PlayerCombat] Windows RESET (state change)");
        }
    }
}