using UnityEngine;

[RequireComponent(typeof(Animator))]
public abstract class Unit : MonoBehaviour
{
    private static readonly UnitStats DefaultStats = new UnitStats();

    public enum UnitState
    {
        Idle,
        Move,
        Attack,
        Die
    }

    [Header("State")]
    [SerializeField] protected UnitState curState = UnitState.Idle;

    [Header("Enemy Detection")]
    [Tooltip("Character는 Monster 레이어, Monster는 Character 레이어를 지정합니다.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0.02f)] private float detectionInterval = 0.15f;

    [Header("Basic Attack")]
    [SerializeField] private Collider2D attackHitbox;

    protected int curHp;
    [SerializeField] protected Unit target;
    protected RuntimeUnitStats RuntimeStats { get; private set; }
    protected Animator UnitAnimator { get; private set; }

    protected abstract UnitStats BaseStats { get; }

    // 발견한 결과 저장 배열 및 최대치 설정
    private const int DetectionCapacity = 32;
    private readonly Collider2D[] detectionResults = new Collider2D[DetectionCapacity];

    // 적 레이어 탐지 & Trigger 중복 방지
    private ContactFilter2D enemyFilter;
    private float nextDetectionTime;
    private bool actionStopped;
    private bool basicAttackInProgress;
    private float nextBasicAttackTime;

    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int OnIdleHash = Animator.StringToHash("OnIdle");
    private static readonly int OnMoveHash = Animator.StringToHash("OnMove");
    private static readonly int OnAttackHash = Animator.StringToHash("OnAttack");
    private static readonly int OnDieHash = Animator.StringToHash("OnDie");

    protected virtual UnitState NoTargetState => UnitState.Idle;

    public UnitState CurrentState => curState;
    public Unit CurrentTarget => target;
    public int CurrentHp => curHp;
    public int MaxHp => RuntimeStats != null ? RuntimeStats.MaxHp : 0;
    public float HealthRatio => MaxHp > 0 ? (float)curHp / MaxHp : 0f;
    public SkillData BasicAttack => RuntimeStats != null ? RuntimeStats.BasicAttack : null;
    public bool IsDead => curState == UnitState.Die;
    public bool IsTargetable => isActiveAndEnabled && !IsDead;

    private float CurrentBasicAttackInterval =>
        RuntimeStats.BaseAttackInterval * (100f / RuntimeStats.AttackSpeedPercent);

    // 적 탐지 필터를 초기화하고 유닛의 런타임 상태를 준비합니다.
    protected virtual void Awake()
    {
        UnitAnimator = GetComponent<Animator>();

        // 물리 충돌을 사용하지 않는 Trigger Collider도 적 탐지에 포함합니다.
        enemyFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayer,
            useTriggers = true
        };

        Init();
    }

    // 원본 능력치를 런타임 능력치로 복사하고 체력과 상태를 초기화합니다.
    protected virtual void Init()
    {
        UnitStats sourceStats = BaseStats;
        if (sourceStats == null)
        {
            Debug.LogWarning($"{name} has no data asset assigned. Default unit stats will be used.", this);
            sourceStats = DefaultStats;
        }

        RuntimeStats = new RuntimeUnitStats(sourceStats);
        ConfigureRuntimeStats(RuntimeStats);
        curHp = RuntimeStats.MaxHp;
        target = null;
        actionStopped = false;
        basicAttackInProgress = false;
        nextBasicAttackTime = 0f;

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        ChangeState(UnitState.Idle);
    }

    // 자식 클래스가 직업 보정치처럼 추가 능력치를 적용할 수 있는 확장 지점입니다.
    protected virtual void ConfigureRuntimeStats(RuntimeUnitStats runtimeStats) { }

    // 현재 상태에 따라 타깃 탐색과 상태별 행동을 매 프레임 갱신합니다.
    protected virtual void Update()
    {
        // 사망 처리
        if (curState == UnitState.Die)
        {
            UpdateCurrentState();
            return;
        }

        if (actionStopped)
        {
            return;
        }

        // 기존 타겟이 유효한지 확인
        if (target != null && !IsTargetValid(target))
        {
            target = null;
            ChangeState(NoTargetState);
        }

        // 캐릭터는 Idle, 몬스터는 Idle과 Move 상태에서 공격 범위 안의 적을 탐색합니다.
        if (curState == UnitState.Idle || curState == UnitState.Move)
        {
            UpdateTarget();
        }

        // 현재 상태의 동작 실행
        UpdateCurrentState();
    }

    // 일정한 탐지 주기마다 기존 타깃을 확인하고 가장 가까운 적을 다시 선택합니다.
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

        // 공격 범위 안에서 타겟을 찾으면 공격하고, 없으면 유닛별 대기 상태로 전환합니다.
        target = FindNearestEnemy();
        ChangeState(target == null ? NoTargetState : UnitState.Attack);
    }

    // 공격 범위 안에서 공격 가능한 가장 가까운 적을 찾아 반환합니다.
    private Unit FindNearestEnemy()
    {
        // 공격 범위 내 적 레이어 탐색
        int resultCount = Physics2D.OverlapCircle(
            transform.position,
            RuntimeStats.AttackRange,
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

    // 대상이 자신이 아니며 살아 있고 공격 가능한 상태인지 확인합니다.
    private bool CanTarget(Unit candidate)
    {
        // null & 자기자신 & 죽어서 비활성화 된 대상
        return candidate != null &&
               candidate != this &&
               candidate.IsTargetable;
    }

    // 기존 타깃이 여전히 공격 가능하고 공격 범위 안에 있는지 확인합니다.
    private bool IsTargetValid(Unit candidate)
    {
        // 타겟의 사망여부 체크
        if (!CanTarget(candidate))
        {
            return false;
        }

        float distanceSqr =
            (candidate.transform.position - transform.position).sqrMagnitude;

        float attackRange = RuntimeStats.AttackRange;
        return distanceSqr <= attackRange * attackRange;
    }

    // 현재 유닛 상태에 대응하는 상태별 행동 함수를 실행합니다.
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

    // 이전 상태를 종료하고 새로운 상태로 전환한 뒤 진입 처리를 실행합니다.
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

    // 상태에 맞는 공통 애니메이션을 실행하고 사망 시 진행 중인 공격을 정리합니다.
    protected virtual void EnterState(UnitState state)
    {
        ResetStateTriggers();

        switch (state)
        {
            case UnitState.Idle:
                SetAnimatorTrigger(OnIdleHash);
                break;

            case UnitState.Move:
                SetAnimatorTrigger(OnMoveHash);
                break;

            case UnitState.Die:
                CancelBasicAttack();
                SetAnimatorTrigger(OnDieHash);
                break;
        }
    }

    // 공격 상태에서 벗어날 때 진행 중인 기본 공격과 히트박스를 정리합니다.
    protected virtual void ExitState(UnitState state)
    {
        if (state == UnitState.Attack)
        {
            CancelBasicAttack();
        }
    }

    // 대기 상태에서 수행할 행동을 자식 클래스가 구현하는 확장 지점입니다.
    protected virtual void OnIdleState() { }

    // 이동 상태에서 수행할 행동을 자식 클래스가 구현하는 확장 지점입니다.
    protected virtual void OnMoveState() { }

    // 공격 대기시간과 타깃 상태를 확인하고 모든 유닛의 공통 기본 공격을 시작합니다.
    protected virtual void OnAttackState()
    {
        if (basicAttackInProgress || Time.time < nextBasicAttackTime)
        {
            return;
        }

        if (target == null || !target.IsTargetable)
        {
            return;
        }

        StartBasicAttack();
    }

    // 사망 상태에서 수행할 행동을 자식 클래스가 구현하는 확장 지점입니다.
    protected virtual void OnDieState() { }

    // 기본 공격 상태를 시작하고 공통 공격 애니메이션 트리거를 실행합니다.
    protected virtual void StartBasicAttack()
    {
        basicAttackInProgress = true;
        SetAnimatorInteger(AttackTypeHash, 0);
        SetAnimatorTrigger(OnAttackHash);
        OnBasicAttackStarted();
    }

    // 직업이나 몬스터별 클래스가 기본 공격의 실제 효과를 추가하는 확장 지점입니다.
    protected virtual void OnBasicAttackStarted() { }

    // 스킬 구현 클래스가 공격 종류와 함께 공격 애니메이션을 실행할 때 사용합니다.
    protected void PlayAttackAnimation(int attackType)
    {
        SetAnimatorInteger(AttackTypeHash, attackType);
        SetAnimatorTrigger(OnAttackHash);
    }

    // Animator에 해당 Trigger 파라미터가 존재할 때만 안전하게 트리거를 실행합니다.
    private void SetAnimatorTrigger(int parameterHash)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
        {
            UnitAnimator.SetTrigger(parameterHash);
        }
    }

    // Animator에 해당 Int 파라미터가 존재할 때만 안전하게 값을 설정합니다.
    private void SetAnimatorInteger(int parameterHash, int value)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Int))
        {
            UnitAnimator.SetInteger(parameterHash, value);
        }
    }

    // 연결된 Animator Controller에 요청한 이름과 타입의 파라미터가 있는지 확인합니다.
    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (UnitAnimator == null || UnitAnimator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = UnitAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    // 상태 전환 전에 Animator에 남아 있는 공통 트리거를 모두 초기화합니다.
    private void ResetStateTriggers()
    {
        if (UnitAnimator == null)
        {
            return;
        }

        ResetAnimatorTrigger(OnIdleHash);
        ResetAnimatorTrigger(OnMoveHash);
        ResetAnimatorTrigger(OnAttackHash);
        ResetAnimatorTrigger(OnDieHash);
    }

    // Animator에 해당 Trigger 파라미터가 존재할 때만 안전하게 초기화합니다.
    private void ResetAnimatorTrigger(int parameterHash)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
        {
            UnitAnimator.ResetTrigger(parameterHash);
        }
    }

    // 진행 중인 기본 공격을 취소하고 공격 히트박스를 비활성화합니다.
    private void CancelBasicAttack()
    {
        basicAttackInProgress = false;
        DisableAttackHitbox();
    }

    // 기본 공격 애니메이션의 타격 시작 프레임에서 호출하는 Animation Event입니다.
    public void EnableAttackHitbox()
    {
        if (basicAttackInProgress && attackHitbox != null)
        {
            attackHitbox.enabled = true;
        }
    }

    // 기본 공격 애니메이션의 타격 종료 프레임에서 호출하는 Animation Event입니다.
    public void DisableAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    // 기본 공격 애니메이션 종료 시 다음 기본 공격 가능 시간을 계산합니다.
    public void OnAttackAnimationFinished()
    {
        DisableAttackHitbox();

        if (!basicAttackInProgress)
        {
            return;
        }

        basicAttackInProgress = false;
        nextBasicAttackTime = Time.time + CurrentBasicAttackInterval;
    }

    // 피해를 현재 체력에 적용하고 체력이 소진되면 사망 상태로 전환합니다.
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
    // 타깃과 현재 행동을 중단하고 유닛을 대기 상태로 전환합니다.
    public void StopAction()
    {
        if (IsDead)
        {
            return;
        }

        actionStopped = true;
        target = null;
        ChangeState(UnitState.Idle);
    }

    // 전투 시작이나 다음 웨이브 시작 시 탐색과 행동을 다시 허용합니다.
    public void StartAction()
    {
        if (IsDead)
        {
            return;
        }

        actionStopped = false;
        target = null;
        ChangeState(UnitState.Idle);
        nextDetectionTime = 0f;
    }

    // Scene 뷰에서 선택한 유닛의 현재 공격 범위를 표시합니다.
    protected virtual void OnDrawGizmosSelected()
    {
        UnitStats sourceStats = BaseStats;
        float attackRange = RuntimeStats != null
            ? RuntimeStats.AttackRange
            : sourceStats != null ? sourceStats.AttackRange : DefaultStats.AttackRange;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
