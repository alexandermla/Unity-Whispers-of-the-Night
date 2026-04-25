using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Este script contiene todas las implementaciones de los estados del jugador

// Estado base abstracto para compartir código común
public abstract class BasePlayerState : IPlayerState
{
    protected PlayerStateMachine stateMachine;
    protected Animator animator;
    protected PlayerController controller;

    public BasePlayerState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller)
    {
        this.stateMachine = stateMachine;
        this.animator = animator;
        this.controller = controller;
    }

    public virtual void EnterState() { }
    public abstract PlayerStateMachine.PlayerState UpdateState();
    public virtual void ExitState() { }
}

// Estado de caminar
public class PlayerWalkingState : BasePlayerState
{
    public PlayerWalkingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de caminar
        animator.SetFloat("Speed", 2.0f);
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar transiciones a otros estados
        if (!controller.IsMoving())
        {
            return PlayerStateMachine.PlayerState.Idle;
        }
        else if (controller.IsRunning())
        {
            return PlayerStateMachine.PlayerState.Running;
        }
        else if (controller.IsActuallyJumping())
        {
            return PlayerStateMachine.PlayerState.Jumping;
        }
        else if (controller.IsCrouching())
        {
            return PlayerStateMachine.PlayerState.WalkingCrouching;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Walking;
    }

    public override void ExitState()
    {
        // Limpiar cualquier cosa al salir del estado
    }
}

// Estado de correr
public class PlayerRunningState : BasePlayerState
{
    public PlayerRunningState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de correr
        animator.SetFloat("Speed", 6.0f);
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar transiciones a otros estados
        if (!controller.IsMoving())
        {
            return PlayerStateMachine.PlayerState.Idle;
        }
        else if (!controller.IsRunning())
        {
            return PlayerStateMachine.PlayerState.Walking;
        }
        else if (controller.IsActuallyJumping())
        {
            return PlayerStateMachine.PlayerState.Jumping;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Running;
    }
}

// Estado de caída
public class PlayerFallingState : BasePlayerState
{
    public PlayerFallingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de caída
        animator.SetBool("IsFalling", true);
        animator.SetBool("IsGrounded", false);
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar si el jugador ha aterrizado
        if (controller.IsGrounded())
        {
            return PlayerStateMachine.PlayerState.Landing;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Falling;
    }

    public override void ExitState()
    {
        animator.SetBool("IsFalling", false);
    }
}

// Estado de doble salto
public class PlayerDoubleJumpingState : BasePlayerState
{
    private float jumpStartTime;

    public PlayerDoubleJumpingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de doble salto
        animator.SetTrigger("DoubleJump");
        jumpStartTime = Time.time;
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Transición a caída después de cierto tiempo o si la velocidad vertical es negativa
        if (Time.time - jumpStartTime > 0.5f || controller.GetVerticalVelocity() < 0)
        {
            return PlayerStateMachine.PlayerState.Falling;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.DoubleJumping;
    }
}

// Estado de aterrizaje
public class PlayerLandingState : BasePlayerState
{
    private float landingTime;
    private float landingDuration = 0.3f;

    public PlayerLandingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de aterrizaje
        animator.SetBool("IsLanding", true);
        landingTime = Time.time;
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Transición a idle/walk/run después de la duración del aterrizaje
        if (Time.time - landingTime > landingDuration)
        {
            if (controller.IsMoving())
            {
                if (controller.IsRunning())
                {
                    return PlayerStateMachine.PlayerState.Running;
                }
                else
                {
                    return PlayerStateMachine.PlayerState.Walking;
                }
            }
            else
            {
                return PlayerStateMachine.PlayerState.Idle;
            }
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Landing;
    }

    public override void ExitState()
    {
        animator.SetBool("IsLanding", false);
    }
}

// Estado de agacharse (transición)
public class PlayerCrouchingState : BasePlayerState
{
    private float crouchStartTime;
    private float crouchTransitionDuration = 0.2f;

    public PlayerCrouchingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de agacharse
        animator.SetBool("IsCrouching", true);
        crouchStartTime = Time.time;
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Transición a IdleCrouch después de la duración de la transición
        if (Time.time - crouchStartTime > crouchTransitionDuration)
        {
            if (controller.IsMoving())
            {
                return PlayerStateMachine.PlayerState.WalkingCrouching;
            }
            else
            {
                return PlayerStateMachine.PlayerState.IdleCrouching;
            }
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Crouching;
    }
}

// Estado de reposo agachado
public class PlayerIdleCrouchingState : BasePlayerState
{
    public PlayerIdleCrouchingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de reposo agachado
        animator.SetBool("IsCrouching", true);
        animator.SetFloat("CrouchSpeed", 0);
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar transiciones a otros estados
        if (!controller.IsCrouching())
        {
            return PlayerStateMachine.PlayerState.Idle;
        }
        else if (controller.IsMoving())
        {
            return PlayerStateMachine.PlayerState.WalkingCrouching;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.IdleCrouching;
    }

    public override void ExitState()
    {
        if (!controller.IsCrouching())
        {
            animator.SetBool("IsCrouching", false);
        }
    }
}

// Estado de caminar agachado
public class PlayerWalkingCrouchingState : BasePlayerState
{
    public PlayerWalkingCrouchingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de caminar agachado
        animator.SetBool("IsCrouching", true);
        animator.SetFloat("CrouchSpeed", 1.5f);
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar transiciones a otros estados
        if (!controller.IsCrouching())
        {
            return PlayerStateMachine.PlayerState.Walking;
        }
        else if (!controller.IsMoving())
        {
            return PlayerStateMachine.PlayerState.IdleCrouching;
        }
        else if (controller.IsRunning())
        {
            // Si intentan correr mientras están agachados, el personaje se levanta
            return PlayerStateMachine.PlayerState.Running;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.WalkingCrouching;
    }

    public override void ExitState()
    {
        if (!controller.IsCrouching())
        {
            animator.SetBool("IsCrouching", false);
        }
    }
}

// Estado de interacción
public class PlayerInteractingState : BasePlayerState
{
    private float interactionStartTime;
    private float interactionDuration = 1.0f;

    public PlayerInteractingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller) 
        : base(stateMachine, animator, controller) { }

    public override void EnterState()
    {
        // Configurar la animación de interacción
        animator.SetTrigger("Interact");
        interactionStartTime = Time.time;
    }

    public override PlayerStateMachine.PlayerState UpdateState()
    {
        // Transición a idle después de la duración de la interacción
        if (Time.time - interactionStartTime > interactionDuration)
        {
            return PlayerStateMachine.PlayerState.Idle;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Interacting;
    }
}