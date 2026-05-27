using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// ESC 키로 열고 닫는 인게임 메뉴.
    ///
    /// 핵심 결정사항:
    /// - 게임 일시정지 X (Time.timeScale 변경하지 않음). 보스 계속 공격.
    /// - 마우스 입력 전체 차단 (Look + LightAttack + HeavyAttack).
    ///   → 메뉴 위에서 클릭만 가능. 카메라 회전/공격 불가.
    /// - 키보드 입력은 정상 작동 (WASD/Sprint/Roll/UsePotion/LockOn).
    ///   → 플레이어는 메뉴 띄운 채로 이동/회피/포션 계속 가능.
    /// - 메뉴 닫을 때 마우스 커서 다시 잠금/숨김.
    /// - 메뉴 구성: Resume (닫기) / Return to Title.
    ///
    /// 플레이어가 사망 상태일 때는 ESC 무시 (GameOverUI가 우선).
    ///
    /// 차단 방식 2중:
    /// - PlayerController.SetMouseInputEnabled(false): Look/LightAttack/HeavyAttack 비활성
    /// - 카메라의 CinemachineInputAxisController 컴포넌트 enabled=false (안전망)
    /// 둘 중 하나만 효과적이어도 카메라 회전 멈춤. Cinemachine 버전/설정에 따라 작동 방식 다를 수 있어 둘 다 처리.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("메뉴 패널 (배경 + 버튼들). 시작 시 비활성 상태여야 함.")]
        [SerializeField] private GameObject menuPanel;
        [Tooltip("Resume 버튼 — 메뉴 닫고 게임으로 복귀.")]
        [SerializeField] private Button resumeButton;
        [Tooltip("타이틀로 돌아가기 버튼.")]
        [SerializeField] private Button returnToTitleButton;

        [Header("References")]
        [Tooltip("입력 차단할 PlayerController. 비우면 씬에서 자동 검색.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("사망 상태 체크할 PlayerHealth. ESC를 사망 시점에 누르면 무시. 비우면 자동 검색.")]
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("메뉴 열 때 비활성할 카메라 컴포넌트들 (보통 CinemachineInputAxisController). " +
                 "PlayerInputActions.Look 비활성만으로 카메라가 안 멈출 때 대비. 비워둬도 무방.")]
        [SerializeField] private List<Behaviour> cameraInputComponentsToDisable = new List<Behaviour>();

        [Header("Scene")]
        [Tooltip("Return to Title 클릭 시 로드할 씬 이름.")]
        [SerializeField] private string titleSceneName = "TitleScene";

        // ESC 키만 위한 별도 InputAction. PlayerInputActions에 추가하지 않고 여기서 자체 정의 —
        // 마우스가 차단되어도 ESC(키보드)는 받아야 메뉴 닫을 수 있음.
        private InputAction _menuToggleAction;

        // 메뉴 열림 상태
        private bool _isOpen;

        // 메뉴 열기 직전 마우스 화면 좌표. 메뉴 닫을 때 이 위치로 워프 →
        // Cinemachine이 delta=0으로 인식 → 카메라 점프 없음.
        private Vector2 _mousePositionBeforeMenu;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            // 시작 상태: 메뉴 닫혀있음
            if (menuPanel != null)
                menuPanel.SetActive(false);

            // 버튼 이벤트
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Close);
            if (returnToTitleButton != null)
                returnToTitleButton.onClick.AddListener(ReturnToTitle);

            // ESC 키 액션 자체 정의 — 플레이어 InputActions와 독립적으로 작동
            _menuToggleAction = new InputAction(name: "MenuToggle", type: InputActionType.Button,
                                                binding: "<Keyboard>/escape");
            _menuToggleAction.performed += OnMenuTogglePressed;
        }

        private void Start()
        {
            // 게임 시작 시 커서 잠금 (TitleScene → GameScene 전환 시 커서가 풀려있는 상태).
            // 어디서도 잠가주는 코드가 없으면 여기서 책임.
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnEnable()
        {
            _menuToggleAction?.Enable();
        }

        private void OnDisable()
        {
            _menuToggleAction?.Disable();
        }

        private void OnDestroy()
        {
            if (_menuToggleAction != null)
            {
                _menuToggleAction.performed -= OnMenuTogglePressed;
                _menuToggleAction.Dispose();
            }
        }

        private void OnMenuTogglePressed(InputAction.CallbackContext context)
        {
            // 사망 상태면 무시 (GameOverUI가 자동 복귀 처리 중)
            if (playerHealth != null && playerHealth.IsDead) return;

            if (_isOpen) Close();
            else Open();
        }

        // ========== 열기 / 닫기 ==========

        private void Open()
        {
            _isOpen = true;

            // 마우스 현재 위치 기억 (메뉴 닫을 때 여기로 복귀시켜 카메라 점프 방지).
            // CursorLockMode.Locked 상태에서는 위치가 화면 중앙이지만, Mouse.current.position.value로 안전하게 읽음.
            if (Mouse.current != null)
                _mousePositionBeforeMenu = Mouse.current.position.ReadValue();

            if (menuPanel != null) menuPanel.SetActive(true);

            // 마우스 입력 전체 차단 (Look/LightAttack/HeavyAttack). 키보드는 그대로.
            if (playerController != null) playerController.SetMouseInputEnabled(false);

            // 안전망 — Cinemachine InputAxisController 같은 카메라 입력 컴포넌트 직접 비활성.
            // PlayerInputActions.Look.Disable만으로 안 막히는 케이스 대비.
            SetCameraInputComponents(false);

            // 마우스 커서 노출 + 자유 (메뉴 클릭용)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Close()
        {
            _isOpen = false;

            if (menuPanel != null) menuPanel.SetActive(false);

            // 마우스 커서를 메뉴 열기 직전 위치로 워프.
            // 메뉴 열린 동안 마우스가 다른 곳으로 이동했어도, 닫는 순간 원래 자리로 돌아감.
            // 이게 없으면: 마우스 위치 차이만큼 Cinemachine이 delta를 인식 → 카메라가 펑 회전.
            // 워프는 입력 복구 직전에 — Enable() 직후 첫 프레임의 delta가 0이 되어야 함.
            if (Mouse.current != null)
                Mouse.current.WarpCursorPosition(_mousePositionBeforeMenu);

            // 마우스 커서 잠금 + 숨김 (게임 화면 정석)
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // 마우스 입력 복구 (워프와 커서 잠금 후에 — Cinemachine이 안정된 상태에서 받도록)
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

        // ========== 씬 전환 ==========

        private void ReturnToTitle()
        {
            // Time.timeScale 안전 복원 (혹시 다른 시스템이 멈춰뒀을 경우 대비)
            Time.timeScale = 1f;

            // 마우스 커서 노출 (TitleScene에서 메뉴 조작 필요)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 카메라 입력 컴포넌트 복구 — 다음 씬에서 같은 컴포넌트가 살아있으면 비활성 상태 유지될 수 있음
            // (단, 보통 씬 전환 시 카메라가 새로 로드되므로 자동 초기화됨. 안전망 차원).
            SetCameraInputComponents(true);

            // 보스 전투 BGM 정지. AudioManager는 DontDestroyOnLoad라 씬 전환에도 살아남아
            // BGM이 계속 재생되는 버그 방지. 짧은 페이드아웃으로 자연스럽게.
            // 씬 전환 시간이 짧아도 AudioManager는 다음 씬에서도 살아있어 코루틴이 끝까지 실행됨.
            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.StopBGM(0.5f);

            if (string.IsNullOrEmpty(titleSceneName))
            {
                Debug.LogError("[PauseMenu] titleSceneName이 비어있음. 인스펙터에서 시작 씬 이름 지정 필요.");
                return;
            }

            SceneManager.LoadScene(titleSceneName);
        }
    }
}