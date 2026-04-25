using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [Header("Referencias")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Referencia a la cámara principal para cálculos de raycast y dirección.")]
    [SerializeField] private Camera mainCamera;
    public bool hasMovedWithWASD = false;

    [Header("UI References")]
    [SerializeField] private GameObject staminafillObj;
    [SerializeField] private GameObject staminaEmptyObj;
    //[SerializeField] private Image staminaFillImage; // Añadir referencia directa a la Image

    [Header("Configuración de Movimiento")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float runSpeed = 7.0f;
    [SerializeField] private float crouchSpeed = 2.0f;
    [SerializeField] private float turnSmoothTime = 0.05f;
    [SerializeField] private float speedChangeSmoothTime = 0.1f;
    [SerializeField] private float airControlFactor = 0.5f;

    [Header("Lock-On Aiming")] // Nueva sección para claridad
    public float aimLockOnRotationSpeed = 25f;

    [Header("Configuración de Salto")]
    [SerializeField] private float jumpHeight = 1.8f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float coyoteTimeDuration = 0.15f;
    [SerializeField] private float jumpBufferDuration = 0.15f;

    [Header("Configuración de Agachado")]
    [SerializeField] private float standingHeight = 2.0f;
    [SerializeField] private float crouchingHeight = 1.0f;
    [SerializeField] private Vector3 standingCenter = new Vector3(0, 1.0f, 0);
    [SerializeField] private Vector3 crouchingCenter = new Vector3(0, 0.5f, 0);
    [SerializeField] private float crouchObstacleCheckDistance = 0.1f;

    private ThirdPersonCamera thirdPersonCamera;

    private Vector3 currentMovementInput;
    private Vector3 lastMovementDirection;
    private float currentSpeed;
    private float targetSpeed;
    private float speedSmoothVelocity;
    private float turnSmoothVelocity;
    private float verticalVelocity;
    private Vector3 airVelocity;

    private bool isGrounded;
    private float lastTimeGrounded;
    private bool jumpInputPressed;
    private float jumpBufferTimer;
    private bool isActuallyJumping;
    private bool isFalling;
    private bool isLanding;
    private bool hasPlayedLandingSound;
    private bool isCrouching;
    private bool isSprinting;
    private float landingTimer;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 15f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float staminaRegenDelay = 1.5f;
    [SerializeField] private float minStaminaToSprint = 10f;
    public float currentStamina;
    private float timeSinceStaminaUsed = 0f;
    private bool canRegenerateStamina = true;

    [Header("Configuración Avanzada")]
    [SerializeField] private float animationBlendSpeed = 8.0f;
    [SerializeField] private float landAnimationDuration = 0.3f;
    [SerializeField] private float animationParameterSmoothTime = 0.1f; // Renombrado para claridad, antes speedChangeSmoothTime en SetFloat
    private float lastInteractTime = -1f;
    private float interactCooldown = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private AudioClip landingSound;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float landingVolume = 0.7f;

    private Transform currentTarget;
    private IInteractable currentInteractable;

    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int crouchSpeedHash = Animator.StringToHash("CrouchSpeed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int jumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private readonly int isFallingHash = Animator.StringToHash("IsFalling");
    private readonly int landTriggerHash = Animator.StringToHash("LandTrigger");
    private readonly int isCrouchingHash = Animator.StringToHash("IsCrouching");

    private InputSystem_Actions inputActions;

    public event Action<bool> onCrouchStateChanged;
    public event Action<bool> onAimStateChanged;
    public event Action onAttackPerformed;
    public event Action OnJumpPerformed;

    public bool isAiming { get; private set; }
    private bool isLockOn;

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (cameraTransform == null && mainCamera != null) cameraTransform = mainCamera.transform;
        thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>();

        inputActions = new InputSystem_Actions();
        inputActions.Player.SetCallbacks(this);

        currentStamina = maxStamina;
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void Update()
    {
        HandleJumpBuffer();
        CheckGrounded();
        HandleMovement();
        HandleJumpAndGravity();
        HandleCrouch();
        HandleStamina();
        UpdateAnimator();
        UpdateStaminaUI();
    }

    private void HandleJumpBuffer()
    {
        if (jumpBufferTimer > 0)
        {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    public void UpdateStaminaUI()
    {
        if (staminafillObj != null) // Solo necesitamos chequear staminafillObj para esto
        {
            // Es buena idea obtener el componente Image una vez y guardarlo si no lo has hecho.
            // Pero para este ejemplo, mantenemos tu lógica original.
            UnityEngine.UI.Image staminaImage = staminafillObj.GetComponent<UnityEngine.UI.Image>();
            if (staminaImage != null)
            {
                float fillAmount = currentStamina / maxStamina;
                staminaImage.fillAmount = fillAmount;
            }
            else
            {
                Debug.LogError("El GameObject 'staminafillObj' no tiene un componente Image.", this);
            }
        }
        else
        {
            Debug.LogError("'staminafillObj' no está asignado en el Inspector.", this);
        }
    }

    private void CheckGrounded()
    {
        Vector3 spherePosition = transform.position + characterController.center + Vector3.down * (characterController.height / 2f - groundCheckRadius + 0.05f);
        bool currentlyTouchingGround = Physics.SphereCast(spherePosition, groundCheckRadius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        if (currentlyTouchingGround)
        {
            if (!isGrounded && verticalVelocity < -0.1f)
            {
                isLanding = true;
                landingTimer = landAnimationDuration;
                hasPlayedLandingSound = false;
                animator.SetTrigger(landTriggerHash);
            }
            isGrounded = true;
            isActuallyJumping = false;
            isFalling = false;
            lastTimeGrounded = Time.time;
        }
        else
        {
            isGrounded = false;
            if (!isActuallyJumping && verticalVelocity < 0.1f)
            {
                isFalling = true;
            }
        }

        if (isLanding)
        {
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0)
            {
                isLanding = false;
            }
        }
    }

    private void HandleMovement()
    {
        float speedMultiplier = isCrouching ? crouchSpeed : (isSprinting ? runSpeed : walkSpeed);
        bool hasInput = currentMovementInput.magnitude > 0.1f;
        targetSpeed = hasInput ? speedMultiplier : 0f;

        if (isGrounded)
        {
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedChangeSmoothTime);
        }

        Vector3 moveDirectionRelativeToCamera = Vector3.zero;
        if (hasInput)
        {
            Vector3 cameraForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 cameraRight = Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized;
            moveDirectionRelativeToCamera = (cameraForward * currentMovementInput.z + cameraRight * currentMovementInput.x).normalized;
            lastMovementDirection = moveDirectionRelativeToCamera;
        }
        else if (!isGrounded)
        {
            moveDirectionRelativeToCamera = lastMovementDirection;
        }

        Vector3 horizontalMovement;
        if (isGrounded)
        {
            horizontalMovement = moveDirectionRelativeToCamera * currentSpeed;
            airVelocity = horizontalMovement; // Actualizar airVelocity aquí también
        }
        else
        {
            Vector3 airControlMovement = moveDirectionRelativeToCamera * (hasInput ? walkSpeed : 0f) * airControlFactor;
            Vector3 combinedAirVelocity = new Vector3(airVelocity.x, 0, airVelocity.z) + airControlMovement;
            float currentAirSpeed = new Vector2(combinedAirVelocity.x, combinedAirVelocity.z).magnitude;
            if (currentAirSpeed > runSpeed) // Limitar la velocidad en el aire si excede la de correr
            {
                combinedAirVelocity = combinedAirVelocity.normalized * runSpeed;
            }
            airVelocity.x = combinedAirVelocity.x;
            airVelocity.z = combinedAirVelocity.z;
            horizontalMovement = new Vector3(airVelocity.x, 0, airVelocity.z);
        }

        // --- MODIFICACIÓN PARA ROTACIÓN AL APUNTAR EN LOCK-ON ---
        if (isAiming && thirdPersonCamera != null && thirdPersonCamera.IsLockOnActive)
        {
            Transform lockOnTargetTransform = thirdPersonCamera.GetCurrentLockOnTarget();
            if (lockOnTargetTransform != null)
            {
                // Obtener el LockOnTarget para usar su GetTargetPoint si es necesario,
                // o simplemente usar la posición del transform si es suficiente.
                LockOnTarget lockOnTargetComponent = lockOnTargetTransform.GetComponent<LockOnTarget>();
                Vector3 pointToLookAt = lockOnTargetComponent != null ? lockOnTargetComponent.GetTargetPoint() : lockOnTargetTransform.position;

                Vector3 directionToLockOnTarget = pointToLookAt - transform.position;
                directionToLockOnTarget.y = 0; // Mantener la rotación solo en el eje Y para el cuerpo del personaje

                if (directionToLockOnTarget.sqrMagnitude > 0.001f) // Evitar LookRotation de cero
                {
                    Quaternion targetBodyRotation = Quaternion.LookRotation(directionToLockOnTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetBodyRotation, Time.deltaTime * aimLockOnRotationSpeed);
                }
            }
            // Si no hay un objetivo de Lock-On, o la cámara no está asignada, no se hace rotación especial de apuntado.
            // El jugador se moverá y rotará según 'moveDirectionRelativeToCamera' si hay input.
        }
        else if (hasInput && moveDirectionRelativeToCamera != Vector3.zero) // Rotación normal si no se está apuntando en Lock-On
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirectionRelativeToCamera);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSmoothTime);
        }

        characterController.Move(horizontalMovement * Time.deltaTime);

        if (hasInput && !hasMovedWithWASD)
        {
            hasMovedWithWASD = true;
            TutorialQuestManager questManager = FindAnyObjectByType<TutorialQuestManager>();
            if (questManager != null)
            {
                questManager.TriggerPlayerMoved();
            }
        }
    }

    private void HandleJumpAndGravity()
    {
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }

        bool canCoyoteJump = Time.time < lastTimeGrounded + coyoteTimeDuration && !isGrounded && !isActuallyJumping;

        if ((jumpInputPressed || jumpBufferTimer > 0) && (isGrounded || canCoyoteJump))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityMultiplier);
            isActuallyJumping = true;
            isGrounded = false;
            isFalling = false;
            jumpBufferTimer = 0;
            jumpInputPressed = false;
            animator.SetTrigger(jumpTriggerHash);
            OnJumpPerformed?.Invoke();
        }

        jumpInputPressed = false;
        Vector3 verticalMove = new Vector3(0, verticalVelocity, 0);
        characterController.Move(verticalMove * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        // La lógica de cambio de altura y centro se maneja en OnCrouch
    }

    private bool CanStandUp()
    {
        Vector3 sphereCastOrigin = transform.position + crouchingCenter + Vector3.up * (standingHeight / 2f - crouchingHeight / 2f + 0.01f);
        float sphereCastRadius = characterController.radius - 0.05f;
        float sphereCastDistance = standingHeight - crouchingHeight + crouchObstacleCheckDistance;
        return !Physics.SphereCast(sphereCastOrigin, sphereCastRadius, Vector3.up, out _, sphereCastDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void HandleStamina()
    {
        if (isSprinting && currentMovementInput.magnitude > 0.1f)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(0, currentStamina);
            timeSinceStaminaUsed = 0f;
            canRegenerateStamina = false;
        }

        if (currentStamina <= 0.01f && isSprinting)
        {
            isSprinting = false;
        }

        timeSinceStaminaUsed += Time.deltaTime;
        if (timeSinceStaminaUsed >= staminaRegenDelay)
        {
            canRegenerateStamina = true;
        }

        if (canRegenerateStamina && currentStamina < maxStamina && !isSprinting)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);
        }
    }

    private void UpdateAnimator()
    {
        float animationSpeedPercent;

        // Lógica para determinar el valor objetivo del parámetro "Speed"
        if (isCrouching)
        {
            // Si estás agachado y tienes un Blend Tree separado para agachado, usarías crouchSpeedHash
            // Si tu Blend Tree "locomotion" también maneja el agachado (por ejemplo, con otro parámetro o un valor negativo/diferente de Speed)
            // esta lógica necesitaría ajustarse.
            // Por ahora, asumiré que "Speed" es para el movimiento normal y "CrouchSpeed" para agachado.
            float crouchAnimationSpeed = (currentSpeed > 0.01f) ? 1f : 0f; // Simplificado: 0 si no se mueve, 1 si se mueve agachado
            animator.SetFloat(crouchSpeedHash, crouchAnimationSpeed, animationParameterSmoothTime, Time.deltaTime);
            animator.SetFloat(speedHash, 0f, animationParameterSmoothTime, Time.deltaTime); // No moverse en el blend tree principal si está agachado
        }
        else
        {
            // Lógica para el Blend Tree "locomotion" (idle, walk, run)
            if (currentSpeed < 0.1f) // Umbral para considerar que está en Idle
            {
                animationSpeedPercent = 0f; // Valor para Idle
            }
            else if (currentSpeed <= walkSpeed)
            {
                // Normaliza la velocidad entre 0 (idle) y 1 (walkSpeed)
                // Si walkSpeed es 3, y currentSpeed es 1.5, animationSpeedPercent será 0.5.
                // Este valor se mapeará a tu Blend Tree.
                // Si tu threshold de walk en el BlendTree es 1, cuando llegues a walkSpeed, este valor será 1.
                animationSpeedPercent = currentSpeed / walkSpeed;
            }
            else // Corriendo (currentSpeed > walkSpeed)
            {
                // Normaliza la velocidad entre 1 (walkSpeed) y 2 (runSpeed)
                // Si runSpeed es 7 y walkSpeed es 3:
                // Cuando currentSpeed es 3 (walkSpeed), esto da (3-3)/(7-3) + 1 = 1.
                // Cuando currentSpeed es 7 (runSpeed), esto da (7-3)/(7-3) + 1 = 2.
                // Esto asume que tu Blend Tree tiene:
                //  - Idle en threshold 0
                //  - Walk en threshold 1
                //  - Run en threshold 2
                animationSpeedPercent = 1f + (currentSpeed - walkSpeed) / (runSpeed - walkSpeed);
            }
            // Asegurarse de que el valor no exceda el máximo esperado por el Blend Tree (ej. 2 si ese es el threshold de Run)
            animationSpeedPercent = Mathf.Clamp(animationSpeedPercent, 0f, 2f); // Ajusta el '2f' si tu threshold de run es diferente

            animator.SetFloat(speedHash, animationSpeedPercent, animationParameterSmoothTime, Time.deltaTime);
            animator.SetFloat(crouchSpeedHash, 0f, animationParameterSmoothTime, Time.deltaTime); // No moverse en el blend tree de agachado si no está agachado
        }

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isFallingHash, isFalling && !isLanding);
        animator.SetBool(isCrouchingHash, isCrouching);
    }

    public void PlayFootstepSound()
    {
        if (isGrounded && currentSpeed > 0.1f && footstepSounds.Length > 0)
        {
            if (audioSource != null)
            {
                int index = UnityEngine.Random.Range(0, footstepSounds.Length);
                audioSource.PlayOneShot(footstepSounds[index], footstepVolume);
            }
        }
    }

    public void PlayLandingSound()
    {
        if (!hasPlayedLandingSound && landingSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(landingSound, landingVolume);
            }
            hasPlayedLandingSound = true;
        }
    }

    // ---- MÉTODO AÑADIDO PARA COMPATIBILIDAD ----
    public void SetAimingState(bool aiming)
    {
        if (this.isAiming != aiming)
        {
            this.isAiming = aiming; // Asigna a la propiedad (el setter privado lo permite)
            onAimStateChanged?.Invoke(this.isAiming); // Dispara el evento

            // Replica la lógica de notificación al QuestManager que está en OnAim
            if (this.isAiming && thirdPersonCamera != null && thirdPersonCamera.IsLockOnActive)
            {
                VillageQuestManager questManager = FindAnyObjectByType<VillageQuestManager>();
                if (questManager != null)
                {
                    questManager.OnPlayerAim();
                }
            }
        }
    }
    // ------------------------------------------

    #region Input System Callback Methods

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        currentMovementInput = new Vector3(input.x, 0, input.y);

        if (context.performed && input.magnitude > 0.1f && !hasMovedWithWASD)
        {
            hasMovedWithWASD = true;
            TutorialQuestManager questManager = FindAnyObjectByType<TutorialQuestManager>();
            if (questManager != null)
            {
                questManager.TriggerPlayerMoved();
            }
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Implementado en ThirdPersonCamera
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpInputPressed = true;
            jumpBufferTimer = jumpBufferDuration;
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            bool previousCrouchState = isCrouching;
            if (isCrouching)
            {
                if (CanStandUp())
                {
                    isCrouching = false;
                }
            }
            else
            {
                isCrouching = true;
            }

            if (isCrouching)
            {
                characterController.height = crouchingHeight;
                characterController.center = crouchingCenter;
            }
            else
            {
                characterController.height = standingHeight;
                characterController.center = standingCenter;
            }

            if (previousCrouchState != isCrouching)
            {
                onCrouchStateChanged?.Invoke(isCrouching);
            }
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            onAttackPerformed?.Invoke();
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed && currentStamina > minStaminaToSprint)
        {
            isSprinting = true;
        }
        else if (context.canceled || currentStamina <= 0.01f)
        {
            isSprinting = false;
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (Time.time < lastInteractTime + interactCooldown) return;
            lastInteractTime = Time.time;

            if (currentInteractable != null)
            {
                currentInteractable.Interact();
            }
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        bool newAimState = context.ReadValueAsButton();

        if (this.isAiming != newAimState) // Usar this.isAiming para la propiedad
        {
            this.isAiming = newAimState; // Asignar a la propiedad
            onAimStateChanged?.Invoke(this.isAiming);

            if (this.isAiming && thirdPersonCamera != null && thirdPersonCamera.IsLockOnActive)
            {
                VillageQuestManager questManager = FindAnyObjectByType<VillageQuestManager>();
                if (questManager != null)
                {
                    questManager.OnPlayerAim();
                }
            }
        }
    }


    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (thirdPersonCamera != null)
            {
                // Asumimos que ToggleLockOn está en ThirdPersonCamera, si no, implementar ToggleLockOnInternal
                // thirdPersonCamera.ToggleLockOn(); 
                ToggleLockOnInternal(); // Si la lógica de LockOn es interna al PlayerController
            }
             // Notificar al VillageQuestManager si estamos en la misión correcta
            VillageQuestManager vqm = FindAnyObjectByType<VillageQuestManager>();
            if (vqm != null && vqm.IsQuestActive("LOCK_ON_TUTORIAL")) // Reemplaza con el ID correcto de tu misión de LockOn
            {
                vqm.OnPlayerLockOn();
            }
        }
    }

    private void ToggleLockOnInternal()
    {
        isLockOn = !isLockOn;
        // Aquí iría la lógica para activar/desactivar el LockOn en la cámara
        // o en el propio PlayerController si gestiona la selección de objetivos.
        // Por ejemplo, si ThirdPersonCamera tiene el método:
        if (thirdPersonCamera != null)
        {
            // Esto es un placeholder, necesitarías un método real en ThirdPersonCamera
            // thirdPersonCamera.HandleLockOnToggle(isLockOn); 
            // O si la cámara tiene su propio ToggleLockOn:
            // thirdPersonCamera.ToggleLockOn(); // Si la cámara maneja el estado de lock-on internamente
        }
        Debug.Log("PlayerController LockOn Toggled: " + isLockOn);
    }


    public void OnPrevious(InputAction.CallbackContext context) { /* No implementado */ }
    public void OnNext(InputAction.CallbackContext context) { /* No implementado */ }

    #endregion

    #region Trigger Interaction Methods

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            currentInteractable = interactable;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentInteractable != null && other.gameObject == ((MonoBehaviour)currentInteractable).gameObject)
        {
            currentInteractable = null;
        }
    }

    #endregion

    public bool IsMoving() => currentMovementInput.magnitude > 0.1f && currentSpeed > 0.1f;
    public bool IsRunning() => isSprinting;
    public bool IsActuallyJumping() => isActuallyJumping;
    public bool IsCrouching() => isCrouching;
    public bool IsInteracting() => false;
    public bool IsInLockOnMode() => isLockOn;
    public bool IsGrounded() => isGrounded;
    public float GetVerticalVelocity() => verticalVelocity;

    public void SetMovementInput(Vector2 input)
    {
        currentMovementInput = new Vector3(input.x, 0, input.y);
    }

    public void StopMovement()
    {
        currentMovementInput = Vector3.zero;
        currentSpeed = 0f;
        speedSmoothVelocity = 0f;
        airVelocity = Vector3.zero;
    }

    public void SetIdleAnimation()
    {
        animator.SetFloat(speedHash, 0f);
        animator.SetBool(isCrouchingHash, isCrouching);
    }

    private void OnDrawGizmosSelected()
    {
        if (characterController == null) return;

        Vector3 sphereCastOrigin = transform.position + characterController.center + Vector3.down * (characterController.height / 2f - groundCheckRadius + 0.05f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphereCastOrigin + Vector3.down * groundCheckDistance, groundCheckRadius);
        Gizmos.DrawRay(sphereCastOrigin, Vector3.down * groundCheckDistance);

        if (isCrouching)
        {
            Vector3 standUpCheckOrigin = transform.position + crouchingCenter + Vector3.up * (standingHeight / 2f - crouchingHeight / 2f + 0.01f);
            float standUpCheckDistance = standingHeight - crouchingHeight + crouchObstacleCheckDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(standUpCheckOrigin + Vector3.up * standUpCheckDistance, characterController.radius - 0.05f);
            Gizmos.DrawRay(standUpCheckOrigin, Vector3.up * standUpCheckDistance);
        }
    }
}