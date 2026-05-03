using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(CombatCharacter))]
[RequireComponent(typeof(CooldownSystem))]
[RequireComponent(typeof(CombatActionController))]
// ML-Agents template that drives the same CombatActionController API as BT/manual input.
public class StudentCombatAgent : Agent
{
    public CombatCharacter self;
    public CombatCharacter opponent;
    public CombatActionController actionController;
    public CooldownSystem cooldownSystem;
    public EpisodeManager episodeManager;

    [SerializeField] private bool resetEpisodeOnBegin = true;
    [SerializeField] private bool isPrimaryResetAgent = true;
    [SerializeField] private bool enableInternalTestReward;
    [SerializeField] private float observationDistance = 10f;

    private const int MoveStay = 0;
    private const int MoveForward = 1;
    private const int MoveBackward = 2;
    private const int MoveLeft = 3;
    private const int MoveRight = 4;

    private const int SkillNone = 0;
    private const int SkillAttack = 1;
    private const int SkillBlock = 2;
    private const int SkillDodge = 3;

    public override void Initialize()
    {
        FillDefaultReferences();
        WarnIfMissingReferences();
    }

    private void Reset()
    {
        FillDefaultReferences();
    }

    public override void OnEpisodeBegin()
    {
        if (resetEpisodeOnBegin && isPrimaryResetAgent && episodeManager != null)
        {
            episodeManager.ResetEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (self == null || opponent == null)
        {
            AddEmptyObservations(sensor);
            return;
        }

        Vector3 relativePosition = opponent.transform.position - self.transform.position;
        relativePosition.y = 0f;
        Vector3 localRelativePosition = self.transform.InverseTransformDirection(relativePosition);
        float safeObservationDistance = Mathf.Max(0.001f, observationDistance);

        CombatActionController selfAction = self.ActionController;
        CombatActionController opponentAction = opponent.ActionController;
        CooldownSystem opponentCooldown = opponent.CooldownSystem;

        sensor.AddObservation(self.CurrentHealthRatio);
        sensor.AddObservation(opponent.CurrentHealthRatio);
        sensor.AddObservation(Mathf.Clamp(localRelativePosition.x / safeObservationDistance, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localRelativePosition.z / safeObservationDistance, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp01(relativePosition.magnitude / safeObservationDistance));
        sensor.AddObservation(cooldownSystem != null ? cooldownSystem.GetAttackCooldownRatio() : 0f);
        sensor.AddObservation(cooldownSystem != null ? cooldownSystem.GetBlockCooldownRatio() : 0f);
        sensor.AddObservation(cooldownSystem != null ? cooldownSystem.GetDodgeCooldownRatio() : 0f);
        sensor.AddObservation(opponentCooldown != null ? opponentCooldown.GetAttackCooldownRatio() : 0f);
        sensor.AddObservation(opponentCooldown != null ? opponentCooldown.GetBlockCooldownRatio() : 0f);
        sensor.AddObservation(opponentCooldown != null ? opponentCooldown.GetDodgeCooldownRatio() : 0f);
        sensor.AddObservation(selfAction != null && selfAction.IsBlocking ? 1f : 0f);
        sensor.AddObservation(selfAction != null && selfAction.IsInvincible ? 1f : 0f);
        sensor.AddObservation(opponentAction != null && opponentAction.IsBlocking ? 1f : 0f);
        sensor.AddObservation(opponentAction != null && opponentAction.IsInvincible ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (actionController == null || actions.DiscreteActions.Length < 2)
        {
            return;
        }

        int moveAction = actions.DiscreteActions[0];
        int skillAction = actions.DiscreteActions[1];
        Vector3 moveDirection = GetMoveDirection(moveAction);

        actionController.Move(moveDirection);
        ExecuteSkill(skillAction, moveDirection);

        // TODO: Students should design and justify their own reward function.
        if (enableInternalTestReward)
        {
            AddReward(-0.001f);
        }

        CheckEpisodeEndForAgent();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        if (discreteActions.Length < 2)
        {
            return;
        }

        discreteActions[0] = MoveStay;
        discreteActions[1] = SkillNone;

        if (Input.GetKey(KeyCode.W))
        {
            discreteActions[0] = MoveForward;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActions[0] = MoveBackward;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActions[0] = MoveLeft;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            discreteActions[0] = MoveRight;
        }

        if (Input.GetKey(KeyCode.J))
        {
            discreteActions[1] = SkillAttack;
        }
        else if (Input.GetKey(KeyCode.K))
        {
            discreteActions[1] = SkillBlock;
        }
        else if (Input.GetKey(KeyCode.L))
        {
            discreteActions[1] = SkillDodge;
        }
    }

    private Vector3 GetMoveDirection(int moveAction)
    {
        switch (moveAction)
        {
            case MoveForward:
                return transform.forward;
            case MoveBackward:
                return -transform.forward;
            case MoveLeft:
                return -transform.right;
            case MoveRight:
                return transform.right;
            default:
                return Vector3.zero;
        }
    }

    private void ExecuteSkill(int skillAction, Vector3 moveDirection)
    {
        switch (skillAction)
        {
            case SkillAttack:
                actionController.Attack();
                break;
            case SkillBlock:
                actionController.Block();
                break;
            case SkillDodge:
                actionController.Dodge(moveDirection.sqrMagnitude > 0.0001f ? moveDirection : transform.forward);
                break;
        }
    }

    private void CheckEpisodeEndForAgent()
    {
        bool episodeDone = episodeManager != null && episodeManager.IsEpisodeDone();
        bool selfDead = self != null && self.IsDead;
        bool opponentDead = opponent != null && opponent.IsDead;

        if (!episodeDone && !selfDead && !opponentDead)
        {
            return;
        }

        if (enableInternalTestReward)
        {
            if (opponentDead && !selfDead)
            {
                AddReward(1f);
            }
            else if (selfDead && !opponentDead)
            {
                AddReward(-1f);
            }
        }

        EndEpisode();
    }

    private void AddEmptyObservations(VectorSensor sensor)
    {
        for (int i = 0; i < 15; i++)
        {
            sensor.AddObservation(0f);
        }
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

        if (episodeManager == null)
        {
            episodeManager = FindFirstObjectByType<EpisodeManager>();
        }
    }

    private void WarnIfMissingReferences()
    {
        if (self == null)
        {
            Debug.LogWarning($"{name}: StudentCombatAgent missing self CombatCharacter reference.");
        }

        if (opponent == null)
        {
            Debug.LogWarning($"{name}: StudentCombatAgent missing opponent CombatCharacter reference.");
        }

        if (actionController == null)
        {
            Debug.LogWarning($"{name}: StudentCombatAgent missing CombatActionController reference.");
        }

        if (cooldownSystem == null)
        {
            Debug.LogWarning($"{name}: StudentCombatAgent missing CooldownSystem reference.");
        }

        if (episodeManager == null)
        {
            Debug.LogWarning($"{name}: StudentCombatAgent missing EpisodeManager reference.");
        }
    }
}
