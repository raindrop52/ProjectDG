using System.Collections.Generic;
using UnityEngine;

// 캐릭터, 몬스터, 보스가 공통으로 사용하는 전투 기반 클래스입니다.
// 원본 능력치 초기화, 상태 전환, 적 탐지, 피해 및 행동 제어를 담당합니다.
// 기본 공격의 실행 주기, 공통 공격 애니메이션과 Animation Event도 여기에서 처리합니다.
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
    private readonly HashSet<Unit> hitUnits = new HashSet<Unit>();
    private readonly HashSet<int> animatorTriggerParameters = new HashSet<int>();
    private readonly HashSet<int> animatorIntParameters = new HashSet<int>();
    private float nextDetectionTime;
    private bool actionStopped;
    private bool attackInProgress;
    private bool idleAnimationPending;
    private float nextActionTime;
    private SkillData currentAttack;

    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int OnIdleHash = Animator.StringToHash("OnIdle");
    private static readonly int OnMoveHash = Animator.StringToHash("OnMove");
    private static readonly int OnAttackHash = Animator.StringToHash("OnAttack");
    private static readonly int OnDieHash = Animator.StringToHash("OnDie");

    protected virtual UnitState NoTargetState => UnitState.Idle;
    protected virtual int AttackPower => RuntimeStats != null ? RuntimeStats.AttackPower : 0;

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

    private float CurrentBasicAttackAnimationSpeed =>
        Mathf.Max(0.01f, RuntimeStats.AttackSpeedPercent / 100f);

    #region 초기화
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
        attackInProgress = false;
        idleAnimationPending = false;
        nextActionTime = 0f;
        currentAttack = null;
        CacheAnimatorParameters();

        ChangeState(UnitState.Idle);
    }

    // 연결된 Animator Controller의 파라미터를 타입별 HashSet에 한 번만 저장합니다.
    private void CacheAnimatorParameters()
    {
        animatorTriggerParameters.Clear();
        animatorIntParameters.Clear();

        if (UnitAnimator == null || UnitAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = UnitAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animatorTriggerParameters.Add(parameter.nameHash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Int)
            {
                animatorIntParameters.Add(parameter.nameHash);
            }
        }
    }

    #endregion
    // 자식 클래스가 직업 보정치처럼 추가 능력치를 적용할 수 있는 확장 지점입니다.
    protected virtual void ConfigureRuntimeStats(RuntimeUnitStats runtimeStats) { }


    #region 탐색 및 공격 루틴
    // 현재 상태에 따라 타깃 탐색과 상태별 행동을 매 프레임 갱신합니다.
    protected virtual void Update()
    {
        // 사망 처리
        if (curState == UnitState.Die)
        {
            UpdateCurrentState();
            return;
        }

        ProcessPendingAnimation();

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
    #endregion

    #region 상태 관리

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

    // 대기 상태에서 수행할 행동을 자식 클래스가 구현하는 확장 지점입니다.
    protected virtual void OnIdleState() { }

    // 이동 상태에서 수행할 행동을 자식 클래스가 구현하는 확장 지점입니다.
    protected virtual void OnMoveState() { }

    // 공격 대기시간과 타깃 상태를 확인하고 모든 유닛의 공통 기본 공격을 시작합니다.
    protected virtual void OnAttackState()
    {
        if (attackInProgress || Time.time < nextActionTime)
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
    #endregion

    #region 애니메이션 제어

    // 예약된 공격 종료 애니메이션 전환을 다음 프레임에 공통 Idle 상태로 확정합니다.
    private void ProcessPendingAnimation()
    {
        if (!idleAnimationPending)
        {
            return;
        }

        idleAnimationPending = false;

        if (UnitAnimator != null && UnitAnimator.HasState(0, IdleStateHash))
        {
            ResetStateTriggers();
            UnitAnimator.Play(IdleStateHash, 0, 0f);
            return;
        }

        SetAnimatorTrigger(OnIdleHash);
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
                idleAnimationPending = false;
                CancelAttack();
                SetAnimatorTrigger(OnDieHash);
                break;
        }
    }

    // Animator에 해당 Trigger 파라미터가 존재할 때만 안전하게 트리거를 실행합니다.
    private void SetAnimatorTrigger(int parameterHash)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
        {
            UnitAnimator.SetTrigger(parameterHash);
        }
    }

    // 공격 상태에서 벗어날 때 진행 중인 기본 공격과 히트박스를 정리합니다.
    protected virtual void ExitState(UnitState state)
    {
        if (state == UnitState.Attack)
        {
            CancelAttack();
        }
    }

    // 초기화 시 저장한 목록에서 요청한 이름과 타입의 Animator 파라미터를 확인합니다.
    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (parameterType == AnimatorControllerParameterType.Trigger)
        {
            return animatorTriggerParameters.Contains(parameterHash);
        }

        if (parameterType == AnimatorControllerParameterType.Int)
        {
            return animatorIntParameters.Contains(parameterHash);
        }

        return false;
    }

    // Animator에 해당 Int 파라미터가 존재할 때만 안전하게 값을 설정합니다.
    private void SetAnimatorInteger(int parameterHash, int value)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Int))
        {
            UnitAnimator.SetInteger(parameterHash, value);
        }
    }

    // 기본 공격 상태를 시작하고 공통 공격 애니메이션 트리거를 실행합니다.
    protected virtual void StartBasicAttack()
    {
        attackInProgress = true;
        currentAttack = BasicAttack;
        nextActionTime = Time.time + CurrentBasicAttackInterval;
        SetAnimatorSpeed(CurrentBasicAttackAnimationSpeed);
        SetAnimatorInteger(AttackTypeHash, 0);
        SetAnimatorTrigger(OnAttackHash);
        OnBasicAttackStarted();
    }

    // 기본 공격 명령 직후 직업이나 몬스터별 로직을 실행하는 확장 지점입니다.
    protected virtual void OnBasicAttackStarted() { }

    // 스킬 구현 클래스가 공격 데이터와 함께 공격 애니메이션을 실행할 때 사용합니다.
    protected void PlayAttackAnimation(SkillData attack)
    {
        if (attack == null)
        {
            return;
        }

        attackInProgress = true;
        currentAttack = attack;
        SetAnimatorSpeed(1f);
        SetAnimatorInteger(AttackTypeHash, (int)attack.AttackType);
        SetAnimatorTrigger(OnAttackHash);
    }

    // 기본 공격과 스킬에 필요한 Animator 전체 재생 속도를 안전하게 적용합니다.
    private void SetAnimatorSpeed(float speed)
    {
        if (UnitAnimator != null)
        {
            UnitAnimator.speed = Mathf.Max(0.01f, speed);
        }
    }
    
    // 진행 중인 공격을 취소하고 현재 공격 데이터를 정리합니다.
    private void CancelAttack()
    {
        attackInProgress = false;
        currentAttack = null;
        SetAnimatorSpeed(1f);
    }

    // 애니메이션 함수 : 실제 타격 프레임에서 Box 범위 안의 모든 적에게 피해를 적용합니다.
    public void ApplyAttackHit()
    {
        if (currentAttack == null || IsDead)
        {
            return;
        }

        Vector2 center = transform.TransformPoint(currentAttack.HitboxOffset);
        Vector3 scale = transform.lossyScale;
        Vector2 size = Vector2.Scale(
            currentAttack.HitboxSize,
            new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
        );

        int resultCount = Physics2D.OverlapBox(
            center,
            size,
            transform.eulerAngles.z,
            enemyFilter,
            detectionResults
        );

        hitUnits.Clear();
        int damage = Mathf.Max(1, Mathf.RoundToInt(AttackPower * currentAttack.DamageMultiplier));

        for (int i = 0; i < resultCount; i++)
        {
            Collider2D hitCollider = detectionResults[i];
            Unit hitUnit = hitCollider != null ? hitCollider.GetComponentInParent<Unit>() : null;

            if (!CanTarget(hitUnit) || !hitUnits.Add(hitUnit))
            {
                continue;
            }

            hitUnit.TakeDamage(damage);
        }
    }

    // 애니메이션 함수 : 공격 종료를 처리하고 다음 프레임의 Idle 전환을 예약합니다.
    public void OnAttackAnimationFinished()
    {
        if (!attackInProgress)
        {
            currentAttack = null;
            return;
        }

        attackInProgress = false;
        currentAttack = null;
        SetAnimatorSpeed(1f);
        idleAnimationPending = true;
    }

    // 애니메이션 함수 : 사망 종료 프레임에서 비활성화 상태로 숨김
    public void OnDieAnimationFinished()
    {
        if (!IsDead)
        {
            return;
        }

        gameObject.SetActive(false);
        SetAnimatorTrigger(OnIdleHash);
    }

    #endregion

    // 피해를 현재 체력에 적용하고 체력이 소진되면 사망 상태로 전환합니다.
    public virtual void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        curHp = Mathf.Max(curHp - damage, 0);
        Debug.Log($"{this.name}의 현재 체력 : {curHp}");

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
        nextActionTime = 0f;
    }

    // Scene 뷰에서 선택한 유닛의 탐지 범위와 기본 공격 Box 범위를 표시합니다.
    protected virtual void OnDrawGizmosSelected()
    {
        UnitStats sourceStats = BaseStats;
        float attackRange = RuntimeStats != null
            ? RuntimeStats.AttackRange
            : sourceStats != null ? sourceStats.AttackRange : DefaultStats.AttackRange;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        SkillData previewAttack = RuntimeStats != null
            ? RuntimeStats.BasicAttack
            : sourceStats != null ? sourceStats.BasicAttack : null;

        if (previewAttack == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(previewAttack.HitboxOffset, previewAttack.HitboxSize);
        Gizmos.matrix = previousMatrix;
    }
}
