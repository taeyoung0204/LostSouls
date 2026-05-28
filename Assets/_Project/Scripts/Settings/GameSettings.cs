using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Settings
{
    /// <summary>
    /// 게임 전역 설정 싱글톤. 씬 전환 시에도 유지 (DontDestroyOnLoad).
    /// 책임:
    /// - 현재 난이도 보관 (DifficultyData 참조)
    /// - 볼륨 설정 보관 (Master/BGM/SFX, 0~1)
    /// - 난이도 해금 진행도 보관 (Normal 클리어 여부)
    /// - PlayerPrefs로 영구 저장/로드
    /// - AudioManager에 볼륨 자동 반영
    ///
    /// 설치: 빈 GameObject 'GameSettings'에 이 컴포넌트 부착 → 첫 씬(TitleScene)에 배치.
    /// DontDestroyOnLoad라 첫 씬에만 두면 이후 모든 씬에서 접근 가능.
    ///
    /// 해금 시스템:
    /// - Normal 난이도로 보스 처치 시 VictoryUI가 MarkNormalClearedIfApplicable() 호출 → PlayerPrefs 기록.
    /// - 그 메서드는 '이번 호출로 처음 해금됐는지'를 bool로 반환 → VictoryUI가 최초 1회 해금 연출 발동.
    /// - requiresNormalClear=true인 난이도(Nightmare)는 NormalCleared 전까지 잠김.
    /// - TitleScene은 GetUnlockedDifficulties()로 해금된 목록만 받아 순환 (잠긴 건 안 보임).
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        [Header("Difficulty")]
        [Tooltip("선택 가능한 난이도 목록. 인스펙터에서 Easy/Normal/Nightmare 에셋을 순서대로 드래그.")]
        [SerializeField] private DifficultyData[] availableDifficulties;
        [Tooltip("저장된 설정 없을 때 기본 선택될 난이도 인덱스. 신규 플레이어 친화적으로 0(Easy) 또는 1(Normal).")]
        [SerializeField] private int defaultDifficultyIndex = 1;

        [Header("Audio - Default Values")]
        [Tooltip("저장된 설정 없을 때 기본 볼륨 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultBGMVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float defaultSFXVolume = 0.8f;

        // PlayerPrefs 키 (오타 방지 위해 상수)
        private const string KEY_DIFFICULTY_INDEX = "LostSouls.DifficultyIndex";
        private const string KEY_MASTER_VOLUME = "LostSouls.MasterVolume";
        private const string KEY_BGM_VOLUME = "LostSouls.BGMVolume";
        private const string KEY_SFX_VOLUME = "LostSouls.SFXVolume";
        private const string KEY_NORMAL_CLEARED = "LostSouls.NormalCleared";

        // "Normal" 난이도를 식별하는 이름. MarkNormalClearedIfApplicable이 현재 난이도 이름과 비교.
        // DifficultyData.difficultyName이 이 값과 일치하면 Normal로 간주.
        // 주의: Normal.asset의 Difficulty Name이 정확히 이 값이어야 해금 로직이 작동.
        private const string NORMAL_DIFFICULTY_NAME = "Normal";

        // 현재 설정 값
        private int _currentDifficultyIndex;
        private float _masterVolume;
        private float _bgmVolume;
        private float _sfxVolume;

        // === 외부 접근자 ===
        public DifficultyData CurrentDifficulty
        {
            get
            {
                if (availableDifficulties == null || availableDifficulties.Length == 0) return null;
                int idx = Mathf.Clamp(_currentDifficultyIndex, 0, availableDifficulties.Length - 1);
                return availableDifficulties[idx];
            }
        }
        public int CurrentDifficultyIndex => _currentDifficultyIndex;
        public DifficultyData[] AvailableDifficulties => availableDifficulties;

        public float MasterVolume => _masterVolume;
        public float BGMVolume => _bgmVolume;
        public float SFXVolume => _sfxVolume;

        /// <summary>Normal 난이도를 클리어한 적 있는지. Nightmare 해금 판정의 기준.</summary>
        public bool IsNormalCleared => PlayerPrefs.GetInt(KEY_NORMAL_CLEARED, 0) == 1;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            // AudioManager가 같은 프레임에 Awake되어도 Instance 보장 안 됨 → Start에서 적용
            ApplyVolumesToAudioManager();
        }

        // ========== 난이도 ==========

        /// <summary>
        /// 난이도 변경 + PlayerPrefs 저장.
        /// TitleScene의 난이도 선택 버튼에서 호출.
        /// </summary>
        public void SetDifficulty(int index)
        {
            if (availableDifficulties == null || availableDifficulties.Length == 0) return;
            _currentDifficultyIndex = Mathf.Clamp(index, 0, availableDifficulties.Length - 1);
            PlayerPrefs.SetInt(KEY_DIFFICULTY_INDEX, _currentDifficultyIndex);
            PlayerPrefs.Save();
        }

        // ========== 난이도 해금 ==========

        /// <summary>
        /// 특정 난이도가 현재 해금되어 있는지 판정.
        /// requiresNormalClear=false면 항상 해금. true면 IsNormalCleared 여부에 따름.
        /// </summary>
        public bool IsDifficultyUnlocked(DifficultyData data)
        {
            if (data == null) return false;
            if (!data.requiresNormalClear) return true;
            return IsNormalCleared;
        }

        /// <summary>
        /// 현재 해금된 난이도만 추려서 반환. TitleScene 난이도 순환이 이 목록을 사용.
        /// 잠긴 난이도(Nightmare, Normal 클리어 전)는 제외되어 순환에 안 나타남.
        /// 반환 순서는 availableDifficulties 원본 순서 유지.
        /// </summary>
        public List<DifficultyData> GetUnlockedDifficulties()
        {
            var result = new List<DifficultyData>();
            if (availableDifficulties == null) return result;

            foreach (var data in availableDifficulties)
            {
                if (data == null) continue;
                if (IsDifficultyUnlocked(data))
                    result.Add(data);
            }
            return result;
        }

        /// <summary>
        /// Normal 난이도 클리어 기록. VictoryUI가 보스 처치 시 호출.
        /// 현재 난이도가 Normal일 때만 기록 (Easy 클리어로는 Nightmare 안 열림).
        ///
        /// 반환값: '이번 호출로 처음 해금됐는지'.
        /// - true: 직전까지 NormalCleared=false였는데 이번에 처음 true로 기록됨 (최초 해금)
        ///         → VictoryUI가 이 값을 보고 1회성 해금 연출(포효 + 시그널 텍스트) 발동.
        /// - false: 현재 난이도가 Normal이 아니거나, 이미 해금돼 있었음 (연출 안 함)
        /// </summary>
        public bool MarkNormalClearedIfApplicable()
        {
            DifficultyData current = CurrentDifficulty;
            if (current == null) return false;

            // 현재 난이도가 Normal인지 이름으로 판정.
            // (인덱스 하드코딩 대신 이름 비교 — 난이도 순서 바뀌어도 안전)
            if (current.difficultyName != NORMAL_DIFFICULTY_NAME) return false;

            if (IsNormalCleared) return false; // 이미 기록됨 → 최초 해금 아님

            // 여기 도달 = 현재 Normal + 아직 미해금 → 이번이 최초 해금
            PlayerPrefs.SetInt(KEY_NORMAL_CLEARED, 1);
            PlayerPrefs.Save();

            Debug.Log("[GameSettings] Normal 최초 클리어 기록됨 → Nightmare 난이도 해금");
            return true;
        }

        // ========== 볼륨 ==========

        public void SetMasterVolume(float normalized01)
        {
            _masterVolume = Mathf.Clamp01(normalized01);
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, _masterVolume);
            PlayerPrefs.Save();

            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.SetMasterVolume(_masterVolume);
        }

        public void SetBGMVolume(float normalized01)
        {
            _bgmVolume = Mathf.Clamp01(normalized01);
            PlayerPrefs.SetFloat(KEY_BGM_VOLUME, _bgmVolume);
            PlayerPrefs.Save();

            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.SetBGMVolume(_bgmVolume);
        }

        public void SetSFXVolume(float normalized01)
        {
            _sfxVolume = Mathf.Clamp01(normalized01);
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, _sfxVolume);
            PlayerPrefs.Save();

            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.SetSFXVolume(_sfxVolume);
        }

        /// <summary>
        /// AudioManager가 늦게 생성된 경우(예: GameScene 진입 후) 호출해 현재 볼륨 반영.
        /// </summary>
        public void ApplyVolumesToAudioManager()
        {
            if (LostSouls.Audio.AudioManager.Instance == null) return;
            LostSouls.Audio.AudioManager.Instance.SetMasterVolume(_masterVolume);
            LostSouls.Audio.AudioManager.Instance.SetBGMVolume(_bgmVolume);
            LostSouls.Audio.AudioManager.Instance.SetSFXVolume(_sfxVolume);
        }

        // ========== 저장/로드 ==========

        private void LoadSettings()
        {
            _currentDifficultyIndex = PlayerPrefs.GetInt(KEY_DIFFICULTY_INDEX, defaultDifficultyIndex);
            _masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, defaultMasterVolume);
            _bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, defaultBGMVolume);
            _sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, defaultSFXVolume);

            // 로드 시점에 잠긴 난이도가 저장돼 있을 가능성 방어:
            // 이전에 Nightmare를 골랐다가 PlayerPrefs를 초기화해 NormalCleared가 사라진 경우 등.
            // 현재 인덱스의 난이도가 잠겨 있으면 기본값으로 되돌림.
            if (availableDifficulties != null && availableDifficulties.Length > 0)
            {
                int idx = Mathf.Clamp(_currentDifficultyIndex, 0, availableDifficulties.Length - 1);
                DifficultyData saved = availableDifficulties[idx];
                if (saved != null && !IsDifficultyUnlocked(saved))
                {
                    _currentDifficultyIndex = Mathf.Clamp(defaultDifficultyIndex, 0, availableDifficulties.Length - 1);
                }
            }
        }

        /// <summary>
        /// 모든 설정을 기본값으로 리셋 (옵션 메뉴의 'Reset' 버튼용).
        /// 주의: 난이도 해금 진행도(NormalCleared)는 리셋하지 않음 — 볼륨/난이도 선택만 초기화.
        /// 해금 진행도까지 지우려면 별도 ResetProgress() 같은 메서드를 두는 게 안전.
        /// </summary>
        public void ResetToDefaults()
        {
            SetDifficulty(defaultDifficultyIndex);
            SetMasterVolume(defaultMasterVolume);
            SetBGMVolume(defaultBGMVolume);
            SetSFXVolume(defaultSFXVolume);
        }
    }
}