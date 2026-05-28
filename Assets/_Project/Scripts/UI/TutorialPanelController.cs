using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 보스룸 입구 표지판 상호작용 시 표시되는 튜토리얼 패널.
    /// 키 조작 설명을 화면에 띄우고, 사용자가 닫기 전까지 마우스 입력 차단.
    ///
    /// 흐름:
    /// 1. SignpostInteractable이 외부에서 Open() 호출
    /// 2. 패널 표시 + 마우스 입력 차단 + 커서 노출
    /// 3. E 키 / ESC 키 / 닫기 버튼 중 하나로 Close
    /// 4. 마우스 위치 원복 (메뉴 열기 전 자리로 워프) + 입력 복구
    ///
    /// PauseMenu와 동일 패턴 — 게임 일시정지 X, 보스 계속 공격, 카메라만 멈춤.
    /// 게임이 안 멈추는 디자인이라 패널 열기/닫기 SFX는 사용하지 않음 (PauseMenu와 일관).
    /// 닫기 버튼도 별도 SFX 없음 — 닫기는 명시적 의도라 굳이 사운드로 강조할 필요 없음.
    /// </summary>
    public class TutorialPanelController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("패널 GameObject. 시작 시 비활성 상태여야 함.")]
        [SerializeField] private GameObject panel;
        [Tooltip("닫기 버튼.")]
        [SerializeField] private Button closeButton;

        [Header("References")]
        [Tooltip("마우스 입력 차단할 PlayerController. 비우면 자동 검색.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("패널 표시 중 비활성할 카메라 컴포넌트들 (CinemachineInputAxisController). " +
                 "PauseMenuController에 연결한 것과 동일.")]
        [SerializeField] private List<Behaviour> cameraInputComponentsToDisable = new List<Behaviour>();

        // 닫기용 InputAction (E + ESC). 패널 열려있을 때만 활성.
        private InputAction _closeEAction;
        private InputAction _closeEscAction;

        // 마우스 위치 원복용
        private Vector2 _mousePositionBeforeOpen;

        // 열림 상태
        private bool _isOpen;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindAnyObjectByType<PlayerController>();

            if (panel != null) panel.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            // 닫기 키 액션 — 패널 열려있을 때만 Enable, 닫혀있을 땐 Disable
            // E와 ESC 둘 다 지원
            _closeEAction = new InputAction(name: "TutorialClose_E", type: InputActionType.Button,
                                            binding: "<Keyboard>/e");
            _closeEscAction = new InputAction(name: "TutorialClose_Esc", type: InputActionType.Button,
                                              binding: "<Keyboard>/escape");
            _closeEAction.performed += _ => Close();
            _closeEscAction.performed += _ => Close();
        }

        private void OnDestroy()
        {
            _closeEAction?.Dispose();
            _closeEscAction?.Dispose();
        }

        // ========== 외부 API ==========

        /// <summary>SignpostInteractable이 호출. 패널 열기.</summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            // 마우스 위치 기억 (닫을 때 여기로 워프)
            if (Mouse.current != null)
                _mousePositionBeforeOpen = Mouse.current.position.ReadValue();

            if (panel != null) panel.SetActive(true);

            // 마우스 입력 차단
            if (playerController != null) playerController.SetMouseInputEnabled(false);
            SetCameraInputComponents(false);

            // 커서 노출
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 닫기 액션 활성화
            _closeEAction?.Enable();
            _closeEscAction?.Enable();
        }

        /// <summary>패널 닫기. 키, 버튼, 외부 호출 어디서나.</summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            // 닫기 액션 먼저 비활성 (E 키가 게임 내 다른 액션과 충돌 안 하게)
            _closeEAction?.Disable();
            _closeEscAction?.Disable();

            if (panel != null) panel.SetActive(false);

            // 마우스 위치 원복 (PauseMenu와 동일 — 카메라 점프 방지)
            if (Mouse.current != null)
                Mouse.current.WarpCursorPosition(_mousePositionBeforeOpen);

            // 커서 잠금 + 숨김
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // 한 프레임 지연 후 입력 복구 (PauseMenu와 동일 패턴)
            StartCoroutine(RestoreInputNextFrame());
        }

        private IEnumerator RestoreInputNextFrame()
        {
            yield return null;

            if (playerController != null) playerController.SetMouseInputEnabled(true);
            SetCameraInputComponents(true);
        }

        private void SetCameraInputComponents(bool enabled)
        {
            if (cameraInputComponentsToDisable == null) return;
            foreach (var c in cameraInputComponentsToDisable)
            {
                if (c != null) c.enabled = enabled;
            }
        }
    }
}