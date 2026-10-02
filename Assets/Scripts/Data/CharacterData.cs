using UnityEngine;

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
