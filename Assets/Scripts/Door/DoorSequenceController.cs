using System.Collections;
using UnityEngine;

public class DoorSequenceController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ThirdPersonCamera cameraController;
    [SerializeField] private FireflyGuide fireflyGuide;
    [SerializeField] private TutorialQuestManager questManager;
    
    [Header("Enemigos (Padres)")]
    [SerializeField] private GameObject[] parents;
    [SerializeField] private Transform[] parentLookTargets;
    
    [Header("Look Targets")]
    [SerializeField] private Transform playerLookTarget; // Objeto hacia el que mirarán los padres
    
    [Header("Configuración de Secuencia")]
    [SerializeField] private Transform cutsceneCameraPosition;
    [SerializeField] private float initialPause = 0.5f;
    [SerializeField] private float turnDuration = 2.0f;
    [SerializeField] private float postTurnPause = 0.5f;
    [SerializeField] private float freezeDuration = 3.0f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip breathingSound;
    [SerializeField, Range(0f, 1f)] private float breathingVolumeScale = 1.0f; // Volumen para respiración
    [SerializeField] private AudioClip screamSound;
    [SerializeField, Range(0f, 1f)] private float screamVolumeScale = 1.0f; // Volumen para grito
    [SerializeField] private AudioClip preDoorOpenSound; // Sonido antes de abrir la puerta
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1.0f; // Volumen máximo
    [SerializeField] private float maxVolumeDistance = 5f; // Distancia a la que el volumen es máximo
    [SerializeField] private float minVolumeDistance = 20f; // Distancia a la que el volumen es mínimo
    
    public AudioSource parentAudioSource;
    private bool isPlayingPreDoorSound = false;
    
    private void Start()
    {
        // Obtener el AudioSource del primer padre
        if (parents != null && parents.Length > 0 && parents[0] != null)
        {
            parentAudioSource = parents[0].GetComponent<AudioSource>();
            if (parentAudioSource == null)
            {
                parentAudioSource = parents[0].AddComponent<AudioSource>();
            }
            
            // Configurar el AudioSource para sonido 3D
            parentAudioSource.spatialBlend = 1f;
            parentAudioSource.rolloffMode = AudioRolloffMode.Linear;
            parentAudioSource.minDistance = maxVolumeDistance;
            parentAudioSource.maxDistance = minVolumeDistance;
            parentAudioSource.dopplerLevel = 0f; // Desactivar efecto Doppler
            parentAudioSource.spread = 180f; // Sonido omnidireccional
            parentAudioSource.volume = 0f; // Empezar en silencio
        }
    }

    private void UpdateParentAudioVolume()
    {
        if (parentAudioSource != null && parentAudioSource.isPlaying && playerController != null)
        {
            float distance = Vector3.Distance(playerController.transform.position, parentAudioSource.transform.position);
            float volume = maxVolume;
            
            if (distance > maxVolumeDistance)
            {
                if (distance >= minVolumeDistance)
                {
                    volume = 0f;
                }
                else
                {
                    float t = (distance - maxVolumeDistance) / (minVolumeDistance - maxVolumeDistance);
                    volume = Mathf.Lerp(maxVolume, 0f, t);
                }
            }
            
            // Suavizar la transición del volumen
            parentAudioSource.volume = Mathf.Lerp(parentAudioSource.volume, volume, Time.deltaTime * 5f);
        }
    }

    private void Update()
    {
        UpdateParentAudioVolume();
    }
    
    public bool sequenceActivated = false;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    
    // Start() ya no controla el sonido ambiental
    
    public void ActivateSequence()
    {
        // ActivateSequence ya no detiene el sonido ambiental
        if (sequenceActivated) return;
        sequenceActivated = true;
        
        StartCoroutine(PlayDoorSequence());
    }
    
    private IEnumerator PlayDoorSequence()
    {
        // Iniciar sonido en bucle al principio de la secuencia
        if (parentAudioSource != null && preDoorOpenSound != null)
        {
            parentAudioSource.clip = preDoorOpenSound;
            parentAudioSource.loop = true;
            parentAudioSource.volume = maxVolume;
            parentAudioSource.Play();
        }

        // 1. Preparación
        // Desactivar control del jugador
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Detener la luciérnaga
        if (fireflyGuide != null)
        {
            fireflyGuide.StopDuringSequence();
        }
        
        // Hacer visibles a los padres
        foreach (GameObject parent in parents)
        {
            if (parent != null)
            {
                // Activar renderer
                Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    r.enabled = true;
                }
                
                // Asegurarse de que están en la posición correcta
                parent.SetActive(true);
            }
        }
        
        // 2. Movimiento de cámara
        if (cameraController != null)
        {
            // Guardar posición original
            originalCameraPosition = cameraController.transform.position;
            originalCameraRotation = cameraController.transform.rotation;
            
            // Desactivar temporalmente el script de cámara
            cameraController.enabled = false;
            
            // Mover la cámara a la posición de cutscene
            if (cutsceneCameraPosition != null)
            {
                // Movimiento suavizado hacia la posición de cutscene
                float elapsedTime = 0;
                float transitionDuration = 1.0f;
                Vector3 startPos = cameraController.transform.position;
                Quaternion startRot = cameraController.transform.rotation;
                
                while (elapsedTime < transitionDuration)
                {
                    float t = elapsedTime / transitionDuration;
                    cameraController.transform.position = Vector3.Lerp(startPos, cutsceneCameraPosition.position, t);
                    cameraController.transform.rotation = Quaternion.Slerp(startRot, cutsceneCameraPosition.rotation, t);
                    
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                
                // Asegurar posición final exacta
                cameraController.transform.position = cutsceneCameraPosition.position;
                cameraController.transform.rotation = cutsceneCameraPosition.rotation;
            }
        }
        
        // 3. Secuencia de los padres
        // Ya no reproducimos la respiración aquí, suena desde el Start() y se detiene al inicio de esta corrutina.
        
        // Pausa dramática inicial
        yield return new WaitForSeconds(initialPause);
        
        // Giro lento hacia el objetivo específico
        if (playerLookTarget != null)
        {
            foreach (GameObject parent in parents)
            {
                if (parent != null)
                {
                    EnemyIA2 enemyAI = parent.GetComponent<EnemyIA2>();
                    if (enemyAI != null)
                    {
                        // Usar el nuevo método para girar hacia el target específico
                        enemyAI.TurnToTarget(playerLookTarget, turnDuration);
                    }
                }
            }
            
            // Esperar a que complete el giro
            yield return new WaitForSeconds(turnDuration);
        }
        else
        {
            // Comportamiento original si no hay playerLookTarget
            float turnElapsedTime = 0;
            Quaternion[] startRotations = new Quaternion[parents.Length];
            Quaternion[] targetRotations = new Quaternion[parents.Length];
            
            for (int i = 0; i < parents.Length; i++)
            {
                if (parents[i] != null && parentLookTargets.Length > i && parentLookTargets[i] != null)
                {
                    // Activar la animación de giro
                    EnemyIA2 enemyAI = parents[i].GetComponent<EnemyIA2>();
                    if (enemyAI != null)
                    {
                        enemyAI.PerformTurn();
                    }
                    
                    startRotations[i] = parents[i].transform.rotation;
                    
                    // Calcular rotación hacia el target
                    Vector3 directionToTarget = (parentLookTargets[i].position - parents[i].transform.position).normalized;
                    targetRotations[i] = Quaternion.LookRotation(directionToTarget);
                }
            }
            
            while (turnElapsedTime < turnDuration)
            {
                float t = turnElapsedTime / turnDuration;
                
                // Aplicar rotación a cada padre
                for (int i = 0; i < parents.Length; i++)
                {
                    if (parents[i] != null && parentLookTargets.Length > i && parentLookTargets[i] != null)
                    {
                        parents[i].transform.rotation = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
                    }
                }
                
                turnElapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Asegurar rotación final exacta
            for (int i = 0; i < parents.Length; i++)
            {
                if (parents[i] != null && parentLookTargets.Length > i && parentLookTargets[i] != null)
                {
                    parents[i].transform.rotation = targetRotations[i];
                }
            }
        }
        
        // Pausa dramática después del giro
        yield return new WaitForSeconds(postTurnPause);

        // Reproducir el grito con un AudioSource separado
        if (screamSound != null)
        {
            // Bajar temporalmente el volumen del sonido principal
            float originalVolume = parentAudioSource.volume;
            parentAudioSource.volume = originalVolume * 0.3f;

            // Crear y configurar AudioSource temporal para el grito
            GameObject tempAudio = new GameObject("TempScreamAudio");
            tempAudio.transform.position = parentAudioSource.transform.position;
            AudioSource screamAudio = tempAudio.AddComponent<AudioSource>();
            screamAudio.clip = screamSound;
            screamAudio.spatialBlend = 1f;
            screamAudio.volume = screamVolumeScale;
            screamAudio.Play();

            // Esperar a que termine el grito
            yield return new WaitForSeconds(screamSound.length);

            // Restaurar el volumen original del sonido principal
            parentAudioSource.volume = originalVolume;

            // Destruir el objeto temporal
            Destroy(tempAudio, 0.1f);
        }
        
        // Activar animaciones de grito
        foreach (GameObject parent in parents)
        {
            if (parent != null)
            {
                EnemyIA2 enemyAI = parent.GetComponent<EnemyIA2>();
                if (enemyAI != null)
                {
                    // Activar la animación de rugido
                    enemyAI.PerformRoar();
                }
            }
        }
        
        // Mantener congelada la escena durante el grito
        yield return new WaitForSeconds(freezeDuration);
        
        // 4. Restaurar cámara
        if (cameraController != null)
        {
            // Movimiento suavizado de regreso
            float elapsedTime = 0;
            float transitionDuration = 1.0f;
            Vector3 startPos = cameraController.transform.position;
            Quaternion startRot = cameraController.transform.rotation;
            
            while (elapsedTime < transitionDuration)
            {
                float t = elapsedTime / transitionDuration;
                cameraController.transform.position = Vector3.Lerp(startPos, originalCameraPosition, t);
                cameraController.transform.rotation = Quaternion.Slerp(startRot, originalCameraRotation, t);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Asegurar posición final exacta
            cameraController.transform.position = originalCameraPosition;
            cameraController.transform.rotation = originalCameraRotation;
            
            // Reactivar el script de cámara
            cameraController.enabled = true;
        }
        
        // 5. Conclusión de la secuencia
        // Reactivar el control del jugador
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        // Activar misión de huida - SOLO AQUÍ debe activarse
        if (questManager != null)
        {
            Debug.Log("Door sequence activating RUN_AWAY quest");
            questManager.TriggerRunQuest();
        }
        
        // Activar IA de persecución de los padres
        foreach (GameObject parent in parents)
        {
            if (parent != null)
            {
                // Activar componente de IA de enemigo
                EnemyIA2 enemyAI = parent.GetComponent<EnemyIA2>();
                if (enemyAI != null)
                {
                    enemyAI.StartChasing(playerController.transform);
                }
            }
        }
        
        // Indicar a la luciérnaga que guíe hacia la salida
        if (fireflyGuide != null)
        {
            fireflyGuide.GoToExit(); // Este método ahora usará el sistema de waypoints
        }
        
        HandlePostSequence();
    }
    
    public void HandlePostSequence()
    {
        // Aquí puedes poner cualquier lógica adicional que deba ocurrir después de la secuencia
        Debug.Log("Door sequence completed, starting escape");
    }
}