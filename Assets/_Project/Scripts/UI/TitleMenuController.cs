using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using LostSouls.Settings;

namespace LostSouls.UI
{
    /// <summary>
    /// TitleScene의 메인 컨트롤러. 패널 전환 + 버튼 핸들러.
    ///
    /// 패널 3개:
    /// - mainMenuPanel: 시작 화면 (Start Game / Options / Quit)
    /// - difficultyPanel: 난이도 선택 (화살표로 토글 + 시작 버튼 확정)
    /// - optionsPanel: 옵션 (볼륨 슬라이더)
    ///
    /// 난이도 선택 UX:
    /// 좌/우 화살표 버튼으로 GameSettings.AvailableDifficulties 순환.
    /// 현재 선택 인덱스를 _selectedDifficultyIndex에 보관, UI에 이름/설명 갱신.
    /// 시작 버튼 클릭 시 GameSettings에 저장 + GameScene 로드.
    /// </summary>
    public class TitleMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject optionsPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Difficulty Panel - Selector")]
        [Tooltip("난이도 선택 영역의 좌측 화살표 (◄).")]
        [SerializeField] private Button leftArrowButton;
        [Tooltip("난이도 선택 영역의 우측 화살표 (►).")]
        [SerializeField] private Button rightArrowButton;
        [Tooltip("현재 선택된 난이도 이름 표시 (예: 'Easy').")]
        [SerializeField] private TextMeshProUGUI difficultyNameText;
        [Tooltip("현재 선택된 난이도 설명 표시 (DifficultyData.description).")]
        [SerializeField] private TextMeshProUGUI difficultyDescriptionText;

        [Header("Difficulty Panel - Buttons")]
        [Tooltip("선택한 난이도로 게임 시작.")]
        [SerializeField] private Button difficultyStartButton;
        [Tooltip("메인 메뉴로 돌아가기.")]
        [SerializeField] private Button difficultyBackButton;

        [Header("Options Panel Buttons")]
        [Tooltip("옵션 패널에서 메인 메뉴로 돌아가는 버튼.")]
        [SerializeField] private Button optionsBackButton;

        [Header("Scene")]
        [Tooltip("난이도 선택 후 로드할 게임 씬 이름. Build Settings에 등록되어 있어야 함.")]
        [SerializeField] private string gameSceneName = "GameScene";

        // 난이도 선택 상태 (난이도 패널 열려있을 때 화살표로 변경됨)
        private int _selectedDifficultyIndex;

        private void Awake()
        {
            // 시작 상태: 메인 메뉴만 활성
            ShowMainMenu();

            // 마우스 커서 노출 (게임 씬에서 잠가뒀을 수 있음)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 버튼 이벤트 등록
            if (startGameButton != null) startGameButton.onClick.AddListener(ShowDifficulty);
            if (optionsButton != null) optionsButton.onClick.AddListener(ShowOptions);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            if (leftArrowButton != null) leftArrowButton.onClick.AddListener(SelectPreviousDifficulty);
            if (rightArrowButton != null) rightArrowButton.onClick.AddListener(SelectNextDifficulty);
            if (difficultyStartButton != null) difficultyStartButton.onClick.AddListener(StartGameWithSelected);
            if (difficultyBackButton != null) difficultyBackButton.onClick.AddListener(ShowMainMenu);

            if (optionsBackButton != null) optionsBackButton.onClick.AddListener(ShowMainMenu);
        }

        // ========== 패널 전환 ==========

        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel);
        }

        public void ShowDifficulty()
        {
            SetPanel(difficultyPanel);

            // 난이도 패널 열 때마다 마지막 저장 값으로 초기화 (PlayerPrefs에서 로드된 값).
            // 사용자가 옵션에서 난이도 변경한 적 있으면 그 값부터 시작.
            if (GameSettings.Instance != null)
            {
                _selectedDifficultyIndex = GameSettings.Instance.CurrentDifficultyIndex;
            }
            else
            {
                _selectedDifficultyIndex = 0;
            }

            UpdateDifficultyDisplay();
        }

        public void ShowOptions()
        {
            SetPanel(optionsPanel);
        }

        private void SetPanel(GameObject panel)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == mainMenuPanel);
            if (difficultyPanel != null) difficultyPanel.SetActive(panel == difficultyPanel);
            if (optionsPanel != null) optionsPanel.SetActive(panel == optionsPanel);
        }

        // ========== 난이도 선택 ==========

        /// <summary>좌측 화살표 — 이전 난이도. 첫 번째에서 누르면 마지막으로 wrap.</summary>
        private void SelectPreviousDifficulty()
        {
            int count = GetDifficultyCount();
            if (count <= 0) return;

            _selectedDifficultyIndex = (_selectedDifficultyIndex - 1 + count) % count;
            UpdateDifficultyDisplay();
        }

        /// <summary>우측 화살표 — 다음 난이도. 마지막에서 누르면 첫 번째로 wrap.</summary>
        private void SelectNextDifficulty()
        {
            int count = GetDifficultyCount();
            if (count <= 0) return;

            _selectedDifficultyIndex = (_selectedDifficultyIndex + 1) % count;
            UpdateDifficultyDisplay();
        }

        private int GetDifficultyCount()
        {
            if (GameSettings.Instance == null) return 0;
            var diffs = GameSettings.Instance.AvailableDifficulties;
            return diffs != null ? diffs.Length : 0;
        }

        /// <summary>현재 _selectedDifficultyIndex에 해당하는 난이도 이름/설명을 UI에 반영.</summary>
        private void UpdateDifficultyDisplay()
        {
            if (GameSettings.Instance == null)
            {
                if (difficultyNameText != null) difficultyNameText.text = "(no GameSettings)";
                if (difficultyDescriptionText != null) difficultyDescriptionText.text = "";
                return;
            }

            var diffs = GameSettings.Instance.AvailableDifficulties;
            if (diffs == null || diffs.Length == 0)
            {
                if (difficultyNameText != null) difficultyNameText.text = "(no difficulties)";
                if (difficultyDescriptionText != null) difficultyDescriptionText.text = "";
                return;
            }

            int idx = Mathf.Clamp(_selectedDifficultyIndex, 0, diffs.Length - 1);
            DifficultyData data = diffs[idx];
            if (data == null)
            {
                if (difficultyNameText != null) difficultyNameText.text = "(null)";
                if (difficultyDescriptionText != null) difficultyDescriptionText.text = "";
                return;
            }

            if (difficultyNameText != null) difficultyNameText.text = data.difficultyName;
            if (difficultyDescriptionText != null) difficultyDescriptionText.text = data.description;
        }

        // ========== 게임 시작 ==========

        /// <summary>화살표로 고른 난이도로 게임 시작.</summary>
        private void StartGameWithSelected()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.SetDifficulty(_selectedDifficultyIndex);
            }
            else
            {
                Debug.LogWarning("[TitleMenu] GameSettings.Instance가 없음. 난이도 저장 스킵하고 씬 로드.");
            }

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("[TitleMenu] gameSceneName이 비어있음. 인스펙터에서 게임 씬 이름 지정 필요.");
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        // ========== 종료 ==========

        private void QuitGame()
        {
            // 에디터에서는 Application.Quit이 안 먹히므로 에디터 플레이 모드 종료
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}