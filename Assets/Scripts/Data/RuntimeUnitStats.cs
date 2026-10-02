using UnityEngine;

// UnitStats의 원본값을 유닛마다 복사하여 전투 중 실제로 사용하는 능력치입니다.
// UnitStatModifiers를 적용한 결과와 버프 및 디버프에 따른 변경값을 이 객체에서 관리합니다.
// ScriptableObject 원본을 보호하기 위해 각 유닛 인스턴스가 별도로 생성하여 사용합니다.
public sealed class RuntimeUnitStats
{
    // ScriptableObject가 보유한 원본 능력치를 개별 유닛용 런타임 값으로 복사합니다.
    public RuntimeUnitStats(UnitStats source)
    {
        MaxHp = source.MaxHp;
        AttackPower = source.AttackPower;
        Defense = source.Defense;
        MagicPower = source.MagicPower;
        AttackRange = source.AttackRange;
        AttackSpeedPercent = source.AttackSpeedPercent;
        BaseAttackInterval = source.BaseAttackInterval;
        BasicAttack = source.BasicAttack;
    }

    public int MaxHp { get; private set; }
    public int AttackPower { get; private set; }
    public int Defense { get; private set; }
    public int MagicPower { get; private set; }
    public float AttackRange { get; private set; }
    public float AttackSpeedPercent { get; private set; }
    public float BaseAttackInterval { get; private set; }
    public SkillData BasicAttack { get; }

    // 직업이나 기타 데이터에서 제공한 능력치 보정값을 안전한 범위로 적용합니다.
    public void Apply(UnitStatModifiers modifiers)
    {
        if (modifiers == null)
        {
            return;
        }

        MaxHp = Mathf.Max(1, MaxHp + modifiers.MaxHp);
        AttackPower = Mathf.Max(0, AttackPower + modifiers.AttackPower);
        Defense = Mathf.Max(0, Defense + modifiers.Defense);
        MagicPower = Mathf.Max(0, MagicPower + modifiers.MagicPower);
        AttackRange = Mathf.Max(0.01f, AttackRange + modifiers.AttackRange);
        AttackSpeedPercent = Mathf.Max(1f, AttackSpeedPercent + modifiers.AttackSpeedPercent);
    }
}
