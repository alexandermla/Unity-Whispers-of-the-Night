using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq; // Necesario para OrderBy y ThenBy

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float distance = 5f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 8f;

    [Header("Camera Controls")]
    public float sensitivity = 1f;
    [SerializeField] private float smoothSpeed = 15f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 70f);
    [SerializeField] private float shoulderOffset = 0.5f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask collisionLayers;

    [Header("Lock-on Settings")]
    [SerializeField] private float lockOnDistance = 10f;
    [SerializeField] private LayerMask targetableLayers;
    [SerializeField] private float lockOnSmoothSpeed = 20f;
    [SerializeField] private float breakLockOnDistance = 15f;
    [SerializeField] private float targetLostTimeout = 6f;
    [SerializeField] private float targetSwitchDeadzone = 0.25f; // Deadzone for target switching
    [SerializeField] private float targetSwitchInputThreshold = 0.6f; // Minimum input value to trigger target switch

    [Header("Target Point Settings")]
    [SerializeField] private Vector3 additionalTargetOffset = Vector3.zero; // Fine-tune target offset
    [SerializeField] private bool showTargetPointDebug = true; // Show visual indicator of target point
    [SerializeField] private Color targetPointColor = new Color(1f, 0f, 0f, 0.7f); // Red semi-transparent
    [SerializeField] private float targetPointSize = 0.3f; // Size of visual indicator

    // --- Eliminado: Configuración del Indicador In-Game ---
    // [Header("In-Game Target Indicator")]
    // [SerializeField] private bool showInGameTargetIndicator = true; // Show visible indicator in game
    // [SerializeField] private GameObject targetIndicatorPrefab; // Prefab for the visual indicator (optional)
    // [SerializeField] private Color indicatorColor = Color.red; // Color of the in-game indicator
    // [SerializeField] private float indicatorSize = 0.2f; // Size of the in-game indicator
    // ------------------------------------------------------

    private InputSystem_Actions inputActions;
    private Vector2 lookInput;
    private float currentPitch;
    private float currentYaw;
    private Vector3 smoothVelocity;
    private float targetDistance;
    private Vector3 pivotPosition;
    private LockOnTarget currentLockOnTarget;
    public bool isLockOnActive;
    private float targetLostTimer;
    private Vector3 lastKnownTargetPosition;
    private float lastTargetSwitchTime;
    private List<LockOnTarget> availableTargets = new List<LockOnTarget>();
    private Vector3 currentTargetPoint; // Store current target point for visualization
    //private GameObject targetIndicator; // Eliminado: In-game visual indicator

    public bool IsLockOnActive => isLockOnActive;

    public delegate void LockOnEventHandler();
    public event LockOnEventHandler onLockOnPerformed;

    private void Awake()
    {
        ValidateComponents();
        InitializeCamera();
        //CreateTargetIndicator(); // Eliminado
    }

    private void ValidateComponents()
    {
        if (!target)
        {
            Debug.LogError($"[{GetType().Name}] No target assigned to the camera!");
            enabled = false;
            return;
        }

        inputActions = new InputSystem_Actions();
    }

    // --- Eliminado: CreateTargetIndicator ---
    // private void CreateTargetIndicator() { ... }
    // ---------------------------------------

    private void InitializeCamera()
    {
        targetDistance = distance;
        currentYaw = target.eulerAngles.y;
        // Calcular pitch inicial basado en la rotación de la cámara si existe
        currentPitch = transform.eulerAngles.x;
        // Asegurarse que el pitch inicial esté dentro de los límites
        if (currentPitch > 180) currentPitch -= 360; // Ajustar ángulos negativos
        currentPitch = Mathf.Clamp(currentPitch, pitchLimits.x, pitchLimits.y);

        UpdateCameraPosition(true); // Calcular posición inicial
    }

    private void Start()
    {
        SetupInputCallbacks();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupInputCallbacks()
    {
        inputActions.Player.Look.started += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        inputActions.Player.LockOn.performed += _ => ToggleLockOn();

        // Setup target switching with horizontal look when locked on
        inputActions.Player.Look.performed += ctx => {
            if (isLockOnActive && currentLockOnTarget != null) {
                Vector2 lookValue = ctx.ReadValue<Vector2>();

                // Only switch if there's significant horizontal input and minimal vertical input
                if (Mathf.Abs(lookValue.x) > targetSwitchInputThreshold &&
                    Mathf.Abs(lookValue.y) < targetSwitchDeadzone &&
                    Time.time - lastTargetSwitchTime > 0.2f) // Cooldown to prevent too rapid switching
                {
                    // Switch target based on input direction
                    if (lookValue.x > 0) {
                        SwitchTarget(true); // Switch right
                    } else {
                        SwitchTarget(false); // Switch left
                    }
                    lastTargetSwitchTime = Time.time;
                }
            }
        };
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void LateUpdate()
    {
        if (!target) return;

        UpdateLockOnState();

        // Update available targets list for quick switching
        if (isLockOnActive) {
            UpdateAvailableTargets();
        }

        if (isLockOnActive && currentLockOnTarget != null)
        {
            UpdateLockOnCamera();
            // UpdateTargetIndicator(true); // Eliminado
        }
        else
        {
            UpdateFreeCamera();
            // UpdateTargetIndicator(false); // Eliminado
        }

        UpdateCameraPosition(false);
    }

    // --- Eliminado: UpdateTargetIndicator ---
    // private void UpdateTargetIndicator(bool active) { ... }
    // ---------------------------------------

    private void UpdateAvailableTargets()
    {
        // Get all potential targets within range
        Collider[] potentialTargets = Physics.OverlapSphere(target.position, lockOnDistance, targetableLayers);

        // Clear and update available targets list
        availableTargets.Clear();

        foreach (Collider collider in potentialTargets)
        {
            if (collider.TryGetComponent<LockOnTarget>(out var lockOnTarget) &&
                lockOnTarget.IsValidTarget() && // Usa el método IsValidTarget de LockOnTarget
                IsTargetVisible(lockOnTarget))
            {
                availableTargets.Add(lockOnTarget);
            }
        }
    }


    private bool IsTargetVisible(LockOnTarget targetToCheck)
    {
        if (targetToCheck == null) return false;

        Vector3 origin = target.position + targetOffset; // Origen desde el pivot del jugador
        Vector3 targetPoint = targetToCheck.GetTargetPoint(); // Punto objetivo en el enemigo
        Vector3 directionToTarget = (targetPoint - origin).normalized;
        float distance = Vector3.Distance(origin, targetPoint);

        // Primero, chequear distancia máxima
        if (distance > breakLockOnDistance) return false;

        // Luego, chequear obstáculos
        // Usar SphereCast para tener en cuenta el radio de la cámara
        if (Physics.SphereCast(origin, collisionRadius, directionToTarget, out RaycastHit hit, distance, collisionLayers))
        {
            // Es visible SOLO si el rayo NO golpea nada ANTES del objetivo
            // O si lo que golpea ES el objetivo o un hijo suyo
            return hit.transform == targetToCheck.transform || hit.transform.IsChildOf(targetToCheck.transform);
        }

        // Si no golpeó nada, es visible
        return true;
    }

    private void UpdateLockOnState()
    {
        if (!isLockOnActive || currentLockOnTarget == null) return;

        // Verificar si el objetivo sigue siendo válido (ahora usa el método de LockOnTarget)
        if (!currentLockOnTarget.IsValidTarget())
        {
            DisableLockOn();
            return;
        }

        // Verificar si hay línea de visión hacia el objetivo (usa IsTargetVisible que ya chequea distancia)
        if (!IsTargetVisible(currentLockOnTarget))
        {
            // Incrementar el timer de pérdida de objetivo
            targetLostTimer += Time.deltaTime;
            if (targetLostTimer >= targetLostTimeout)
            {
                DisableLockOn();
            }
            return; // Salir si no es visible
        }

        // Resetear el timer si el objetivo sigue visible y válido
        targetLostTimer = 0f;
    }

    private void UpdateFreeCamera()
    {
        currentYaw += lookInput.x * sensitivity;
        currentPitch = Mathf.Clamp(currentPitch - lookInput.y * sensitivity, pitchLimits.x, pitchLimits.y);
    }

    private void UpdateLockOnCamera()
    {
        if (currentLockOnTarget == null) return;

        // Get base target point and apply the enemy-specific camera offset
        Vector3 targetPosition = currentLockOnTarget.GetTargetPoint() + currentLockOnTarget.GetCameraAdditionalOffset();

        // Store current target point for visualization
        currentTargetPoint = targetPosition;

        // Dirección desde el PIVOTE de la cámara (posición del jugador + offset) hacia el punto objetivo
        Vector3 pivotPos = target.position + targetOffset;
        Vector3 directionToTarget = (targetPosition - pivotPos).normalized;


        // Calculate target rotation based on enemy position (Yaw)
        float targetYaw = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;

        // Calculate vertical angle precisely based on target point height (Pitch)
        float heightDifference = targetPosition.y - pivotPos.y;

        // Using horizontal distance for better pitch calculation with tall enemies
        float horizontalDistance = Vector3.Distance(
            new Vector3(pivotPos.x, 0, pivotPos.z), // Usar pivotPos horizontal
            new Vector3(targetPosition.x, 0, targetPosition.z)
        );
        // Evitar división por cero si la distancia horizontal es muy pequeña
        float targetPitch = (horizontalDistance > 0.1f) ? Mathf.Atan2(heightDifference, horizontalDistance) * Mathf.Rad2Deg : currentPitch;


        // Dynamic speed adjustment based on angle difference (Elden Ring-like)
        float yawDifference = Mathf.DeltaAngle(currentYaw, targetYaw); // Usar DeltaAngle para manejo correcto de ángulos
        float pitchDifference = targetPitch - currentPitch;

        // Higher speed for larger angle differences, creating responsive initial movement
        // Añadir Mathf.Abs para que la diferencia sea siempre positiva para el cálculo de velocidad
        float yawSpeed = lockOnSmoothSpeed * (1f + (Mathf.Abs(yawDifference) / 90f));
        float pitchSpeed = lockOnSmoothSpeed * (1f + (Mathf.Abs(pitchDifference) / 45f));

        // Apply smoother interpolation usando LerpAngle para Yaw
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * yawSpeed);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * pitchSpeed);
        currentPitch = Mathf.Clamp(currentPitch, pitchLimits.x, pitchLimits.y); // Asegurar que el pitch se mantenga en límites

        // Reduced manual adjustment influence (Elden Ring-like)
        // Permitir ajustes manuales leves si el jugador mueve el stick
        currentYaw = Mathf.LerpAngle(currentYaw, currentYaw + lookInput.x * sensitivity * 0.2f, Time.deltaTime * 5f); // Suavizar ajuste manual
        currentPitch = Mathf.Clamp(currentPitch - lookInput.y * sensitivity * 0.2f, pitchLimits.x, pitchLimits.y);

    }


    private void UpdateCameraPosition(bool instant)
    {
        pivotPosition = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        // Calcular offset lateral basado en la rotación actual
        Vector3 rightOffset = rotation * Vector3.right * shoulderOffset;

        // Calcular posición deseada base (detrás del pivot)
        Vector3 baseDesiredPosition = pivotPosition + rotation * Vector3.back * distance;

        // Aplicar offset lateral (hombro)
        Vector3 finalDesiredPosition = baseDesiredPosition + rightOffset;


        Vector3 directionToCamera = (finalDesiredPosition - pivotPosition).normalized;
        float adjustedDistance = HandleCameraCollision(pivotPosition, directionToCamera, distance); // Pasar distancia original para el raycast
        Vector3 collisionAdjustedPosition = pivotPosition + directionToCamera * adjustedDistance;


        if (instant)
        {
            transform.position = collisionAdjustedPosition;
            transform.rotation = rotation;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, collisionAdjustedPosition, ref smoothVelocity, 1f / smoothSpeed);
            // Rotar la cámara para que siempre mire al pivot
            transform.LookAt(pivotPosition);
            // Opcional: suavizar la rotación también si se desea
            // transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(pivotPosition - transform.position), Time.deltaTime * smoothSpeed * 2);
        }

        // Ya no se usa LookAt(pivotPosition) aquí, la rotación se calcula antes
    }


    private void ToggleLockOn()
    {
        if (isLockOnActive)
        {
            DisableLockOn();
        }
        else
        {
            if (FindAndLockOnTarget())
            {
                onLockOnPerformed?.Invoke();
            }
        }
    }

    private bool FindAndLockOnTarget()
    {
        // Actualizar lista de objetivos disponibles ANTES de buscar el mejor
        UpdateAvailableTargets();

        // Si no hay objetivos disponibles después de actualizar, salir
        if (availableTargets.Count == 0)
            return false;

        // Priorizar por ángulo y distancia
        LockOnTarget bestTarget = null;
        float bestScore = float.MaxValue; // Menor score es mejor

        Vector3 camForwardHorizontal = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 playerPosHorizontal = new Vector3(target.position.x, 0, target.position.z);

        foreach (var potentialTarget in availableTargets)
        {
            Vector3 targetPoint = potentialTarget.GetTargetPoint();
            Vector3 targetPosHorizontal = new Vector3(targetPoint.x, 0, targetPoint.z);
            Vector3 dirToTargetHorizontal = (targetPosHorizontal - playerPosHorizontal).normalized;

            // Calcular ángulo horizontal
            float angle = Vector3.Angle(camForwardHorizontal, dirToTargetHorizontal);

            // Calcular distancia
            float distance = Vector3.Distance(target.position, targetPoint);

            // Calcular puntuación (ejemplo: 60% ángulo, 40% distancia)
            // Puntuaciones más bajas son mejores
            float score = (angle * 0.6f) + (distance * 0.4f);

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = potentialTarget;
            }
        }

        // Fijar el mejor objetivo encontrado
        if (bestTarget != null)
        {
            currentLockOnTarget = bestTarget;
            currentLockOnTarget.UpdateLockOnVisual(true); // --- Cambiado a UpdateLockOnVisual ---
            isLockOnActive = true;
            targetLostTimer = 0f; // Reiniciar timer al fijar
            return true;
        }

        return false;
    }

    [SerializeField] private GameObject lamp;
    private void DisableLockOn()
    {
        if (currentLockOnTarget != null)
        {
            currentLockOnTarget.UpdateLockOnVisual(false); // --- Cambiado a UpdateLockOnVisual ---
        }
        isLockOnActive = false;
        currentLockOnTarget = null;
        targetLostTimer = 0f;
         // Resetear rotacion de la lámpara rotacion local -100,0,0
        lamp.transform.localRotation = Quaternion.Euler(-100, 0, 0);
    }

    private void SwitchTarget(bool switchRight)
    {
        if (!isLockOnActive || currentLockOnTarget == null || availableTargets.Count <= 1) return;

        // Posición del objetivo actual en el espacio de la pantalla
        Vector3 currentTargetScreenPos = Camera.main.WorldToScreenPoint(currentLockOnTarget.GetTargetPoint());
        Transform bestCandidate = null;
        float bestScore = float.MaxValue; // Queremos minimizar el ángulo al centro, y luego la distancia

        // Iterar sobre todos los objetivos disponibles (excluyendo el actual)
        foreach (var potentialTarget in availableTargets)
        {
            if (potentialTarget == currentLockOnTarget) continue;

            Vector3 potentialTargetScreenPos = Camera.main.WorldToScreenPoint(potentialTarget.GetTargetPoint());

            // Verificar si el objetivo está en la dirección deseada (izquierda/derecha)
            // Y si está visible en pantalla (podrías añadir un chequeo de Z > 0 si es necesario)
            if (potentialTargetScreenPos.z > 0 && // Asegurarse que esté delante de la cámara
                ((switchRight && potentialTargetScreenPos.x > currentTargetScreenPos.x) ||
                (!switchRight && potentialTargetScreenPos.x < currentTargetScreenPos.x)))
            {
                // Calcular el vector desde el centro de la pantalla al objetivo potencial
                Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
                Vector2 targetDirScreen = new Vector2(potentialTargetScreenPos.x, potentialTargetScreenPos.y) - screenCenter;

                // Ángulo respecto al centro de la pantalla (usamos magnitud como proxy de ángulo aquí para simplificar,
                // pero Vector2.Angle podría ser más preciso si lo necesitas)
                float angleToCenter = targetDirScreen.magnitude;

                // Puntuación: priorizar el ángulo más pequeño.
                // Si los ángulos son muy similares, desempatar por distancia al jugador.
                float score = angleToCenter;

                if (bestCandidate == null || score < bestScore - 5.0f) // Si es significativamente mejor angularmente
                {
                    bestScore = score;
                    bestCandidate = potentialTarget.transform;
                }
                else if (Mathf.Abs(score - bestScore) < 5.0f) // Ángulos similares, desempatar por distancia
                {
                    if (target != null && bestCandidate != null) // Asegurarse que 'target' (jugador) está asignado
                    {
                        float distToCurrentBest = Vector3.Distance(target.position, bestCandidate.position);
                        float distToPotential = Vector3.Distance(target.position, potentialTarget.transform.position);

                        if (distToPotential < distToCurrentBest)
                        {
                            bestScore = score; // Mantener la puntuación (ya que el ángulo es similar)
                            bestCandidate = potentialTarget.transform;
                        }
                    }
                }
            }
        }

        // Si encontramos un nuevo mejor candidato, cambiar
        if (bestCandidate != null && bestCandidate.TryGetComponent<LockOnTarget>(out LockOnTarget newLockOnTarget))
        {
            if (currentLockOnTarget != null)
            {
                currentLockOnTarget.UpdateLockOnVisual(false);
            }
            currentLockOnTarget = newLockOnTarget;
            currentLockOnTarget.UpdateLockOnVisual(true);
            targetLostTimer = 0f; // Reiniciar timer al cambiar
            lastTargetSwitchTime = Time.time; // Actualizar tiempo del último cambio
            Debug.Log($"Switched LockOn Target to: {currentLockOnTarget.name}");
        }
    }

    private float HandleCameraCollision(Vector3 fromPosition, Vector3 direction, float originalDistance)
    {
        // Usar SphereCast para detectar colisiones
        if (Physics.SphereCast(fromPosition, collisionRadius, direction, out RaycastHit hit, originalDistance, collisionLayers))
        {
            // Si colisiona, ajustar la distancia para que la cámara esté justo antes de la colisión
            // Restar un poco más que el radio para evitar que el borde de la esfera atraviese
            return Mathf.Clamp(hit.distance - collisionRadius * 1.1f, minDistance, originalDistance);
        }

        // Si no hay colisión, devolver la distancia original
        return originalDistance;
    }

    public Transform GetCurrentLockOnTarget() // Renombrado para claridad
    {
        return currentLockOnTarget != null ? currentLockOnTarget.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!target) return;

        // Pivot (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position + targetOffset, 0.1f); // Usar targetOffset

        // Cámara (rojo) - Dibujar en la posición REAL de la cámara
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        // Rango de LockOn (azul)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(target.position, lockOnDistance);

        // Rango de Break LockOn (azul claro semi-transparente)
        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(target.position, breakLockOnDistance);

        // Punto objetivo (rojo semi-transparente) - Solo si está fijado y en modo debug
        if (showTargetPointDebug && currentLockOnTarget != null && Application.isPlaying)
        {
            Gizmos.color = targetPointColor;
            Gizmos.DrawSphere(currentTargetPoint, targetPointSize);

            // Línea desde el pivot del jugador al punto objetivo
            Gizmos.DrawLine(target.position + targetOffset, currentTargetPoint);
        }
    }


    // --- Eliminado: OnDestroy para el indicador ---
    // private void OnDestroy() { ... }
    // ---------------------------------------------

    public void HandleAimState(bool isAiming)
    {
        // Esta función podría usarse para ajustar el comportamiento de la cámara
        // al apuntar, pero por ahora no hace nada específico relacionado con LockOn.
        // Por ejemplo, podrías reducir la sensibilidad o cambiar el offset del hombro.
    }

    public void SetAdditionalTargetOffset(Vector3 offset)
    {
        additionalTargetOffset = offset;
    }

    // --- Eliminado: SetIndicatorVisibility ---
    // public void SetIndicatorVisibility(bool visible) { ... }
    // ------------------------------------------
}