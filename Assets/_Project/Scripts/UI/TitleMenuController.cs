using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using LostSouls.Settings;
using LostSouls.Audio;

namespace LostSouls.UI
{
    /// <summary>
    /// TitleScene의 메인 컨트롤러. 패널 전환 + 버튼 핸들러 + 타이틀 사운드.
    ///
    /// 패널 3개:
    /// - mainMenuPanel: 시작 화면 (Start Game / Options / Quit)
    /// - difficultyPanel: 난이도 선택 (화살표로 토글 + 시작 버튼 확정)
    /// - optionsPanel: 옵션 (볼륨 슬라이더)
    ///
    /// 난이도 선택 UX:
    /// 좌/우 화살표 버튼으로 '해금된' 난이도만 순환 (GameSettings.GetUnlockedDifficulties()).
    /// 잠긴 난이도(Nightmare, Normal 클리어 전)는 목록에 안 나타남 — 화살표로도 도달 불가.
    /// 현재 선택을 _unlockedDifficulties 리스트의 로컬 인덱스(_selectedUnlockedIndex)로 관리.
    /// 시작 시 선택된 DifficultyData를 전체 배열에서 찾아 그 인덱스로 GameSettings.SetDifficulty.
    /// 난이도 이름 텍스트는 DifficultyData.nameColor로 색칠 (Nightmare는 보라색 등 강조).
    ///
    /// 난이도 인덱스 두 종류 주의:
    /// - 전체 배열 인덱스: GameSettings.AvailableDifficulties 기준 (SetDifficulty/CurrentDifficultyIndex가 쓰는 값)
    /// - 해금 목록 로컬 인덱스: _unlockedDifficulties 기준 (화살표 순환이 쓰는 값)
    /// 둘을 혼동하면 잠긴 난이도가 선택되거나 엉뚱한 난이도로 시작하는 버그 발생.
    ///
    /// 사운드:
    /// - Start에서 TitleBGM 재생 시작 (AudioManager 싱글톤 경유, 페이드인).
    /// - 일반 버튼 → uiButtonClick SFX. 난이도 화살표 ◄► → uiArrowToggle SFX.
    /// - GameScene/Quit 진입 시 StopBGM (페이드아웃) — 보스 BGM과 안 겹치게.
    /// AudioManager나 ClipBank이 없어도 조용히 무음 동작 (null 안전).
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
        [Tooltip("현재 선택된 난이도 이름 표시 (예: 'Easy'). 색은 DifficultyData.nameColor로 적용됨.")]
        [SerializeField] private TextMeshProUGUI difficultyNameText;
        [Tooltip("현재 선택된 난이도 설명 표시 (DifficultyData.description). 색은 고정 (nameColor 영향 없음).")]
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

        [Header("BGM")]
        [Tooltip("GameScene 진입 / Quit 시 BGM 페이드아웃 길이(초).")]
        [SerializeField] private float bgmFadeOutDuration = 1.0f;

        // 해금된 난이도 목록 (난이도 패널 진입 시 갱신). 잠긴 난이도는 제외됨.
        private List<DifficultyData> _unlockedDifficulties = new List<DifficultyData>();
        // 위 리스트 내 현재 선택 인덱스 (전체 배열 인덱스 아님 — 해금 목록 로컬 인덱스).
        private int _selectedUnlockedIndex;

        private void Awake()
        {
            // 시작 상태: 메인 메뉴만 활성
            ShowMainMenu();

            // 마우스 커서 노출 (게임 씬에서 잠가뒀을 수 있음)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 버튼 이벤트 등록 — 람다로 묶어서 SFX 먼저, 동작 후.
            // SFX는 PlayOneShot이라 동작이 씬 전환을 동반해도 이미 재생 시작된 상태로 안전.
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => { PlayButtonClick(); ShowDifficulty(); });
            if (optionsButton != null)
                optionsButton.onClick.AddListener(() => { PlayButtonClick(); ShowOptions(); });
            if (quitButton != null)
                quitButton.onClick.AddListener(() => { PlayButtonClick(); QuitGame(); });

            if (leftArrowButton != null)
                leftArrowButton.onClick.AddListener(() => { PlayArrowToggle(); SelectPreviousDifficulty(); });
            if (rightArrowButton != null)
                rightArrowButton.onClick.AddListener(() => { PlayArrowToggle(); SelectNextDifficulty(); });
            if (difficultyStartButton != null)
                difficultyStartButton.onClick.AddListener(() => { PlayButtonClick(); StartGameWithSelected(); });
            if (difficultyBackButton != null)
                difficultyBackButton.onClick.AddListener(() => { PlayButtonClick(); ShowMainMenu(); });

            if (optionsBackButton != null)
                optionsBackButton.onClick.AddListener(() => { PlayButtonClick(); ShowMainMenu(); });
        }

        private void Start()
        {
            // 타이틀 BGM 재생 — Start에서 호출하는 이유:
            // AudioManager 싱글톤이 Awake에서 인스턴스화되는데, 다른 컴포넌트(TitleMenu)도 Awake라
            // 실행 순서에 따라 AudioManager.Instance가 아직 null일 수 있음. Start는 모든 Awake 후라 안전.
            PlayTitleBGM();
        }

        private void PlayTitleBGM()
        {
            // AudioManager가 없거나 ClipBank이 없거나 titleBGM이 비어있으면 조용히 스킵 (무음).
            if (AudioManager.Instance == null) return;
            if (AudioManager.Instance.ClipBank == null) return;

            AudioClip bgm = AudioManager.Instance.ClipBank.titleBGM;
            if (bgm == null) return;

            AudioManager.Instance.PlayBGM(bgm);
        }

        // ========== 사운드 헬퍼 ==========

        /// <summary>
        /// 일반 버튼 클릭 SFX. AudioManager/ClipBank/SoundSet 어디든 비어있으면 무음.
        /// ESC 메뉴(PauseMenu) 버튼과 동일 ClipBank 필드 공유.
        /// </summary>
        private void PlayButtonClick()
        {
            if (AudioManager.Instance == null) return;
            if (AudioManager.Instance.ClipBank == null) return;
            AudioManager.Instance.PlayUISound(AudioManager.Instance.ClipBank.uiButtonClick);
        }

        /// <summary>
        /// 난이도 화살표 ◄► 토글 SFX. 일반 버튼과 다른 가벼운 'tick' 느낌의 사운드 권장.
        /// </summary>
        private void PlayArrowToggle()
        {
            if (AudioManager.Instance == null) return;
            if (AudioManager.Instance.ClipBank == null) return;
            AudioManager.Instance.PlayUISound(AudioManager.Instance.ClipBank.uiArrowToggle);
        }

        // ========== 패널 전환 ==========

        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel);
        }

        public void ShowDifficulty()
        {
            SetPanel(difficultyPanel);

            // 해금된 난이도 목록 갱신 (Normal 클리어 여부 등 최신 상태 반영).
            RefreshUnlockedDifficulties();

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

        /// <summary>
        /// 해금된 난이도 목록을 GameSettings에서 다시 받아오고,
        /// 현재 선택을 마지막 저장 난이도(있으면)에 맞춰 초기화.
        /// 난이도 패널 진입 시마다 호출 — 직전 게임에서 Normal 클리어했으면 Nightmare가 새로 나타남.
        /// </summary>
        private void RefreshUnlockedDifficulties()
        {
            _unlockedDifficulties.Clear();

            if (GameSettings.Instance == null)
            {
                _selectedUnlockedIndex = 0;
                return;
            }

            _unlockedDifficulties = GameSettings.Instance.GetUnlockedDifficulties();

            if (_unlockedDifficulties.Count == 0)
            {
                _selectedUnlockedIndex = 0;
                return;
            }

            // 마지막으로 저장된 난이도를 해금 목록에서 찾아 그 위치를 초기 선택으로.
            // (옵션에서 난이도 바꾼 적 있으면 그 값부터 시작. 못 찾으면 0.)
            DifficultyData saved = GameSettings.Instance.CurrentDifficulty;
            int found = (saved != null) ? _unlockedDifficulties.IndexOf(saved) : -1;
            _selectedUnlockedIndex = (found >= 0) ? found : 0;
        }

        /// <summary>좌측 화살표 — 이전 난이도. 첫 번째에서 누르면 마지막으로 wrap.</summary>
        private void SelectPreviousDifficulty()
        {
            int count = _unlockedDifficulties.Count;
            if (count <= 0) return;

            _selectedUnlockedIndex = (_selectedUnlockedIndex - 1 + count) % count;
            UpdateDifficultyDisplay();
        }

        /// <summary>우측 화살표 — 다음 난이도. 마지막에서 누르면 첫 번째로 wrap.</summary>
        private void SelectNextDifficulty()
        {
            int count = _unlockedDifficulties.Count;
            if (count <= 0) return;

            _selectedUnlockedIndex = (_selectedUnlockedIndex + 1) % count;
            UpdateDifficultyDisplay();
        }

        /// <summary>
        /// 현재 _selectedUnlockedIndex에 해당하는 난이도 이름/설명을 UI에 반영.
        /// 이름 텍스트에는 DifficultyData.nameColor를 적용 (Nightmare 보라색 등 강조).
        /// 설명 텍스트는 색을 건드리지 않음 (인스펙터 지정 색 그대로).
        /// </summary>
        private void UpdateDifficultyDisplay()
        {
            if (_unlockedDifficulties == null || _unlockedDifficulties.Count == 0)
            {
                if (difficultyNameText != null) difficultyNameText.text = "(no difficulties)";
                if (difficultyDescriptionText != null) difficultyDescriptionText.text = "";
                return;
            }

            int idx = Mathf.Clamp(_selectedUnlockedIndex, 0, _unlockedDifficulties.Count - 1);
            DifficultyData data = _unlockedDifficulties[idx];
            if (data == null)
            {
                if (difficultyNameText != null) difficultyNameText.text = "(null)";
                if (difficultyDescriptionText != null) difficultyDescriptionText.text = "";
                return;
            }

            if (difficultyNameText != null)
            {
                difficultyNameText.text = data.difficultyName;
                // 난이도별 이름 색 적용. nameColor 기본값은 흰색(Easy/Normal),
                // Nightmare는 보라색 등으로 인스펙터에서 지정.
                difficultyNameText.color = data.nameColor;
            }
            if (difficultyDescriptionText != null)
                difficultyDescriptionText.text = data.description;
        }

        // ========== 게임 시작 ==========

        /// <summary>화살표로 고른 난이도로 게임 시작.</summary>
        private void StartGameWithSelected()
        {
            if (GameSettings.Instance != null && _unlockedDifficulties != null && _unlockedDifficulties.Count > 0)
            {
                int idx = Mathf.Clamp(_selectedUnlockedIndex, 0, _unlockedDifficulties.Count - 1);
                DifficultyData chosen = _unlockedDifficulties[idx];

                // 선택된 DifficultyData를 전체 배열에서 찾아 그 인덱스로 저장.
                // 해금 목록 로컬 인덱스 ≠ 전체 배열 인덱스라 변환 필요.
                int globalIndex = FindGlobalIndex(chosen);
                if (globalIndex >= 0)
                    GameSettings.Instance.SetDifficulty(globalIndex);
                else
                    Debug.LogWarning("[TitleMenu] 선택한 난이도를 전체 배열에서 못 찾음. 저장 스킵.");
            }
            else
            {
                Debug.LogWarning("[TitleMenu] GameSettings 없음 또는 해금 난이도 없음. 난이도 저장 스킵하고 씬 로드.");
            }

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("[TitleMenu] gameSceneName이 비어있음. 인스펙터에서 게임 씬 이름 지정 필요.");
                return;
            }

            // 타이틀 BGM 페이드아웃 — AudioManager가 DontDestroyOnLoad라 씬 전환 후에도
            // 페이드 코루틴 계속 돌아감. GameScene의 보스 BGM과 겹치지 않게 미리 꺼둠.
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopBGM(bgmFadeOutDuration);

            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>
        /// DifficultyData가 GameSettings.AvailableDifficulties 전체 배열에서 몇 번째인지 반환.
        /// 못 찾으면 -1.
        /// </summary>
        private int FindGlobalIndex(DifficultyData data)
        {
            if (GameSettings.Instance == null || data == null) return -1;
            var all = GameSettings.Instance.AvailableDifficulties;
            if (all == null) return -1;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == data) return i;
            }
            return -1;
        }

        private void QuitGame()
        {
            // 종료 직전 BGM 정지 (Editor에서 Stop 안 눌러도 잔여 사운드 안 남게).
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopBGM(0.3f);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}