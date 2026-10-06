using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Dungeon Guidance/Monster Data")]
public sealed class MonsterData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private UnitStats stats = new UnitStats();
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0)] private int experienceReward;
    [SerializeField] private MaterialDropEntry[] materialRewards;

    public string DisplayName => displayName;
    public UnitStats Stats => stats;
    public float MoveSpeed => moveSpeed;
    public int ExperienceReward => experienceReward;
    public MaterialDropEntry[] MaterialRewards => materialRewards;
}
