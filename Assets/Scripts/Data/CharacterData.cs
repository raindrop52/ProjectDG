using UnityEngine;

// 캐릭터가 생성되거나 사망 후 초기화될 때 다시 사용할 원본 기본값입니다.
// 실행 중 변경되는 체력과 능력치는 이 자산을 수정하지 않고 RuntimeUnitStats에서 관리합니다.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Dungeon Guidance/Character Data")]
public sealed class CharacterData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private UnitStats baseStats = new UnitStats();
    [SerializeField] private PrimaryStatType primaryStatType;
    [SerializeField] private SkillData[] skillsByPriority;
    [SerializeField] private Sprite portrait;

    public string DisplayName => displayName;
    public UnitStats BaseStats => baseStats;
    public PrimaryStatType PrimaryStatType => primaryStatType;
    public SkillData[] SkillsByPriority => skillsByPriority;
    public Sprite Portrait => portrait;
}
