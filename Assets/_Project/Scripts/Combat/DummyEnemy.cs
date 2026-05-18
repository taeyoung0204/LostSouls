using System.Collections;
using UnityEngine;

namespace LostSouls.Combat
{
    /// <summary>
    /// 테스트용 더미. 체력이 있고, 일정 간격으로 주변 플레이어에게 데미지를 가한다.
    /// </summary>
    public class DummyEnemy : MonoBehaviour, IDamageable, ITargetable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Auto Attack (Test)")]
        [SerializeField] private bool autoAttackEnabled = false;
        [SerializeField] private float attackInterval = 2f;
        [SerializeField] private float attackRange = 2.5f;
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private LayerMask targetLayers;

        [Header("Lock-On")]
        [SerializeField] private Transform lockOnPoint;

        private float _currentHealth;
        private float _attackTimer;

        public Transform LockOnPoint => lockOnPoint != null ? lockOnPoint : transform;
        public bool IsTargetable => _currentHealth > 0f;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        private void Update()
        {
            if (!autoAttackEnabled) return;

            _attackTimer += Time.deltaTime;
            if (_attackTimer >= attackInterval)
            {
                _attackTimer = 0f;
                TryAttackNearbyPlayer();
            }
        }

        private void TryAttackNearbyPlayer()
        {
            // Player 레이어만 검사
            Collider[] nearby = Physics.OverlapSphere(transform.position, attackRange, targetLayers);

            foreach (Collider col in nearby)
            {
                IDamageable target = col.GetComponent<IDamageable>();
                if (target == null) continue;

                target.TakeDamage(attackDamage);
                Debug.Log($"[{name}] Auto-attacked {col.name} for {attackDamage} damage");
                break;
            }
        }

        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            Debug.Log($"[{name}] Took {damage} damage. HP: {_currentHealth}/{maxHealth}");

            StartCoroutine(FlashWhite());

            if (_currentHealth <= 0)
            {
                Debug.Log($"[{name}] Died!");
                Destroy(gameObject);
            }
        }

        private IEnumerator FlashWhite()
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend == null) yield break;

            Color originalColor = rend.material.color;
            rend.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            rend.material.color = originalColor;
        }

        private void OnDrawGizmosSelected()
        {
            // 공격 범위 시각화
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}