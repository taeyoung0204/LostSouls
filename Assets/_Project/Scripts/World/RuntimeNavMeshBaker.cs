using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace LostSouls.World
{
    /// <summary>
    /// 게임 시작 시 NavMeshSurface를 런타임 베이크하고, 검증 후 NavMeshAgent들을 활성화한다.
    ///
    /// ★타이밍이 핵심★:
    /// - 이전 버전은 [DefaultExecutionOrder(-10000)] + Awake에서 베이크 → '너무 일러서'
    ///   다른 오브젝트(바닥 Collider 등)가 아직 씬에 준비되기 전에 구워 빈 NavMesh(삼각형 0개)가 나왔다.
    /// - 수정: 베이크는 Start에서 (모든 Awake 완료 = 지오메트리 준비됨).
    ///   단 NavMeshAgent는 Awake에서 미리 꺼두어, 베이크 전에 NavMesh 등록을 시도하다 크래시나는 걸 막는다.
    ///   베이크 완료 후 Agent를 켠다.
    ///
    /// 순서:
    ///   Awake: Agent들 enabled=false (NavMesh 등록 차단)
    ///   Start: 베이크 → 검증(삼각형 수 로그) → Agent들 enabled=true
    ///
    /// ★검증 로그★:
    /// - 베이크 후 NavMesh.CalculateTriangulation()으로 삼각형 수를 로그로 찍는다.
    ///   0이면 빈 베이크, >0이면 성공. 빌드/에디터 로그로 즉시 판별.
    ///
    /// 전제:
    /// - NavMeshSurface Use Geometry = Physics Colliders, 바닥에 Collider 존재.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        [Header("Target Surface")]
        [SerializeField] private NavMeshSurface surface;

        [Header("Agents")]
        [Tooltip("베이크 후 활성화할 NavMeshAgent들. 비우면 씬 전체 자동 수집.")]
        [SerializeField] private List<NavMeshAgent> agentsToEnable = new List<NavMeshAgent>();

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = true;

        private bool _baked;

        private void Awake()
        {
            ResolveSurface();
            CollectAgentsIfEmpty();

            // Agent 먼저 끔 — 베이크(Start) 전에 NavMesh 등록 시도 차단.
            // DefaultExecutionOrder(-10000)로 다른 스크립트보다 먼저 실행되어 확실히 꺼둠.
            SetAgentsEnabled(false);
        }

        private void Start()
        {
            // 모든 Awake 완료 후 = 바닥 Collider 등 지오메트리 준비됨 → 이제 베이크.
            Bake();
            VerifyNavMesh();
            SetAgentsEnabled(true);
        }

        private void ResolveSurface()
        {
            if (surface != null) return;
            surface = GetComponent<NavMeshSurface>();
            if (surface != null) return;
            surface = GetComponentInParent<NavMeshSurface>(includeInactive: true);
            if (surface != null) return;
            surface = FindAnyObjectByType<NavMeshSurface>(FindObjectsInactive.Include);
        }

        private void CollectAgentsIfEmpty()
        {
            if (agentsToEnable != null && agentsToEnable.Count > 0) return;
            agentsToEnable = new List<NavMeshAgent>(
                FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (drawDebugInfo)
                Debug.Log($"[RuntimeNavMeshBaker] Agent 자동 수집: {agentsToEnable.Count}개");
        }

        private void SetAgentsEnabled(bool enabled)
        {
            if (agentsToEnable == null) return;
            foreach (var agent in agentsToEnable)
            {
                if (agent == null) continue;
                agent.enabled = enabled;
            }
        }

        private void Bake()
        {
            if (_baked) return;

            if (surface == null)
            {
                Debug.LogError("[RuntimeNavMeshBaker] NavMeshSurface 못 찾음. surface 슬롯에 직접 할당하라.");
                return;
            }

            if (!surface.gameObject.activeInHierarchy)
                Debug.LogWarning("[RuntimeNavMeshBaker] NavMeshSurface GameObject 비활성. 베이크 비어있을 수 있음.");

            surface.BuildNavMesh();
            _baked = true;

            if (drawDebugInfo)
                Debug.Log($"[RuntimeNavMeshBaker] BuildNavMesh 호출 완료 (surface: {surface.name}).");
        }

        private void VerifyNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int triCount = tri.indices != null ? tri.indices.Length / 3 : 0;
            int vertCount = tri.vertices != null ? tri.vertices.Length : 0;

            if (triCount > 0)
            {
                Debug.Log($"[RuntimeNavMeshBaker] ★NavMesh 검증 성공★ 삼각형 {triCount}개, 정점 {vertCount}개.");
            }
            else
            {
                Debug.LogError("[RuntimeNavMeshBaker] ★NavMesh 검증 실패★ 삼각형 0개 (빈 NavMesh). " +
                               "원인 후보: NavMeshSurface Transform이 바닥에서 너무 떨어짐 / " +
                               "Collect Objects 범위 밖 / Collider가 베이크에 안 잡힘.");
            }
        }
    }
}