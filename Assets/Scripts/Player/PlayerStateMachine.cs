using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    // Referencias
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    // Estados disponibles
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Jumping,
        DoubleJumping,
        Falling,
        Landing,
        Crouching,
        IdleCrouching,
        WalkingCrouching,
        Interacting
    }

    // Estado actual y previo
    private PlayerState currentState;
    private PlayerState previousState;

    // Diccionario para mapear estados a sus comportamientos
    private Dictionary<PlayerState, IPlayerState> states = new Dictionary<PlayerState, IPlayerState>();

    private void Awake()
    {
        // Inicializar estados
        states.Add(PlayerState.Idle, new PlayerIdleState(this, animator, playerController));
        states.Add(PlayerState.Walking, new PlayerWalkingState(this, animator, playerController));
        states.Add(PlayerState.Running, new PlayerRunningState(this, animator, playerController));
        states.Add(PlayerState.Jumping, new PlayerJumpingState(this, animator, playerController));
        states.Add(PlayerState.DoubleJumping, new PlayerDoubleJumpingState(this, animator, playerController));
        states.Add(PlayerState.Falling, new PlayerFallingState(this, animator, playerController));
        states.Add(PlayerState.Landing, new PlayerLandingState(this, animator, playerController));
        states.Add(PlayerState.Crouching, new PlayerCrouchingState(this, animator, playerController));
        states.Add(PlayerState.IdleCrouching, new PlayerIdleCrouchingState(this, animator, playerController));
        states.Add(PlayerState.WalkingCrouching, new PlayerWalkingCrouchingState(this, animator, playerController));
        states.Add(PlayerState.Interacting, new PlayerInteractingState(this, animator, playerController));
        
        // Estado inicial
        TransitionToState(PlayerState.Idle);
    }

    private void Update()
    {
        // Verificar y actualizar el estado actual
        if (states.TryGetValue(currentState, out IPlayerState currentStateObj))
        {
            PlayerState newState = currentStateObj.UpdateState();
            
            // Si se requiere un cambio de estado
            if (newState != currentState)
            {
                TransitionToState(newState);
            }
        }
    }

    public void TransitionToState(PlayerState newState)
    {
        // Guardar el estado previo
        previousState = currentState;
        
        // Ejecutar la salida del estado anterior
        if (states.TryGetValue(previousState, out IPlayerState prevStateObj))
        {
            prevStateObj.ExitState();
        }
        
        // Actualizar el estado actual
        currentState = newState;
        
        // Ejecutar la entrada al nuevo estado
        if (states.TryGetValue(currentState, out IPlayerState newStateObj))
        {
            newStateObj.EnterState();
        }
    }
    
    // Método para verificar si un estado puede transicionar a otro
    public bool CanTransitionTo(PlayerState from, PlayerState to)
    {
        // Definir las transiciones permitidas aquí
        switch (from)
        {
            case PlayerState.Idle:
                // Desde Idle podemos ir a varios estados
                return to == PlayerState.Walking || 
                       to == PlayerState.Running || 
                       to == PlayerState.Jumping || 
                       to == PlayerState.Crouching ||
                       to == PlayerState.Interacting;
                
            case PlayerState.Walking:
                return to == PlayerState.Idle ||
                       to == PlayerState.Running ||
                       to == PlayerState.Jumping ||
                       to == PlayerState.Crouching ||
                       to == PlayerState.Interacting ||
                       to == PlayerState.Falling;
                
            case PlayerState.Running:
                return to == PlayerState.Idle ||
                       to == PlayerState.Walking ||
                       to == PlayerState.Jumping ||
                       to == PlayerState.Falling;
                
            case PlayerState.Jumping:
                return to == PlayerState.DoubleJumping ||
                       to == PlayerState.Falling;
                
            case PlayerState.DoubleJumping:
                return to == PlayerState.Falling;
                
            case PlayerState.Falling:
                return to == PlayerState.Landing;
                
            case PlayerState.Landing:
                return to == PlayerState.Idle ||
                       to == PlayerState.Walking ||
                       to == PlayerState.Running;
                
            case PlayerState.Crouching:
                return to == PlayerState.IdleCrouching;
                
            case PlayerState.IdleCrouching:
                return to == PlayerState.WalkingCrouching ||
                       to == PlayerState.Idle;
                
            case PlayerState.WalkingCrouching:
                return to == PlayerState.IdleCrouching ||
                       to == PlayerState.Walking;
                
            case PlayerState.Interacting:
                return to == PlayerState.Idle;
                
            default:
                return false;
        }
    }
    
    // Obtener el estado actual
    public PlayerState GetCurrentState()
    {
        return currentState;
    }
    
    // Obtener el estado previo
    public PlayerState GetPreviousState()
    {
        return previousState;
    }
}

// Interfaz para todos los estados del jugador
public interface IPlayerState
{
    void EnterState();
    PlayerStateMachine.PlayerState UpdateState();
    void ExitState();
}

// Implementaciones de estados concretos
public class PlayerIdleState : IPlayerState
{
    private PlayerStateMachine stateMachine;
    private Animator animator;
    private PlayerController controller;

    public PlayerIdleState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller)
    {
        this.stateMachine = stateMachine;
        this.animator = animator;
        this.controller = controller;
    }

    public void EnterState()
    {
        // Activar la animación de idle
        animator.SetFloat("Speed", 0);
    }

    public PlayerStateMachine.PlayerState UpdateState()
    {
        // Comprobar condiciones para cambiar a otros estados
        if (controller.IsMoving() && controller.IsRunning())
        {
            return PlayerStateMachine.PlayerState.Running;
        }
        else if (controller.IsMoving())
        {
            return PlayerStateMachine.PlayerState.Walking;
        }
        else if (controller.IsActuallyJumping())
        {
            return PlayerStateMachine.PlayerState.Jumping;
        }
        else if (controller.IsCrouching())
        {
            return PlayerStateMachine.PlayerState.Crouching;
        }
        else if (controller.IsInteracting())
        {
            return PlayerStateMachine.PlayerState.Interacting;
        }

        // Permanecer en el mismo estado
        return PlayerStateMachine.PlayerState.Idle;
    }

    public void ExitState()
    {
        // Limpiar/resetear cualquier cosa necesaria al salir del estado
    }
}

// Ejemplo de otro estado implementado
public class PlayerJumpingState : IPlayerState
{
    private PlayerStateMachine stateMachine;
    private Animator animator;
    private PlayerController controller;
    private float jumpStartTime;

    public PlayerJumpingState(PlayerStateMachine stateMachine, Animator animator, PlayerController controller)
    {
        this.stateMachine = stateMachine;
        this.animator = animator;
        this.controller = controller;
    }

    public void EnterState()
    {
        // Activar la animación de salto
        animator.SetBool("IsJumping", true);
        animator.SetBool("IsGrounded", false);
        
        // Registrar el tiempo de inicio del salto
        jumpStartTime = Time.time;
    }

    public PlayerStateMachine.PlayerState UpdateState()
    {
        // Transición a caída después de cierto tiempo o si la velocidad vertical es negativa
        if (Time.time - jumpStartTime > 0.5f || controller.GetVerticalVelocity() < 0)
        {
            return PlayerStateMachine.PlayerState.Falling;
        }

        return PlayerStateMachine.PlayerState.Jumping;
    }

    public void ExitState()
    {
        animator.SetBool("IsJumping", false);
    }
}