using UnityEngine;
using LostSouls.Audio;

namespace LostSouls.Player
{
    /// <summary>
    /// 플레이어 본체에서 나는 사운드를 모아 재생하는 헬퍼.
    /// PlayerHealth, PlayerController, PlayerPotion 등 여러 컴포넌트가
    /// 자기 ClipBank/AudioSource 참조를 따로 들고 있지 않고 이 헬퍼를 통해 재생.
    ///
    /// AudioSource는 캐릭터 본체에 붙어있고 3D — 위치 자동.
    /// 무기 휘두름은 별도 AudioSource(무기 GameObject)를 쓰므로 여기 안 거침.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("플레이어 본체에 붙인 AudioSource. 3D 사운드용.")]
        [SerializeField] private AudioSource bodyAudioSource;
        [Tooltip("사운드 클립 보관소.")]
        [SerializeField] private AudioClipBank clipBank;

        public AudioClipBank ClipBank => clipBank;

        /// <summary>
        /// SoundSet 재생. 카테고리 볼륨/피치 랜덤 자동 적용.
        /// 다른 사운드와 동시 재생 가능 (PlayOneShot).
        /// pitch는 AudioSource 단위라 동시 재생 시 마지막 호출의 pitch가 모두에게 반영됨 —
        /// Player 사운드는 동시 다발 가능성 낮아 무방. 우려되면 별도 AudioSource 추가.
        /// </summary>
        public void Play(SoundSet set)
        {
            if (set == null || bodyAudioSource == null) return;
            AudioClip clip = set.PickRandomClip();
            if (clip == null) return;

            bodyAudioSource.pitch = set.GetRandomPitch();
            bodyAudioSource.PlayOneShot(clip, set.SafeVolume);
        }

        // === 카테고리별 편의 메서드 ===
        // 각 컴포넌트가 _audio.PlayRoll() 같이 호출하면 됨. null 안전.

        public void PlayRoll()       => Play(clipBank != null ? clipBank.playerRoll : null);
        public void PlayHurt()       => Play(clipBank != null ? clipBank.playerHurt : null);
        public void PlayDeath()      => Play(clipBank != null ? clipBank.playerDeath : null);
        public void PlayDrink()      => Play(clipBank != null ? clipBank.playerDrink : null);
    }
}