using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LockOnTutorialTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireflyGuide fireflyGuide;
    [SerializeField] private GameObject sleepingEnemyPrefab;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private Transform playerTransform;
    
    [Header("Tutorial Settings")]
    [SerializeField] private float delayBeforeGuiding = 1.5f;
    [SerializeField] private float delayAfterKillingEnemy = 2.0f;
    [SerializeField] private UnityEvent onTutorialCompleted;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;
    
    private VillageQuestManager questManager;
    private GameObject spawnedEnemy;
    private bool tutorialTriggered = false;
    private bool lockOnDetected = false;
    public static bool aimingDetected = false;
    private bool flashUsedDetected = false;
    
    private void Start()
    {
        // Find quest manager
        questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (questManager == null)
        {
            Debug.LogError("LockOnTutorialTrigger: VillageQuestManager not found");
        }
        
        // Find firefly guide if not assigned
        if (fireflyGuide == null)
        {
            fireflyGuide = Object.FindAnyObjectByType<FireflyGuide>();
            if (fireflyGuide == null)
            {
                Debug.LogError("LockOnTutorialTrigger: FireflyGuide not found");
            }
        }
        
        // Find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("LockOnTutorialTrigger: Player not found");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Only trigger once and only for the player
        if (tutorialTriggered || !other.CompareTag("Player")) return;
        
        // Verificar si el tutorial ya está completado
        if (questManager != null && questManager.IsQuestCompleted("FIREFLY_FUSION"))
        {
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Tutorial already completed, skipping");
            }
            return;
        }

        StartTutorial();
    }
    
    public void StartTutorial()
    {
        if (tutorialTriggered) return;
        
        // Verificar si el tutorial ya está completado
        if (questManager != null && questManager.IsQuestCompleted("FIREFLY_FUSION"))
        {
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Tutorial already completed, skipping");
            }
            return;
        }
        
        tutorialTriggered = true;
        
        if (showDebugMessages)
        {
            Debug.Log("LockOnTutorialTrigger: Starting Lock-On tutorial sequence");
        }
        
        SpawnSleepingEnemy();
        StartCoroutine(TutorialSequence());
    }
    
    private void SpawnSleepingEnemy()
    {
        if (sleepingEnemyPrefab == null || enemySpawnPoint == null)
        {
            Debug.LogError("LockOnTutorialTrigger: Missing enemy prefab or spawn point");
            return;
        }
        
        spawnedEnemy = Instantiate(sleepingEnemyPrefab, enemySpawnPoint.position, enemySpawnPoint.rotation);
        
        var sleepingEnemy = spawnedEnemy.GetComponent<SleepingEnemyController>();
        if (sleepingEnemy != null)
        {
            sleepingEnemy.SetSleepingState(true);
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Sleeping enemy spawned");
            }
        }
        else
        {
            Debug.LogError("LockOnTutorialTrigger: SleepingEnemyController component not found on prefab");
        }
    }
    
    private IEnumerator TutorialSequence()
    {
        yield return new WaitForSeconds(delayBeforeGuiding);
        
        if (questManager != null && !questManager.IsQuestCompleted("LOCK_ON_TUTORIAL"))
        {
            questManager.ActivateQuest("LOCK_ON_TUTORIAL");
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Started 'Press Q to lock on' quest");
            }
        }
        
        if (fireflyGuide != null && spawnedEnemy != null)
        {
            fireflyGuide.GoToTarget(spawnedEnemy.transform);
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Firefly is guiding player to the sleeping enemy");
            }
        }
        
        yield return StartCoroutine(WaitForLockOn());
        
        if (questManager != null && !questManager.IsQuestCompleted("AIM_TUTORIAL"))
        {
            questManager.ActivateQuest("AIM_TUTORIAL");
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Started 'Right Click to aim' quest");
            }
        }
        
        yield return StartCoroutine(WaitForAiming());
        
        if (questManager != null && !questManager.IsQuestCompleted("FLASHLIGHT_TUTORIAL"))
        {
            questManager.ActivateQuest("FLASHLIGHT_TUTORIAL");
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Started 'Left Click to use flashlight' quest");
            }
        }
        
        yield return StartCoroutine(WaitForFlashlightUse());
        yield return new WaitForSeconds(delayAfterKillingEnemy);
        
        if (fireflyGuide != null)
        {
            fireflyGuide.FuseWithLamp();
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Firefly is fusing with lamp");
            }
        }
        
        if (questManager != null && !questManager.IsQuestCompleted("FIREFLY_FUSION"))
        {
            questManager.TriggerEvent("FLASHLIGHT_USED_ON_ENEMY");
            questManager.ActivateQuest("FIREFLY_FUSION");
            
            if (showDebugMessages)
            {
                Debug.Log("LockOnTutorialTrigger: Lock-on tutorial completed");
            }
        }
        
        onTutorialCompleted?.Invoke();
    }
    
    private IEnumerator WaitForLockOn()
    {
        lockOnDetected = false;
        
        var thirdPersonCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
        
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.onLockOnPerformed += OnLockOnDetected;
        }
        
        while (!lockOnDetected)
        {
            yield return null;
        }
        
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.onLockOnPerformed -= OnLockOnDetected;
        }
        
        if (showDebugMessages)
        {
            Debug.Log("LockOnTutorialTrigger: Lock-on detected");
        }
    }
    
    private void OnLockOnDetected()
    {
        lockOnDetected = true;
    }
    
    private IEnumerator WaitForAiming()
    {
        aimingDetected = false;
        
        var playerController = Object.FindAnyObjectByType<PlayerController>();
        
        while (!aimingDetected)
        {
            if (playerController != null && playerController.IsInLockOnMode())
            {
                aimingDetected = true;
            }

            yield return null;
        }
        
        if (showDebugMessages)
        {
            Debug.Log("LockOnTutorialTrigger: Aiming detected");
        }
    }
    
    private IEnumerator WaitForFlashlightUse()
    {
        flashUsedDetected = false;
        
        while (!flashUsedDetected && spawnedEnemy != null)
        {
            var sleepingEnemy = spawnedEnemy.GetComponent<SleepingEnemyController>();
            if (sleepingEnemy != null && sleepingEnemy.WasHitByLight())
            {
                flashUsedDetected = true;
            }
            
            yield return null;
        }
        
        if (showDebugMessages)
        {
            Debug.Log("LockOnTutorialTrigger: Flashlight used on enemy");
        }
    }
    
    public void CompleteTutorial()
    {
        StopAllCoroutines();
        
        if (questManager != null)
        {
            questManager.TriggerEvent("FLASHLIGHT_USED_ON_ENEMY");
            questManager.ActivateQuest("FIREFLY_FUSION");
        }
        
        onTutorialCompleted?.Invoke();
        
        if (showDebugMessages)
        {
            Debug.Log("LockOnTutorialTrigger: Tutorial force-completed");
        }
    }
}