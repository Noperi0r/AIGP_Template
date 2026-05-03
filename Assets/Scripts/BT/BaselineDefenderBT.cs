using UnityEngine;

[RequireComponent(typeof(CombatCharacter))]
[RequireComponent(typeof(CooldownSystem))]
[RequireComponent(typeof(CombatActionController))]
// Simple reference defender for BT/RL comparison. It favors block/dodge over attacking.
public class BaselineDefenderBT : MonoBehaviour
{
    [SerializeField] private CombatCharacter self;
    [SerializeField] private CombatCharacter target;
    [SerializeField] private CombatActionController actionController;
    [SerializeField] private CooldownSystem cooldownSystem;
    [SerializeField] private float closeDistance = 2.0f;
    [SerializeField] private float preferredDistance = 3.0f;
    [SerializeField] private float facingAngle = 60f;
    [SerializeField] private float lowHealthRatio = 0.3f;

    private BTNode root;

    private void Awake()
    {
        FillDefaultReferences();
        BuildTree();
    }

    private void Reset()
    {
        FillDefaultReferences();
    }

    private void Update()
    {
        if (!CanTick())
        {
            return;
        }

        root.Tick();
    }

    private void BuildTree()
    {
        root = new SelectorNode(
            new SequenceNode(
                new ConditionNode(ShouldDodge),
                new ActionNode(DodgeAway)),
            new RandomSelectorNode(
                new SequenceNode(
                    new ConditionNode(CanBlockCloseTarget),
                    new ActionNode(Block)),
                new SequenceNode(
                    new ConditionNode(CanDodgeCloseTarget),
                    new ActionNode(DodgeAway)),
                new SequenceNode(
                    new ConditionNode(CanCounterAttack),
                    new ActionNode(Attack))),
            new ActionNode(MaintainDistance));
    }

    private bool CanTick()
    {
        return root != null
            && self != null
            && target != null
            && actionController != null
            && !self.IsDead
            && !target.IsDead;
    }

    private bool ShouldDodge()
    {
        return self.CurrentHealthRatio <= lowHealthRatio
            && cooldownSystem != null
            && cooldownSystem.IsDodgeReady();
    }

    private bool CanBlockCloseTarget()
    {
        return IsTargetClose()
            && cooldownSystem != null
            && cooldownSystem.IsBlockReady();
    }

    private bool CanDodgeCloseTarget()
    {
        return IsTargetClose()
            && cooldownSystem != null
            && cooldownSystem.IsDodgeReady();
    }

    private bool CanCounterAttack()
    {
        return IsTargetClose()
            && IsFacingTarget()
            && cooldownSystem != null
            && cooldownSystem.IsAttackReady();
    }

    private BTNodeStatus Block()
    {
        actionController.Block();
        return BTNodeStatus.Success;
    }

    private BTNodeStatus DodgeAway()
    {
        actionController.Dodge(-GetDirectionToTarget());
        return BTNodeStatus.Success;
    }

    private BTNodeStatus Attack()
    {
        actionController.Attack();
        return BTNodeStatus.Success;
    }

    private BTNodeStatus MaintainDistance()
    {
        Vector3 offset = GetHorizontalOffsetToTarget();
        if (offset.magnitude < preferredDistance)
        {
            actionController.Move(-GetDirectionToTarget());
        }

        return BTNodeStatus.Running;
    }

    private bool IsTargetClose()
    {
        return GetHorizontalOffsetToTarget().magnitude <= closeDistance;
    }

    private bool IsFacingTarget()
    {
        Vector3 direction = GetDirectionToTarget();
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, direction) <= facingAngle;
    }

    private Vector3 GetDirectionToTarget()
    {
        Vector3 offset = GetHorizontalOffsetToTarget();
        return offset.sqrMagnitude <= 0.0001f ? transform.forward : offset.normalized;
    }

    private Vector3 GetHorizontalOffsetToTarget()
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        return offset;
    }

    private void FillDefaultReferences()
    {
        if (self == null)
        {
            self = GetComponent<CombatCharacter>();
        }

        if (actionController == null)
        {
            actionController = GetComponent<CombatActionController>();
        }

        if (cooldownSystem == null)
        {
            cooldownSystem = GetComponent<CooldownSystem>();
        }
    }
}
