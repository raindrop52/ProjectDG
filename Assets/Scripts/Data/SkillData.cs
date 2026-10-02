using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Dungeon Guidance/Skill Data")]
public sealed class SkillData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Character.AttackType attackType = Character.AttackType.Skill1;
    [SerializeField, Min(0f)] private float cooldown = 5f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private bool learnedByDefault;
    [SerializeField] private Sprite icon;

    public string DisplayName => displayName;
    public Character.AttackType AttackType => attackType;
    public float Cooldown => cooldown;
    public float DamageMultiplier => damageMultiplier;
    public bool LearnedByDefault => learnedByDefault;
    public Sprite Icon => icon;
}
