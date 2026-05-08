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
    [SerializeField] private bool enableStudentAttackTestReward;
    [SerializeField] private float observationDistance = 10f;
    [SerializeField] private float closeRangeSkillDistance = 3f;

    private const int SkillNone = 0;
    private const int SkillAttack = 1;
    private const int SkillBlock = 2;
    private const int SkillDodge = 3;

    private float previousSelfHealth;
    private float previousOpponentHealth;
    private float previousDistanceToOpponent;
    private bool studentTestRewardStateInitialized;


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

        InitializeStudentTestRewardState();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (self == null || opponent == null)
        {
            Debug.Log("CollectObservations returned early.");
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
        if (actionController == null || actions.DiscreteActions.Length < 1)
        {
            Debug.Log("OnActionReceived returned early.");
            return;
        }

        int skillAction = GetSkillAction(actions.DiscreteActions);
        Vector3 moveDirection = GetDirectionToOpponent();

        actionController.Move(moveDirection);
        ExecuteSkill(skillAction, moveDirection);

        // TODO: Students should design and justify their own reward function.
        if (enableInternalTestReward)
        {
            AddReward(-0.001f);
        }

        if (enableStudentAttackTestReward)
        {
            AddStudentAttackTestReward(skillAction);
        }

        CheckEpisodeEndForAgent();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        if (discreteActions.Length < 2)
        {
            if (discreteActions.Length < 1)
            {
                return;
            }

            discreteActions[0] = ReadSkillInput();
            return;
        }

        discreteActions[0] = 0;
        discreteActions[1] = ReadSkillInput();
    }

    private int ReadSkillInput()
    {
        if (Input.GetKey(KeyCode.J))
        {
            return SkillAttack;
        }

        if (Input.GetKey(KeyCode.K))
        {
            return SkillBlock;
        }

        if (Input.GetKey(KeyCode.L))
        {
            return SkillDodge;
        }

        return SkillNone;
    }

    private int GetSkillAction(ActionSegment<int> discreteActions)
    {
        if (discreteActions.Length >= 2)
        {
            return discreteActions[1];
        }

        return discreteActions[0];
    }

    private Vector3 GetDirectionToOpponent()
    {
        if (self == null || opponent == null)
        {
            return transform.forward;
        }

        Vector3 offset = opponent.transform.position - self.transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= 0.0001f ? transform.forward : offset.normalized;
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

    private void InitializeStudentTestRewardState()
    {
        if (self == null || opponent == null)
        {
            studentTestRewardStateInitialized = false;
            return;
        }

        previousSelfHealth = self.CurrentHealth;
        previousOpponentHealth = opponent.CurrentHealth;
        previousDistanceToOpponent = GetDistanceToOpponent();
        studentTestRewardStateInitialized = true;
    }

    private void AddStudentAttackTestReward(int skillAction)
    {
        if (self == null || opponent == null)
        {
            studentTestRewardStateInitialized = false;
            return;
        }

        if (!studentTestRewardStateInitialized)
        {
            InitializeStudentTestRewardState();
            return;
        }

        if (opponent.CurrentHealth < previousOpponentHealth)
        {
            AddReward(0.2f);
        }

        float currentDistanceToOpponent = GetDistanceToOpponent();
        if (currentDistanceToOpponent > closeRangeSkillDistance
            && (skillAction == SkillAttack || skillAction == SkillDodge))
        {
            AddReward(-0.3f);
        }

        if (currentDistanceToOpponent < previousDistanceToOpponent)
        {
            AddReward(0.2f);
        }

        if (currentDistanceToOpponent > previousDistanceToOpponent)
        {
            AddReward(-0.2f);
        }

        if (self.CurrentHealth < previousSelfHealth)
        {
            AddReward(-0.1f);
        }

        previousSelfHealth = self.CurrentHealth;
        previousOpponentHealth = opponent.CurrentHealth;
        previousDistanceToOpponent = currentDistanceToOpponent;
    }

    private float GetDistanceToOpponent()
    {
        if (self == null || opponent == null)
        {
            return 0f;
        }

        Vector3 offset = opponent.transform.position - self.transform.position;
        offset.y = 0f;
        return offset.magnitude;
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
