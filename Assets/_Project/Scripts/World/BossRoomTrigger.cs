using UnityEngine;
using LostSouls.Boss;

namespace LostSouls.World
{
    /// <summary>
    /// 보스룸 입구에 배치되는 진입 트리거.
    /// 플레이어가 통과하면 보스 AI를 활성화하고 BossHUD를 표시한다.
    ///
    /// 보스 GameObject 자체는 처음부터 활성 — Idle 클립 재생하며 가만히 서있음 (다크소울 정석).
    /// 트리거 통과 시 BossController.Activate() 호출 → State 머신/AI 깨어남.
    ///
    /// 1회만 발동. 사용자가 보스를 죽이고 다시 입장해도 재발동 없음.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossRoomTrigger : MonoBehaviour
    {
        [Header("Activation Targets")]
        [Tooltip("플레이어 진입 시 활성화할 BossController. AI/State 머신만 깨어남 (GameObject는 처음부터 활성).")]
        [SerializeField] private BossController bossController;
        [Tooltip("플레이어 진입 시 활성화할 BossHUD GameObject.")]
        [SerializeField] private GameObject bossHUD;

        [Header("Detection")]
        [Tooltip("플레이어 판별용 태그. PlayerLockOn 등에서도 쓰는 동일한 태그.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Debug")]
        [SerializeField] private bool drawDebugInfo = false;

        private bool _activated;

        private void Awake()
        {
            // 시작 시 HUD 비활성 (보스 자체는 활성 유지 — Idle 클립으로 가만히 서있음)
            if (bossHUD != null) bossHUD.SetActive(false);

            // 자기 Collider가 트리거인지 검증
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[{name}] BossRoomTrigger의 Collider가 IsTrigger=false. 인스펙터에서 체크해라.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_activated) return;
            if (!other.CompareTag(playerTag)) return;

            Activate();
        }

        private void Activate()
        {
            _activated = true;

            // BossController에 활성화 신호 — AI/State 머신 깨어남
            if (bossController != null) bossController.Activate();

            // HUD 표시 시작
            if (bossHUD != null) bossHUD.SetActive(true);

            Debug.Log($"[BossRoom] Player entered. Boss + HUD activated.");

            // 트리거 자체는 비활성. 다시 발동되거나 OnTriggerExit가 호출되지 않도록.
            gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugInfo) return;

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.color = _activated ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 1f, 0f, 0.3f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = _activated ? Color.green : Color.yellow;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}