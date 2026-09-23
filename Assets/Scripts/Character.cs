using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Character : Unit
{
    public enum AttackType
    {
        BasicAttack = 0,
        Skill1 = 1,
        Skill2 = 2,
        Skill3 = 3
    }

    [Serializable]
    private sealed class SkillAttack
    {
        [SerializeField] private AttackType attackType = AttackType.Skill1;
        [SerializeField] private bool learned;
        [SerializeField, Min(0f)] private float cooldown = 5f;

        private float nextReadyTime;

        public AttackType Type => attackType;

        public bool CanUse(float currentTime)
        {
            return learned && currentTime >= nextReadyTime;
        }

        public void MarkUsed(float currentTime)
        {
            nextReadyTime = currentTime + cooldown;
        }
    }

    [Header("Attack")]
    [Tooltip("기본 공격 애니메이션이 끝난 뒤 적용할 기본 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float baseAttackInterval = 0.5f;

    [Tooltip("100이 기본 속도입니다. 200이면 대기 시간이 절반이 됩니다.")]
    [SerializeField, Min(1f)] private float attackSpeedPercent = 100f;

    [Tooltip("위에 있는 스킬부터 우선 사용합니다. 습득하지 않았거나 쿨타임이면 다음 스킬을 확인합니다.")]
    [SerializeField] private SkillAttack[] skillsByPriority;

    [Header("Hitbox")]
    [SerializeField] private Collider2D attackHitbox;

    private Animator animator;
    private AttackType currentAttackType = AttackType.BasicAttack;
    private bool isAttackInProgress;
    private float nextAttackTime;

    private static readonly int AttackTypeHash =
        Animator.StringToHash("AttackType");

    private static readonly int OnIdleHash =
        Animator.StringToHash("OnIdle");

    private static readonly int OnMoveHash =
        Animator.StringToHash("OnMove");

    private static readonly int OnAttackHash =
        Animator.StringToHash("OnAttack");

    private static readonly int OnDieHash =
        Animator.StringToHash("OnDie");

    public float AttackSpeedPercent => attackSpeedPercent;

    private float CurrentAttackInterval =>
        baseAttackInterval * (100f / attackSpeedPercent);

    protected override void Awake()
    {
        base.Awake();

        animator = GetComponent<Animator>();

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void EnterState(UnitState state)
    {
        base.EnterState(state);

        ResetStateTriggers();

        switch (state)
        {
            case UnitState.Idle:
                animator.SetTrigger(OnIdleHash);
                break;

            case UnitState.Move:
                animator.SetTrigger(OnMoveHash);
                break;

            case UnitState.Attack:
                // 실제 공격 시작은 OnAttackState에서 대기 시간을 확인한 뒤 처리합니다.
                break;

            case UnitState.Die:
                CancelAttack();
                animator.SetTrigger(OnDieHash);
                break;
        }
    }

    protected override void ExitState(UnitState state)
    {
        base.ExitState(state);

        if (state == UnitState.Attack)
        {
            CancelAttack();
        }
    }

    protected override void OnIdleState()
    {
    }

    protected override void OnMoveState()
    {
        // Rigidbody2D 이동 로직을 연결할 위치입니다.
    }

    protected override void OnAttackState()
    {
        if (isAttackInProgress || Time.time < nextAttackTime)
        {
            return;
        }

        if (CurrentTarget == null || !CurrentTarget.IsTargetable)
        {
            return;
        }

        StartNextAttack();
    }

    protected override void OnDieState()
    {
    }

    private void StartNextAttack()
    {
        currentAttackType = SelectAttackByPriority();
        isAttackInProgress = true;

        animator.SetInteger(AttackTypeHash, (int)currentAttackType);
        animator.SetTrigger(OnAttackHash);
    }

    private AttackType SelectAttackByPriority()
    {
        if (skillsByPriority != null)
        {
            for (int i = 0; i < skillsByPriority.Length; i++)
            {
                SkillAttack skill = skillsByPriority[i];

                if (skill == null || !skill.CanUse(Time.time))
                {
                    continue;
                }

                skill.MarkUsed(Time.time);
                return skill.Type;
            }
        }

        return AttackType.BasicAttack;
    }

    private void CancelAttack()
    {
        isAttackInProgress = false;

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    private void ResetStateTriggers()
    {
        animator.ResetTrigger(OnIdleHash);
        animator.ResetTrigger(OnMoveHash);
        animator.ResetTrigger(OnAttackHash);
        animator.ResetTrigger(OnDieHash);
    }

    // 공격 애니메이션의 타격 시작 프레임에서 호출하는 Animation Event입니다.
    public void EnableAttackHitbox()
    {
        if (isAttackInProgress && attackHitbox != null)
        {
            attackHitbox.enabled = true;
        }
    }

    // 공격 애니메이션의 타격 종료 프레임에서 호출하는 Animation Event입니다.
    public void DisableAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    // 공격 애니메이션의 마지막 프레임에서 호출하는 Animation Event입니다.
    public void OnAttackAnimationFinished()
    {
        DisableAttackHitbox();

        if (!isAttackInProgress)
        {
            return;
        }

        isAttackInProgress = false;
        nextAttackTime = Time.time + CurrentAttackInterval;
    }
}
