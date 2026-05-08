using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CooldownSystem))]
// Common movement and action API used by manual input now, and BT/RL controllers later.
public class CombatActionController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float dodgeImpulse = 2f;
    [SerializeField] private float dodgeInvincibleDuration = 0.35f;
    [SerializeField] private float blockDuration = 0.75f;
    [SerializeField] private float attackDuration = 0.35f;

    [SerializeField] private Rigidbody body;
    [SerializeField] private CooldownSystem cooldownSystem;
    [SerializeField] private CombatCharacter character;
    [SerializeField] private CombatHitDetector hitDetector;
    [SerializeField] private CombatAnimatorDriver animatorDriver;
    [SerializeField] private bool useAnimationEventHit = true;

    private Coroutine attackRoutine;
    private Coroutine blockRoutine;
    private Coroutine dodgeRoutine;
    private bool attackHitResolved;
    private int lastMoveFrame = -1;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public float DodgeImpulse
    {
        get => dodgeImpulse;
        set => dodgeImpulse = Mathf.Max(0f, value);
    }

    public float DodgeInvincibleDuration
    {
        get => dodgeInvincibleDuration;
        set => dodgeInvincibleDuration = Mathf.Max(0f, value);
    }

    public float BlockDuration
    {
        get => blockDuration;
        set => blockDuration = Mathf.Max(0f, value);
    }

    public float AttackDuration
    {
        get => attackDuration;
        set => attackDuration = Mathf.Max(0f, value);
    }

    public bool IsBlocking { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsBusy => IsAttacking || IsBlocking || IsInvincible;

    private void Awake()
    {
        FillDefaultReferences();
    }

    private void Reset()
    {
        FillDefaultReferences();
    }

    private void LateUpdate()
    {
        animatorDriver?.SetMoving(lastMoveFrame == Time.frameCount && !IsBusy);
    }

    public void Move(Vector3 direction)
    {
        if (!TryGetMoveDirection(direction, out Vector3 horizontalDirection))
        {
            return;
        }

        body.MovePosition(body.position + horizontalDirection * moveSpeed * Time.deltaTime);
        Face(horizontalDirection);
        lastMoveFrame = Time.frameCount;
    }

    public void Face(Vector3 direction)
    {
        if (!CanAct() || IsBusy)
        {
            return;
        }

        Vector3 horizontalDirection = Flatten(direction);
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        horizontalDirection.Normalize();
        transform.rotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
    }

    public void Attack()
    {
        if (!CanAct() || IsBusy || !cooldownSystem.IsAttackReady())
        {
            return;
        }

        cooldownSystem.TriggerAttackCooldown();
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    public void Block()
    {
        if (!CanAct() || IsBusy || !cooldownSystem.IsBlockReady())
        {
            return;
        }

        cooldownSystem.TriggerBlockCooldown();
        blockRoutine = StartCoroutine(BlockRoutine());
    }

    public void Dodge(Vector3 direction)
    {
        if (!CanAct() || IsBusy || !cooldownSystem.IsDodgeReady())
        {
            return;
        }

        Vector3 dodgeDirection = Flatten(direction);
        if (dodgeDirection.sqrMagnitude <= 0.0001f)
        {
            dodgeDirection = Flatten(transform.forward);
        }

        dodgeDirection.Normalize();
        cooldownSystem.TriggerDodgeCooldown();
        dodgeRoutine = StartCoroutine(DodgeRoutine(dodgeDirection));
    }

    public void ResetActionState()
    {
        StopRunningCoroutine(ref attackRoutine);
        StopRunningCoroutine(ref blockRoutine);
        StopRunningCoroutine(ref dodgeRoutine);

        IsAttacking = false;
        IsBlocking = false;
        IsInvincible = false;
        attackHitResolved = false;
        lastMoveFrame = -1;
        animatorDriver?.ResetAnimationState();
    }

    public void OnAttackHitFrame()
    {
        if (!IsAttacking || attackHitResolved)
        {
            return;
        }

        attackHitResolved = true;
        hitDetector?.TryHit();
    }

    public void PlayHitReaction()
    {
        animatorDriver?.PlayHit();
    }

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;
        attackHitResolved = false;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} started attack.");
        }

        animatorDriver?.PlayAttack();

        if (animatorDriver == null || !useAnimationEventHit)
        {
            OnAttackHitFrame();
        }

        yield return new WaitForSeconds(attackDuration);

        IsAttacking = false;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} ended attack.");
        }

        attackRoutine = null;
    }

    private IEnumerator BlockRoutine()
    {
        IsBlocking = true;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} started block.");
        }

        animatorDriver?.SetBlocking(true);

        yield return new WaitForSeconds(blockDuration);

        IsBlocking = false;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} ended block.");
        }

        animatorDriver?.SetBlocking(false);
        blockRoutine = null;
    }

    private IEnumerator DodgeRoutine(Vector3 direction)
    {
        IsInvincible = true;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} started dodge.");
        }

        animatorDriver?.PlayDodge();
        body.AddForce(direction * dodgeImpulse, ForceMode.VelocityChange);

        yield return new WaitForSeconds(dodgeInvincibleDuration);

        IsInvincible = false;
        if (!ShouldSuppressCombatDebug())
        {
            Debug.Log($"{name} ended dodge.");
        }

        dodgeRoutine = null;
    }

    private bool ShouldSuppressCombatDebug()
    {
        StudentCombatAgent[] agents = FindObjectsByType<StudentCombatAgent>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (StudentCombatAgent agent in agents)
        {
            if (agent.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanAct()
    {
        return character == null || !character.IsDead;
    }

    private bool TryGetMoveDirection(Vector3 direction, out Vector3 horizontalDirection)
    {
        horizontalDirection = Flatten(direction);
        if (!CanAct() || IsBusy || horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalDirection.Normalize();
        return true;
    }

    private Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction;
    }

    private void FillDefaultReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (cooldownSystem == null)
        {
            cooldownSystem = GetComponent<CooldownSystem>();
        }

        if (character == null)
        {
            character = GetComponent<CombatCharacter>();
        }

        if (hitDetector == null)
        {
            hitDetector = GetComponent<CombatHitDetector>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<CombatAnimatorDriver>();
        }
    }

    private void StopRunningCoroutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }
}
