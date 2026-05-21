using UnityEngine;

namespace LostSouls.Settings
{
    /// <summary>
    /// 난이도별 게임플레이 배율 데이터.
    /// 인스펙터에서 자유 조정 가능. 새 난이도 추가 = 에셋 하나 만들기.
    ///
    /// 게임 시작 시 GameSettings.CurrentDifficulty가 이 데이터를 참조.
    /// 각 게임 시스템(BossHealth/PlayerHealth/PlayerPotion 등)이 GameSettings에서 읽어 자기 값에 곱한다.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyData", menuName = "LostSouls/Difficulty Data")]
    public class DifficultyData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("UI에 표시될 이름 (예: Easy, Normal, Hard).")]
        public string difficultyName = "Normal";
        [Tooltip("UI 설명 (예: '도전적인 난이도. 정석 다크소울 경험.').")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("Boss Multipliers")]
        [Tooltip("보스 최대 HP 배율. 1.0이 기본, 0.7이면 30% 약화, 1.3이면 30% 강화.")]
        public float bossHealthMultiplier = 1.0f;
        [Tooltip("보스가 주는 데미지 배율. 1.0이 기본.")]
        public float bossDamageMultiplier = 1.0f;
        [Tooltip("보스 최대 Poise(경직치) 배율. 1.0이 기본, 0.7이면 Poise Break 잘 됨 (Easy), " +
                 "1.3이면 더 안 됨 (Hard). " +
                 "낮을수록 플레이어가 보스를 자주 비틀거리게 만들 수 있어 딜타임이 늘어남.")]
        public float bossPoiseMultiplier = 1.0f;

        [Header("Player Multipliers")]
        [Tooltip("플레이어가 받는 데미지 배율. 1.0이 기본, 0.7이면 적은 데미지, 1.3이면 더 큰 데미지. " +
                 "bossDamageMultiplier와 곱셈 합성됨 (Easy면 보스가 약하게 때리고 + 플레이어가 적게 받음 = 둘 다 적용).")]
        public float playerDamageTakenMultiplier = 1.0f;

        [Header("Player Resources")]
        [Tooltip("시작 포션 개수. 5가 기본.")]
        public int startingPotionCharges = 5;
    }
}