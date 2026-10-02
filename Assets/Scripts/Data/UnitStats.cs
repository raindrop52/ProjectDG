using System;
using UnityEngine;

[Serializable]
// 캐릭터, 몬스터, 보스가 공통으로 사용하는 원본 능력치입니다.
// ScriptableObject 데이터에 저장하며 전투 중 직접 변경하지 않습니다.
// 실행 시 RuntimeUnitStats가 이 값을 복사하여 실제 전투 능력치로 사용합니다.
public sealed class UnitStats
{
    [SerializeField, Min(1)] private int maxHp = 100;
    [SerializeField, Min(0)] private int attackPower = 10;
    [SerializeField, Min(0)] private int defense;
    [SerializeField, Min(0)] private int magicPower;
    [SerializeField, Min(0.01f)] private float attackRange = 1f;
    [SerializeField, Min(1f)] private float attackSpeedPercent = 100f;
    [SerializeField, Min(0f)] private float baseAttackInterval = 0.5f;
    [SerializeField] private SkillData basicAttack;

    public int MaxHp => maxHp;
    public int AttackPower => attackPower;
    public int Defense => defense;
    public int MagicPower => magicPower;
    public float AttackRange => attackRange;
    public float AttackSpeedPercent => attackSpeedPercent;
    public float BaseAttackInterval => baseAttackInterval;
    public SkillData BasicAttack => basicAttack;
}
