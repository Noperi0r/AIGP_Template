using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CooldownSystem))]
// Common movement and action API used by manual input now, and BT/RL controllers later.
public class CombatActionController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float dodgeImpulse = 7f;
    [SerializeField] private float dodgeInvincibleDuration = 0.35f;
    [SerializeField] private float blockDuration = 0.75f;
    [SerializeField] private float attackDuration = 0.35f;

    [SerializeField] private Rigidbody body;
    [SerializeField] private CooldownSystem cooldownSystem;
    [SerializeField] private CombatCharacter character;
    [SerializeField] private CombatHitDetector hitDetector;

    private Coroutine attackRoutine;
    private Coroutine blockRoutine;
    private Coroutine dodgeRoutine;

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

    private void Awake()
    {
        FillDefaultReferences();
    }

    private void Reset()
    {
        FillDefaultReferences();
    }

    public void Move(Vector3 direction)
    {
        if (character != null && character.IsDead)
        {
            return;
        }

        Vector3 horizontalDirection = Flatten(direction);
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        horizontalDirection.Normalize();
        body.MovePosition(body.position + horizontalDirection * moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
    }

    public void Attack()
    {
        if (!CanAct() || IsAttacking || !cooldownSystem.IsAttackReady())
        {
            return;
        }

        cooldownSystem.TriggerAttackCooldown();
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    public void Block()
    {
        if (!CanAct() || IsBlocking || !cooldownSystem.IsBlockReady())
        {
            return;
        }

        cooldownSystem.TriggerBlockCooldown();
        blockRoutine = StartCoroutine(BlockRoutine());
    }

    public void Dodge(Vector3 direction)
    {
        if (!CanAct() || IsInvincible || !cooldownSystem.IsDodgeReady())
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
    }

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;
        hitDetector?.TryHit();

        yield return new WaitForSeconds(attackDuration);

        IsAttacking = false;
        attackRoutine = null;
    }

    private IEnumerator BlockRoutine()
    {
        IsBlocking = true;

        yield return new WaitForSeconds(blockDuration);

        IsBlocking = false;
        blockRoutine = null;
    }

    private IEnumerator DodgeRoutine(Vector3 direction)
    {
        IsInvincible = true;
        body.AddForce(direction * dodgeImpulse, ForceMode.VelocityChange);

        yield return new WaitForSeconds(dodgeInvincibleDuration);

        IsInvincible = false;
        dodgeRoutine = null;
    }

    private bool CanAct()
    {
        return character == null || !character.IsDead;
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
