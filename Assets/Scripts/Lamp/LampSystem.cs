using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using WoN.Interfaces;

public class LampSystem : MonoBehaviour, ILampController
{
    // --- Modificado: isFireflyMerged ahora es estático ---
    public static bool isFireflyMerged = false;
    // ----------------------------------------------------
    //private bool isQuestCompleted = false; // Ya no necesario aquí, se lee de PlayerPrefs
    private bool isAutoRecharging = false;
    //private bool isFusing = false; // Ya no se controla aquí la fusión

    [Header("Lamp Settings")]
    [SerializeField] private Light spotLight;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyDrainRate = 5f;
    [SerializeField] private float passiveRechargeRate = 2.0f; // Aumentado un poco para que se note
    [SerializeField] private float maxLightDistance = 15f;
    [SerializeField] private float flashlightAngle = 45f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("UI References")]
    [SerializeField] private GameObject lampEmptyObj;
    [SerializeField] private GameObject lampFillObj;
    [SerializeField] private Image lampFillImage; // Añadir referencia directa a la Image

    [Header("Aim Settings")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private float aimAnimationSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;
    [SerializeField] private bool drawLightConeGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 1f, 0f, 0.25f);
    [SerializeField] private Color raycastHitColor = Color.red;
    [SerializeField] private Color raycastMissColor = Color.green;

    private bool isAiming = false;
    private ThirdPersonCamera thirdPersonCamera;
    private VillageQuestManager questManager; // Mantenido por si se usa para otros triggers
    //private FireflyGuide fireflyGuide; // Ya no es necesario aquí
    public float currentEnergy { get; private set; }
    private bool isLampOn;
    private InputSystem_Actions inputActions;

    

    public bool IsLampOn => isLampOn;
    public float CurrentEnergyPercentage => maxEnergy > 0 ? currentEnergy / maxEnergy : 0;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Player.Attack.performed += OnAttackPerformed;
        inputActions.Player.Aim.performed += SetAimModeWrapper;
        inputActions.Player.Aim.canceled += SetAimModeWrapper;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (playerController == null) playerController = Object.FindAnyObjectByType<PlayerController>(); // Cambio a FindObjectOfType
        thirdPersonCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
        questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        //fireflyGuide = FindObjectOfType<FireflyGuide>(); // Ya no necesario
        if (spotLight == null) spotLight = GetComponent<Light>();

        // Asegurar referencia a Image
        if (lampFillObj != null && lampFillImage == null)
        {
            lampFillImage = lampFillObj.GetComponent<Image>();
        }

        // --- Leer estado inicial de PlayerPrefs ---
        isFireflyMerged = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;
        if(showDebugMessages) Debug.Log($"LampSystem Awake: isFireflyMerged leído de PlayerPrefs = {isFireflyMerged}");
        // ------------------------------------------

        // Validaciones
        if (playerController == null && showDebugMessages) Debug.LogWarning("LampSystem: PlayerController no encontrado!", this);
        if (thirdPersonCamera == null && showDebugMessages) Debug.LogWarning("LampSystem: ThirdPersonCamera no encontrada!", this);
        if (spotLight == null) Debug.LogError("LampSystem: Componente Light no encontrado!", this);
        if (lampFillImage == null && showDebugMessages) Debug.LogWarning("LampSystem: lampFillImage (UI Image) no asignada.", this);
    }

    private void OnEnable() => inputActions?.Enable();
    private void OnDisable() => inputActions?.Disable();

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (inputActions != null)
        {
            inputActions.Player.Attack.performed -= OnAttackPerformed;
            inputActions.Player.Aim.performed -= SetAimModeWrapper;
            inputActions.Player.Aim.canceled -= SetAimModeWrapper;
        }
        // Ya no se necesita desuscribir del evento de fusión
    }

    public void RewardEnergy(float amount)
    {
        currentEnergy += amount; // Recompensa de energía
        UpdateEnergyUI();
    }

    private void SetAimModeWrapper(InputAction.CallbackContext ctx) => SetAimMode(ctx.ReadValueAsButton());

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Volver a leer estado al cargar escena por si acaso
        isFireflyMerged = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;
         if(showDebugMessages) Debug.Log($"LampSystem OnSceneLoaded ({scene.name}): isFireflyMerged = {isFireflyMerged}");

        if (scene.name == "mainSceneQuest" || scene.name == "TimmyHouse_Tutorial")
        {
            InitializeLampState();
            if (SceneManager.GetActiveScene().name == "mainSceneQuest")
            {
                SetEnergyUIVisible(true);
            }
        }
         else // Ocultar UI en otras escenas
         {
             SetEnergyUIVisible(false);
         }
    }

    private void Start()
    {
        InitializeLampState();
        if (SceneManager.GetActiveScene().name == "mainSceneQuest")
         {
            SetEnergyUIVisible(true);
         }
    }

    private void InitializeLampState()
    {
        // Cargar estado desde PlayerPrefs al inicializar también
        isFireflyMerged = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;

        currentEnergy = 0f; // Empezar sin energía o cargar desde save? Por ahora empieza en 0.
        UpdateLampState(false); // Empezar apagada

        bool lampPickedUp = PlayerPrefs.GetInt("HasLampBeenPickedUp", 0) == 1;
        // Mostrar UI solo si se recogió Y estamos en una escena válida
         bool showUI = lampPickedUp && (SceneManager.GetActiveScene().name == "mainSceneQuest" || SceneManager.GetActiveScene().name == "TimmyHouse_Tutorial");
         SetEnergyUIVisible(showUI);
        UpdateEnergyUI();

        // Iniciar recarga si es necesario
        isAutoRecharging = isFireflyMerged && currentEnergy < maxEnergy;
         if(isAutoRecharging && showDebugMessages) Debug.Log("LampSystem Initialize: Iniciando auto-recarga.");

    }

    private void SetAimMode(bool aimState)
    {
        // --- Lógica de Fusión Eliminada ---
        // Ya no se controla la fusión desde aquí
        // ----------------------------------

        isAiming = aimState;
        if (playerController != null)
        {
            playerController.SetAimingState(aimState);
        }
        if (thirdPersonCamera != null) thirdPersonCamera.HandleAimState(aimState);
    }

    // --- Métodos de Fusión Eliminados ---
    // private void StartFireflyFusion() { ... }
    // private void OnFireflyFusionCompleted() { ... }
    // private IEnumerator WaitForEnemyDefeat() { ... }
    // ---------------------------------


    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        // Si la lámpara está apagada Y tiene fusión Y está recargando O ya está llena
        if (!isLampOn && isFireflyMerged && (isAutoRecharging || currentEnergy >= maxEnergy))
        {
            if (currentEnergy > 0.01f) // Solo encender si hay algo de energía
            {
                 isLampOn = true;
                 isAutoRecharging = false; // Dejar de mostrar "recargando" al encender
                 UpdateLampState(isLampOn);
                 if(showDebugMessages) Debug.Log("LampSystem: Encendida (desde estado recargando/lleno).");
            } else {
                 if(showDebugMessages) Debug.Log("LampSystem: Intento de encender fallido (energía 0).");
                 // Opcional: sonido de fallo
            }
        }
        // Si la lámpara está encendida O está apagada SIN fusión (o sin recarga activa)
        else
        {
             bool canTurnOn = currentEnergy > 0.01f;
             // Si está encendida, apagarla
             if (isLampOn)
             {
                 isLampOn = false;
                 UpdateLampState(isLampOn);
                 // Si tiene fusión, iniciar recarga al apagar
                 if (isFireflyMerged) {
                     isAutoRecharging = true;
                      if(showDebugMessages) Debug.Log("LampSystem: Apagada. Iniciando auto-recarga.");
                 } else {
                      if(showDebugMessages) Debug.Log("LampSystem: Apagada (sin fusión).");
                 }

             }
             // Si está apagada y se puede encender (sin estar en modo autorecarga especial)
             else if (canTurnOn)
             {
                 isLampOn = true;
                 isAutoRecharging = false; // Asegurar que no esté en modo recarga
                 UpdateLampState(isLampOn);
                  if(showDebugMessages) Debug.Log("LampSystem: Encendida (normal).");
             }
             else {
                 // Intento de encender sin energía y sin modo recarga
                  if(showDebugMessages) Debug.Log("LampSystem: Intento de encender fallido (sin energía/fusión).");
             }
        }
    }


    private void Update()
    {
        if (isLampOn)
        {
            currentEnergy = Mathf.Max(0, currentEnergy - energyDrainRate * Time.deltaTime);
            if (currentEnergy <= 0)
            {
                UpdateLampState(false); // Apagar
                if (isFireflyMerged) // Si tiene fusión...
                {
                    isAutoRecharging = true; // ...empezar a recargar
                    if (showDebugMessages) Debug.Log("LampSystem: Energía agotada -> Iniciando auto-recarga.");
                }
            }
            else
            {
                CheckForEnemiesInLight();
            }
        }
        // Si está apagada y tiene fusión
        else if (isFireflyMerged)
        {
             // Recargar si no está llena
             if (currentEnergy < maxEnergy)
             {
                 currentEnergy = Mathf.Min(maxEnergy, currentEnergy + passiveRechargeRate * Time.deltaTime);
                 // Si estaba en modo autorecarga y llega al máximo
                 if (isAutoRecharging && currentEnergy >= maxEnergy)
                 {
                      // No desactivamos isAutoRecharging aquí, se desactiva al encender
                      if(showDebugMessages && Time.frameCount % 60 == 0) // Loguear menos frecuente
                        Debug.Log("LampSystem: Recarga completa. Lista para usar.");
                 }
             }
             // Si ya está llena y estaba en modo autorecarga (improbable pero por si acaso)
             else if (isAutoRecharging) {
                 // isAutoRecharging = false; // Podría desactivarse aquí, pero es más seguro al encender
             }
        }

        UpdateEnergyUI();
        HandleAiming(); // Lógica de apuntado sigue igual
    }

    private void UpdateEnergyUI()
    {
        // Usar la referencia directa a Image si existe
        if (lampFillImage != null)
        {
             lampFillImage.fillAmount = CurrentEnergyPercentage;
        }
        // Fallback si no está asignada la Image pero sí el GameObject
        else if (lampFillObj != null)
        {
            Image fillImage = lampFillObj.GetComponent<Image>();
            if (fillImage != null) fillImage.fillAmount = CurrentEnergyPercentage;
        }
    }

    // HandleAiming sin cambios
     private void HandleAiming()
    {
        if (isAiming && thirdPersonCamera != null && thirdPersonCamera.IsLockOnActive)
        {
            Transform lockTargetTransform = thirdPersonCamera.GetCurrentLockOnTarget(); // Usar método renombrado

            if (lockTargetTransform != null)
            {
                 LockOnTarget lockTargetComponent = lockTargetTransform.GetComponent<LockOnTarget>();
                 Vector3 targetPoint = lockTargetComponent != null ? lockTargetComponent.GetTargetPoint() : lockTargetTransform.position;
                 Vector3 targetDirection = targetPoint - transform.position;

                 if (targetDirection != Vector3.zero)
                 {
                     Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                     // Aplicar rotación más directa al apuntar
                     transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * aimAnimationSpeed * 2f); // Más rápida
                 }
            }
             // Si no hay objetivo fijado, mantener la rotación actual o volver a la del jugador?
             // Por ahora, no hace nada si no hay objetivo, la rotación la controla el PlayerController
        }
         // Si no está apuntando, la rotación es manejada por PlayerController
    }

    // CheckForEnemiesInLight sin cambios
     /// <summary>
    /// Comprueba qué enemigos están dentro del cono de luz, verifica línea de visión
    /// y llama a OnLightExposure en los enemigos válidos.
    /// </summary>
    private void CheckForEnemiesInLight()
    {
        // Salir si la linterna no está lista o apagada
        if (spotLight == null || !spotLight.enabled || !isLampOn) return;

        // --- LOG INICIAL ---
        // Descomenta para ver si la comprobación se está ejecutando
        // Debug.Log($"LampSystem: Checking for enemies. Position: {transform.position}, Range: {maxLightDistance}");
        // -----------------

        // Encuentra todos los colliders en la capa de enemigos dentro del rango máximo
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, maxLightDistance, enemyLayer);

        // --- LOG DETECCIÓN INICIAL ---
        if (enemiesInRange.Length > 0 && Time.frameCount % 60 == 0) { // Loguear menos frecuente
            // Descomenta si quieres ver todos los colliders detectados
            // string detectedNames = "";
            // foreach(var c in enemiesInRange) { detectedNames += c.name + ", "; }
            // Debug.Log($"LampSystem: OverlapSphere found {enemiesInRange.Length} colliders on enemyLayer: {detectedNames}");
        }
        // ---------------------------

        foreach (var enemyCollider in enemiesInRange)
        {
            // Obtener la raíz del enemigo y el componente reactivo a la luz
            Transform enemyRoot = enemyCollider.transform;
            ILightReactive lightReactive = enemyCollider.GetComponentInParent<ILightReactive>(); // Busca en padres por si acaso

            if (lightReactive == null) {
                 // --- LOG SI NO ENCUENTRA COMPONENTE ---
                 // Descomenta si sospechas que falta el componente ILightReactive
                 // Debug.LogWarning($"LampSystem: Collider {enemyCollider.name} en layer Enemy no tiene ILightReactive.");
                 // ------------------------------------
                 continue; // Saltar este collider si no es reactivo a la luz
            }

            // Usar el centro del collider detectado como punto objetivo
            Vector3 enemyCenter = enemyCollider.bounds.center;
            Vector3 directionToEnemyRaw = enemyCenter - transform.position;
            float distanceToEnemy = directionToEnemyRaw.magnitude;

            // Comprobar distancia mínima y máxima (doble chequeo por si acaso)
            if (distanceToEnemy <= 0.01f || distanceToEnemy > maxLightDistance) continue;

            Vector3 directionToEnemyNorm = directionToEnemyRaw.normalized;
            // Calcular ángulo entre la dirección de la linterna y la dirección al enemigo
            float angleToEnemy = Vector3.Angle(transform.forward, directionToEnemyNorm);

            // Comprobar si está dentro del ángulo del cono de luz
            if (angleToEnemy <= flashlightAngle * 0.5f)
            {
                // --- LOG ÁNGULO OK ---
                // Descomenta para confirmar que pasa el chequeo de ángulo
                // Debug.Log($"LampSystem: Enemy {enemyRoot.name} is within angle ({angleToEnemy:F1} <= {flashlightAngle * 0.5f:F1}). Checking LoS...");
                // -------------------

                // Origen del raycast/spherecast (ligeramente delante de la linterna)
                Vector3 rayOrigin = transform.position + transform.forward * 0.1f;
                Vector3 rayTarget = enemyCenter; // Apuntar al centro del collider
                Vector3 directionToTarget = (rayTarget - rayOrigin).normalized;
                float distanceToTarget = Vector3.Distance(rayOrigin, rayTarget);

                // Salir si la distancia recalculada es mayor (poco probable pero posible)
                if (distanceToTarget > maxLightDistance) continue;

                // Lanzar un SphereCast para simular el grosor del haz y detectar obstáculos
                // obstacleLayer debe contener las capas de objetos que bloquean la luz (paredes, etc.)
                // ¡Asegúrate que la capa del enemigo NO esté en obstacleLayer!
                bool hitObstacle = Physics.SphereCast(rayOrigin, 0.1f, directionToTarget, out RaycastHit hitInfo, distanceToTarget, obstacleLayer);
                // Comprobar si lo que golpeó es el propio enemigo
                bool hitIsEnemy = hitObstacle && (hitInfo.transform == enemyRoot || hitInfo.collider == enemyCollider);

                // Dibujar rayo para depuración visual en la escena
                if (drawLightConeGizmo)
                {
                    Debug.DrawRay(rayOrigin, directionToTarget * distanceToTarget, (hitObstacle && !hitIsEnemy) ? raycastHitColor : raycastMissColor);
                }

                // Si NO golpea un obstáculo O si el obstáculo golpeado es el enemigo mismo...
                 if (!hitObstacle || hitIsEnemy)
                 {
                    // --- LOG FINAL ANTES DE LLAMAR A ONLIGHTEXPOSURE ---
                    // Este es el log crucial que indica que se va a aplicar el efecto
                    Debug.Log($"LampSystem: LoS OK to {enemyRoot.name}! Calling OnLightExposure...");
                    // ----------------------------------------------------

                    // Calcular la intensidad basada en la distancia (cae con la distancia)
                    float intensityFalloff = 1.0f - Mathf.Clamp01((distanceToEnemy - 1.0f) / (maxLightDistance - 1.0f));
                    float finalIntensity = 1.0f * intensityFalloff; // Asumiendo intensidad base 1.0

                    // Llamar al método OnLightExposure del enemigo
                    lightReactive.OnLightExposure(transform, finalIntensity);
                }
                else // Golpeó un obstáculo que NO es el enemigo
                {
                     // --- LOG SI HAY OBSTÁCULO ---
                     // Descomenta para ver qué bloquea la luz
                     // if (hitObstacle) Debug.Log($"LampSystem: LoS to {enemyRoot.name} blocked by {hitInfo.collider.name} on layer {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)}");
                     // -----------------------------
                }
            }
            // else // Fuera del ángulo
            // {
                // --- LOG SI FALLA ÁNGULO ---
                 // Descomenta para ver si falla el ángulo
                 // Debug.Log($"LampSystem: Enemy {enemyRoot.name} is outside angle ({angleToEnemy:F1} > {flashlightAngle * 0.5f:F1}).");
                // -------------------------
            // }
        } // Fin foreach
    } // Fin CheckForEnemiesInLight


    private void UpdateLampState(bool state)
    {
        isLampOn = state;
        if (spotLight != null)
        {
            spotLight.enabled = state;
            // Optimización: solo actualizar rango y ángulo si cambian
            // spotLight.range = maxLightDistance;
            // spotLight.spotAngle = flashlightAngle;
        }
    }

    // Métodos de la interfaz ILampController (sin cambios)
    public float GetFlashlightAngle() => flashlightAngle;
    public float GetMaxLightDistance() => maxLightDistance;
    public float GetMaxEnergy() => maxEnergy;
    public void SetLampActive(bool active) => UpdateLampState(active);

    // Método para recargar (sin cambios)
    public void RechargeEnergy(float amount = float.MaxValue)
    {
        currentEnergy = (amount >= maxEnergy) ? maxEnergy : Mathf.Min(currentEnergy + amount, maxEnergy);
        isAutoRecharging = isFireflyMerged && currentEnergy < maxEnergy; // Re-evaluar modo recarga
        UpdateEnergyUI();
        if(showDebugMessages) Debug.Log($"LampSystem: Energía recargada. Actual: {currentEnergy}/{maxEnergy}");
    }

    // Método para visibilidad UI (modificado para asegurar referencias)
     public void SetEnergyUIVisible(bool visible)
    {
        if (lampEmptyObj != null) lampEmptyObj.SetActive(visible);
        // Activar/desactivar el objeto que contiene la imagen de relleno
        if (lampFillObj != null) lampFillObj.SetActive(visible);
        // También podrías querer controlar la Image directamente si es diferente al objeto
        // if (lampFillImage != null) lampFillImage.enabled = visible;
    }


    // OnDrawGizmosSelected sin cambios
    private void OnDrawGizmosSelected()
    {
        if (!drawLightConeGizmo || spotLight == null) return;
        Gizmos.color = gizmoColor;
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        float angleRad = flashlightAngle * Mathf.Deg2Rad; // Usar valor del script
        float range = maxLightDistance; // Usar valor del script
        float radiusAtEnd = range * Mathf.Tan(angleRad * 0.5f);
        Gizmos.DrawWireSphere(Vector3.forward * range, radiusAtEnd);
        // Líneas del cono
        int steps = 12;
        for (int i = 0; i < steps; i++) {
             float angle1 = (float)i / steps * Mathf.PI * 2f;
             float angle2 = (float)(i + 1) / steps * Mathf.PI * 2f;
             Vector3 p1 = new Vector3(Mathf.Cos(angle1) * radiusAtEnd, Mathf.Sin(angle1) * radiusAtEnd, range);
             Vector3 p2 = new Vector3(Mathf.Cos(angle2) * radiusAtEnd, Mathf.Sin(angle2) * radiusAtEnd, range);
             Gizmos.DrawLine(Vector3.zero, p1);
             Gizmos.DrawLine(p1, p2);
         }

        Gizmos.matrix = originalMatrix;
    }
}