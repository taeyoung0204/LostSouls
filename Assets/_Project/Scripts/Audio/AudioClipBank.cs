using UnityEngine;

namespace LostSouls.Audio
{
    /// <summary>
    /// 카테고리 단위 사운드 셋. 클립들 + 카테고리 공통 볼륨/피치 설정.
    /// 카테고리 안에서는 매 재생마다 클립 1개 랜덤 선택 (변형 효과).
    /// </summary>
    [System.Serializable]
    public class SoundSet
    {
        [Tooltip("이 카테고리에 속한 클립들. 매 재생마다 1개 랜덤 선택.")]
        public AudioClip[] clips;

        [Tooltip("이 카테고리 전체에 적용되는 볼륨 배율. 1.0이 원본 크기, 2.0이면 2배 큰 소리. " +
                 "1 초과 값도 가능하지만 너무 크면 클리핑(소리 깨짐). 안전 상한 5.0.")]
        public float volume = 1f;

        [Tooltip("재생 시 피치 랜덤 범위. 1.0 ± 이 값. 0이면 항상 정상 피치. 0.05~0.1 권장 (자연스러운 변화).")]
        [Range(0f, 0.3f)]
        public float pitchRandomness = 0.05f;

        /// <summary>
        /// 실제 재생에 쓸 안전한 볼륨 값. 음수와 과도한 값(>5.0)을 클램프.
        /// 5.0 상한은 클리핑(소리 깨짐) 회피용 — Unity AudioSource는 1.0 이상에서도 작동하지만
        /// 너무 크면 파형이 짤려 왜곡 발생. 그 이상 필요하면 클립 자체를 Audacity로 Normalize 권장.
        /// </summary>
        public float SafeVolume => Mathf.Clamp(volume, 0f, 5f);

        /// <summary>이 카테고리에서 랜덤 클립 1개 반환. 비어있으면 null.</summary>
        public AudioClip PickRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>피치 랜덤 적용된 값 반환. pitchRandomness=0이면 항상 1.0.</summary>
        public float GetRandomPitch()
        {
            if (pitchRandomness <= 0f) return 1f;
            return 1f + Random.Range(-pitchRandomness, pitchRandomness);
        }
    }

    /// <summary>
    /// 모든 사운드 클립을 한 곳에 모아두는 ScriptableObject.
    /// 카테고리마다 SoundSet (클립 배열 + 볼륨 + 피치 랜덤) 보유.
    ///
    /// 사용 흐름:
    /// 1. Project 창에서 Create → LostSouls → Audio Clip Bank 로 에셋 생성
    /// 2. 인스펙터에서 각 카테고리의 SoundSet에 클립과 볼륨 설정
    /// 3. AudioManager가 이 에셋을 참조해 어디서나 접근
    /// 4. 개별 GameObject(무기/보스)도 직접 참조해 자기 사운드 재생
    /// </summary>
    [CreateAssetMenu(fileName = "AudioClipBank", menuName = "LostSouls/Audio Clip Bank")]
    public class AudioClipBank : ScriptableObject
    {
        [Header("Player Combat")]
        [Tooltip("R1 1타 (Attack1) 휘두름 소리.")]
        public SoundSet weaponSwingLight1;
        [Tooltip("R1 2타 (Attack2) 휘두름 소리.")]
        public SoundSet weaponSwingLight2;
        [Tooltip("R1 3타 (Attack3, 마무리) 휘두름 소리. 보통 더 묵직한 소리.")]
        public SoundSet weaponSwingLight3;
        [Tooltip("R2 강공격 (HeavyAttack) 휘두름 소리. 가장 무거운 소리.")]
        public SoundSet weaponSwingHeavy;
        [Tooltip("무기가 적에게 맞았을 때 (타격 성공).")]
        public SoundSet weaponHit;
        [Tooltip("플레이어가 구를 때.")]
        public SoundSet playerRoll;
        [Tooltip("플레이어가 피격될 때 (Flinch/Knockback 모두).")]
        public SoundSet playerHurt;
        [Tooltip("플레이어 사망 보이스.")]
        public SoundSet playerDeath;
        [Tooltip("물약 마시는 소리.")]
        public SoundSet playerDrink;

        [Header("Boss Combat")]
        [Tooltip("보스 도끼 가벼운 휘두름 (Vertical/Horizontal/Low Slash 등).")]
        public SoundSet bossSwingLight;
        [Tooltip("보스 도끼 무거운 휘두름 (Slam/Spin/Dash Jump 등).")]
        public SoundSet bossSwingHeavy;
        [Tooltip("보스 발차기 (Kick) 휘두름.")]
        public SoundSet bossKick;
        [Tooltip("Dash Jump 착지 시 발생하는 충격파 사운드.")]
        public SoundSet bossShockwave;
        [Tooltip("Poise Break 순간 재생되는 임팩트 사운드. 묵직한 슬램/크리티컬 신호음.")]
        public SoundSet bossPoiseBreak;

        [Header("Boss Voice")]
        [Tooltip("보스 하울링 (Roar 페이즈 전환 시).")]
        public SoundSet bossRoar;
        [Tooltip("보스 신음 (Poise Break 시).")]
        public SoundSet bossGroan;
        [Tooltip("보스 사망 보이스.")]
        public SoundSet bossDeath;

        [Header("Music")]
        [Tooltip("보스 전투 BGM. 루프되는 트랙 권장.")]
        public AudioClip bossBattleBGM;

        [Header("UI")]
        [Tooltip("YOU DIED 화면 페이드인 시 재생되는 효과음. 다크소울 시리즈의 결정적 순간 SFX.")]
        public SoundSet youDiedSting;
        [Tooltip("VICTORY 화면 페이드인 시 재생되는 효과음. 보스 처치 임팩트.")]
        public SoundSet victorySting;
        [Tooltip("보스 사망 즉시 (HP 0이 되는 순간) 재생되는 임팩트 사운드. " +
                 "2D UI 사운드로 화면 전체 울림. 다크소울/엘든링 풍 '결정타' 효과.")]
        public SoundSet bossDefeated;

        [Header("Title / UI Sounds")]
        [Tooltip("타이틀 화면 BGM. 루프되는 트랙 권장. TitleScene 진입 시 자동 재생, " +
                 "GameScene 진입 시 페이드아웃. 비워두면 무음.")]
        public AudioClip titleBGM;
        [Tooltip("일반 버튼 클릭 SFX. 타이틀의 모든 일반 버튼(Start/Options/Quit/Back/Reset 등) " +
                 "과 ESC 메뉴 버튼(Resume/Return to Title)에서 공유 사용. " +
                 "피치 랜덤 0.05~0.1 권장 (반복 시 똑같이 들리는 거 회피).")]
        public SoundSet uiButtonClick;
        [Tooltip("난이도 선택 화살표 ◄ ► 토글 SFX. 일반 버튼과 다른 가벼운 'tick' 느낌의 소리 권장. " +
                 "(같은 클릭 사운드 써도 무방하지만 분리하면 UX가 더 풍부.)")]
        public SoundSet uiArrowToggle;
        [Tooltip("메뉴/패널 '열림' SFX. PauseMenu(ESC) 열 때, TutorialPanel(표지판 E) 열 때 공유. " +
                 "묵직한 'whoosh/스윽' 느낌 권장. 다크소울 시리즈의 메뉴 열기 톤.")]
        public SoundSet uiMenuOpen;
        [Tooltip("메뉴/패널 '닫힘' SFX. PauseMenu, TutorialPanel 닫힐 때 공유. " +
                 "open과 다른 가벼운 'tap' 느낌 권장 — 열기/닫기 톤이 달라야 UX가 풍부.")]
        public SoundSet uiMenuClose;
    }
}