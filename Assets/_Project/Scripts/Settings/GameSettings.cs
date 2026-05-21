using UnityEngine;

namespace LostSouls.Settings
{
    /// <summary>
    /// 게임 전역 설정 싱글톤. 씬 전환 시에도 유지 (DontDestroyOnLoad).
    /// 책임:
    /// - 현재 난이도 보관 (DifficultyData 참조)
    /// - 볼륨 설정 보관 (Master/BGM/SFX, 0~1)
    /// - PlayerPrefs로 영구 저장/로드
    /// - AudioManager에 볼륨 자동 반영
    ///
    /// 설치: 빈 GameObject 'GameSettings'에 이 컴포넌트 부착 → 첫 씬(TitleScene)에 배치.
    /// DontDestroyOnLoad라 첫 씬에만 두면 이후 모든 씬에서 접근 가능.
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        [Header("Difficulty")]
        [Tooltip("선택 가능한 난이도 목록. 인스펙터에서 Easy/Normal 에셋을 순서대로 드래그.")]
        [SerializeField] private DifficultyData[] availableDifficulties;
        [Tooltip("저장된 설정 없을 때 기본 선택될 난이도 인덱스. 2단계라 0(Easy) 또는 1(Normal). " +
                 "신규 플레이어 친화적으로 0(Easy) 기본 권장.")]
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
        }

        /// <summary>
        /// 모든 설정을 기본값으로 리셋 (옵션 메뉴의 'Reset' 버튼용).
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