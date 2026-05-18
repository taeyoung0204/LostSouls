// using UnityEngine;

// // WeaponData.cs - ScriptableObject
// [CreateAssetMenu(fileName = "NewWeapon", menuName = "LostSouls/Weapon")]
// public class WeaponData : ScriptableObject
// {
//     public string weaponName;
//     public WeaponType weaponType;  // 나중에 확장: OneHandSword, GreatSword, Spear...
//     public float baseDamage;
//     public float weightStaminaCost;
//     public AttackData[] lightAttackCombo;   // R1 콤보
//     public AttackData[] heavyAttackCombo;   // R2 콤보
//     public AttackData dashAttack;
//     public AttackData backstepAttack;
// }

// [System.Serializable]
// public class AttackData
// {
//     public AnimationClip animation;
//     public float motionValue;        // 데미지 배율
//     public float staminaCost;
//     public float poiseDamage;        // 보스 경직치
//     public AnimationCurve hitboxActiveCurve;  // 히트박스 활성 구간
// }