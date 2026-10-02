using UnityEngine;

public class Boss : Monster
{
    [Header("Boss Pattern")]
    [SerializeField, Range(0f, 1f)] private float patternHealthRatio = 0.5f;

    private bool patternStarted;

    // 일반 몬스터 행동을 갱신하고 지정 체력 이하에서 보스 패턴을 한 번 시작합니다.
    protected override void Update()
    {
        base.Update();

        if (!IsDead && !patternStarted && HealthRatio <= patternHealthRatio)
        {
            patternStarted = true;
            OnPatternStarted();
        }
    }

    // 실제 보스 클래스가 체력 조건에 맞는 고유 패턴을 구현하는 확장 지점입니다.
    protected virtual void OnPatternStarted() { }
}
