using UnityEngine;
using UnityEngine.InputSystem;
using LostSouls.UI;

namespace LostSouls.World
{
    /// <summary>
    /// 보스룸 입구 표지판. 플레이어가 가까이 가서 E 키 누르면 튜토리얼 패널 열림.
    ///
    /// 구조:
    /// - GameObject에 Trigger Collider (SphereCollider 등) 부착
    /// - 플레이어가 트리거 들어오면: InteractionPromptUI에 "Press E" 표시
    /// - 플레이어가 트리거 안에서 E 누르면: TutorialPanelController.Open() 호출
    /// - 플레이어가 트리거 나가면: 프롬프트 숨김
    ///
    /// 패널 열린 상태에서는 트리거 이벤트 무시 (이중 호출 방지).
    /// </summary>
    public class SignpostInteractable : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("플레이어 식별용 태그. 보통 'Player'.")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("프롬프트에 표시할 텍스트. 다국어/디자인 따라 변경.")]
        [SerializeField] private string promptMessage = "Press E to read";

        [Header("References")]
        [Tooltip("열어줄 튜토리얼 패널 컨트롤러. 비우면 씬에서 자동 검색.")]
        [SerializeField] private TutorialPanelController tutorialPanel;

        // E 키 액션 — 플레이어가 트리거 안에 있을 때만 활성.
        // PauseMenu의 ESC처럼 PlayerInputActions와 독립적으로 작동.
        // 마우스 입력 차단된 상태에서도 E는 키보드라 통과 — OK.
        private InputAction _interactAction;

        // 플레이어가 트리거 범위 안에 있는지
        private bool _playerInRange;

        private void Awake()
        {
            if (tutorialPanel == null)
                tutorialPanel = FindAnyObjectByType<TutorialPanelController>();

            _interactAction = new InputAction(name: "SignpostInteract", type: InputActionType.Button,
                                              binding: "<Keyboard>/e");
            _interactAction.performed += OnInteractPressed;
        }

        private void OnEnable()
        {
            // 플레이어가 범위 안에 있을 때만 Enable되도록 설계.
            // 시작 시점엔 범위 밖이라 비활성.
            _interactAction?.Disable();
        }

        private void OnDisable()
        {
            _interactAction?.Disable();
        }

        private void OnDestroy()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteractPressed;
                _interactAction.Dispose();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = true;

            // 패널 이미 열려있으면 프롬프트 안 띄움 (혼란 방지)
            if (tutorialPanel != null && tutorialPanel.IsOpen) return;

            if (InteractionPromptUI.Instance != null)
                InteractionPromptUI.Instance.Show(promptMessage);

            _interactAction?.Enable();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = false;

            if (InteractionPromptUI.Instance != null)
                InteractionPromptUI.Instance.Hide();

            _interactAction?.Disable();
        }

        private void OnInteractPressed(InputAction.CallbackContext context)
        {
            if (!_playerInRange) return;
            if (tutorialPanel == null) return;
            if (tutorialPanel.IsOpen) return;  // 이미 열려있으면 무시 (E 키가 닫기로 해석되어야)

            // 프롬프트 숨김 (패널이 열리니까)
            if (InteractionPromptUI.Instance != null)
                InteractionPromptUI.Instance.Hide();

            // 인터랙트 액션 잠시 비활성 — 같은 E 키가 패널 닫기로 즉시 해석되지 않게.
            // 패널 닫혀서 OnTriggerExit이 발생하지 않으면(플레이어가 범위 유지) 다시 Enable되어야 하므로
            // TutorialPanelController는 닫힐 때 별도 신호를 주지 않음 → 여기서 Update로 IsOpen 폴링.
            _interactAction?.Disable();

            tutorialPanel.Open();
        }

        private void Update()
        {
            // 패널 닫혔는데 플레이어가 아직 범위 안에 있으면 인터랙트 액션 재활성 + 프롬프트 다시 표시.
            // (트리거 이벤트는 패널 열린 동안 발생하지 않음 — 플레이어가 안 움직였으니)
            if (_playerInRange && tutorialPanel != null && !tutorialPanel.IsOpen)
            {
                if (!_interactAction.enabled)
                {
                    _interactAction.Enable();
                    if (InteractionPromptUI.Instance != null)
                        InteractionPromptUI.Instance.Show(promptMessage);
                }
            }
        }
    }
}