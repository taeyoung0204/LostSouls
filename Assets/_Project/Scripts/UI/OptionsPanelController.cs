using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Settings;

namespace LostSouls.UI
{
    /// <summary>
    /// 옵션 패널 컨트롤러. 볼륨 슬라이더 처리.
    ///
    /// 동작:
    /// - OnEnable: GameSettings에서 현재 값 읽어 슬라이더 동기화
    /// - 슬라이더 변경 시: GameSettings.SetXxxVolume → PlayerPrefs 저장 + AudioMixer 즉시 반영
    /// - Reset: GameSettings.ResetToDefaults + 슬라이더 다시 동기화
    ///
    /// TitleScene + ESC 메뉴 양쪽에서 재사용 가능하도록 독립 컴포넌트로 분리.
    /// Prefab으로 빼면 ESC 메뉴에서도 그대로 쓸 수 있음.
    /// </summary>
    public class OptionsPanelController : MonoBehaviour
    {
        [Header("Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Value Labels (Optional)")]
        [Tooltip("슬라이더 옆에 퍼센트 표시. 비워두면 표시 안 함.")]
        [SerializeField] private TextMeshProUGUI masterVolumeLabel;
        [SerializeField] private TextMeshProUGUI bgmVolumeLabel;
        [SerializeField] private TextMeshProUGUI sfxVolumeLabel;

        [Header("Buttons")]
        [Tooltip("기본값 복원 버튼 (선택). 비워둬도 무방.")]
        [SerializeField] private Button resetButton;

        // 초기화 중 onValueChanged가 발동되어 무한 루프 빠지는 거 방지
        private bool _isInitializing;

        private void Awake()
        {
            // 슬라이더 onValueChanged 등록
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
        }

        private void OnEnable()
        {
            // 패널 열릴 때마다 GameSettings의 현재 값 동기화.
            // (다른 곳에서 값 바꿨을 가능성 대비)
            SyncSlidersFromSettings();
        }

        /// <summary>
        /// GameSettings의 현재 값을 슬라이더에 반영. 초기화/리셋 시 호출.
        /// onValueChanged가 다시 발동되어 PlayerPrefs 저장이 중복되는 걸 막기 위해
        /// _isInitializing 플래그로 보호.
        /// </summary>
        private void SyncSlidersFromSettings()
        {
            if (GameSettings.Instance == null) return;

            _isInitializing = true;

            if (masterVolumeSlider != null)
                masterVolumeSlider.value = GameSettings.Instance.MasterVolume;
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.value = GameSettings.Instance.BGMVolume;
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = GameSettings.Instance.SFXVolume;

            _isInitializing = false;

            // 라벨 업데이트
            UpdateLabel(masterVolumeLabel, GameSettings.Instance.MasterVolume);
            UpdateLabel(bgmVolumeLabel, GameSettings.Instance.BGMVolume);
            UpdateLabel(sfxVolumeLabel, GameSettings.Instance.SFXVolume);
        }

        // ========== 슬라이더 이벤트 ==========

        private void OnMasterVolumeChanged(float value)
        {
            if (_isInitializing) return;
            if (GameSettings.Instance == null) return;

            GameSettings.Instance.SetMasterVolume(value);
            UpdateLabel(masterVolumeLabel, value);
        }

        private void OnBGMVolumeChanged(float value)
        {
            if (_isInitializing) return;
            if (GameSettings.Instance == null) return;

            GameSettings.Instance.SetBGMVolume(value);
            UpdateLabel(bgmVolumeLabel, value);
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (_isInitializing) return;
            if (GameSettings.Instance == null) return;

            GameSettings.Instance.SetSFXVolume(value);
            UpdateLabel(sfxVolumeLabel, value);
        }

        // ========== 리셋 ==========

        private void OnResetClicked()
        {
            if (GameSettings.Instance == null) return;

            GameSettings.Instance.ResetToDefaults();
            SyncSlidersFromSettings();
        }

        // ========== 헬퍼 ==========

        private void UpdateLabel(TextMeshProUGUI label, float value)
        {
            if (label == null) return;
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}