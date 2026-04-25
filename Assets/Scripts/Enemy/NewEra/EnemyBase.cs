// Archivo: Scripts/Enemy/NewEra/EnemyBase.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using WoN.Interfaces;
using UnityEngine.VFX;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))] // AudioSource principal
[RequireComponent(typeof(AudioSource))] // AudioSource secundario (para daño por luz)
[RequireComponent(typeof(AudioSource))] // Movement (Nuevo)
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(LockOnTarget))]
public abstract class EnemyBase : MonoBehaviour, ILightReactive, ILockOnVisuals
{
    public enum EnemyState { Idle, Wandering, Chasing, Attacking, Fleeing, Alerting, Searching, SpecificAction, Neutralized }

    [Header("AI State (Read Only)")]
    [SerializeField] protected EnemyState currentState = EnemyState.Wandering;
    public EnemyState CurrentState => currentState;

    [Header("Configuration")]
    [Tooltip("Asigna aquí el Asset del Perfil de IA para este enemigo.")]
    public EnemyAIProfile profile;

    
    [Header("Energy reward")]
    [Tooltip("Cantidad de energía que se obtiene al neutralizar al enemigo.")]
    public float energyReward = 10f;

    [Header("Core Components")]
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [Tooltip("AudioSource principal para sonidos de ataque, pasos, etc.")]
    [SerializeField] protected AudioSource mainAudioSource;
    [Tooltip("AudioSource secundario para el sonido de daño por luz.")]
    [SerializeField] protected AudioSource lightDamageAudioSource;
    [Tooltip("AudioSource terciario para sonidos en bucle (correr, caminar). Configurar 'Loop' y 'Spatial Blend=1'.")]
    [SerializeField] protected AudioSource movementAudioSource;
    [SerializeField] protected Renderer enemyRenderer;
    [SerializeField] protected Collider mainCollider;
    [SerializeField] protected LockOnTarget lockOnTargetComponent;

    [Header("Targeting")]
    [SerializeField] protected Transform player;
    [SerializeField] protected LayerMask obstacleLayer;
    protected Vector3 lastKnownPlayerPosition = Vector3.positiveInfinity; // Usar un valor inválido inicial

    // --- NUEVO: Variables para Sonido de Pisadas ---
    [Header("Audio - Movement")]
    [Tooltip("Array de sonidos de pisadas que se reproducirán aleatoriamente.")]
    [SerializeField] private AudioClip[] footstepSounds;
    [Tooltip("Volumen de los sonidos de pisadas (0 a 1).")]
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;
    // -------------------------------------------

    [Header("Light Reaction & Death Effects")]
    [SerializeField] protected Material dissolveMaterial;
    [SerializeField] protected float dissolveDuration = 2.0f;
    [SerializeField] protected GameObject deathVFXPrefab;
    [SerializeField] protected float deathVFXDuration = 5.0f;
    [SerializeField] protected AudioClip hitSound;
    [SerializeField] protected float lightAccumulationRate = 1.0f;
    protected float lightExposureTime = 0f;

    protected Material currentMaterialInstance;
    protected bool isCurrentlyLockedOn = false;
    protected Color currentTargetEmissionColor;

    protected readonly int neutralizedTriggerAnimHash = Animator.StringToHash("Neutralized");
    protected static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
    protected static readonly int EmissiveColorID = Shader.PropertyToID("_EmissiveColor");
    protected readonly int walkSpeedAnimHash = Animator.StringToHash("MoveSpeed");

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        mainCollider = GetComponent<Collider>();
        lockOnTargetComponent = GetComponent<LockOnTarget>();
        enemyRenderer = GetComponentInChildren<Renderer>();

        AudioSource[] audioSources = GetComponents<AudioSource>();
        if (audioSources.Length >= 1) mainAudioSource = audioSources[0];
        else Debug.LogError($"Falta el AudioSource principal en {gameObject.name}", this);
        if (audioSources.Length >= 2) lightDamageAudioSource = audioSources[1];
        else { Debug.LogWarning($"Falta el SEGUNDO AudioSource para daño por luz en {gameObject.name}. Añadiendo...", this); lightDamageAudioSource = gameObject.AddComponent<AudioSource>(); }
        if (audioSources.Length >= 3) movementAudioSource = audioSources[2];
        else { Debug.LogWarning($"Falta el TERCER AudioSource para sonidos de movimiento en {gameObject.name}. Intentando añadir...", this); movementAudioSource = gameObject.AddComponent<AudioSource>(); }
        if (movementAudioSource != null) {
            movementAudioSource.playOnAwake = false;
            movementAudioSource.loop = true; // Generalmente los sonidos de movimiento son en bucle
            movementAudioSource.spatialBlend = 1.0f; // Asegurar que sea 3D
        }

        if (profile == null) { Debug.LogError($"¡ERROR CRÍTICO! EnemyAIProfile no asignado en el Inspector para {gameObject.name}. Desactivando script.", this); enabled = false; return; }

        if (agent != null) { agent.speed = profile.wanderSpeed; agent.angularSpeed = profile.rotationSpeed * 12; agent.autoBraking = true; }
        currentTargetEmissionColor = profile.idleEmissionColor;

        if (lightDamageAudioSource != null) {
            if (profile.lightDamageSoundClip != null) {
                lightDamageAudioSource.clip = profile.lightDamageSoundClip;
                lightDamageAudioSource.loop = true;
                lightDamageAudioSource.playOnAwake = false;
                lightDamageAudioSource.volume = 0f;
                lightDamageAudioSource.spatialBlend = 1.0f;
            } else { lightDamageAudioSource.enabled = false; }
        } else { Debug.LogError($"Error configurando Light Damage AudioSource en {gameObject.name}", this); }

        SetupInitialMaterial();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogError($"Jugador con tag 'Player' no encontrado por {gameObject.name}.", this);
    }

    protected virtual void Start()
    {
        if (!enabled) return;
        if (agent != null) agent.enabled = true;
        SetLockOnMaterial(false);
    }

    protected virtual void Update()
    {
        if (profile == null || player == null || currentState == EnemyState.Neutralized) return;

        bool wasExposedLastFrame = lightExposureTime > 0;

        // --- CAMBIO DE ORDEN ---
        // 1. Comprobar si se alcanza el umbral ANTES de aplicar recuperación
        if (lightExposureTime >= profile.exposureTimeToKill)
        {
            // Log que ya teníamos (si se cumple la condición)
            Debug.Log($"[{gameObject.name}] Threshold Reached! Exposure: {lightExposureTime:F2} >= TimeToKill: {profile.exposureTimeToKill:F2}. Current State: {currentState}");
            if (currentState != EnemyState.Neutralized)
            {
                NeutralizeEnemy();
                return; // Salir del Update si se neutraliza
            }
        }

        // 2. Aplicar recuperación SI AÚN no está neutralizado y hay exposición
        if (currentState != EnemyState.Neutralized && lightExposureTime > 0)
        {
             lightExposureTime = Mathf.Max(0, lightExposureTime - Time.deltaTime * 0.5f);
        }
        // --- FIN CAMBIO DE ORDEN ---

        // Log de diagnóstico en Update (opcional mantenerlo)
        if (lightExposureTime > 0 && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[{gameObject.name}] Update Check -> Exposure: {lightExposureTime:F3} | TimeToKill: {profile.exposureTimeToKill:F3} | State: {currentState}");
        }

        // El resto del Update sigue igual
        UpdateAIState();
        UpdateEmissionBasedOnExposure();
        UpdateCommonAnimations();
        UpdateLightDamageSound(wasExposedLastFrame);
    }

    /// <summary>
    /// Intenta encontrar el punto más cercano en el NavMesh a una posición dada.
    /// </summary>
    /// <param name="position">La posición deseada.</param>
    /// <param name="searchRadius">El radio máximo para buscar un punto válido en el NavMesh.</param>
    /// <param name="foundPosition">La posición válida encontrada en el NavMesh (si se encuentra).</param>
    /// <returns>True si se encontró un punto válido, False en caso contrario.</returns>
    protected bool TryGetNearestNavMeshPoint(Vector3 position, float searchRadius, out Vector3 foundPosition)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(position, out navHit, searchRadius, NavMesh.AllAreas)) // NavMesh.AllAreas asume todas las áreas son caminables
        {
            foundPosition = navHit.position;
            return true;
        }
        else
        {
            foundPosition = position; // Devuelve la posición original si no se encuentra nada
            //Debug.LogWarning($"No se encontró un punto NavMesh cerca de {position} dentro de un radio de {searchRadius}m.");
            return false;
        }
    }

    public virtual void OnLightExposure(Transform lightSource, float intensity)
    {
        // Log inicial (opcional mantenerlo)
        // Debug.Log($"[{gameObject.name}] >>> OnLightExposure Received! Intensity: {intensity:F3}, Current Exposure BEFORE add: {lightExposureTime:F3}");

        if (profile == null || currentState == EnemyState.Neutralized) return;

        float adjustedIntensity = Mathf.Clamp01(intensity);
        float exposureToAdd = Time.deltaTime * lightAccumulationRate * adjustedIntensity;
        // Sigue usando Mathf.Min para no superar el límite innecesariamente
        lightExposureTime = Mathf.Min(profile.exposureTimeToKill, lightExposureTime + exposureToAdd);

        // Log de acumulación (opcional mantenerlo)
        // Debug.Log($"[{gameObject.name}] Exposure Added: {exposureToAdd:F4}, New Total Exposure: {lightExposureTime:F4}");

        if (hitSound != null && mainAudioSource != null && !mainAudioSource.isPlaying && Random.value < 0.15f) { mainAudioSource.PlayOneShot(hitSound); }
        if (lightExposureTime > 0 && lightDamageAudioSource != null && lightDamageAudioSource.enabled && !lightDamageAudioSource.isPlaying) { lightDamageAudioSource.Play(); }
    }
     public void OnLightExposure() => OnLightExposure(null, 1.0f);

    protected virtual void UpdateLightDamageSound(bool wasExposedLastFrame)
    {
        if (lightDamageAudioSource == null || !lightDamageAudioSource.enabled || profile == null || profile.lightDamageSoundClip == null || currentState == EnemyState.Neutralized) {
            if(lightDamageAudioSource != null && lightDamageAudioSource.isPlaying) { lightDamageAudioSource.Stop(); } // Detener si no debe sonar
             if(lightDamageAudioSource != null && lightDamageAudioSource.volume > 0) { lightDamageAudioSource.volume = 0f; } // Poner volumen a 0
            return;
        }
        bool isCurrentlyExposed = lightExposureTime > 0;
        if (isCurrentlyExposed) {
            float volumeRatio = Mathf.Clamp01(lightExposureTime / profile.exposureTimeToKill);
            float targetVolume = Mathf.Lerp(0f, profile.lightDamageMaxVolume, volumeRatio);
            lightDamageAudioSource.volume = targetVolume;
            if (!lightDamageAudioSource.isPlaying) { lightDamageAudioSource.Play(); }
        } else if (wasExposedLastFrame) {
            lightDamageAudioSource.volume = 0f;
            if (lightDamageAudioSource.isPlaying) { lightDamageAudioSource.Stop(); }
        }
    }

    protected virtual void NeutralizeEnemy()
    {
        Debug.Log($"[{gameObject.name}] --- Entering NeutralizeEnemy() ---");
        if (currentState == EnemyState.Neutralized) { Debug.Log($"[{gameObject.name}] NeutralizeEnemy() called but already neutralized. Exiting."); return; }
        SetLockOnMaterial(false);
        ChangeState(EnemyState.Neutralized);
        if (agent != null && agent.enabled) { agent.isStopped = true; agent.enabled = false; Debug.Log($"[{gameObject.name}] Agent stopped and disabled."); } else { Debug.LogWarning($"[{gameObject.name}] Agent was null or disabled during NeutralizeEnemy."); }
        if (mainCollider != null) { mainCollider.enabled = false; Debug.Log($"[{gameObject.name}] Main Collider disabled."); } else { Debug.LogWarning($"[{gameObject.name}] Main Collider was null during NeutralizeEnemy."); }
        if (lockOnTargetComponent != null) { lockOnTargetComponent.enabled = false; Debug.Log($"[{gameObject.name}] LockOnTarget disabled.");} else { Debug.LogWarning($"[{gameObject.name}] LockOnTarget was null during NeutralizeEnemy."); }
        if (mainAudioSource != null && mainAudioSource.isPlaying) { mainAudioSource.Stop(); Debug.Log($"[{gameObject.name}] Main AudioSource stopped."); }
        if (lightDamageAudioSource != null && lightDamageAudioSource.isPlaying) { lightDamageAudioSource.Stop(); Debug.Log($"[{gameObject.name}] Light Damage AudioSource stopped."); }
        if (animator != null) { animator.SetTrigger(neutralizedTriggerAnimHash); Debug.Log($"[{gameObject.name}] Neutralized animation triggered."); } else { Debug.LogWarning($"[{gameObject.name}] Animator was null during NeutralizeEnemy."); }
        if (deathVFXPrefab != null) { GameObject vfx = Instantiate(deathVFXPrefab, transform.position + Vector3.up * 0.5f, transform.rotation); Destroy(vfx, deathVFXDuration); Debug.Log($"[{gameObject.name}] Death VFX instantiated."); } else { Debug.LogWarning($"[{gameObject.name}] Death VFX Prefab not assigned."); }
        Debug.Log($"[{gameObject.name}] Starting Dissolve Coroutine...");
        StartCoroutine(DissolveEnemyCoroutine());
        LampSystem lampSystem = FindAnyObjectByType<LampSystem>();
        {
            lampSystem.RewardEnergy(energyReward);
        }
    }

    // --- NUEVO MÉTODO PÚBLICO ReceiveAlert ---
    /// <summary>
    /// Método llamado cuando este enemigo es alertado por otro (ej. un Screamer).
    /// </summary>
    /// <param name="alertOrigin">Posición desde donde se originó la alerta.</param>
    /// <param name="targetPosition">Posición hacia la que el enemigo debe investigar (normalmente la última posición conocida del jugador).</param>
    public virtual void ReceiveAlert(Vector3 alertOrigin, Vector3 targetPosition)
    {
        // Ignorar alerta si está en ciertos estados o muerto
        if (currentState == EnemyState.Chasing || currentState == EnemyState.Attacking ||
            currentState == EnemyState.Fleeing || currentState == EnemyState.Neutralized ||
            currentState == EnemyState.SpecificAction || currentState == EnemyState.Alerting)
        {
            return; // No interrumpir estas acciones con una simple alerta
        }

        // Si está Wandering, Idle o incluso Searching (para actualizar el punto de búsqueda), reaccionar.
        //Debug.Log($"{gameObject.name} recibió alerta desde {alertOrigin}. Investigando hacia {targetPosition}. Estado actual era {currentState}.");

        // Guardar la posición del jugador (proporcionada por el Screamer) como el punto a buscar
        lastKnownPlayerPosition = targetPosition;

        // Cambiar al estado de búsqueda
        ChangeState(EnemyState.Searching);
    }

    /// <summary>
    /// Reproduce un sonido de pisada aleatorio. Debe ser llamado por Animation Events
    /// en las animaciones de caminar/correr del enemigo.
    /// </summary>
    public void PlayFootstepSound()
    {
        // Solo reproducir si está en un estado de movimiento relevante y hay sonidos configurados
        bool isMovingState = (currentState == EnemyState.Wandering ||
                              currentState == EnemyState.Chasing ||
                              currentState == EnemyState.Fleeing ||
                              currentState == EnemyState.Searching);

        if (isMovingState && footstepSounds != null && footstepSounds.Length > 0 && mainAudioSource != null)
        {
            // Elegir un clip aleatorio del array
            int index = Random.Range(0, footstepSounds.Length);
            AudioClip clipToPlay = footstepSounds[index];

            if (clipToPlay != null)
            {
                // Reproducir como one-shot con el volumen especificado
                mainAudioSource.PlayOneShot(clipToPlay, footstepVolume);
                // Debug.Log($"[{gameObject.name}] Footstep sound played: {clipToPlay.name}"); // Log opcional
            }
        }
        // else // Log opcional para depurar por qué no suena
        // {
        //     if (!isMovingState)
        //         Debug.Log($"[{gameObject.name}] Footstep skipped: Not in movement state ({currentState})");
        //     else if (footstepSounds == null || footstepSounds.Length == 0)
        //         Debug.Log($"[{gameObject.name}] Footstep skipped: No footstepSounds assigned.");
        //     else if (mainAudioSource == null)
        //          Debug.Log($"[{gameObject.name}] Footstep skipped: mainAudioSource is null.");
        // }
    }

    // --- Métodos restantes (sin cambios lógicos) ---
    protected abstract void UpdateAIState();
    protected virtual void SetupInitialMaterial() { if (enemyRenderer == null || profile == null) return; Material materialToUse = null; if (profile.baseMaterial != null) { materialToUse = profile.baseMaterial; } else if (dissolveMaterial != null) { materialToUse = dissolveMaterial; Debug.LogWarning($"No Base Material en perfil de {gameObject.name}. Usando Dissolve Material como base visual.", this); } else { materialToUse = enemyRenderer.sharedMaterial; if (materialToUse == null) { Debug.LogError($"Ningún material base asignado (perfil, dissolve o renderer) en {gameObject.name}.", this); currentMaterialInstance = null; return; } Debug.LogWarning($"No Base Material en perfil ni Dissolve Material. Usando material existente del Renderer: {materialToUse.name}", this); } currentMaterialInstance = new Material(materialToUse); enemyRenderer.material = currentMaterialInstance; if (materialToUse == dissolveMaterial) { if (currentMaterialInstance.HasProperty(DissolveAmountID)) { currentMaterialInstance.SetFloat(DissolveAmountID, 0f); } else { Debug.LogError($"El Material '{dissolveMaterial.name}' asignado como Dissolve Material no tiene la propiedad '_DissolveAmount'.", this); } } UpdateEmissionBasedOnExposure(); }
    protected virtual void UpdateEmissionBasedOnExposure() { if (profile == null || currentMaterialInstance == null || currentState == EnemyState.Neutralized) return; if (!currentMaterialInstance.HasProperty(EmissiveColorID)) return; float healthRatio = 1.0f - Mathf.Clamp01(lightExposureTime / profile.exposureTimeToKill); Color currentEmission = Color.Lerp(Color.black, currentTargetEmissionColor, healthRatio); currentMaterialInstance.SetColor(EmissiveColorID, currentEmission); bool shouldEmit = healthRatio > 0.01f && (currentTargetEmissionColor.maxColorComponent > 0.01f); if (currentMaterialInstance.IsKeywordEnabled("_EMISSION") != shouldEmit) { if (shouldEmit) currentMaterialInstance.EnableKeyword("_EMISSION"); else currentMaterialInstance.DisableKeyword("_EMISSION"); } }
    public virtual void SetLockOnMaterial(bool isLockedOn) { if (profile == null || currentState == EnemyState.Neutralized || enemyRenderer == null) return; isCurrentlyLockedOn = isLockedOn; Material materialToAssign = null; if (isLockedOn && profile.lockedOnMaterial != null) { materialToAssign = profile.lockedOnMaterial; enemyRenderer.sharedMaterial = materialToAssign; currentMaterialInstance = null; } else { SetupInitialMaterial(); materialToAssign = currentMaterialInstance; } UpdateEmissionBasedOnExposure(); }
    public virtual void RegisterHit(Collider playerCollider) { if (playerCollider == null || currentState == EnemyState.Neutralized) return; if (playerCollider.CompareTag("Player")) { PlayerDamageEffects playerEffects = playerCollider.GetComponentInParent<PlayerDamageEffects>(); if (playerEffects != null) { playerEffects.TakeHit(); } else Debug.LogWarning($"{gameObject.name}: Player hit lacks PlayerDamageEffects script."); } }
    protected virtual IEnumerator DissolveEnemyCoroutine() { if (enemyRenderer == null) { Destroy(gameObject); yield break; } Material mat = enemyRenderer.material; if (mat == null || !mat.HasProperty(DissolveAmountID)) { if (dissolveMaterial != null) { mat = new Material(dissolveMaterial); enemyRenderer.material = mat; if (mat.HasProperty(DissolveAmountID)) { mat.SetFloat(DissolveAmountID, 0f); } else { Debug.LogError($"El material '{dissolveMaterial.name}' asignado como Dissolve Material no tiene la propiedad '_DissolveAmount'. Destruyendo objeto.", this); Destroy(gameObject); yield break; } } else { Debug.LogWarning($"No hay material de disolución configurado para {gameObject.name}. Destruyendo directamente.", this); Destroy(gameObject); yield break; } } float elapsed = 0f; float startDissolve = mat.GetFloat(DissolveAmountID); float endDissolve = 1.0f; while (elapsed < dissolveDuration) { elapsed += Time.deltaTime; mat.SetFloat(DissolveAmountID, Mathf.Lerp(startDissolve, endDissolve, elapsed / dissolveDuration)); yield return null; } mat.SetFloat(DissolveAmountID, endDissolve); Destroy(gameObject); }
    protected virtual void UpdateCommonAnimations() { if (animator == null || agent == null || !agent.enabled || profile == null) return; float normalizedSpeed = (profile.chaseSpeed > 0.01f) ? agent.velocity.magnitude / profile.chaseSpeed : 0f; animator.SetFloat(walkSpeedAnimHash, normalizedSpeed, 0.1f, Time.deltaTime); }
    protected virtual void ChangeState(EnemyState newState) { if (profile == null || currentState == newState || currentState == EnemyState.Neutralized) return; currentState = newState; OnEnterState(currentState); }
    protected virtual void OnEnterState(EnemyState newState) { if(profile == null) return; switch (newState) { case EnemyState.Wandering: case EnemyState.Idle: case EnemyState.Fleeing: currentTargetEmissionColor = profile.idleEmissionColor; if (agent != null && agent.enabled) agent.isStopped = false; break; case EnemyState.Chasing: case EnemyState.Searching: case EnemyState.Alerting: case EnemyState.SpecificAction: currentTargetEmissionColor = profile.alertEmissionColor; if (agent != null && agent.enabled) agent.isStopped = false; break; case EnemyState.Attacking: currentTargetEmissionColor = profile.alertEmissionColor; if (agent != null && agent.enabled) agent.isStopped = true; break; case EnemyState.Neutralized: currentTargetEmissionColor = Color.black; if (agent != null && agent.enabled) agent.isStopped = true; break; } UpdateEmissionBasedOnExposure(); }
    protected virtual void RotateTowards(Vector3 targetPosition) { if (profile == null || currentState == EnemyState.Neutralized || agent == null || !agent.enabled || !agent.updateRotation) return; Vector3 direction = (targetPosition - transform.position); direction.y = 0; if (direction.sqrMagnitude > 0.01f) { Quaternion lookRotation = Quaternion.LookRotation(direction.normalized); transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * profile.rotationSpeed); } }

} // Fin de la clase EnemyBase