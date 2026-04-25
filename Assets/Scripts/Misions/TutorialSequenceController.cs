using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TutorialSequenceController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private VillageQuestManager questManager;
    [SerializeField] private GameObject sleepingEnemyPrefab;
    [SerializeField] private Transform enemySpawnPoint;
    // [SerializeField] private Transform[] tutorialWaypoints; // No parece usarse aquí
    [SerializeField] private FireflyGuide fireflyGuide;

    [Header("Configuración de Secuencia")]
    [SerializeField] private float initialDelay = 1.0f; // Reducido
    [SerializeField] private float stepDelay = 1.0f; // Reducido
    [SerializeField] private bool activateOnSceneLoad = true;

    //[SerializeField] private GameObject spawner; // Referencia al objeto spawner si existe

    [Header("Lock-on Tutorial")]
    [SerializeField] private string lockOnQuestId = "LOCK_ON_TUTORIAL";
    //[SerializeField] private string lockOnCompletionEvent = "ENEMY_LOCKED_ON"; // No se usa directamente aquí

    [Header("Aim Tutorial")]
    [SerializeField] private string aimQuestId = "AIM_TUTORIAL";
    //[SerializeField] private string aimCompletionEvent = "PLAYER_AIMED"; // No se usa directamente aquí

    [Header("Fusión de Luciérnaga")]
    [SerializeField] private string fusionQuestId = "FIREFLY_FUSION";
    [SerializeField] private string fusionCompletionEvent = "FIREFLY_FUSION_COMPLETE";
    [SerializeField] private float fusionDelay = 1.0f;

    [Header("Lamp Tutorial")]
    [SerializeField] private string lampQuestId = "FLASHLIGHT_TUTORIAL";
    [SerializeField] private string lampCompletionEvent = "ENEMY_KILLED_BY_LIGHT";

    [Header("Eventos")]
    [SerializeField] private UnityEvent onTutorialStarted;
    [SerializeField] private UnityEvent onTutorialCompleted;

    private bool tutorialInProgress = false;
    private GameObject spawnedEnemy;
    private bool lockOnDetected = false;
    public static bool aimingDetected = false; // Mantenido estático como estaba
    private bool flashUsedDetected = false;
    private bool isFusionCompleted = false; // Para verificar estado cargado


    private void Start()
    {
        // --- Comprobar estado cargado ---
        isFusionCompleted = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;
        Debug.Log($"TutorialSequenceController Start: isFusionCompleted (from PlayerPrefs) = {isFusionCompleted}");
        // ---------------------------------

        FindRequiredComponents();

        if (isFusionCompleted)
        {
            Debug.Log("Tutorial ya completado (fusión detectada en PlayerPrefs), desactivando controlador y spawner.");
            // if (spawner != null) spawner.SetActive(false); // Desactivar spawner si existe
            gameObject.SetActive(false); // Desactivar este controlador
             // Asegurarse que la luciérnaga esté desactivada
             if(fireflyGuide != null) fireflyGuide.gameObject.SetActive(false);
            return; // No continuar si ya está completado
        }

        // Iniciar solo si no está completado y está configurado para auto-activarse
        if (activateOnSceneLoad)
        {
            Invoke(nameof(StartTutorialSequence), initialDelay);
        }
    }

    private void FindRequiredComponents()
    {
        if (questManager == null) questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (fireflyGuide == null) fireflyGuide = Object.FindAnyObjectByType<FireflyGuide>();

        // Validaciones básicas
        if (questManager == null) Debug.LogError("TutorialSequenceController: VillageQuestManager no encontrado!", this);
        if (sleepingEnemyPrefab == null) Debug.LogError("TutorialSequenceController: sleepingEnemyPrefab no asignado!", this);
        if (enemySpawnPoint == null) Debug.LogError("TutorialSequenceController: enemySpawnPoint no asignado!", this);
        // fireflyGuide es opcional para el funcionamiento básico pero necesario para guiar
        if (fireflyGuide == null) Debug.LogWarning("TutorialSequenceController: FireflyGuide no encontrado (guía visual no funcionará).", this);
    }

    public void StartTutorialSequence()
    {
        if (tutorialInProgress)
        {
            Debug.LogWarning("Tutorial sequence already in progress");
            return;
        }

        // Doble chequeo del estado de fusión por si acaso
        isFusionCompleted = PlayerPrefs.GetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 0) == 1;
        if (isFusionCompleted)
        {
            Debug.Log("StartTutorialSequence: Tutorial ya completado, no se iniciará.");
            gameObject.SetActive(false);
            return;
        }

        tutorialInProgress = true;
        StartCoroutine(RunTutorialSequence());
    }

    private IEnumerator RunTutorialSequence()
    {
        Debug.Log("Iniciando secuencia tutorial de la escena mainSceneQuest");
        onTutorialStarted?.Invoke();

        // Esperar un poco
        yield return new WaitForSeconds(0.5f); // Reducido

        // Solo ejecutar pasos si la misión correspondiente NO está completada
        // (Aunque el chequeo inicial debería prevenir esto, es una salvaguarda)

        // 1. Lock-On
        if (questManager != null && !questManager.IsQuestCompleted(lockOnQuestId))
        {
            yield return StartCoroutine(LockOnTutorialSequence());
        } else Debug.Log("Saltando LockOnTutorialSequence (ya completado)");

        // 2. Aim
        if (questManager != null && !questManager.IsQuestCompleted(aimQuestId))
        {
            yield return StartCoroutine(AimTutorialSequence());
        } else Debug.Log("Saltando AimTutorialSequence (ya completado)");

        // 3. Fusion
        if (questManager != null && !questManager.IsQuestCompleted(fusionQuestId))
        {
            yield return StartCoroutine(FireflyFusionSequence());
        } else Debug.Log("Saltando FireflyFusionSequence (ya completado)");

        // 4. Flashlight Use
        if (questManager != null && !questManager.IsQuestCompleted(lampQuestId))
        {
            yield return StartCoroutine(FlashlightTutorialSequence());
        } else Debug.Log("Saltando FlashlightTutorialSequence (ya completado)");

        // Marcar como completado (si no lo estaba ya)
        if (!isFusionCompleted) {
            PlayerPrefs.SetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 1);
            PlayerPrefs.Save();
             LampSystem.isFireflyMerged = true; // Actualizar estado estático también
             Debug.Log("PlayerPrefs actualizado: FireflyFusedState = 1");
        }

        onTutorialCompleted?.Invoke();
        tutorialInProgress = false;
        Debug.Log("Secuencia tutorial completada logicamente.");

        // Desactivar este controlador al finalizar
        gameObject.SetActive(false);
        // if (spawner != null) spawner.SetActive(false); // Desactivar spawner si existe

         // Asegurarse que la luciérnaga se desactive al final
         if (fireflyGuide != null)
         {
             fireflyGuide.gameObject.SetActive(false);
             Debug.Log("Luciérnaga desactivada al completar tutorial.");
         }
    }

    private GameObject SpawnSleepingEnemy()
    {
        if (sleepingEnemyPrefab == null || enemySpawnPoint == null) return null;

        // Verificar si ya existe un enemigo tutorial antes de spawnear
         GameObject existingEnemy = GameObject.Find(sleepingEnemyPrefab.name + "(Clone)"); // Busca por nombre clonado
         if (existingEnemy != null)
         {
             Debug.LogWarning("Ya existía un enemigo tutorial, reutilizándolo.");
             spawnedEnemy = existingEnemy;
             // Resetear su estado por si acaso
             var existingSleepingComp = spawnedEnemy.GetComponent<SleepingEnemyController>();
             if (existingSleepingComp != null) existingSleepingComp.ResetLightHitStatus();
             return spawnedEnemy;
         }


        spawnedEnemy = Instantiate(sleepingEnemyPrefab, enemySpawnPoint.position, enemySpawnPoint.rotation);
        var sleepingEnemy = spawnedEnemy.GetComponent<SleepingEnemyController>();
        if (sleepingEnemy != null)
        {
            sleepingEnemy.SetSleepingState(true);
            Debug.Log("Sleeping enemy spawned");
        }
        else
        {
            Debug.LogError("SleepingEnemyController component not found on prefab!");
            Destroy(spawnedEnemy); // Destruir si no tiene el componente necesario
            return null;
        }
        return spawnedEnemy;
    }

    private IEnumerator LockOnTutorialSequence()
    {
        Debug.Log("Iniciando paso 1: Lock-On Tutorial");

        // Spawnear enemigo SOLO si no existe ya uno
        if (spawnedEnemy == null) {
            spawnedEnemy = SpawnSleepingEnemy();
            if (spawnedEnemy == null)
            {
                Debug.LogError("No se pudo spawnear el enemigo para el tutorial");
                yield break; // Salir si no se puede spawnear
            }
        }


        if (fireflyGuide != null && fireflyGuide.gameObject.activeSelf)
        {
            fireflyGuide.GoToTarget(spawnedEnemy.transform);
            Debug.Log("Luciérnaga guiando al jugador hacia el enemigo dormido");
        }

        if (questManager != null)
        {
            questManager.ActivateQuest(lockOnQuestId);
            Debug.Log("Misión de Lock-on activada");
        }

        yield return StartCoroutine(WaitForLockOn());

        yield return new WaitForSeconds(stepDelay);
    }

    private IEnumerator AimTutorialSequence()
    {
        Debug.Log("Iniciando paso 2: Aim Tutorial");

        if (questManager != null)
        {
            questManager.ActivateQuest(aimQuestId);
            Debug.Log("Misión de Apuntado activada");
        }

        yield return StartCoroutine(WaitForAiming());

        yield return new WaitForSeconds(stepDelay);
    }

    private IEnumerator FireflyFusionSequence()
    {
        Debug.Log("Iniciando paso 3: Firefly Fusion");

        // Esperar un poco antes de activar la misión de fusión
        yield return new WaitForSeconds(fusionDelay);

        if (questManager != null)
        {
            questManager.ActivateQuest(fusionQuestId);
            Debug.Log("Misión de Fusión activada");
        }

        // Iniciar fusión si la luciérnaga existe y está activa
        if (fireflyGuide != null && fireflyGuide.gameObject.activeSelf)
        {
            fireflyGuide.FuseWithLamp();
            Debug.Log("Iniciando fusión de luciérnaga con lámpara");
        } else {
            // Si no hay luciérnaga, simular completitud inmediata para no bloquear
            Debug.LogWarning("No se encontró FireflyGuide activa para la fusión, simulando completitud.");
             if (questManager != null) questManager.TriggerEvent(fusionCompletionEvent);
             PlayerPrefs.SetInt(SaveSystem.FIREFLY_FUSED_PREF_KEY, 1); // Marcar completado
             PlayerPrefs.Save();
             LampSystem.isFireflyMerged = true;
        }

        // Esperar a que la fusión se complete (el evento lo dispara FireflyGuide o el bloque anterior)
        yield return StartCoroutine(WaitForFusionCompletion());

        yield return new WaitForSeconds(stepDelay);
    }

    private IEnumerator FlashlightTutorialSequence()
    {
        Debug.Log("Iniciando paso 4: Flashlight Tutorial");

        if (questManager != null)
        {
            questManager.ActivateQuest(lampQuestId);
            Debug.Log("Misión de uso de lámpara activada");
        }

        // Esperar a que el jugador use la luz en el enemigo
        yield return StartCoroutine(WaitForLightUsedOnEnemy());

        // Esperar a que el enemigo muera (la animación/efecto termine)
        // Asumiendo que el enemigo se desactiva o destruye al morir
         yield return new WaitUntil(() => spawnedEnemy == null || !spawnedEnemy.activeSelf);
         Debug.Log("Enemigo tutorial derrotado.");


        yield return new WaitForSeconds(0.5f); // Pequeña pausa extra

        Debug.Log("Tutorial de uso de lámpara completado");
    }

    // --- Métodos de espera (WaitFor...) sin cambios ---
     private IEnumerator WaitForLockOn()
    {
        lockOnDetected = false;
        var thirdPersonCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.onLockOnPerformed += OnLockOnDetected;
        } else {
             Debug.LogError("WaitForLockOn: ThirdPersonCamera no encontrada!");
             yield break; // Salir si no hay cámara
        }

        // Añadir un timeout por si acaso
        float waitStartTime = Time.time;
        float timeoutDuration = 30f; // 30 segundos

        while (!lockOnDetected && (Time.time - waitStartTime < timeoutDuration))
        {
            yield return null;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.onLockOnPerformed -= OnLockOnDetected;
        }

        if(lockOnDetected) Debug.Log("Lock-on detectado");
        else Debug.LogWarning("Timeout esperando Lock-on.");
    }

    private void OnLockOnDetected()
    {
        lockOnDetected = true;
    }

    private IEnumerator WaitForAiming()
    {
        aimingDetected = false;
        var playerController = Object.FindAnyObjectByType<PlayerController>();
         if(playerController == null) {
             Debug.LogError("WaitForAiming: PlayerController no encontrado!");
             yield break;
         }

        // Añadir timeout
        float waitStartTime = Time.time;
        float timeoutDuration = 30f;

        while (!aimingDetected && (Time.time - waitStartTime < timeoutDuration))
        {
            // El PlayerController debería poner aimingDetected a true al apuntar
             // Si usamos un evento en PlayerController sería más limpio:
             // yield return new WaitUntil(() => aimingDetected);
             // Pero por ahora, verificamos directamente si está en modo LockOn (que implica apuntar en tu setup)
            if (playerController.IsInLockOnMode() && playerController.isAiming) // Verificar ambos
            {
                aimingDetected = true;
            }

            yield return null;
        }

        if(aimingDetected) Debug.Log("Apuntado detectado");
         else Debug.LogWarning("Timeout esperando Aiming.");
    }

    private IEnumerator WaitForFusionCompletion()
    {
         // Esperar a que el QuestManager marque la misión como completada
         if (questManager == null) yield break;

         // Añadir timeout
        float waitStartTime = Time.time;
        float timeoutDuration = 10f; // Timeout más corto para la fusión

        yield return new WaitUntil(() => questManager.IsQuestCompleted(fusionQuestId) || (Time.time - waitStartTime > timeoutDuration));

         if(questManager.IsQuestCompleted(fusionQuestId)) Debug.Log("Fusión completada (detectada por QuestManager)");
         else Debug.LogWarning("Timeout esperando Fusion Completion.");
    }


    private IEnumerator WaitForLightUsedOnEnemy()
    {
        flashUsedDetected = false;
         if (spawnedEnemy == null) {
             Debug.LogError("WaitForLightUsedOnEnemy: spawnedEnemy es null!");
             yield break; // No se puede esperar si no hay enemigo
         }
        var sleepingEnemy = spawnedEnemy.GetComponent<SleepingEnemyController>();
         if (sleepingEnemy == null) {
             Debug.LogError("WaitForLightUsedOnEnemy: SleepingEnemyController no encontrado en spawnedEnemy!");
             yield break;
         }

         // Añadir timeout
        float waitStartTime = Time.time;
        float timeoutDuration = 60f; // Timeout más largo, depende del jugador

        while (!flashUsedDetected && spawnedEnemy != null && spawnedEnemy.activeSelf && (Time.time - waitStartTime < timeoutDuration))
        {
            if (sleepingEnemy.WasHitByLight())
            {
                flashUsedDetected = true;
            }
            yield return null;
        }

         if(flashUsedDetected) Debug.Log("Lámpara usada en enemigo detectada");
         else Debug.LogWarning("Timeout esperando uso de lámpara en enemigo.");

         // --- Añadido: Esperar a que la misión de la lámpara se complete ---
          if (questManager != null)
          {
                yield return new WaitUntil(() => questManager.IsQuestCompleted(lampQuestId) || (Time.time - waitStartTime > timeoutDuration + 5f)); // Timeout extendido
                 if(questManager.IsQuestCompleted(lampQuestId)) Debug.Log("Misión FLASHLIGHT_TUTORIAL completada.");
                 else Debug.LogWarning("Timeout esperando completar FLASHLIGHT_TUTORIAL.");
          }
         // -------------------------------------------------------------

    }

}