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

    [Header("Hitbox")]
    [Tooltip("공격자 위치를 기준으로 한 Box 판정의 중심 위치입니다.")]
    [SerializeField] private Vector2 hitboxOffset;
    [Tooltip("공격 판정에 사용할 Box의 가로와 세로 크기입니다.")]
    [SerializeField] private Vector2 hitboxSize = Vector2.one;

    public string DisplayName => displayName;
    public Character.AttackType AttackType => attackType;
    public float Cooldown => cooldown;
    public float DamageMultiplier => damageMultiplier;
    public Vector2 HitboxOffset => hitboxOffset;
    public Vector2 HitboxSize => new Vector2(
        Mathf.Max(0.01f, hitboxSize.x),
        Mathf.Max(0.01f, hitboxSize.y)
    );
    public bool LearnedByDefault => learnedByDefault;
    public Sprite Icon => icon;
}
