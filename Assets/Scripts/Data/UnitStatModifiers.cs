using System;
using UnityEngine;

[Serializable]
// 장비, 버프, 음식, 패시브 등이 원본 능력치에 더하거나 빼는 변화량입니다.
// UnitStats의 원본값을 직접 변경하지 않고 RuntimeUnitStats에 적용할 때 사용합니다.
// 값이 0이면 해당 능력치에는 아무런 변화도 주지 않습니다.
public sealed class UnitStatModifiers
{
    [SerializeField] private int maxHp;
    [SerializeField] private int attackPower;
    [SerializeField] private int defense;
    [SerializeField] private int magicPower;
    [SerializeField] private float attackRange;
    [SerializeField] private float attackSpeedPercent;

    public int MaxHp => maxHp;
    public int AttackPower => attackPower;
    public int Defense => defense;
    public int MagicPower => magicPower;
    public float AttackRange => attackRange;
    public float AttackSpeedPercent => attackSpeedPercent;
}
