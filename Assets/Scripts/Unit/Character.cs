using UnityEngine;

// 모든 직업 캐릭터가 공유하는 데이터 접근 기반 클래스입니다.
// CharacterData의 원본 능력치를 Unit에 제공하고 물리/마법 주 능력치를 구분합니다.
// 기본 공격 효과와 스킬 동작 및 스킬 애니메이션은 직업별 자식 클래스에서 구현합니다.
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
    protected override int AttackPower => PrimaryAttackPower;

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
