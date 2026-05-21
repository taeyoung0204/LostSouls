using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace LostSouls.Audio
{
    /// <summary>
    /// 전역 사운드 매니저. 씬에 1개 두고 DontDestroyOnLoad로 유지.
    /// 책임:
    /// - BGM 재생/페이드 (2D)
    /// - UI 사운드 등 위치 없는 효과음 재생 (2D)
    /// - AudioMixer 볼륨 제어 (옵션 메뉴용)
    ///
    /// 3D 효과음(무기 휘두름, 보스 신음 등)은 각 GameObject가 자체 AudioSource로 재생.
    /// AudioManager는 관여하지 않음.
    ///
    /// AudioMixer 그룹 구조 (MainMixer):
    ///   Master
    ///   ├── BGM       ← bgmSource가 출력
    ///   └── SFX       ← UI 사운드 + 3D 효과음 출력 (그룹 전체)
    ///       ├── Player
    ///       └── Boss
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Clip Bank")]
        [SerializeField] private AudioClipBank clipBank;

        [Header("Audio Mixer")]
        [Tooltip("MainMixer 에셋. 볼륨 제어용.")]
        [SerializeField] private AudioMixer mainMixer;
        [Tooltip("BGM 그룹. bgmSource의 Output에 자동 할당.")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [Tooltip("SFX 그룹. UI 사운드 출력용. 3D 효과음은 각 GameObject가 직접 참조.")]
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Mixer Parameter Names")]
        [Tooltip("Audio Mixer에서 노출한 Master Volume 파라미터 이름.")]
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string bgmVolumeParam = "BGMVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";

        [Header("BGM Settings")]
        [SerializeField] private float defaultFadeDuration = 1.5f;

        // 내부 AudioSource (런타임 생성)
        private AudioSource _bgmSource;
        private AudioSource _uiSource;  // UI/2D 효과음용

        // 진행 중인 페이드 코루틴
        private Coroutine _bgmFadeRoutine;

        // === 외부 접근자 ===
        public AudioClipBank ClipBank => clipBank;
        public AudioMixerGroup SfxGroup => sfxGroup;  // 다른 GameObject의 AudioSource가 Output 슬롯에 할당 가능

        private void Awake()
        {
            // 싱글톤 패턴 — 씬 전환 시 중복 생성 방지
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 내부 AudioSource 셋업 (BGM 1개 + UI용 1개)
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;  // 2D
            _bgmSource.outputAudioMixerGroup = bgmGroup;

            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;  // 2D
            _uiSource.outputAudioMixerGroup = sfxGroup;
        }

        // ========== BGM 제어 ==========

        /// <summary>
        /// BGM 재생 (페이드인). 이미 같은 클립 재생 중이면 무시.
        /// </summary>
        public void PlayBGM(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null) return;
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            float fade = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;

            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(BGMCrossfade(clip, fade));
        }

        /// <summary>
        /// BGM 정지 (페이드아웃).
        /// </summary>
        public void StopBGM(float fadeDuration = -1f)
        {
            float fade = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;

            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(BGMFadeOut(fade));
        }

        private IEnumerator BGMCrossfade(AudioClip newClip, float duration)
        {
            // 페이드아웃 (현재 재생 중이라면)
            if (_bgmSource.isPlaying)
            {
                float startVol = _bgmSource.volume;
                float t = 0f;
                while (t < duration * 0.5f)
                {
                    t += Time.deltaTime;
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / (duration * 0.5f));
                    yield return null;
                }
                _bgmSource.Stop();
            }

            // 새 클립 페이드인
            _bgmSource.clip = newClip;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            float fadeInTime = 0f;
            while (fadeInTime < duration * 0.5f)
            {
                fadeInTime += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, 1f, fadeInTime / (duration * 0.5f));
                yield return null;
            }
            _bgmSource.volume = 1f;
            _bgmFadeRoutine = null;
        }

        private IEnumerator BGMFadeOut(float duration)
        {
            float startVol = _bgmSource.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.volume = 1f;  // 다음 재생 위해 복원
            _bgmFadeRoutine = null;
        }

        // ========== 2D 효과음 (UI 등) ==========

        /// <summary>
        /// 2D 효과음 1회 재생. UI 클릭, 게임 시작 등.
        /// 3D 효과음(무기/보스 등)은 호출하면 안 됨 — 각 GameObject가 자체 AudioSource로.
        /// </summary>
        public void PlayUISound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || _uiSource == null) return;
            _uiSource.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// SoundSet 기반 2D 효과음 재생. 카테고리 볼륨/피치 자동 적용.
        /// YOU DIED 같은 UI 연출에 사용.
        /// </summary>
        public void PlayUISound(SoundSet set)
        {
            if (set == null || _uiSource == null) return;
            AudioClip clip = set.PickRandomClip();
            if (clip == null) return;

            _uiSource.pitch = set.GetRandomPitch();
            _uiSource.PlayOneShot(clip, set.SafeVolume);
        }

        // ========== 볼륨 제어 (옵션 메뉴용) ==========

        /// <summary>
        /// 볼륨을 0~1 범위로 받아 AudioMixer dB(-80~0)로 변환해 설정.
        /// 0 = 음소거 (-80dB), 1 = 최대 (0dB), 0.5 ≈ -6dB.
        /// 로그 스케일이라 단순 곱셈 X.
        /// </summary>
        public void SetMasterVolume(float normalized01) => SetMixerVolume(masterVolumeParam, normalized01);
        public void SetBGMVolume(float normalized01) => SetMixerVolume(bgmVolumeParam, normalized01);
        public void SetSFXVolume(float normalized01) => SetMixerVolume(sfxVolumeParam, normalized01);

        private void SetMixerVolume(string paramName, float normalized01)
        {
            if (mainMixer == null) return;

            normalized01 = Mathf.Clamp(normalized01, 0.0001f, 1f);  // log(0) 회피
            float dB = Mathf.Log10(normalized01) * 20f;
            mainMixer.SetFloat(paramName, dB);
        }
    }
}