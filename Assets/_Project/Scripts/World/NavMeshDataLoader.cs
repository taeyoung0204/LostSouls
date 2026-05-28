using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LostSouls.World
{
    /// <summary>
    /// 미리 구운 NavMeshData를 직접 참조로 로드하고, 그 다음에 'NavMeshAgent를 가진 오브젝트들'을
    /// GameObject.SetActive(true)로 활성화한다.
    ///
    /// 왜 GameObject를 끄고 켜나 (이전 모든 시도가 실패한 진짜 이유):
    /// - NavMeshAgent는 네이티브 컴포넌트라, Agent가 붙은 GameObject가 '씬 로드 시 활성 상태'면
    ///   씬 로드 순간(우리 어떤 스크립트 Awake보다도 먼저) NavMesh에 등록을 시도한다.
    ///   이때 NavMesh가 아직 없으면 "Failed to create agent" → 빌드에서 엔진 크래시(UnityPlayer ACCESS_VIOLATION).
    /// - agent.enabled=false로는 이 '초기 등록'을 못 막는다. 우리 코드가 돌기 전에 이미 일어나기 때문.
    /// - 유일하게 확실한 차단: Agent를 가진 GameObject(보스)를 '씬에 비활성으로 저장'해 두는 것.
    ///   그러면 씬 로드 시 Agent가 아예 안 깨어난다. NavMesh 로드 후 이 스크립트가 SetActive(true)로 켠다.
    ///
    /// 사용 절차:
    /// 1. 씬에서 보스(Boss_Brute) GameObject를 '비활성(체크 OFF)'으로 저장. ★필수★
    /// 2. 이 스크립트의 navMeshData에 미리 구운 NavMesh-*.asset 할당.
    /// 3. objectsToActivate에 보스 GameObject를 할당.
    /// 4. Awake에서: NavMesh 로드 → 보스 SetActive(true).
    ///
    /// DefaultExecutionOrder(-10000): 다른 스크립트보다 먼저 실행 (NavMesh를 가장 먼저 준비).
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class NavMeshDataLoader : MonoBehaviour
    {
        [Header("NavMesh Data (필수)")]
        [Tooltip("미리 구운 NavMeshData 에셋. Project에서 NavMesh-*.asset을 드래그. 직접 참조라 빌드 포함 보장.")]
        [SerializeField] private NavMeshData navMeshData;

        [Tooltip("NavMesh 배치 기준 Transform. 데이터 구울 때의 위치/회전과 일치해야 함. 비우면 이 오브젝트.")]
        [SerializeField] private Transform surfaceTransform;

        [Header("Objects To Activate (필수)")]
        [Tooltip("NavMesh 로드 후 SetActive(true)로 켤 GameObject들. " +
                 "NavMeshAgent를 가진 오브젝트(보스)를 여기 넣고, 씬에서는 '비활성'으로 저장해 둔다. " +
                 "이게 핵심 — Agent가 씬 로드 시 NavMesh보다 먼저 깨어나는 걸 막는다.")]
        [SerializeField] private List<GameObject> objectsToActivate = new List<GameObject>();

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = true;

        private NavMeshDataInstance _instance;

        private void Awake()
        {
            // 1) NavMesh 먼저 로드 (보스 켜기 전에 NavMesh 존재 보장).
            LoadNavMesh();
            VerifyNavMesh();

            // 2) NavMesh 준비됐으니 이제 보스 등 Agent 오브젝트 활성화.
            //    이 오브젝트들은 씬에 '비활성'으로 저장돼 있어야 한다 (그래야 씬 로드 시 Agent 안 깨어남).
            ActivateObjects();
        }

        private void OnDestroy()
        {
            if (_instance.valid)
                _instance.Remove();
        }

        private void LoadNavMesh()
        {
            if (navMeshData == null)
            {
                Debug.LogError("[NavMeshDataLoader] navMeshData 슬롯 비어있음. NavMesh-*.asset을 드래그하라.");
                return;
            }

            Transform t = surfaceTransform != null ? surfaceTransform : transform;
            _instance = NavMesh.AddNavMeshData(navMeshData, t.position, t.rotation);

            if (drawDebugInfo)
                Debug.Log($"[NavMeshDataLoader] NavMeshData 로드 완료 (pos: {t.position}, valid: {_instance.valid}).");
        }

        private void VerifyNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int triCount = tri.indices != null ? tri.indices.Length / 3 : 0;

            if (triCount > 0)
                Debug.Log($"[NavMeshDataLoader] ★NavMesh 검증 성공★ 삼각형 {triCount}개.");
            else
                Debug.LogError("[NavMeshDataLoader] ★NavMesh 검증 실패★ 삼각형 0개.");
        }

        private void ActivateObjects()
        {
            if (objectsToActivate == null || objectsToActivate.Count == 0)
            {
                Debug.LogWarning("[NavMeshDataLoader] objectsToActivate가 비어있음. " +
                                 "보스 GameObject를 여기 넣고 씬에서 비활성으로 저장해야 Agent 크래시를 막을 수 있음.");
                return;
            }

            foreach (var go in objectsToActivate)
            {
                if (go == null) continue;
                go.SetActive(true);
                if (drawDebugInfo)
                    Debug.Log($"[NavMeshDataLoader] 활성화: {go.name}");
            }
        }
    }
}