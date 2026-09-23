using UnityEngine;

public abstract class Unit : MonoBehaviour
{
    public enum UnitState
    {
        Idle,
        Move,
        Attack,
        Die
    }

    [Header("Unit Data")]
    [SerializeField] protected int maxHp = 100;
    [SerializeField] protected float attackRange = 3f;

    [Header("State")]
    [SerializeField] protected UnitState curState = UnitState.Idle;

    [Header("Enemy Detection")]
    [Tooltip("Character는 Monster 레이어, Monster는 Character 레이어를 지정합니다.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0.02f)] private float detectionInterval = 0.15f;

    protected int curHp;
    protected Unit target;

    // 발견한 결과 저장 배열 및 최대치 설정
    private const int DetectionCapacity = 32;
    private readonly Collider2D[] detectionResults = new Collider2D[DetectionCapacity];

    // 적 레이어 탐지 & Trigger 중복 방지
    private ContactFilter2D enemyFilter;
    private float nextDetectionTime;

    public UnitState CurrentState => curState;
    public Unit CurrentTarget => target;
    public bool IsDead => curState == UnitState.Die;
    public bool IsTargetable => isActiveAndEnabled && !IsDead;

    protected virtual void Awake()
    {
        // 적 레이어 탐지 초기 설정 및 공격 Trigger제외하고 탐지하도록 설정
        enemyFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayer,
            useTriggers = false
        };

        Init();
    }

    protected virtual void Init()
    {
        curHp = maxHp;
        target = null;

        ChangeState(UnitState.Idle);
    }

    protected virtual void Update()
    {
        // 사망 처리
        if (curState == UnitState.Die)
        {
            UpdateCurrentState();
            return;
        }

        // 기존 타겟이 유효한지 확인
        if (target != null && !IsTargetValid(target))
        {
            target = null;
            ChangeState(UnitState.Move);
        }

        // Idle은 스테이지 정지 같은 외부 제어에 사용할 수 있도록 자동 탐색하지 않습니다.
        if (curState != UnitState.Idle)
        {
            UpdateTarget();
        }

        // 현재 상태의 동작 실행
        UpdateCurrentState();
    }

    private void UpdateTarget()
    {
        // 탐지 시간이 되지 않으면 동작X
        if (Time.time < nextDetectionTime)
        {
            return;
        }

        // 다음 탐지 시간 설정
        nextDetectionTime = Time.time + detectionInterval;

        // 기존 타겟이 유효하면 동작X
        if (IsTargetValid(target))
        {
            return;
        }

        // 타겟이 없으면 새로 추적
        target = FindNearestEnemy();
        ChangeState(target == null ? UnitState.Move : UnitState.Attack);
    }

    private Unit FindNearestEnemy()
    {
        // 공격 범위 내 적 레이어 탐색
        int resultCount = Physics2D.OverlapCircle(
            transform.position,
            attackRange,
            enemyFilter,
            detectionResults
        );

        Unit nearestEnemy = null;
        float nearestDistanceSqr = float.MaxValue;

        // 탐지한 적의 수 만큼 반복
        for (int i = 0; i < resultCount; i++)
        {
            Collider2D detectedCollider = detectionResults[i];

            // 탐지 결과가 없으면 넘어감
            if (detectedCollider == null)
            {
                continue;
            }

            // 탐지 결과의 Unit 클래스 보유 체크
            Unit candidate = detectedCollider.GetComponentInParent<Unit>();

            if (!CanTarget(candidate))
            {
                continue;
            }

            // 적과의 거리 계산
            float distanceSqr =
                (candidate.transform.position - transform.position).sqrMagnitude;

            // 가장 가까운 적 탐색 알고리즘
            if (distanceSqr >= nearestDistanceSqr)
            {
                continue;
            }

            nearestEnemy = candidate;
            nearestDistanceSqr = distanceSqr;
        }

        return nearestEnemy;
    }

    private bool CanTarget(Unit candidate)
    {
        // null & 자기자신 & 죽어서 비활성화 된 대상
        return candidate != null &&
               candidate != this &&
               candidate.IsTargetable;
    }

    private bool IsTargetValid(Unit candidate)
    {
        // 타겟의 사망여부 체크
        if (!CanTarget(candidate))
        {
            return false;
        }

        float distanceSqr =
            (candidate.transform.position - transform.position).sqrMagnitude;

        return distanceSqr <= attackRange * attackRange;
    }

    private void UpdateCurrentState()
    {
        switch (curState)
        {
            case UnitState.Idle:
                OnIdleState();
                break;

            case UnitState.Move:
                OnMoveState();
                break;

            case UnitState.Attack:
                OnAttackState();
                break;

            case UnitState.Die:
                OnDieState();
                break;
        }
    }

    protected void ChangeState(UnitState newState)
    {
        if (curState == newState)
        {
            return;
        }

        ExitState(curState);
        curState = newState;
        EnterState(curState);
    }

    protected virtual void EnterState(UnitState state) { }
    protected virtual void ExitState(UnitState state) { }

    protected virtual void OnIdleState() { }
    protected virtual void OnMoveState() { }
    protected virtual void OnAttackState() { }
    protected virtual void OnDieState() { }

    public virtual void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        curHp = Mathf.Max(curHp - damage, 0);

        if (curHp > 0)
        {
            return;
        }

        target = null;
        ChangeState(UnitState.Die);
    }

    // 스테이지 클리어처럼 유닛 행동을 멈출 때 호출합니다.
    public void StopAction()
    {
        if (IsDead)
        {
            return;
        }

        target = null;
        ChangeState(UnitState.Idle);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
