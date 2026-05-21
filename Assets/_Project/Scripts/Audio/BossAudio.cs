using UnityEngine;
using LostSouls.Audio;

namespace LostSouls.Boss
{
    /// <summary>
    /// 보스 본체에서 나는 사운드를 모아 재생하는 헬퍼.
    /// BossController, BossPoise, BossHitReactState 등이 공유 사용.
    ///
    /// AudioSource 2개로 분리:
    /// - sfxSource: 휘두름/충격파/타격음 등 액션 SFX
    /// - voiceSource: Roar/Groan/Death 등 보이스
    /// 분리 이유: 휘두름 도중 Poise Break로 신음 발동되어도 둘 다 끊김 없이 동시 재생.
    ///
    /// 둘 다 3D — 위치는 보스 본체 transform 따라감.
    /// </summary>
    public class BossAudio : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("일반 SFX용 AudioSource (휘두름, 충격파, 타격음).")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("보이스용 AudioSource (Roar, Groan, Death). SFX와 동시 재생되어도 끊기지 않음.")]
        [SerializeField] private AudioSource voiceSource;
        [Tooltip("사운드 클립 보관소.")]
        [SerializeField] private AudioClipBank clipBank;

        public AudioClipBank ClipBank => clipBank;

        /// <summary>SFX 재생 (휘두름/충격파/타격음).</summary>
        private void PlaySfx(SoundSet set)
        {
            if (set == null || sfxSource == null) return;
            AudioClip clip = set.PickRandomClip();
            if (clip == null) return;

            sfxSource.pitch = set.GetRandomPitch();
            sfxSource.PlayOneShot(clip, set.SafeVolume);
        }

        /// <summary>
        /// 보이스 재생 (Roar/Groan/Death). 진행 중인 보이스가 있으면 끊고 새로 재생.
        /// Roar 중 Groan 재생 같은 충돌 시 신음이 더 자연스럽게 들리도록.
        /// </summary>
        private void PlayVoice(SoundSet set, bool interrupt = true)
        {
            if (set == null || voiceSource == null) return;
            AudioClip clip = set.PickRandomClip();
            if (clip == null) return;

            if (interrupt && voiceSource.isPlaying)
                voiceSource.Stop();

            voiceSource.pitch = set.GetRandomPitch();
            voiceSource.clip = clip;
            voiceSource.volume = set.SafeVolume;
            voiceSource.Play();
        }

        // === 카테고리별 편의 메서드 ===

        public void PlaySwingLight()  => PlaySfx(clipBank != null ? clipBank.bossSwingLight : null);
        public void PlaySwingHeavy()  => PlaySfx(clipBank != null ? clipBank.bossSwingHeavy : null);
        public void PlayKick()        => PlaySfx(clipBank != null ? clipBank.bossKick : null);
        public void PlayShockwave()   => PlaySfx(clipBank != null ? clipBank.bossShockwave : null);
        public void PlayPoiseBreak()  => PlaySfx(clipBank != null ? clipBank.bossPoiseBreak : null);

        public void PlayRoar()        => PlayVoice(clipBank != null ? clipBank.bossRoar : null);
        public void PlayGroan()       => PlayVoice(clipBank != null ? clipBank.bossGroan : null);
        public void PlayDeath()       => PlayVoice(clipBank != null ? clipBank.bossDeath : null);
    }
}