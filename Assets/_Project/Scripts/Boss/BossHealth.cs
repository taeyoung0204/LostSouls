using System;
using UnityEngine;
using LostSouls.Combat;

namespace LostSouls.Boss
{
    /// <summary>
    /// 보스의 체력 관리.
    /// - IDamageable: 플레이어 무기에 맞을 수 있음
    /// - ITargetable: 락온 가능
    /// - OnDeath: HP가 0에 도달한 순간 1회 발생. BossController가 구독해서 사망 처리 수행.
    /// </summary>
    public class BossHealth : MonoBehaviour, IDamageable, ITargetable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 500f;

        [Header("Targeting")]
        [Tooltip("락온 시 카메라가 바라볼 지점. 비우면 transform 사용.")]
        [SerializeField] private Transform lockOnPoint;

        [Header("Debug")]
        [Tooltip("매 피격 시 체력 로그 출력. 필요할 때만 켜라. 사망 로그는 항상 출력됨.")]
        [SerializeField] private bool drawDebugInfo = false;

        private float _currentHealth;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => _isDead;

        /// <summary>
        /// HP가 0에 도달하여 사망 처리가 시작되는 순간 1회 발생.
        /// BossController가 구독하여 AI 정지, Collider 비활성, Death 애니메이션 트리거 등 수행.
        /// 향후 보상 시스템, 사망 UI 등도 같은 이벤트 구독.
        /// </summary>
        public event Action OnDeath;

        // ITargetable 구현
        public Transform LockOnPoint => lockOnPoint != null ? lockOnPoint : transform;
        public bool IsTargetable => !_isDead;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            _currentHealth -= damage;
            _currentHealth = Mathf.Max(0f, _currentHealth);

            if (drawDebugInfo)
                Debug.Log($"[Boss] Took {damage} damage. HP: {_currentHealth:F0}/{maxHealth}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;

            // 1회성 중요 이벤트라 항상 출력
            Debug.Log($"[Boss] Died!");

            // 시체는 남긴다. 실제 처리 (AI 정지, Collider 끄기, Death 애니메이션)는
            // BossController가 이 이벤트를 받아서 수행.
            OnDeath?.Invoke();
        }
    }
}