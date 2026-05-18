using UnityEngine;
using UnityEngine.InputSystem;
using LostSouls.Combat;
using System.Collections.Generic;
using Unity.Cinemachine;

namespace LostSouls.Player
{
    /// <summary>
    /// 플레이어의 락온 시스템을 관리한다.
    /// 타겟 탐색, 선택, 락온 ON/OFF, 거리 검증.
    /// </summary>
    public class PlayerLockOn : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 15f;       // 락온 가능한 최대 거리
        [SerializeField] private float maxLockOnDistance = 20f;     // 락온 중 자동 해제 거리
        [SerializeField] private LayerMask targetableLayers;        // 검사할 레이어 (Enemy)
        [SerializeField] private float maxLockOnAngle = 60f;        // 화면 중앙 기준 허용 각도

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Debug")]
        [Tooltip("락온 획득/해제, 카메라 동기화 등 운영 흐름 로그 + 기즈모. 필요할 때만 켜라.")]
        [SerializeField] private bool drawDebugInfo = false;

        [Header("Camera References")]
        [SerializeField] private CinemachineCamera freeCamera;
        [SerializeField] private CinemachineCamera lockOnCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 0;

        private PlayerInputActions _inputActions;
        private ITargetable _currentTarget;

        // Public 접근자
        public bool IsLockedOn => _currentTarget != null && _currentTarget.IsTargetable;
        public ITargetable CurrentTarget => _currentTarget;
        public Transform CurrentTargetTransform => _currentTarget?.LockOnPoint;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.LockOn.performed += OnLockOnPressed;
        }

        private void OnDisable()
        {
            _inputActions.Player.LockOn.performed -= OnLockOnPressed;
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            // 락온 중인 타겟이 무효해지면 자동 해제
            if (_currentTarget != null)
            {
                if (!_currentTarget.IsTargetable)
                {
                    if (drawDebugInfo)
                        Debug.Log($"[LockOn] Target no longer valid. Releasing.");
                    ReleaseLockOn();
                    return;
                }

                // 거리 체크
                float distance = Vector3.Distance(transform.position, _currentTarget.Position);
                if (distance > maxLockOnDistance)
                {
                    if (drawDebugInfo)
                        Debug.Log($"[LockOn] Target too far ({distance:F1}m). Releasing.");
                    ReleaseLockOn();
                }
            }
        }

        private void OnLockOnPressed(InputAction.CallbackContext context)
        {
            if (IsLockedOn)
            {
                // 이미 락온 중이면 해제
                ReleaseLockOn();
            }
            else
            {
                // 락온 시도
                ITargetable best = FindBestTarget();
                if (best != null)
                {
                    AcquireLockOn(best);
                }
                else if (drawDebugInfo)
                {
                    Debug.Log($"[LockOn] No valid target found.");
                }
            }
        }

        /// <summary>
        /// 주변에서 가장 적합한 락온 대상을 찾는다.
        /// 거리 + 화면 중앙으로부터의 각도를 종합 평가.
        /// </summary>
        private ITargetable FindBestTarget()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, targetableLayers);

            ITargetable bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (Collider col in colliders)
            {
                ITargetable targetable = col.GetComponent<ITargetable>();
                if (targetable == null) continue;
                if (!targetable.IsTargetable) continue;

                Vector3 toTarget = targetable.Position - cameraTransform.position;
                Vector3 cameraForward = cameraTransform.forward;
                float angle = Vector3.Angle(cameraForward, toTarget);

                if (angle > maxLockOnAngle) continue;

                float distance = Vector3.Distance(transform.position, targetable.Position);
                float score = distance + angle * 0.3f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = targetable;
                }
            }

            return bestTarget;
        }

        private void AcquireLockOn(ITargetable target)
        {
            _currentTarget = target;

            if (lockOnCamera != null)
            {
                lockOnCamera.LookAt = target.LockOnPoint;
                lockOnCamera.Priority = activePriority;
            }

            if (drawDebugInfo)
                Debug.Log($"[LockOn] Locked onto {((MonoBehaviour)target).name}");
        }

        public void ReleaseLockOn()
        {
            if (_currentTarget != null && drawDebugInfo)
                Debug.Log($"[LockOn] Released lock-on.");

            // 락온 해제 직전: 자유 카메라를 락온 카메라의 방향으로 동기화
            SyncFreeCameraToLockOnDirection();

            _currentTarget = null;

            if (lockOnCamera != null)
            {
                lockOnCamera.Priority = inactivePriority;
                lockOnCamera.LookAt = null;
            }
        }

        /// <summary>
        /// 락온 해제 시 자유 카메라를 락온 카메라가 보던 방향으로 동기화.
        /// 카메라 전환 시 화면이 갑자기 튀는 현상 방지.
        /// </summary>
        private void SyncFreeCameraToLockOnDirection()
        {
            if (freeCamera == null || lockOnCamera == null) return;

            // 락온 카메라가 보던 방향 (카메라 → 적 방향)
            Vector3 lockOnForward = lockOnCamera.transform.forward;

            // 수평 각도 계산 (Y축 회전)
            // Atan2(x, z)로 -180~180도 범위 각도 산출
            float horizontalAngle = Mathf.Atan2(lockOnForward.x, lockOnForward.z) * Mathf.Rad2Deg;

            // OrbitalFollow의 Horizontal Axis에 적용
            // 카메라가 캐릭터 반대편을 보니까 180도 더하기 (또는 부호 반전)
            // OrbitalFollow는 캐릭터 주위 카메라 위치 각도이므로 forward의 반대 방향
            var orbitalFollow = freeCamera.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.HorizontalAxis.Value = horizontalAngle;

                // Vertical Axis는 0.5 (가운데 궤도)로 리셋
                // 락온 카메라 높이를 ThreeRing으로 정확히 환산하기 어려워서
                // 가운데로 리셋하는 게 가장 안전
                orbitalFollow.VerticalAxis.Value = 2f;
            }

            if (drawDebugInfo)
                Debug.Log($"[LockOn] Synced free camera angle to {horizontalAngle:F1}°");
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugInfo) return;

            // 탐지 범위
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // 락온 중인 타겟에 선
            if (_currentTarget != null && _currentTarget.IsTargetable)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _currentTarget.Position);

                // 락온 포인트에 작은 구
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_currentTarget.LockOnPoint.position, 0.3f);
            }
        }
    }
}