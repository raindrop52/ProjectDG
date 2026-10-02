using UnityEngine;

public class Monster : Unit
{
    [Header("Monster Data")]
    [SerializeField] private MonsterData monsterData;

    protected virtual MonsterData Data => monsterData;
    protected override UnitStats BaseStats => Data != null ? Data.Stats : null;
    protected override UnitState NoTargetState => UnitState.Move;

    public float MoveSpeed => Data != null ? Data.MoveSpeed : 2f;
    public int ExperienceReward => Data != null ? Data.ExperienceReward : 0;
    public MaterialDropEntry[] MaterialRewards =>
        Data != null ? Data.MaterialRewards : System.Array.Empty<MaterialDropEntry>();

    // 공격 범위 안에 타겟이 없을 때 캐릭터 진영 방향인 왼쪽으로 이동합니다.
    protected override void OnMoveState()
    {
        base.OnMoveState();
        transform.position += Vector3.left * MoveSpeed * Time.deltaTime;
    }
}
