using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

public class FireflyGuide : MonoBehaviour
{
    // Current scene name to determine behavior
    private string currentScene;

    [SerializeField] private string targetScene = "";

    [Header("Visual Settings")]
    [SerializeField] private Light fireflyLight;
    [SerializeField] private float minLightIntensity = 0.5f;
    [SerializeField] private float maxLightIntensity = 1.5f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private ParticleSystem glowParticles;
    [SerializeField] private AudioClip chirpSound;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waypointReachedDistance = 0.5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float hoverHeight = 1.5f; // Ajustado para claridad
    [SerializeField] private float circlingRadius = 1.5f; // Radio para girar alrededor del jugador
    [SerializeField] private float circlingSpeed = 2.5f;  // Velocidad al girar

    [Header("Reference Points (Tutorial Scene)")]
    [SerializeField] private Transform flashlightTransform; // Lámpara (Tutorial)
    [SerializeField] private Transform firstDoorTransform; // Primera puerta (Tutorial)
    [SerializeField] private Transform exitDoorTransform; // Puerta de salida (Tutorial)
    [SerializeField] private float initialCirclingDuration = 5.0f; // Tiempo girando al inicio del tutorial

    [Header("Fusion Settings")]
    [SerializeField] private float fusionDuration = 3.0f;
    [SerializeField] private VisualEffect fusionVisualEffect;
    [SerializeField] private AudioClip fusionSound;
    [SerializeField] private Color fusionColor = new Color(0.5f, 0.8f, 1f);
    public UnityEvent onFusionCompleted;

    [Header("Escape Path Settings (Tutorial Scene)")]
    [SerializeField] private Transform[] escapeWaypoints;
    [SerializeField] private float waypointStayDuration = 0.5f;
    [SerializeField] private Transform exitDoorFinalPosition;

    [Header("Destino Inicial (Main Quest Scene)")]
    [SerializeField] private Transform enemySpawnPoint;

    // --- Estados de la luciérnaga (Añadido CirclingPlayer) ---
    private enum FireflyState {
        Idle,
        CirclingPlayer, // Nuevo estado para el inicio del tutorial
        GuidingToTarget,
        WaitingAtTarget,
        FusingWithLamp,
        FollowingEscapePath,
        Deactivated
    }
    // --------------------------------------------------------

    private FireflyState currentState = FireflyState.Idle;
    private Transform playerTransform;
    private Transform customTargetTransform;

    private Vector3 initialPosition;
    private float currentCirclingAngle = 0f; // Ángulo para el movimiento circular

    private int currentWaypointIndex = 0;
    private AudioSource audioSource;
    private Coroutine movementCoroutine;
    private Coroutine floatingCoroutine; // Mantenido para FloatAroundObject y FloatInPlace

    private float originalMaxLightIntensity;
    private float originalMoveSpeed;
    private bool isInitialized = false;
    private bool isFused = false;

    private LampSystem playerLampSystem;

    private void Awake()
    {
        initialPosition = transform.position;
        if (fireflyLight == null) fireflyLight = GetComponentInChildren<Light>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        isFused = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;

        // Desactivar si está fusionada Y estamos en mainSceneQuest
        if (isFused && SceneManager.GetActiveScene().name == "mainSceneQuest") { // Comprobar escena aquí
            gameObject.SetActive(false);
            return;
        }
         // Si no está fusionada pero estamos en mainSceneQuest, aún no la desactivamos, podría necesitar guiar al enemigo
         else if (!isFused && SceneManager.GetActiveScene().name == "mainSceneQuest") {
             // Se configurará en Start
         }
         // Si estamos en el tutorial, continuar normal (se desactivará si es necesario en Start)
         else if (SceneManager.GetActiveScene().name == "TimmyHouse_Tutorial") {
             // Continuar
         }
         // Si estamos en otra escena y tiene un targetScene definido que no coincide, desactivar
         else if (!string.IsNullOrEmpty(targetScene) && SceneManager.GetActiveScene().name != targetScene) {
             gameObject.SetActive(false);
             return;
         }


        FindLampSystem();
        CheckForDuplicateFireflies();
    }

    private void FindLampSystem() {
         GameObject lampObject = GameObject.FindWithTag("Flashlight");
         if (lampObject != null) {
            playerLampSystem = lampObject.GetComponent<LampSystem>();
             flashlightTransform = lampObject.transform;
         }
    }

    private void CheckForDuplicateFireflies()
    {
        FireflyGuide[] fireflies = FindObjectsOfType<FireflyGuide>();
        if (fireflies.Length > 1) {
            foreach (FireflyGuide firefly in fireflies) {
                if (firefly != this && (string.IsNullOrEmpty(firefly.targetScene) || firefly.targetScene == this.targetScene)) {
                    Destroy(firefly.gameObject);
                }
            }
        }
    }

    private void Start()
    {
        if (!gameObject.activeSelf) return;
        currentScene = SceneManager.GetActiveScene().name;

        if (!string.IsNullOrEmpty(targetScene) && targetScene != currentScene) 
        {
            gameObject.SetActive(false);
            return;
        }
        if (string.IsNullOrEmpty(targetScene)) targetScene = currentScene;

        // Re-leer estado de fusión
        isFused = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;
        if (isFused && targetScene == "mainSceneQuest") {
            gameObject.SetActive(false);
            return;
        }

        if (playerTransform == null) {
             GameObject player = GameObject.FindGameObjectWithTag("Player");
             if (player != null) playerTransform = player.transform;
             else { Debug.LogError("FireflyGuide: Jugador no encontrado!", this); enabled = false; return;} // Salir si no hay jugador
        }
        if (playerLampSystem == null) FindLampSystem();

        if (fireflyLight != null) originalMaxLightIntensity = maxLightIntensity;
        originalMoveSpeed = moveSpeed;

        // Asegurar VFX detenido
        if (fusionVisualEffect != null) {
            fusionVisualEffect.Stop();
            fusionVisualEffect.enabled = false;
        }

        // --- LÓGICA DE INICIO POR ESCENA ---
        if (currentScene == "TimmyHouse_Tutorial")
        {
            // Iniciar secuencia específica del tutorial
            StartCoroutine(TutorialStartSequence());
        }
        else if (currentScene == "mainSceneQuest" && !isFused)
        {
             // Si no está fusionada, prepararse para guiar al enemigo más tarde
             currentState = FireflyState.Idle; // Esperar a ser activada por TutorialSequenceController
             Debug.Log($"FireflyGuide ({targetScene}): Inicializada en Idle, esperando activación.");
        }
        else // Otras escenas o si está fusionada en el tutorial (no debería pasar por chequeos previos)
        {
            currentState = FireflyState.Idle;
            StartCoroutine(FloatInPlace()); // Flotar si no hay comportamiento definido
        }
        // ---------------------------------

        isInitialized = true;
    }


    private void Update()
    {
        if (!isInitialized || !gameObject.activeSelf || currentState == FireflyState.Deactivated) return;

        // Actualizar movimiento circular si está en ese estado
        if (currentState == FireflyState.CirclingPlayer)
        {
            UpdateCirclingMovement();
        }

        UpdateVisualEffects();
    }

    private void UpdateVisualEffects()
    {
        if (fireflyLight != null) {
            float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1);
            fireflyLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, pulse);
        }
        if (glowParticles != null && !glowParticles.isPlaying) glowParticles.Play();
    }

    // --- NUEVA Corutina para inicio del Tutorial ---
    private IEnumerator TutorialStartSequence()
    {
        if (playerTransform == null) {
             Debug.LogError("TutorialStartSequence: PlayerTransform es null!");
             yield break;
        }

        Debug.Log("FireflyGuide (Tutorial): Iniciando secuencia - Girando alrededor del jugador.");
        currentState = FireflyState.CirclingPlayer;
        // La lógica de movimiento está en UpdateCirclingMovement ahora

        // Esperar el tiempo definido
        yield return new WaitForSeconds(initialCirclingDuration);

        // Después de girar, ir a la linterna si existe
        if (currentState == FireflyState.CirclingPlayer) // Solo si sigue girando
        {
             GoToFlashlight(); // Llamar al método para ir a la linterna
        }
    }
    // ---------------------------------------------

    // --- NUEVO Método para actualizar el movimiento circular ---
    private void UpdateCirclingMovement()
    {
        if (playerTransform != null)
        {
            currentCirclingAngle += circlingSpeed * Time.deltaTime;
            float x = Mathf.Sin(currentCirclingAngle) * circlingRadius;
            float z = Mathf.Cos(currentCirclingAngle) * circlingRadius;
            Vector3 offset = new Vector3(x, hoverHeight, z); // Usar hoverHeight

            // Moverse hacia la posición calculada alrededor del jugador
            Vector3 targetPosition = playerTransform.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed); // Usar Lerp para suavidad

            // Mirar hacia adelante en la dirección del círculo (opcional)
            Vector3 tangentDirection = new Vector3(-Mathf.Cos(currentCirclingAngle), 0, Mathf.Sin(currentCirclingAngle));
            if (tangentDirection != Vector3.zero)
            {
                 Quaternion targetRotation = Quaternion.LookRotation(tangentDirection);
                 transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
    // -----------------------------------------------------


    // --- Métodos Públicos para Control Externo ---

    /// <summary>
    /// Ordena a la luciérnaga que guíe al jugador hacia la linterna (SOLO TUTORIAL).
    /// </summary>
    public void GoToFlashlight()
    {
        // Solo ejecutar si estamos en el tutorial
        if (currentScene != "TimmyHouse_Tutorial") {
             Debug.LogWarning("GoToFlashlight llamado fuera de la escena del tutorial.");
             return;
         }
        if (flashlightTransform == null) {
            Debug.LogError("FireflyGuide: Referencia a flashlightTransform no asignada para GoToFlashlight!");
            // Podrías intentar encontrarla de nuevo aquí si es necesario
            FindLampSystem();
            if(flashlightTransform == null) return; // Salir si sigue sin encontrarse
        }

        // Llamar a GoToTarget con la linterna como objetivo
        GoToTarget(flashlightTransform);
        Debug.Log("FireflyGuide: Iniciando guía hacia la linterna.");
    }


    public void GoToTarget(Transform target)
    {
        if (!isInitialized || !gameObject.activeSelf || currentState == FireflyState.Deactivated || currentState == FireflyState.FusingWithLamp) return;
        if (target == null) { Debug.LogError("FireflyGuide: GoToTarget recibió un objetivo nulo."); return; }

        StopCurrentCoroutines();

        customTargetTransform = target;
        currentState = FireflyState.GuidingToTarget;
        movementCoroutine = StartCoroutine(MoveToTargetCoroutine(target));

        PlayChirpSound();
        //Debug.Log($"FireflyGuide: Guiando hacia {target.name}");
    }

    public void FuseWithLamp()
    {
        if (!isInitialized || !gameObject.activeSelf || currentState == FireflyState.Deactivated || currentState == FireflyState.FusingWithLamp || isFused) { return; }

        if (playerLampSystem == null || flashlightTransform == null) FindLampSystem();

        if (playerLampSystem != null && flashlightTransform != null && fusionVisualEffect != null) {
            StopCurrentCoroutines();
            currentState = FireflyState.FusingWithLamp;
            movementCoroutine = StartCoroutine(FusionSequence());
            Debug.Log("FireflyGuide: Iniciando fusión con la lámpara");
        } else {
            Debug.LogError("FireflyGuide: Faltan componentes requeridos para la fusión.");
             PlayerPrefs.SetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 1); PlayerPrefs.Save(); LampSystem.isFireflyMerged = true;
             
             gameObject.SetActive(false);
        }
    }

    public void GoToExit()
    {
        if (!isInitialized || !gameObject.activeSelf || currentState == FireflyState.Deactivated || currentState == FireflyState.FusingWithLamp) return;
         if (currentScene != "TimmyHouse_Tutorial") { return; }

        if (escapeWaypoints == null || escapeWaypoints.Length == 0) {
            if(exitDoorTransform != null) GoToTarget(exitDoorTransform);
            return;
        }

        EnhanceVisibilityDuringEscape();
        StopCurrentCoroutines();
        currentState = FireflyState.FollowingEscapePath;
        currentWaypointIndex = 0;
        movementCoroutine = StartCoroutine(FollowEscapePathCoroutine());
        Debug.Log("FireflyGuide: Siguiendo ruta de escape");
    }


    // --- Corutinas de Movimiento y Estado ---

    private IEnumerator MoveToTargetCoroutine(Transform target)
    {
        if (target == null) yield break;

        Vector3 targetPosition = target.position + Vector3.up * hoverHeight; // Usar hoverHeight

        while (Vector3.Distance(transform.position, targetPosition) > waypointReachedDistance)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            Vector3 direction = (targetPosition - transform.position).normalized;
            if (direction != Vector3.zero) {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
            }
            yield return null;
        }

        currentState = FireflyState.WaitingAtTarget;
        floatingCoroutine = StartCoroutine(FloatAroundObject(target, 0.7f));
        //Debug.Log($"FireflyGuide: Ha llegado a {target.name}");
        movementCoroutine = null;
    }

    private IEnumerator FloatAroundObject(Transform target, float radius)
    {
        if (target == null) yield break;
        float localAngle = Random.Range(0f, 360f);
        float baseHeight = target.position.y + hoverHeight; // Usar hoverHeight
        float currentCirclingSpeed = 2f; // Velocidad local para el círculo

        while (currentState == FireflyState.WaitingAtTarget)
        {
            localAngle += currentCirclingSpeed * Time.deltaTime;
            float xOffset = Mathf.Sin(localAngle) * radius;
            float zOffset = Mathf.Cos(localAngle) * radius;
            float yOffset = Mathf.Sin(Time.time * 1.5f) * 0.3f;

            Vector3 desiredPosition = target.position + new Vector3(xOffset, hoverHeight + yOffset, zOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * moveSpeed * 0.5f);
            yield return null;
        }
        floatingCoroutine = null;
    }

     private IEnumerator FollowEscapePathCoroutine()
    {
        if (escapeWaypoints == null || escapeWaypoints.Length == 0) yield break;
        PlayChirpSound();
        //Debug.Log($"FireflyGuide: Siguiendo {escapeWaypoints.Length} waypoints de escape");

        while (currentWaypointIndex < escapeWaypoints.Length)
        {
            Transform currentTarget = escapeWaypoints[currentWaypointIndex];
            if (currentTarget == null) { currentWaypointIndex++; continue; }
            Vector3 targetPosition = currentTarget.position;

            while (Vector3.Distance(transform.position, targetPosition) > waypointReachedDistance) {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                yield return null;
            }
            PlayChirpSound();
            currentWaypointIndex++;
            yield return new WaitForSeconds(waypointStayDuration);
        }
        //Debug.Log("Completados waypoints de escape.");

        Transform finalTarget = exitDoorFinalPosition ?? exitDoorTransform;
        if (finalTarget != null) {
             currentState = FireflyState.GuidingToTarget;
            yield return StartCoroutine(MoveToTargetCoroutine(finalTarget));
        } else {
             currentState = FireflyState.Idle;
             floatingCoroutine = StartCoroutine(FloatInPlace());
        }
        RestoreNormalVisibility();
        movementCoroutine = null;
    }

    // FusionSequence sin cambios (ya corregido)
    private IEnumerator FusionSequence()
    {
        if (playerLampSystem == null || flashlightTransform == null || fusionVisualEffect == null)
        {
            Debug.LogError("Faltan referencias para FusionSequence.");
            PlayerPrefs.SetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 1); PlayerPrefs.Save(); LampSystem.isFireflyMerged = true;
            currentState = FireflyState.Deactivated; gameObject.SetActive(false);
            yield break;
        }

        if (audioSource != null && fusionSound != null) audioSource.PlayOneShot(fusionSound);

        float elapsedTime = 0f;
        Vector3 initialScale = transform.localScale;
        Color originalLightColor = fireflyLight?.color ?? Color.yellow;

        fusionVisualEffect.enabled = true;
        fusionVisualEffect.Play();

        bool eventInvoked = false;
        UnityEngine.Events.UnityAction fusionCompleteAction = () => { eventInvoked = true; };
        onFusionCompleted.AddListener(fusionCompleteAction);


        while (elapsedTime < fusionDuration)
        {
            if (flashlightTransform == null) {
                Debug.LogError("¡FlashlightTransform se volvió null durante la fusión!");
                 break;
            }
            float t = elapsedTime / fusionDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            Vector3 targetPos = flashlightTransform.position;
            fusionVisualEffect.SetVector3("AttractTarget", targetPos);
            if (fireflyLight != null) {
                float intensityMultiplier = Mathf.Lerp(1.0f, 3.0f, smoothT);
                fireflyLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity * intensityMultiplier, Mathf.PingPong(Time.time * pulseSpeed * 2, 1));
                fireflyLight.color = Color.Lerp(originalLightColor, fusionColor, smoothT);
            }
            transform.localScale = initialScale * (1.0f - smoothT);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

         if (fireflyLight != null) fireflyLight.enabled = false;
         if (glowParticles != null) glowParticles.Stop();
         transform.localScale = Vector3.zero;

        if (playerLampSystem != null) playerLampSystem.RechargeEnergy();
        else Debug.LogWarning("playerLampSystem era null al intentar recargar.");

        PlayerPrefs.SetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 1);
        PlayerPrefs.Save();
        LampSystem.isFireflyMerged = true;
        isFused = true;
        //Debug.Log("Fusión completada. PlayerPrefs actualizado, LampSystem notificado.");

        VillageQuestManager questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (questManager != null)
        {
            questManager.TriggerEvent("FIREFLY_FUSION_COMPLETE");
        }
        
        // Invocar evento de completado
        onFusionCompleted?.Invoke();
        
        // Limpiar
        //fusionInProgress = false;
        
        // Desactivar luciérnaga
        //firefly.gameObject.SetActive(false);

        if (fusionVisualEffect != null) {
             yield return new WaitForSeconds(0.5f);
             fusionVisualEffect.Stop();
             fusionVisualEffect.enabled = false;
        }
        currentState = FireflyState.Deactivated;
        gameObject.SetActive(false);
        movementCoroutine = null;
    }

    // --- Helpers ---

    private void StopCurrentCoroutines()
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        if (floatingCoroutine != null) StopCoroutine(floatingCoroutine);
         // Detener también la corutina de inicio del tutorial si está activa
         // Necesitaríamos una referencia a ella, o usar StopAllCoroutines() aquí
         // StopAllCoroutines(); // Opción simple pero puede detener otras cosas si las hubiera
        movementCoroutine = null;
        floatingCoroutine = null;
    }

    private void PlayChirpSound()
    {
        if (audioSource != null && chirpSound != null) audioSource.PlayOneShot(chirpSound);
    }

    private void EnhanceVisibilityDuringEscape()
    {
        if (fireflyLight != null) maxLightIntensity = originalMaxLightIntensity * 1.5f;
        moveSpeed = originalMoveSpeed * 1.2f;
    }

    private void RestoreNormalVisibility()
    {
        if (fireflyLight != null) maxLightIntensity = originalMaxLightIntensity;
        moveSpeed = originalMoveSpeed;
    }


    // --- Métodos del Tutorial Scene ---

    public void GoToFirstDoor() {
         if(currentScene == "TimmyHouse_Tutorial" && firstDoorTransform != null) {
            GoToTarget(firstDoorTransform);
         } else if (currentScene != "TimmyHouse_Tutorial") {
              //Debug.LogWarning("GoToFirstDoor llamado fuera de la escena del tutorial.");
         } else {
              Debug.LogError("Referencia a firstDoorTransform no asignada.");
         }
    }

    public void StopDuringSequence() {
         if(currentScene == "TimmyHouse_Tutorial") {
             StopCurrentCoroutines();
             currentState = FireflyState.Idle;
             floatingCoroutine = StartCoroutine(FloatInPlace());
             //Debug.Log("FireflyGuide: Detenida durante secuencia (Tutorial).");
         } else {
             //Debug.LogWarning("StopDuringSequence llamado fuera de la escena del tutorial.");
         }
    }

     private IEnumerator FloatInPlace()
    {
        Vector3 basePosition = transform.position;
        float bobTime = Random.Range(0f, 5f);

        // Flotar mientras esté en Idle O WaitingAtTarget
        while (currentState == FireflyState.Idle || currentState == FireflyState.WaitingAtTarget)
        {
            bobTime += Time.deltaTime;
            float yOffset = Mathf.Sin(bobTime * 2f) * 0.1f;
            transform.position = basePosition + new Vector3(0, yOffset, 0);
            yield return null;
        }
        floatingCoroutine = null;
    }


    // --- Gizmos ---
     private void OnDrawGizmos()
    {
        if (!isInitialized) return; // Evitar errores en editor

        Gizmos.color = Color.cyan;
        if ((currentState == FireflyState.GuidingToTarget || currentState == FireflyState.WaitingAtTarget) && customTargetTransform != null) {
            Vector3 targetDrawPos = customTargetTransform.position + Vector3.up * hoverHeight;
            Gizmos.DrawLine(transform.position, targetDrawPos);
            Gizmos.DrawWireSphere(targetDrawPos, waypointReachedDistance);
        }

        Gizmos.color = Color.magenta;
        if (currentState == FireflyState.FollowingEscapePath && escapeWaypoints != null && escapeWaypoints.Length > 0) {
            if (currentWaypointIndex < escapeWaypoints.Length && escapeWaypoints[currentWaypointIndex] != null) {
                Gizmos.DrawLine(transform.position, escapeWaypoints[currentWaypointIndex].position);
            }
            for (int i = currentWaypointIndex; i < escapeWaypoints.Length; i++) {
                if (escapeWaypoints[i] == null) continue;
                 Gizmos.DrawWireSphere(escapeWaypoints[i].position, waypointReachedDistance * 0.8f);
                 if (i + 1 < escapeWaypoints.Length && escapeWaypoints[i+1] != null) { Gizmos.DrawLine(escapeWaypoints[i].position, escapeWaypoints[i+1].position); }
                 else if (i == escapeWaypoints.Length - 1) {
                     Transform finalDest = exitDoorFinalPosition ?? exitDoorTransform;
                     if (finalDest != null) { Gizmos.DrawLine(escapeWaypoints[i].position, finalDest.position); Gizmos.DrawWireSphere(finalDest.position, waypointReachedDistance); }
                 }
            }
        }
        else if (currentState == FireflyState.GuidingToTarget && customTargetTransform != null && (customTargetTransform == exitDoorTransform || customTargetTransform == exitDoorFinalPosition) && (escapeWaypoints == null || escapeWaypoints.Length == 0)) {
             Transform finalDest = exitDoorFinalPosition ?? exitDoorTransform;
             if (finalDest != null) { Gizmos.DrawLine(transform.position, finalDest.position); Gizmos.DrawWireSphere(finalDest.position, waypointReachedDistance); }
        }
    }

} // Fin de la clase FireflyGuide