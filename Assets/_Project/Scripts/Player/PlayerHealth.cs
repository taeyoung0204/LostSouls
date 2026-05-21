using System;
using UnityEngine;
using LostSouls.Combat;

namespace LostSouls.Player
{
    /// <summary>
    /// 플레이어의 체력과 무적 프레임을 관리한다.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;

        [Header("References")]
        [Tooltip("Flinch 발동 위해 같은 GameObject의 PlayerController 참조. 비우면 Awake에서 자동.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("피격/사망음 재생용. 비우면 Awake에서 자동 탐색.")]
        [SerializeField] private PlayerAudio playerAudio;

        [Header("Debug")]
        [Tooltip("매 피격/i-frame 토글 로그 + 캡슐 기즈모. 필요할 때만 켜라. 사망 로그는 항상 출력됨.")]
        [SerializeField] private bool drawDebugInfo = false;

        private float _currentHealth;
        private bool _isInvulnerable;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsInvulnerable => _isInvulnerable;
        public bool IsDead => _isDead;

        /// <summary>
        /// 플레이어 사망 순간 1회 발행. GameOverUI 등이 구독.
        /// </summary>
        public event Action OnDeath;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _isInvulnerable = false;

            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            if (playerAudio == null)
                playerAudio = GetComponent<PlayerAudio>();
        }

        public void TakeDamage(float damage)
        {
            // 이미 죽었으면 더 이상 데미지 받지 않음
            if (_isDead) return;

            // 무적 상태면 데미지 무시
            if (_isInvulnerable)
            {
                if (drawDebugInfo)
                    Debug.Log($"[Player] Damage avoided! (i-frame active)");
                return;
            }

            // 난이도 배율 적용. 현재 게임에서 플레이어가 받는 데미지 출처는 보스뿐이라
            // bossDamageMultiplier × playerDamageTakenMultiplier 둘 다 여기서 곱셈 합성.
            // 향후 환경 데미지(가시, 낙하 등) 추가 시 WeaponHitbox에 출처 표시 추가하고 분리 필요.
            var settings = LostSouls.Settings.GameSettings.Instance;
            if (settings != null && settings.CurrentDifficulty != null)
            {
                var diff = settings.CurrentDifficulty;
                damage *= diff.bossDamageMultiplier * diff.playerDamageTakenMultiplier;
            }

            _currentHealth -= damage;

            if (drawDebugInfo)
                Debug.Log($"[Player] Took {damage} damage. HP: {_currentHealth}/{maxHealth}");

            if (_currentHealth <= 0)
            {
                Die();
                return;
            }

            // 피격음 (사망 시는 사망음만, 피격음 안 나도록 — 위 Die() 분기에서 빠짐)
            if (playerAudio != null) playerAudio.PlayHurt();

            // 피격 시 Flinch 발동. PlayerController 내부에서 Knockback/Flinch 중복/무적 등 무시 조건 처리.
            // 이미 Knockback이 발동되는 공격(knockbackForce>0인 WeaponHitbox)이면
            // PlayerController.TriggerFlinch가 _isBeingKnockedBack 체크로 무시함 → 자연스러운 분기.
            if (playerController != null)
                playerController.TriggerFlinch();
        }

        /// <summary>
        /// 무적 프레임 시작. Animation Event 등에서 호출.
        /// </summary>
        public void EnableInvulnerability()
        {
            _isInvulnerable = true;

            if (drawDebugInfo)
                Debug.Log($"[Player] i-frame ENABLED");
        }

        /// <summary>
        /// 무적 프레임 종료. Animation Event 등에서 호출.
        /// </summary>
        public void DisableInvulnerability()
        {
            _isInvulnerable = false;

            if (drawDebugInfo)
                Debug.Log($"[Player] i-frame DISABLED");
        }

        /// <summary>
        /// HP 회복. 포션 시스템에서 매 프레임 호출 (점진 회복).
        /// 사망 상태에서는 무시. maxHealth 초과 방지.
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;
            if (amount <= 0f) return;

            _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
        }

        private void Die()
        {
            _isDead = true;
            _currentHealth = 0f;

            Debug.Log($"[Player] Died!");

            // 사망음
            if (playerAudio != null) playerAudio.PlayDeath();

            // 전투 BGM 페이드아웃 (플레이어 사망 = 전투 종료)
            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.StopBGM();

            // PlayerController에 사망 통지 → Animator 트리거 + 입력 차단
            if (playerController != null)
                playerController.HandleDeath();

            // 외부 구독자(GameOverUI 등)에게 알림
            OnDeath?.Invoke();
        }
        private void OnDrawGizmos()
        {
            if (!drawDebugInfo) return;

            // 캐릭터 위치 기준 와이어 캡슐 그리기
            // CharacterController가 있으면 그 크기 사용, 없으면 기본값
            CharacterController cc = GetComponent<CharacterController>();

            Vector3 center;
            float radius;
            float height;

            if (cc != null)
            {
                center = transform.position + cc.center;
                radius = cc.radius;
                height = cc.height;
            }
            else
            {
                // 기본값 (CharacterController 없을 때)
                center = transform.position + Vector3.up * 0.9f;
                radius = 0.3f;
                height = 1.8f;
            }

            // 무적 상태에 따라 색상 변경
            Gizmos.color = _isInvulnerable ? Color.cyan : new Color(0f, 0f, 1f, 0.3f);

            DrawWireCapsule(center, radius, height);
        }

        /// <summary>
        /// 와이어 프레임 캡슐을 그린다. Gizmos에 기본 캡슐이 없어서 직접 구현.
        /// </summary>
        private void DrawWireCapsule(Vector3 center, float radius, float height)
        {
            float halfCylinderHeight = (height - radius * 2f) * 0.5f;
            if (halfCylinderHeight < 0f) halfCylinderHeight = 0f;

            Vector3 top = center + Vector3.up * halfCylinderHeight;
            Vector3 bottom = center - Vector3.up * halfCylinderHeight;

            // 위/아래 반구
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);

            // 옆면 4개 직선 (캡슐의 원통 부분)
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
        }
    }
}