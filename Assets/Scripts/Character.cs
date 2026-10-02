using UnityEngine;

public class Character : Unit
{
    public enum AttackType
    {
        BasicAttack = 0,
        Skill1 = 1,
        Skill2 = 2,
        Skill3 = 3
    }

    [Header("Character Data")]
    [SerializeField] private CharacterData characterData;

    protected override UnitStats BaseStats => characterData != null ? characterData.BaseStats : null;

    public CharacterData Data => characterData;
    public PrimaryStatType AttackStatType =>
        characterData != null ? characterData.PrimaryStatType : PrimaryStatType.Physical;
    public int PrimaryAttackPower =>
        AttackStatType == PrimaryStatType.Physical
            ? RuntimeStats != null ? RuntimeStats.AttackPower : 0
            : RuntimeStats != null ? RuntimeStats.MagicPower : 0;

    // 직업별 클래스가 ScriptableObject에 설정된 스킬 목록을 읽을 때 사용합니다.
    protected SkillData[] ConfiguredSkills =>
        characterData != null ? characterData.SkillsByPriority : System.Array.Empty<SkillData>();
}
