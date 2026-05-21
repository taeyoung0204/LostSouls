using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Combat
{
    [RequireComponent(typeof(Collider))]
    public class WeaponHitbox : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private float damage = 20f;
        [Tooltip("0이면 포이즈 깎지 않음. 양수면 IPoiseDamageable 대상에게 포이즈 데미지 적용.")]
        [SerializeField] private float poiseDamage = 0f;

        [Header("Knockback")]
        [Tooltip("0이면 넉백 없음. 양수면 IKnockbackable 대상에게 넉백 적용.")]
        [SerializeField] private float knockbackForce = 0f;
        [Tooltip("넉백 지속 시간 (초). 이 시간 동안 감속하며 이동.")]
        [SerializeField] private float knockbackDuration = 0.3f;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask hittableLayers = ~0;  // 모든 레이어 (나중에 제한 가능)

        [Header("VFX")]
        [Tooltip("히트박스 활성 순간 재생할 파티클. 충격파, 먼지 등.")]
        [SerializeField] private ParticleSystem effectOnEnable;

        [Header("Audio")]
        [Tooltip("타격 성공 순간 재생할 사운드 셋. 비워두면 무음.")]
        [SerializeField] private LostSouls.Audio.SoundSet hitSound;
        [Tooltip("타격음 재생용 AudioSource. 비워두면 같은 GameObject의 AudioSource 자동 탐색.")]
        [SerializeField] private AudioSource hitAudioSource;

        [Header("Debug")]
        [Tooltip("히트/넉백 발생 시 로그 + 기즈모 표시. 필요할 때만 켜라.")]
        [SerializeField] private bool drawDebugInfo = false;

        private Collider _hitboxCollider;
        private bool _isActive;
        private HashSet<IDamageable> _alreadyHitTargets = new HashSet<IDamageable>();

        // 런타임 데미지 배율. 1.0이 기본.
        // 콤보 시스템: PlayerCombat이 EnableHitbox 직전에 클립별 multiplier 설정 (Motion Value).
        // 보스 공격: 보통 1.0 그대로.
        private float _damageMultiplier = 1.0f;

        /// <summary>
        /// 데미지 배율을 외부에서 설정. EnableHitbox 호출 직전에 부르는 게 보통.
        /// 다음 EnableHitbox부터 적용됨.
        /// </summary>
        public void SetDamageMultiplier(float multiplier)
        {
            _damageMultiplier = multiplier;
        }

        private void Awake()
        {
            _hitboxCollider = GetComponent<Collider>();
            _hitboxCollider.enabled = false;
            _isActive = false;

            // 타격음용 AudioSource 자동 탐색 (인스펙터에서 안 넣어둔 경우)
            if (hitAudioSource == null)
                hitAudioSource = GetComponent<AudioSource>();
        }

        public void EnableHitbox()
        {
            _hitboxCollider.enabled = true;
            _isActive = true;
            _alreadyHitTargets.Clear();

            // 활성화 시점에 이미 겹쳐있는 콜라이더 검사
            CheckOverlappingColliders();
        }

        /// <summary>
        /// 파티클만 재생. 콜라이더는 건드리지 않음.
        /// 시각 효과를 데미지 판정보다 먼저 시작하고 싶을 때 사용.
        /// (예: 충격파 파티클 가시화에 시간 걸리는 경우 미리 호출)
        /// </summary>
        public void PlayEffect()
        {
            if (effectOnEnable == null) return;

            effectOnEnable.Clear();  // 이전 재생 잔여물 제거
            effectOnEnable.Play();
        }

        public void DisableHitbox()
        {
            _hitboxCollider.enabled = false;
            _isActive = false;
        }

        /// <summary>
        /// 히트박스 활성화 시점에 이미 안에 있는 콜라이더들을 직접 검사.
        /// OnTriggerEnter는 "진입" 이벤트라 이미 겹친 대상은 감지 못함.
        /// </summary>
        private void CheckOverlappingColliders()
        {
            Collider[] overlapping = GetOverlappingColliders();

            foreach (Collider other in overlapping)
            {
                // 자기 자신 무시
                if (other == _hitboxCollider) continue;

                TryDealDamage(other);
            }
        }

        /// <summary>
        /// 히트박스 콜라이더 형태에 맞게 겹친 콜라이더들을 가져옴.
        /// BoxCollider / SphereCollider 지원.
        /// </summary>
        private Collider[] GetOverlappingColliders()
        {
            // BoxCollider 처리
            BoxCollider box = _hitboxCollider as BoxCollider;
            if (box != null)
            {
                Vector3 worldCenter = box.transform.TransformPoint(box.center);
                Vector3 worldHalfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
                Quaternion worldRotation = box.transform.rotation;

                return Physics.OverlapBox(worldCenter, worldHalfExtents, worldRotation, hittableLayers, QueryTriggerInteraction.Collide);
            }

            // SphereCollider 처리 (포효, 충격파 등 AoE에 자연스러움)
            SphereCollider sphere = _hitboxCollider as SphereCollider;
            if (sphere != null)
            {
                Vector3 worldCenter = sphere.transform.TransformPoint(sphere.center);
                // SphereCollider는 lossyScale 최대값으로 정규화 (Unity 표준)
                float maxScale = Mathf.Max(sphere.transform.lossyScale.x,
                    Mathf.Max(sphere.transform.lossyScale.y, sphere.transform.lossyScale.z));
                float worldRadius = sphere.radius * maxScale;

                return Physics.OverlapSphere(worldCenter, worldRadius, hittableLayers, QueryTriggerInteraction.Collide);
            }

            Debug.LogWarning($"[{name}] WeaponHitbox는 BoxCollider / SphereCollider만 지원함");
            return new Collider[0];
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;
            TryDealDamage(other);
        }

        /// <summary>
        /// 대상에게 데미지 적용 시도. 이미 맞았으면 무시.
        /// 넉백도 함께 적용 (대상이 IKnockbackable 구현했고 knockbackForce > 0인 경우).
        /// </summary>
        private void TryDealDamage(Collider other)
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null) return;

            if (_alreadyHitTargets.Contains(damageable)) return;

            // 넉백 먼저 적용 (대상이 IKnockbackable이고 무기에 knockbackForce > 0 설정된 경우만).
            // 순서가 중요: TakeDamage가 Flinch 트리거를 발동시키는데, Knockback이 우선되어야 하는 케이스에서
            //   TakeDamage(→Flinch) → ApplyKnockback(→Knockback) 순이면 같은 프레임 두 트리거가 충돌해
            //   Flinch가 우선되어버림.
            // 먼저 Knockback을 발동시키면 PlayerController._isBeingKnockedBack=true가 되어
            //   이후 TakeDamage→TriggerFlinch가 자체 가드(_isBeingKnockedBack 체크)로 차단됨.
            if (knockbackForce > 0f)
            {
                IKnockbackable knockable = other.GetComponent<IKnockbackable>();
                if (knockable != null)
                {
                    // 넉백 방향: 히트박스에서 대상 쪽 수평 방향
                    Vector3 dir = other.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        dir.Normalize();
                        knockable.ApplyKnockback(dir, knockbackForce, knockbackDuration);

                        if (drawDebugInfo)
                            Debug.Log($"[{name}] Knockback applied to {other.name} (force={knockbackForce})");
                    }
                }
            }

            // Motion Value 적용: damage * 콤보 타별 배율.
            // damage <= 0이면 TakeDamage 자체를 스킵 — "밀어내기/포이즈 전용" 히트박스 케이스.
            // 그래도 _alreadyHitTargets에는 추가 (같은 사이클에서 또 처리되는 것 방지).
            float finalDamage = damage * _damageMultiplier;
            if (finalDamage > 0f)
            {
                damageable.TakeDamage(finalDamage);

                // 타격음 재생 (한 번의 EnableHitbox 사이클당 대상 1명당 1회).
                // damage 0인 케이스에서는 타격음도 어색하므로 같이 스킵.
                PlayHitSound();

                if (drawDebugInfo)
                    Debug.Log($"[{name}] Hit {other.name} for {finalDamage:F1} damage (base={damage}, x{_damageMultiplier:F2})");
            }
            else if (drawDebugInfo)
            {
                Debug.Log($"[{name}] Damage skipped (damage<=0) for {other.name} — knockback/poise only hitbox");
            }
            _alreadyHitTargets.Add(damageable);

            // 포이즈 데미지 시도 (대상이 IPoiseDamageable이고 무기에 poiseDamage > 0 설정된 경우만)
            // 체력과 독립적으로 적용 — 포이즈 없는 적은 자연스럽게 무시됨.
            // Motion Value 동일 적용: 강한 타가 포이즈도 더 깎음.
            if (poiseDamage > 0f)
            {
                IPoiseDamageable poiseTarget = other.GetComponent<IPoiseDamageable>();
                if (poiseTarget != null)
                {
                    float finalPoiseDamage = poiseDamage * _damageMultiplier;
                    poiseTarget.TakePoiseDamage(finalPoiseDamage);

                    if (drawDebugInfo)
                        Debug.Log($"[{name}] Poise damage {finalPoiseDamage:F1} applied to {other.name}");
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugInfo) return;

            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = _isActive ? Color.red : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        /// <summary>
        /// 타격 성공 시 호출. SoundSet 비어있거나 AudioSource 없으면 조용히 스킵.
        /// </summary>
        private void PlayHitSound()
        {
            if (hitSound == null || hitAudioSource == null) return;
            AudioClip clip = hitSound.PickRandomClip();
            if (clip == null) return;

            hitAudioSource.pitch = hitSound.GetRandomPitch();
            hitAudioSource.PlayOneShot(clip, hitSound.SafeVolume);
        }
    }
}