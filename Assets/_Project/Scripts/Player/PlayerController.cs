using UnityEngine;
using UnityEngine.InputSystem;
using LostSouls.Combat;

namespace LostSouls.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IKnockbackable
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float walkRotationSpeed = 12f;
        [SerializeField] private float runRotationSpeed = 5f;
        [SerializeField] private float gravity = -20f;

        [Header("Roll Settings")]
        [SerializeField] private float rollSpeed = 7f;
        [SerializeField] private float rollDuration = 0.7f;

        [Header("Animation Settings")]
        [SerializeField] private float animationSmoothTime = 0.1f;

        [Header("Camera Reference")]
        [SerializeField] private Transform cameraTransform;

        // Components
        private CharacterController _characterController;
        private Animator _animator;
        private PlayerInputActions _inputActions;
        private PlayerStamina _stamina;
        private PlayerLockOn _lockOn;
        private PlayerHealth _health;
        private PlayerCombat _combat;

        // Input values
        private Vector2 _moveInput;
        private bool _isSprinting;

        // Movement state
        private Vector3 _velocity;
        private float _currentSpeed;

        // Combat state
        private bool _isAttacking;

        // Roll state
        private bool _isRolling;
        private Vector3 _rollDirection;
        private float _rollTimer;

        // Knockback state
        // 두 상태 분리:
        // - _isBeingKnockedBack: 위치 이동 중 (짧음, knockbackDuration)
        // - _isInKnockbackAnimation: 애니메이션 진행 중 (길음, 1~1.5초). 입력/무적/회전 차단의 기준
        private bool _isBeingKnockedBack;
        private bool _isInKnockbackAnimation;
        private Vector3 _knockbackDirection;
        private float _knockbackForce;
        private float _knockbackDuration;
        private float _knockbackTimer;

        // Flinch state — Knockback의 가벼운 버전.
        // 짧은 비틀 모션 + 이동/공격 차단. 단 회피(Roll)는 가능 (다크소울 정석 캔슬).
        // Animator State 이름이 "Flinch"인지로 판정. 별도 위치 이동 없음 — 애니메이션만.
        private bool _isFlinching;

        // 사망 상태. PlayerHealth.Die가 HandleDeath로 통지하면 true.
        // 이후 모든 입력/이동/공격이 영구 차단됨. 씬 리로드 전까지 유지.
        private bool _isDead;

        // Animation
        private float _animatorSpeed;
        private float _animatorSpeedVelocity;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        // 락온 시 4방향 이동/구르기용. 락온 OFF면 항상 (0, 1) (전방 이동/구르기).
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int RollXHash = Animator.StringToHash("RollX");
        private static readonly int RollZHash = Animator.StringToHash("RollZ");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int ComboHash = Animator.StringToHash("Combo");
        private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
        private static readonly int RollHash = Animator.StringToHash("Roll");
        private static readonly int KnockbackHash = Animator.StringToHash("Knockback");
        private static readonly int FlinchHash = Animator.StringToHash("Flinch");
        private static readonly int DieHash = Animator.StringToHash("Die");

        // Animator state hashes
        // 콤보 시스템 도입으로 Attack 단일 State → Attack1/2/3로 분할.
        // 셋 중 하나라도 진행 중이면 _isAttacking = true.
        // R2(HeavyAttack)는 R1과 별도 분기지만 _isAttacking 판정에는 포함 (이동/회피/입력 차단 일관).
        private static readonly int Attack1StateHash = Animator.StringToHash("Attack1");
        private static readonly int Attack2StateHash = Animator.StringToHash("Attack2");
        private static readonly int Attack3StateHash = Animator.StringToHash("Attack3");
        private static readonly int HeavyAttackStateHash = Animator.StringToHash("HeavyAttack");
        private static readonly int RollStateHash = Animator.StringToHash("Roll");
        private static readonly int FlinchStateHash = Animator.StringToHash("Flinch");

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _stamina = GetComponent<PlayerStamina>();
            _lockOn = GetComponent<PlayerLockOn>();
            _health = GetComponent<PlayerHealth>();
            _combat = GetComponent<PlayerCombat>();
            _inputActions = new PlayerInputActions();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Move.performed += OnMove;
            _inputActions.Player.Move.canceled += OnMove;
            _inputActions.Player.Sprint.performed += OnSprint;
            _inputActions.Player.Sprint.canceled += OnSprint;
            _inputActions.Player.LightAttack.performed += OnLightAttack;
            _inputActions.Player.HeavyAttack.performed += OnHeavyAttack;
            _inputActions.Player.Roll.performed += OnRoll;
        }

        private void OnDisable()
        {
            _inputActions.Player.Move.performed -= OnMove;
            _inputActions.Player.Move.canceled -= OnMove;
            _inputActions.Player.Sprint.performed -= OnSprint;
            _inputActions.Player.Sprint.canceled -= OnSprint;
            _inputActions.Player.LightAttack.performed -= OnLightAttack;
            _inputActions.Player.HeavyAttack.performed -= OnHeavyAttack;
            _inputActions.Player.Roll.performed -= OnRoll;
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            // 사망 시: Animator만 갱신, 그 외 모든 처리 차단.
            // 중력은 적용 (공중에서 죽으면 떨어지도록).
            if (_isDead)
            {
                HandleGravity();
                return;
            }

            UpdateCombatState();
            UpdateStaminaRegen();
            HandleComboHold();
            HandleKnockback();
            HandleRoll();
            HandleMovement();
            HandleGravity();
            UpdateAnimator();
        }

        /// <summary>
        /// R1을 누른 채로 유지하면 콤보 윈도우 열릴 때마다 자동으로 다음 타 발동.
        /// performed 이벤트(OnLightAttack)는 누른 순간만 발동하지만,
        /// 이건 매 프레임 IsPressed로 체크해서 윈도우 열린 동안 누르고 있으면 자동 진행.
        ///
        /// 짧은 콤보 윈도우 안에 매번 클릭하는 입력 피로를 줄임. 다크소울 R1 연타 대안.
        /// </summary>
        private void HandleComboHold()
        {
            // 콤보 진행 조건은 OnLightAttack과 동일하게 유지
            if (!_isAttacking) return;
            if (_isRolling || _isBeingKnockedBack || _isFlinching) return;
            if (_combat == null || !_combat.IsComboWindowOpen) return;

            // 버튼이 현재 눌려있는지 체크 (performed 아님 — 누른 순간 + 유지 둘 다 잡음)
            if (!_inputActions.Player.LightAttack.IsPressed()) return;

            // 스태미나 가능 여부만 체크. 실제 소모는 UpdateCombatState가 Attack2/3 State 진입 감지 시.
            if (!_stamina.CanAttack()) return;
            _combat.ConsumeComboInput();
            _animator.SetTrigger(ComboHash);
        }

        // 직전 프레임 Animator State hash. State 진입 감지에 사용.
        private int _previousStateHash;

        private void UpdateCombatState()
        {
            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            int hash = currentState.shortNameHash;

            // 콤보 3타 + R2 중 하나라도 진행 중이면 공격 상태
            _isAttacking = hash == Attack1StateHash
                        || hash == Attack2StateHash
                        || hash == Attack3StateHash
                        || hash == HeavyAttackStateHash;

            _isRolling = hash == RollStateHash;
            _isFlinching = hash == FlinchStateHash;

            // State 진입 감지: 새 공격 State에 막 들어온 순간 스태미나를 실제로 소모.
            // 입력 시점이 아니라 모션 시작 시점에 소모 → 비주얼/리소스 타이밍 일치.
            // 콤보 큐잉(입력 → Exit Time 후 transition) 때문에 입력 시점 소모가 부자연스러웠던 문제 해결.
            if (hash != _previousStateHash)
            {
                if (hash == Attack1StateHash || hash == Attack2StateHash || hash == Attack3StateHash)
                {
                    _stamina.ConsumeAttackCost();
                }
                else if (hash == HeavyAttackStateHash)
                {
                    _stamina.ConsumeHeavyAttackCost();
                }
                _previousStateHash = hash;
            }
        }

        private void HandleRoll()
        {
            if (!_isRolling) return;

            // 회피 중 → 정해진 방향으로 일정 속도로 이동
            _rollTimer += Time.deltaTime;

            if (_rollTimer < rollDuration)
            {
                // 시간에 따라 속도 감소 (시작 빠르게, 끝나가면서 느리게)
                float progress = _rollTimer / rollDuration;
                float speedCurve = Mathf.Lerp(1f, 0.2f, progress);  // 100% → 20%

                _characterController.Move(_rollDirection * rollSpeed * speedCurve * Time.deltaTime);
            }
        }

        private void HandleMovement()
        {
            // 넉백 중에는 일반 이동/회전 차단 (HandleKnockback이 위치 제어)
            if (_isBeingKnockedBack)
            {
                _currentSpeed = 0f;
                return;
            }

            if (_isAttacking)
            {
                _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, Time.deltaTime * 15f);
                return;
            }

            if (_isRolling)
            {
                _currentSpeed = 0f;

                // 락온 중이면 회피 중에도 적을 계속 바라봄
                if (_lockOn != null && _lockOn.IsLockedOn)
                {
                    RotateTowardsTarget();
                }
                return;
            }

            if (_isFlinching)
            {
                // Flinch 중에는 이동/회전 모두 차단. 위치 이동 없음 (애니메이션만).
                _currentSpeed = 0f;
                return;
            }

            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;

            bool isActuallySprinting = _isSprinting && _moveInput.magnitude > 0.1f && _stamina.CanPerformAction;

            float targetSpeed = isActuallySprinting ? runSpeed : walkSpeed;
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed * _moveInput.magnitude, Time.deltaTime * 10f);

            _characterController.Move(moveDirection * _currentSpeed * Time.deltaTime);

            if (isActuallySprinting)
            {
                _stamina.DrainForSprint();
            }

            // 회전 처리 - 락온 여부로 분기
            if (_lockOn != null && _lockOn.IsLockedOn)
            {
                // 락온 중: 항상 타겟 방향으로 회전
                RotateTowardsTarget();
            }
            else if (moveDirection.magnitude > 0.1f)
            {
                // 락온 안 됨: 이동 방향으로 회전
                float currentRotationSpeed = isActuallySprinting ? runRotationSpeed : walkRotationSpeed;

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
            }
        }

        private void RotateTowardsTarget()
        {
            Vector3 toTarget = _lockOn.CurrentTargetTransform.position - transform.position;
            toTarget.y = 0f;  // 수평 회전만

            if (toTarget.sqrMagnitude < 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, walkRotationSpeed * Time.deltaTime);
        }

        private void HandleGravity()
        {
            if (_characterController.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;

            _velocity.y += gravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }

        // MoveX/MoveZ 보간용 (애니메이션 튐 방지)
        private float _animMoveX;
        private float _animMoveZ;
        private float _animMoveXVelocity;
        private float _animMoveZVelocity;

        private void UpdateAnimator()
        {
            bool isActuallySprinting = _isSprinting && _moveInput.magnitude > 0.1f && _stamina.CanPerformAction;

            float targetAnimSpeed;

            if (_isAttacking || _isRolling || _isBeingKnockedBack || _isFlinching || _moveInput.magnitude < 0.1f)
                targetAnimSpeed = 0f;
            else if (isActuallySprinting)
                targetAnimSpeed = 2f;
            else
                targetAnimSpeed = 1f;

            _animatorSpeed = Mathf.SmoothDamp(_animatorSpeed, targetAnimSpeed, ref _animatorSpeedVelocity, animationSmoothTime);
            _animator.SetFloat(SpeedHash, _animatorSpeed);

            // MoveX/MoveZ 계산 (캐릭터 좌표계 기준)
            // 락온 ON: 캐릭터가 적을 바라보고 있으므로 이동 벡터를 transform.forward/right로 분해 → strafe 좌표
            // 락온 OFF: 캐릭터가 이동 방향으로 회전하므로 자연스럽게 MoveX≈0, MoveZ≈1
            UpdateMoveAxes();
        }

        private void UpdateMoveAxes()
        {
            float targetX = 0f;
            float targetZ = 0f;

            if (_moveInput.magnitude > 0.1f && !_isAttacking && !_isRolling && !_isBeingKnockedBack && !_isFlinching)
            {
                // 월드 좌표계의 이동 방향 (카메라 기준 입력을 월드로 변환)
                Vector3 cameraForward = cameraTransform.forward;
                Vector3 cameraRight = cameraTransform.right;
                cameraForward.y = 0f;
                cameraRight.y = 0f;
                cameraForward.Normalize();
                cameraRight.Normalize();

                Vector3 worldMoveDir = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;

                // 캐릭터 좌표계로 분해
                targetZ = Vector3.Dot(worldMoveDir, transform.forward);
                targetX = Vector3.Dot(worldMoveDir, transform.right);
            }

            _animMoveX = Mathf.SmoothDamp(_animMoveX, targetX, ref _animMoveXVelocity, animationSmoothTime);
            _animMoveZ = Mathf.SmoothDamp(_animMoveZ, targetZ, ref _animMoveZVelocity, animationSmoothTime);

            _animator.SetFloat(MoveXHash, _animMoveX);
            _animator.SetFloat(MoveZHash, _animMoveZ);
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            _isSprinting = context.ReadValueAsButton();
        }

        private void OnLightAttack(InputAction.CallbackContext context)
        {
            // 사망/Roll/Knockback/Flinch 중에는 항상 차단
            if (_isDead) return;
            if (_isRolling || _isBeingKnockedBack || _isFlinching) return;

            // 콤보 분기:
            // - 공격 중이고 콤보 윈도우 열려있음 → Combo 트리거 (다음 타로 이어짐)
            // - 공격 중이지만 윈도우 닫힘 → 입력 무시 (이전 타가 끝날 때까지 기다림)
            // - 공격 중 아님 → 새 콤보 시작 (Attack 트리거)
            if (_isAttacking)
            {
                if (_combat != null && _combat.IsComboWindowOpen)
                {
                    if (!_stamina.CanAttack()) return;
                    _combat.ConsumeComboInput();  // 윈도우 즉시 닫기 (중복 발동 방지)
                    _animator.SetTrigger(ComboHash);
                }
                // 윈도우 밖이면 입력 무시 (버퍼링 안 함 — 다크소울 정석은 윈도우 안에서만 받음)
                return;
            }

            // 평상시 → 새 콤보 시작
            if (!_stamina.CanAttack()) return;
            _animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// R2 강공격 (단발). 마우스 우클릭.
        /// R1 콤보와 별도 분기 — 윈도우 시스템 없음. 평상시에만 발동 가능.
        /// </summary>
        private void OnHeavyAttack(InputAction.CallbackContext context)
        {
            // 사망/공격/회피/넉백/Flinch 중에는 차단. R1 콤보와 달리 강공격 도중 이어짐 없음.
            if (_isDead) return;
            if (_isAttacking || _isRolling || _isBeingKnockedBack || _isFlinching) return;
            if (!_stamina.CanHeavyAttack()) return;
            _animator.SetTrigger(HeavyAttackHash);
        }

        private void OnRoll(InputAction.CallbackContext context)
        {
            if (_isDead) return;
            if (_isAttacking || _isRolling || _isBeingKnockedBack) return;
            if (!_stamina.TryConsumeRoll()) return;

            Vector3 rollDir;
            float rollX = 0f;
            float rollZ = 1f;  // 기본값: 전방 구르기 (입력 없을 때 또는 락온 OFF)

            if (_moveInput.magnitude > 0.1f)
            {
                Vector3 cameraForward = cameraTransform.forward;
                Vector3 cameraRight = cameraTransform.right;
                cameraForward.y = 0f;
                cameraRight.y = 0f;
                cameraForward.Normalize();
                cameraRight.Normalize();

                rollDir = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;

                // 락온 중이 아닐 때만 회피 방향으로 즉시 회전
                // (락온 중에는 매 프레임 적을 향한 회전이 자동 적용됨)
                if (_lockOn == null || !_lockOn.IsLockedOn)
                {
                    transform.rotation = Quaternion.LookRotation(rollDir);
                    // 회전 후 캐릭터가 rollDir 방향을 보고 있으므로 → RollX=0, RollZ=1 (기본값 유지)
                }
                else
                {
                    // 락온 중: 캐릭터는 적을 바라보고 있음.
                    // rollDir(월드 좌표)를 캐릭터 좌표계로 분해 → 4방향 구르기 클립 선택.
                    rollX = Vector3.Dot(rollDir, transform.right);
                    rollZ = Vector3.Dot(rollDir, transform.forward);
                }
            }
            else
            {
                rollDir = transform.forward;
                // 입력 없음: 전방 구르기 (기본값 그대로)
            }

            _rollDirection = rollDir;
            _rollTimer = 0f;

            // Blend Tree에 방향 알리기. 트리거 발동 직전에 설정해야 새 State 진입 시 즉시 반영.
            _animator.SetFloat(RollXHash, rollX);
            _animator.SetFloat(RollZHash, rollZ);
            _animator.SetTrigger(RollHash);
        }

        private void UpdateStaminaRegen()
        {
            bool inAction = _isAttacking || _isRolling || _isBeingKnockedBack || _isFlinching;
            _stamina.SetRegenBlocked(inAction);
        }

        // === IKnockbackable 구현 ===

        /// <summary>
        /// 외부(보스 공격 등)에서 호출되어 넉백을 적용한다.
        /// 진행 중인 회피/공격은 캔슬됨 (다크소울 정석).
        /// 무적 상태(회피 i-frame)면 넉백도 무시 — 회피로 완전히 빠져나간 거.
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float force, float duration)
        {
            if (_isDead) return;

            // 무적 중이면 넉백도 무시 (i-frame이 모든 공격 효과를 막아준다)
            if (_health != null && _health.IsInvulnerable) return;

            _isBeingKnockedBack = true;
            _knockbackDirection = direction;
            _knockbackForce = force;
            _knockbackDuration = duration;
            _knockbackTimer = 0f;

            // 넉백 애니메이션 트리거 (짧은 비틀 동작)
            _animator.SetTrigger(KnockbackHash);
        }

        // === Flinch 발동 ===

        /// <summary>
        /// 외부(PlayerHealth.TakeDamage 등)에서 호출. 가벼운 피격 반응.
        /// Knockback의 단순화 버전 — 위치 이동 없이 짧은 비틀 모션 + 행동 제약.
        ///
        /// 무시 조건:
        /// - 무적 (i-frame) 중
        /// - 이미 Knockback 중 (Knockback이 더 강한 효과라 우선)
        /// - 이미 Flinch 중 (stun lock 방지)
        /// </summary>
        public void TriggerFlinch()
        {
            if (_isDead) return;
            if (_health != null && _health.IsInvulnerable) return;
            if (_isBeingKnockedBack) return;
            if (_isFlinching) return;

            _animator.SetTrigger(FlinchHash);
        }

        // === 사망 처리 ===

        /// <summary>
        /// PlayerHealth.Die()가 호출. 사망 모션 트리거 + 모든 입력/행동 영구 차단.
        /// 입력 차단은 _isDead 필드를 통해 이동/공격/회피의 가드에서 모두 처리.
        /// 씬 리로드까지 유지.
        /// </summary>
        public void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            // 진행 중이던 다른 상태 정리 (시각적 깔끔)
            _isBeingKnockedBack = false;

            // 사망 트리거
            _animator.SetTrigger(DieHash);

            // 입력 액션 자체 비활성 (안전망 — _isDead 가드와 중복 차단)
            if (_inputActions != null)
                _inputActions.Player.Disable();
        }

        private void HandleKnockback()
        {
            if (!_isBeingKnockedBack) return;

            _knockbackTimer += Time.deltaTime;

            if (_knockbackTimer >= _knockbackDuration)
            {
                _isBeingKnockedBack = false;
                return;
            }

            // 시간 따라 감속 (회피 곡선과 동일한 패턴)
            float progress = _knockbackTimer / _knockbackDuration;
            float speedCurve = Mathf.Lerp(1f, 0.1f, progress);  // 100% → 10%로 감속

            _characterController.Move(_knockbackDirection * _knockbackForce * speedCurve * Time.deltaTime);
        }
    }
}