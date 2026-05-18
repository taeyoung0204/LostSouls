using UnityEngine;
using UnityEngine.AI;
using LostSouls.Player;

namespace LostSouls.Boss
{
    /// <summary>
    /// 보스의 메인 컨트롤러. FSM의 컨텍스트 역할.
    ///
    /// 책임:
    /// - 컴포넌트 캐싱 (Animator, NavMeshAgent)
    /// - State 인스턴스 생성 및 보관
    /// - State 전이 (ChangeState)
    /// - 매 프레임 현재 State 갱신
    /// - 회전 헬퍼 (모든 State가 공유하는 동작)
    ///
    /// 각 State는 이 클래스를 통해서만 외부 시스템에 접근한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(BossHealth))]
    [RequireComponent(typeof(BossPoise))]
    public class BossController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("플레이어 Transform. 비우면 Tag로 자동 검색.")]
        [SerializeField] private Transform player;

        [Header("Detection")]
        [Tooltip("이 거리 이내면 Chase 진입.")]
        [SerializeField] private float aggroRange = 25f;
        [Tooltip("이 거리 이내면 공격 시도. NavMeshAgent의 stoppingDistance와 동기화됨.")]
        [SerializeField] private float attackRange = 2.5f;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 3.5f;
        [Tooltip("초당 회전 각도.")]
        [SerializeField] private float rotationSpeed = 180f;
        [Tooltip("Animator 파라미터(MoveX/MoveZ) 보간 시간.")]
        [SerializeField] private float animationSmoothTime = 0.15f;

        [Header("Animation - Strafe")]
        [Tooltip("실제 이동 속도가 이 값 이하이면 '제자리 회전' 상태로 간주 — 가짜 strafe 의도를 만들어 옆걸음 애니메이션 재생.")]
        [SerializeField] private float idleVelocityThreshold = 0.3f;
        [Tooltip("제자리 회전 시 플레이어가 보스 정면에서 이 각도 이상 벗어났을 때만 strafe 의도 발동 (도). 너무 작으면 정면 추적 중에도 옆걸음.")]
        [SerializeField] private float strafeAngleThreshold = 20f;
        [Tooltip("제자리 회전 시 strafe 입력 세기 (0~1). Blend Tree에서 Strafe 클립이 X=±1 위치라면 1.0이 정석. 낮추면 Idle과 블렌드되어 옆걸음이 흐려짐.")]
        [Range(0f, 1f)]
        [SerializeField] private float strafeIntent = 1f;

        [Header("Combat")]
        [Tooltip("공격 후 다음 공격까지 최소 대기 (초).")]
        [SerializeField] private float minAttackCooldown = 1.5f;
        [Tooltip("공격 후 다음 공격까지 최대 대기 (초). 이 사이 랜덤.")]
        [SerializeField] private float maxAttackCooldown = 3f;
        [Tooltip("보스 히트박스 목록. Index 0=주무기(검), 1=발 등. 패턴이 hitboxIndex로 선택.")]
        [SerializeField] private LostSouls.Combat.WeaponHitbox[] hitboxes;
        [Tooltip("발동 가능한 패턴이 없을 때 보스가 최소 이 거리까지 다가감. attackRange보다 작아야 의미 있음.")]
        [SerializeField] private float minChaseDistance = 1.5f;

        [Header("Attack Patterns")]
        [Tooltip("보스가 사용할 공격 패턴 목록. AttackState가 거리/연속방지 로직으로 선택.")]
        [SerializeField] private System.Collections.Generic.List<AttackPattern> attackPatterns =
            new System.Collections.Generic.List<AttackPattern>();

        [Header("Death")]
        [Tooltip("사망 시 보스를 안착시킬 지면 레이어. Default + 환경 레이어 정도. 보스 자신/플레이어 레이어는 제외.")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("지면 탐지 Raycast 시작 높이 (보스 위치 기준 상대). 공중 사망 시 충분히 크게.")]
        [SerializeField] private float groundSnapRayUp = 2f;
        [Tooltip("지면 탐지 Raycast 최대 길이.")]
        [SerializeField] private float groundSnapRayLength = 50f;

        [Header("Performance")]
        [Tooltip("SetDestination 호출 주기.")]
        [SerializeField] private float destinationUpdateInterval = 0.15f;

        [Header("Debug")]
        [Tooltip("State 전이, 패턴 선택, 페이즈 전환 등 운영 흐름 로그. 필요할 때만 켜라.")]
        [SerializeField] private bool drawDebugInfo = false;

        // === Public 접근자 (State들이 사용) ===
        public Animator Animator { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public BossHealth Health { get; private set; }
        public BossPoise Poise { get; private set; }
        public Transform Player => player;

        public float AggroRange => aggroRange;
        public float AttackRange => attackRange;
        public float ChaseSpeed => chaseSpeed;
        public float RotationSpeed => rotationSpeed;
        public float AnimationSmoothTime => animationSmoothTime;
        public float DestinationUpdateInterval => destinationUpdateInterval;
        public float MinAttackCooldown => minAttackCooldown;
        public float MaxAttackCooldown => maxAttackCooldown;
        public float MinChaseDistance => minChaseDistance;
        public System.Collections.Generic.IReadOnlyList<AttackPattern> AttackPatterns => attackPatterns;

        // === State 인스턴스 (한 번 만들고 재사용) ===
        public BossIdleState IdleState { get; private set; }
        public BossChaseState ChaseState { get; private set; }
        public BossAttackState AttackState { get; private set; }
        public BossPhaseTransitionState PhaseTransitionState { get; private set; }
        public BossHitReactState HitReactState { get; private set; }

        // === 페이즈 전환 ===
        [Header("Phase Transition")]
        [Tooltip("이 체력 비율 이하로 떨어지면 페이즈 전환 (포효) 발동. 0.5 = 50%.")]
        [SerializeField, Range(0f, 1f)] private float phaseTransitionHealthRatio = 0.5f;
        [Tooltip("진행 중 공격이 끝난 후 포효까지 최소 대기 (초). 플레이어 반응 시간 확보용.")]
        [SerializeField] private float minPhaseTransitionDelay = 1.5f;
        [Tooltip("최대 대기. 이 사이 랜덤.")]
        [SerializeField] private float maxPhaseTransitionDelay = 3f;

        private bool _hasRoared;       // 이미 포효 끝났는지 (재발동 방지)
        private bool _roarPending;     // 50% 도달했지만 아직 발동 안 함
        private float _roarReadyTime = float.MaxValue;  // 이 시각 이후 발동 가능. MaxValue = 아직 카운트 시작 안 함

        // 다음 공격 가능 시각. 쿨다운 만료 여부 판정에 사용.
        public float NextAttackTime { get; set; }
        public bool CanAttack => Time.time >= NextAttackTime;

        // 직전 사용 패턴 (연속 방지용). null이면 직전 패턴 없음.
        public AttackPattern LastUsedPattern { get; set; }

        // === 패턴별 쿨다운 ===
        // 패턴 → 다음 사용 가능 시각.
        // SO 자체에 런타임 상태 두면 안 됨 (에셋이라 디스크에 박제됨).
        private readonly System.Collections.Generic.Dictionary<AttackPattern, float> _patternCooldowns
            = new System.Collections.Generic.Dictionary<AttackPattern, float>();

        /// <summary>
        /// 해당 패턴이 현재 쿨다운 중인지.
        /// </summary>
        public bool IsPatternOnCooldown(AttackPattern pattern)
        {
            if (pattern == null) return false;
            if (!_patternCooldowns.TryGetValue(pattern, out float nextAvailable)) return false;
            return Time.time < nextAvailable;
        }

        /// <summary>
        /// 패턴 사용 등록. 패턴 쿨다운이 0이면 무시.
        /// AttackState.Exit에서 호출.
        /// </summary>
        public void RegisterPatternUsage(AttackPattern pattern)
        {
            if (pattern == null) return;
            if (pattern.cooldownDuration <= 0f) return;

            _patternCooldowns[pattern] = Time.time + pattern.cooldownDuration;
        }

        /// <summary>
        /// 현재 발동 가능한 패턴이 하나라도 있는지.
        /// ChaseState가 "공격 시도 가능"을 판정할 때 사용.
        /// AttackState의 SelectPattern 후보 수집과 같은 조건.
        /// </summary>
        public bool HasAvailablePattern()
        {
            foreach (AttackPattern p in attackPatterns)
            {
                if (p == null) continue;
                if (!p.IsAvailable(this)) continue;
                if (IsPatternOnCooldown(p)) continue;
                return true;
            }
            return false;
        }

        // === Animator 파라미터 hash ===
        // 보스 forward 기준 상대 속도 (Blend Tree 2D Cartesian).
        // - MoveX: 좌우 (-1=왼쪽 strafe, +1=오른쪽 strafe)
        // - MoveZ: 전후 (+1=전진. 후진 클립 없으니 음수값은 사실상 사용 안 함)
        // 둘 다 0이면 Idle.
        public static readonly int MoveXHash = Animator.StringToHash("MoveX");
        public static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        public static readonly int DeathTriggerHash = Animator.StringToHash("Death");

        // === 사망 ===
        // BossHealth.OnDeath 이벤트로 켜짐. 이후 모든 Update/State 로직이 멈춤.
        // 다시 빠져나갈 일 없는 종착점이라 State 머신에 넣지 않고 플래그로만 관리.
        private bool _isDead;
        public bool IsDead => _isDead;

        // 사망 시 스냅된 위치. LateUpdate에서 매 프레임 이 위치로 고정해
        // Death 클립의 root motion이 시체를 다시 띄우거나 묻히게 하는 걸 방지.
        // OnDeathAnimationEnd에서 Animator.enabled=false 될 때까지 유지.
        private Vector3 _deathPosition;
        private Quaternion _deathRotation;
        private bool _lockDeathPose;

        // === 플레이어 사망 추적 ===
        // 캐싱한 PlayerHealth와 사망 플래그.
        // 플레이어가 죽으면 보스는 추격/공격 중단하고 IdleState로 영구 복귀.
        // (다크소울 정석 — 플레이어 시체 옆에서 보스는 그냥 서있음)
        private PlayerHealth _playerHealth;
        private bool _isPlayerDead;
        public bool IsPlayerDead => _isPlayerDead;

        // === 보스룸 활성화 ===
        // 시작 시 false. BossRoomTrigger가 플레이어 진입 시 Activate() 호출.
        // 비활성 상태: Animator만 살아있어서 Idle 클립이 자동 재생됨. State 머신/AI는 정지.
        // 다크소울 정석 — 안개 벽 통과 전엔 보스가 방 안에 보이지만 움직이지 않음.
        [Header("Activation")]
        [Tooltip("기본 false. BossRoomTrigger가 진입 시 Activate(). " +
                 "디버그용으로 처음부터 활성하려면 ON.")]
        [SerializeField] private bool startActivated = false;
        private bool _isActivated;
        public bool IsActivated => _isActivated;

        // === FSM 상태 ===
        private BossStateBase _currentState;
        public BossStateBase CurrentState => _currentState;

        // Animator 보간용 (State 간 공유 - 끊김 방지)
        private float _animMoveX;
        private float _animMoveXVelocity;
        private float _animMoveZ;
        private float _animMoveZVelocity;

        // === 편의 프로퍼티 ===
        public float DistanceToPlayer
        {
            get
            {
                if (player == null) return float.MaxValue;
                return Vector3.Distance(transform.position, player.position);
            }
        }

        private void Awake()
        {
            Animator = GetComponent<Animator>();
            Agent = GetComponent<NavMeshAgent>();
            Health = GetComponent<BossHealth>();
            Poise = GetComponent<BossPoise>();

            // NavMeshAgent 설정
            Agent.speed = chaseSpeed;
            Agent.stoppingDistance = attackRange;
            Agent.angularSpeed = rotationSpeed;
            Agent.updateRotation = false;  // 회전은 직접 처리

            // 히트박스 검증 - 인스펙터에서 비어있으면 경고
            if (hitboxes == null || hitboxes.Length == 0)
            {
                Debug.LogWarning($"[{name}] BossController: Hitboxes 배열이 비어있음. 인스펙터에서 설정 필요.");
            }

            // 기본 활성 히트박스는 Index 0 (보통 주무기)
            if (hitboxes != null && hitboxes.Length > 0)
                ActiveHitbox = hitboxes[0];

            // 플레이어 자동 탐색
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
                else Debug.LogWarning($"[{name}] BossController: 플레이어를 찾을 수 없음.");
            }

            // 플레이어 사망 감지용 PlayerHealth 캐싱 (있으면)
            if (player != null)
                _playerHealth = player.GetComponent<PlayerHealth>();

            // State 인스턴스 생성
            IdleState = new BossIdleState(this);
            ChaseState = new BossChaseState(this);
            AttackState = new BossAttackState(this);
            PhaseTransitionState = new BossPhaseTransitionState(this);
            HitReactState = new BossHitReactState(this);
        }

        private void OnEnable()
        {
            // 포이즈 브레이크 시 HitReactState로 강제 전이
            if (Poise != null)
                Poise.OnPoiseBroken += HandlePoiseBroken;

            // 사망 처리
            if (Health != null)
                Health.OnDeath += HandleDeath;

            // 플레이어 사망 시 보스도 추적 중단 → IdleState 강제 전이
            if (_playerHealth != null)
                _playerHealth.OnDeath += HandlePlayerDeath;
        }

        private void OnDisable()
        {
            if (Poise != null)
                Poise.OnPoiseBroken -= HandlePoiseBroken;

            if (Health != null)
                Health.OnDeath -= HandleDeath;

            if (_playerHealth != null)
                _playerHealth.OnDeath -= HandlePlayerDeath;
        }

        /// <summary>
        /// 플레이어 사망 시 호출. 보스는 진행 중이던 행동을 자연 종료 후 IdleState로 복귀.
        /// 즉시 강제 전이는 어색함 — Roar/HitReact 중이면 ChangeState 가드에 막힘.
        /// 플래그만 켜고, BossIdleState/ChaseState가 _isPlayerDead 보면 추격/공격 안 함.
        /// </summary>
        private void HandlePlayerDeath()
        {
            _isPlayerDead = true;

            if (drawDebugInfo)
                Debug.Log($"[Boss] Player died. Boss will return to Idle.");
        }

        /// <summary>
        /// 포이즈 브레이크 발생 시 HitReactState로 전이.
        /// 페이즈 전환 중이면 ChangeState가 차단하므로 안전.
        /// </summary>
        private void HandlePoiseBroken()
        {
            // HitReact 자체로 들어가는 건 페이즈 전환 외엔 항상 허용
            ChangeState(HitReactState);
        }

        /// <summary>
        /// 보스 사망 처리. BossHealth.OnDeath 이벤트로 호출.
        ///
        /// 순서가 중요:
        /// 1. _isDead 플래그 (이후 모든 Update 차단)
        /// 2. 현재 State Exit (이미 진행 중이던 패턴 정리 — 히트박스 끄기 등)
        /// 3. 모든 히트박스 강제 끄기 (Exit 누락 대비 안전망. 사망 중 데미지 차단)
        /// 4. NavMeshAgent 정지 및 비활성 (Update 안 돌아도 Agent가 회전/이동시킬 수 있음)
        /// 5. 보스 본체의 모든 콜라이더 비활성 (플레이어가 시체 못 때림, 통과 가능)
        ///    단, 히트박스(자식의 별도 콜라이더)는 이미 비활성. 락온 콜라이더가 있다면 같이 꺼짐.
        /// 6. Death 트리거 발동 — Animator Death State는 Loop OFF, Has Exit Time 없음
        /// </summary>
        private void HandleDeath()
        {
            if (_isDead) return;  // 이중 호출 방어
            _isDead = true;

            // 1회성 중요 이벤트라 항상 출력
            Debug.Log($"[Boss] === DEATH SEQUENCE ===");

            // 진행 중이던 State 정리 (패턴이 NavMeshAgent.updatePosition 만져둔 거 복원 등)
            _currentState?.Exit();
            _currentState = null;

            // 안전망: 모든 히트박스 강제 OFF
            DisableAllHitboxes();

            // 본체의 모든 콜라이더 비활성 (플레이어 검에 안 맞고, 캐릭터가 통과 가능)
            // Raycast 전에 끄는 게 중요 — 자기 자신에 안 맞게.
            Collider[] allColliders = GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (Collider c in allColliders)
            {
                c.enabled = false;
            }

            // 공중 사망 대비 — 지면으로 스냅.
            // Agent를 끄면 NavMesh 결착이 풀려서 떠 있는 채로 박제됨. 떨어뜨리려면 Rigidbody가 있어야 하는데
            // 우리 보스는 Rigidbody 없음. 그래서 Raycast로 즉시 안착.
            SnapToGround();

            // NavMeshAgent 정지 후 비활성화 (위치 스냅 후에)
            if (Agent != null)
            {
                if (Agent.isOnNavMesh)
                {
                    Agent.isStopped = true;
                    Agent.ResetPath();
                }
                Agent.enabled = false;
            }

            // Death 클립의 root motion이 시체를 띄우거나 묻히게 하는 걸 방지.
            // Apply Root Motion이 ON이면 매 LateUpdate에 클립의 root 이동량이 transform에 적용됨.
            // 사망 직후엔 위치/회전 모두 동결되어야 하므로 OFF.
            Animator.applyRootMotion = false;

            // 현재 위치/회전을 잠금 기준으로 저장 후 LateUpdate에서 강제 유지
            // (혹시 root motion 외에 다른 영향이 있어도 매 프레임 보정)
            _deathPosition = transform.position;
            _deathRotation = transform.rotation;
            _lockDeathPose = true;

            // Death 애니메이션 발동
            Animator.SetTrigger(DeathTriggerHash);
        }

        /// <summary>
        /// 사망 후 위치/회전을 매 프레임 _deathPosition/_deathRotation로 고정.
        /// applyRootMotion=false로 1차 차단했지만, 혹시 다른 경로(Animator IK, 부모 변경 등)로
        /// 위치가 움직이더라도 LateUpdate에서 다시 되돌려서 시체가 완벽히 정적으로 남게 한다.
        /// OnDeathAnimationEnd에서 _lockDeathPose=false로 해제.
        ///
        /// 또한 RootMotionMeleePattern.Exit에서 예약된 지연 Warp도 여기서 처리.
        /// 이유: 패턴 Exit이 호출되는 Update 시점엔 그 프레임의 root motion이 아직 transform에 반영 안 된 상태.
        /// Animator의 root motion은 같은 프레임 LateUpdate에서 적용됨.
        /// 그래서 Exit에서 즉시 Warp하면 root motion 적용 전 위치로 Agent가 동기화되고,
        /// 다음 프레임 Agent.updatePosition이 transform을 그 어긋난 위치로 끌어와 "살짝 순간이동"이 보임.
        /// LateUpdate에서 (root motion 적용 후) Warp하면 transform 최종 위치 그대로 Agent에 박혀 어긋남 없음.
        /// </summary>
        private void LateUpdate()
        {
            if (_lockDeathPose)
            {
                transform.SetPositionAndRotation(_deathPosition, _deathRotation);
                return;
            }

            if (_pendingAgentWarp)
            {
                _pendingAgentWarp = false;
                if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                {
                    Agent.Warp(transform.position);
                    Agent.updatePosition = true;

                    // Y 어긋남 즉시 해소.
                    // 원인: Agent.Warp는 transform.position을 그대로 받지만 Agent 내부는 NavMesh 표면 Y로 보정한다.
                    // 즉 transform과 Agent.nextPosition의 Y가 어긋남 — 다음 프레임에 Agent가 transform을
                    // 자기 Y로 끌어와 시각적 점프가 보임. 여기서 transform Y를 nextPosition Y로 미리 맞춰
                    // 다음 프레임 보정량을 0으로 만든다.
                    // XZ는 Warp 직후 transform과 Agent가 일치하므로 그대로 둠.
                    Vector3 pos = transform.position;
                    pos.y = Agent.nextPosition.y;
                    transform.position = pos;
                }
            }
        }

        // === 지연 Warp ===
        private bool _pendingAgentWarp;

        /// <summary>
        /// 다음 LateUpdate에서 Agent를 현재 transform 위치로 Warp하고 updatePosition을 다시 켠다.
        /// RootMotionMeleePattern.Exit이 즉시 Warp 대신 이걸 호출해서 root motion 위치 어긋남 방지.
        /// </summary>
        public void ScheduleAgentWarpToTransform()
        {
            _pendingAgentWarp = true;
        }

        /// <summary>
        /// 보스를 가장 가까운 아래 지면 위로 스냅. 공중 사망 시 시체가 떠있지 않게.
        ///
        /// 1) 위쪽에서 아래로 Raycast (사망 위치 위에서 시작 — 천장 등 위 장애물 피함)
        /// 2) 맞으면 그 hit.point로 이동
        /// 3) 못 맞으면 NavMesh.SamplePosition 시도 (NavMesh 위 가장 가까운 점)
        /// 4) 그것도 실패면 원위치 유지 (경고 로그만)
        /// </summary>
        private void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * groundSnapRayUp;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                groundSnapRayLength + groundSnapRayUp, groundLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                if (drawDebugInfo)
                    Debug.Log($"[Boss] Death snap to ground (raycast): y={hit.point.y:F2}");
                return;
            }

            // Fallback — NavMesh 위 가장 가까운 점 찾기
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit navHit,
                groundSnapRayLength, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = navHit.position;
                if (drawDebugInfo)
                    Debug.Log($"[Boss] Death snap to ground (navmesh): y={navHit.position.y:F2}");
                return;
            }

            Debug.LogWarning($"[{name}] Death snap failed — corpse may remain mid-air.");
        }

        private void Start()
        {
            // 초기 상태: Idle
            ChangeState(IdleState);

            // 활성화 상태 결정. 디버그용 startActivated가 true면 처음부터 활성.
            _isActivated = startActivated;

            // 비활성이면 NavMeshAgent 정지 (안 그러면 IdleState.Enter에서 이미 멈췄지만 이중 안전)
            if (!_isActivated && Agent != null && Agent.isOnNavMesh)
            {
                Agent.isStopped = true;
                Agent.ResetPath();
            }
        }

        /// <summary>
        /// 보스룸 진입 시 호출 (BossRoomTrigger가 발동).
        /// 비활성 상태에서 활성 상태로 전환. 이후 State 머신 / Update 로직이 작동.
        /// 사망 후 호출되면 무시.
        /// </summary>
        public void Activate()
        {
            if (_isActivated) return;
            if (_isDead) return;

            _isActivated = true;

            Debug.Log($"[Boss] Activated.");
        }

        private void Update()
        {
            // 사망 처리됐으면 모든 로직 차단. 시체는 Death 애니메이션만 재생되고 정지.
            if (_isDead) return;

            // 비활성 상태 (보스룸 진입 전): State 머신 / AI 모두 정지.
            // Animator만 살아있어서 기본 Idle 클립이 자동 재생됨. 다크소울 정석.
            if (!_isActivated) return;

            // 페이즈 전환 체크 (1회만)
            CheckPhaseTransition();

            _currentState?.Update();

            // Animator 보간은 컨트롤러 레벨에서 매 프레임 (State가 바뀌어도 끊김 없음)
            UpdateAnimator();
        }

        /// <summary>
        /// 페이즈 전환 흐름 (옵션 B — 공격 끝난 후부터 카운트):
        /// 1. 체력 임계치 도달 → _roarPending = true (카운트는 아직)
        /// 2. pending 상태에서 공격 끝나는 순간 → _roarReadyTime 설정 (이때부터 카운트)
        /// 3. readyTime 만료 + 공격 중 아님 → 발동
        ///
        /// 의도: 기존 패턴들의 글로벌 쿨다운(공격 끝난 후 1.5~3초)과 일관된 리듬.
        /// 보스가 50% 깎인 후에도 진행 중 공격은 자연 종료, 그 후 한 박자 쉬다가 포효.
        /// 1회만 발동.
        /// </summary>
        private void CheckPhaseTransition()
        {
            if (_hasRoared) return;
            if (Health == null || Health.MaxHealth <= 0f) return;

            // 1단계: 임계치 도달 감지 → pending 마킹 (카운트는 아직 시작 안 함)
            if (!_roarPending)
            {
                float ratio = Health.CurrentHealth / Health.MaxHealth;
                if (ratio <= phaseTransitionHealthRatio)
                {
                    _roarPending = true;

                    // 1회성 이벤트라 항상 출력
                    Debug.Log($"[Boss] Phase transition pending. Roar will trigger after current action ends.");
                }
                return;
            }

            // 2단계: pending인데 카운트 아직 시작 안 함 → 공격/HitReact 끝난 시점에 카운트 시작
            // HitReact 중에 50% 통과해도 일단 그 비틀거림이 끝나야 페이즈 전환 카운트 시작.
            bool isBusy = _currentState is BossAttackState || _currentState is BossHitReactState;
            if (_roarReadyTime == float.MaxValue && !isBusy)
            {
                float delay = Random.Range(minPhaseTransitionDelay, maxPhaseTransitionDelay);
                _roarReadyTime = Time.time + delay;

                // 1회성 이벤트라 항상 출력
                Debug.Log($"[Boss] Phase transition countdown started: {delay:F1}s");
            }

            // 3단계: 카운트 만료 + busy 아님 → 발동
            // (카운트 도중에 새 공격/HitReact 시작되면 isBusy=true → 대기. 그게 끝난 후 다시 체크 시 통과)
            if (Time.time < _roarReadyTime) return;
            if (isBusy) return;
            if (_currentState is BossPhaseTransitionState) return;

            // 통과 — 발동
            _hasRoared = true;
            ChangeState(PhaseTransitionState);
        }

        /// <summary>
        /// State 전이.
        /// </summary>
        /// <summary>
        /// State 전이.
        /// 페이즈 전환 중에는 외부에서의 어떤 전이도 거부 (PhaseTransitionState 자신이 끝낼 때만 전이 가능).
        /// </summary>
        public void ChangeState(BossStateBase newState)
        {
            if (newState == null) return;
            if (_currentState == newState) return;

            // 죽었으면 어떤 전이도 거부. 시체 안 깨어남.
            if (_isDead) return;

            // 페이즈 전환 중에는 PhaseTransitionState 자신만 빠져나갈 수 있음
            // (다른 State에서 들어오려는 시도 차단)
            if (_currentState is BossPhaseTransitionState && newState != ChaseState)
            {
                if (drawDebugInfo)
                    Debug.Log($"[Boss] State change blocked (phase transition in progress)");
                return;
            }

            // HitReact 진행 중에도 외부 전이 차단. ChaseState로의 복귀만 허용 (자기 자신이 끝낼 때).
            // 페이즈 전환은 CheckPhaseTransition이 HitReact 끝난 후 자연스럽게 발동하므로
            // 여기서 명시적으로 허용할 필요 없음 (_roarPending 카운트가 isInAttack/Phase 외 상태에선 진행됨).
            if (_currentState is BossHitReactState && newState != ChaseState)
            {
                if (drawDebugInfo)
                    Debug.Log($"[Boss] State change blocked (hit react in progress)");
                return;
            }

            if (drawDebugInfo)
                Debug.Log($"[Boss] State: {_currentState?.GetType().Name ?? "null"} → {newState.GetType().Name}");

            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        /// <summary>
        /// Animator MoveX/MoveZ 보간. 2D Blend Tree 입력.
        ///
        /// 흐름:
        /// 1. Agent.velocity를 보스 forward 기준 로컬 좌표로 변환
        ///    → 이동 중일 때 자동으로 전진/strafe 구분됨
        /// 2. 실제 속도가 거의 0인데 회전 필요한 상황(=제자리 정면 추적)이면
        ///    플레이어 좌우 위치 기준으로 '가짜 strafe' 입력 생성
        ///    → 보스가 회전 모션 + 옆걸음 클립을 같이 재생해 자연스러움
        /// 3. SmoothDamp로 보간 후 적용
        /// </summary>
        private void UpdateAnimator()
        {
            // 1) 실제 이동을 로컬 좌표로 분해 (월드 → 보스 forward 기준)
            Vector3 worldVelocity = Agent.enabled ? Agent.velocity : Vector3.zero;
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            float targetX = localVelocity.x / chaseSpeed;
            float targetZ = localVelocity.z / chaseSpeed;

            // 2) 거의 정지 상태인데 플레이어가 옆에 있으면 가짜 strafe 의도 추가
            //    (제자리에서 마네킹처럼 휙 도는 거 방지)
            //    플레이어 사망 후엔 시체 위치에 반응할 이유가 없으므로 스킵 →
            //    안 그러면 IdleState여도 시체 위치 기반 MoveX가 계속 생겨 Strafe(Turn) 모션 무한 재생.
            if (worldVelocity.magnitude < idleVelocityThreshold && player != null && !_isPlayerDead)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    toPlayer.Normalize();

                    // 플레이어가 정면에서 얼마나 벗어났나 (각도)
                    float forwardDot = Vector3.Dot(transform.forward, toPlayer);
                    float angleFromForward = Mathf.Acos(Mathf.Clamp(forwardDot, -1f, 1f)) * Mathf.Rad2Deg;

                    if (angleFromForward > strafeAngleThreshold)
                    {
                        // 좌/우 어느 쪽인지 부호 결정 (right와 dot — 양수=오른쪽)
                        float sideDot = Vector3.Dot(transform.right, toPlayer);
                        targetX = Mathf.Sign(sideDot) * strafeIntent;
                        // Z는 0 유지 (옆걸음만)
                    }
                }
            }

            // 3) 보간
            _animMoveX = Mathf.SmoothDamp(_animMoveX, targetX, ref _animMoveXVelocity, animationSmoothTime);
            _animMoveZ = Mathf.SmoothDamp(_animMoveZ, targetZ, ref _animMoveZVelocity, animationSmoothTime);

            Animator.SetFloat(MoveXHash, _animMoveX);
            Animator.SetFloat(MoveZHash, _animMoveZ);
        }

        /// <summary>
        /// 플레이어를 향해 부드럽게 회전. State들이 호출.
        /// speedMultiplier로 회전 강도 조절 가능 (공격 wind-up에서 약하게 추적할 때).
        /// </summary>
        public void RotateTowardsPlayer(float speedMultiplier = 1f)
        {
            if (player == null) return;

            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, rotationSpeed * speedMultiplier * Time.deltaTime);
        }

        // === Animation Event 중계 ===
        // 보스의 공격 애니메이션 클립에서 호출됨.
        // Animator가 붙은 GameObject(=BossController가 붙은 GameObject)의
        // 컴포넌트 메서드만 호출 가능하므로, 여기서 무기로 중계한다.
        //
        // 다중 히트박스 지원: 패턴이 Enter에서 SetActiveHitboxIndex로
        // 어떤 히트박스를 쓸지 정하면, Animation Event는 ActiveHitbox를 통해 작동.

        /// <summary>
        /// 현재 활성 히트박스. 패턴이 SetActiveHitboxIndex로 바꿈.
        /// </summary>
        public LostSouls.Combat.WeaponHitbox ActiveHitbox { get; private set; }

        /// <summary>
        /// 활성 히트박스를 변경. 패턴 Enter에서 호출.
        /// </summary>
        public void SetActiveHitboxIndex(int index)
        {
            if (hitboxes == null || hitboxes.Length == 0)
            {
                Debug.LogWarning($"[{name}] SetActiveHitboxIndex: hitboxes 비어있음");
                return;
            }
            if (index < 0 || index >= hitboxes.Length)
            {
                Debug.LogWarning($"[{name}] SetActiveHitboxIndex: index {index} 범위 밖 (0~{hitboxes.Length - 1})");
                return;
            }
            ActiveHitbox = hitboxes[index];
        }

        /// <summary>
        /// 공격 애니메이션 중 히트박스가 활성화되는 프레임에서 호출.
        /// 매개변수 없음: ActiveHitbox 사용 (패턴이 Enter에서 지정한 기본 히트박스).
        /// </summary>
        public void OnEnableHitbox()
        {
            if (ActiveHitbox != null) ActiveHitbox.EnableHitbox();
        }

        /// <summary>
        /// 공격 애니메이션 중 히트박스 비활성 프레임에서 호출.
        /// 매개변수 없음: ActiveHitbox 사용.
        /// </summary>
        public void OnDisableHitbox()
        {
            if (ActiveHitbox != null) ActiveHitbox.DisableHitbox();
        }

        /// <summary>
        /// 인덱스 명시 버전. Animation Event에서 int 매개변수로 호출.
        /// 예: 충격파 같은 보조 히트박스를 패턴 중간에 켜기.
        /// ActiveHitbox는 갱신하지 않음 (패턴 기본 히트박스는 그대로 유지).
        /// </summary>
        public void OnEnableHitboxIndex(int index)
        {
            if (hitboxes == null || index < 0 || index >= hitboxes.Length)
            {
                Debug.LogWarning($"[{name}] OnEnableHitboxIndex: index {index} 범위 밖");
                return;
            }
            hitboxes[index].EnableHitbox();
        }

        /// <summary>
        /// 인덱스 명시 버전 비활성.
        /// </summary>
        public void OnDisableHitboxIndex(int index)
        {
            if (hitboxes == null || index < 0 || index >= hitboxes.Length)
            {
                Debug.LogWarning($"[{name}] OnDisableHitboxIndex: index {index} 범위 밖");
                return;
            }
            hitboxes[index].DisableHitbox();
        }

        /// <summary>
        /// 히트박스의 파티클만 재생 (콜라이더는 건드리지 않음).
        /// 시각 효과를 데미지 판정보다 먼저 시작하고 싶을 때 Animation Event로 호출.
        /// </summary>
        public void OnPlayHitboxEffect(int index)
        {
            if (hitboxes == null || index < 0 || index >= hitboxes.Length)
            {
                Debug.LogWarning($"[{name}] OnPlayHitboxEffect: index {index} 범위 밖");
                return;
            }
            hitboxes[index].PlayEffect();
        }

        /// <summary>
        /// 모든 히트박스 강제 끄기. Animation Event 누락 등 비정상 종료 대비 안전망.
        /// 패턴 Exit에서 호출.
        /// </summary>
        public void DisableAllHitboxes()
        {
            if (hitboxes == null) return;
            foreach (var h in hitboxes)
            {
                if (h != null) h.DisableHitbox();
            }
        }

        /// <summary>
        /// 공격/포효/HitReact 애니메이션 마지막 프레임에서 호출.
        /// 현재 State에 따라 적절한 메서드로 중계.
        /// </summary>
        public void OnAttackAnimationEnd()
        {
            if (_currentState is BossAttackState atk)
            {
                atk.NotifyAnimationEnd();
            }
            else if (_currentState is BossPhaseTransitionState phase)
            {
                phase.NotifyAnimationEnd();
            }
            else if (_currentState is BossHitReactState hit)
            {
                hit.NotifyAnimationEnd();
            }
        }

        /// <summary>
        /// Death 애니메이션 마지막 프레임에서 호출. Animation Event로 박아둠.
        /// Animator를 끄면 마지막 자세에서 동결됨 — root motion 영향 차단 + 성능 절약.
        /// 이후 시체는 정적 메시처럼 남는다.
        ///
        /// 주의: 이 시점 전에 클립의 Root Transform Position(Y)를 Bake Into Pose해서
        /// 시체가 바닥에 묻히지 않도록 조정해둬야 함 (Inspector 작업).
        /// </summary>
        public void OnDeathAnimationEnd()
        {
            if (!_isDead) return;  // 사망 처리 안 됐는데 호출되면 무시 (방어)

            if (drawDebugInfo)
                Debug.Log($"[Boss] Death animation finished. Freezing pose.");

            // 위치 잠금 해제 (어차피 Animator도 끌 거라 이중 안전망)
            _lockDeathPose = false;
            Animator.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugInfo) return;

            // Aggro range (노랑) - 추적 시작 거리
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);

            // Attack range (빨강) - 공격 시도 거리
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}