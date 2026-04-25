using UnityEngine;
// using UnityEngine.AI; // <--- ELIMINADO: Ya no se usa NavMeshAgent
using System.Collections;

// Asume que DialogueManager ahora tiene IsDialogueActive() y HideDialogue()

public class GoddessController : MonoBehaviour // Nombre de clase debe coincidir con el archivo: GoddessController.cs
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el Transform del jugador.")]
    public Transform player;
    [Tooltip("Arrastra aquí el Dialogue Manager.")]
    public DialogueManager dialogueManager;
    [Tooltip("Arrastra aquí el componente Animator.")]
    public Animator animator;

    [Header("Comportamiento")] // Se simplifica, ya no hay movimiento NavMesh
    [Tooltip("Distancia a la que la Diosa interactuará/rotará hacia el jugador.")]
    public float interactionDistance = 3.0f; // Renombrado de stopDistance para claridad
    [Tooltip("Distancia máxima a la que el jugador puede estar para que la Diosa continúe/inicie el diálogo.")]
    public float stopTalkingDistance = 10.0f;
    // [Tooltip("Distancia 'virtual' hacia adelante para establecer el destino. Debe ser suficientemente grande.")]
    // public float forwardDestinationDistance = 50f; // <--- ELIMINADO: No relevante sin NavMesh

    [Header("Rotación")]
    [Tooltip("¿Debe mirar al jugador cuando esté cerca de él?")]
    public bool facePlayerWhenClose = true; // Renombrado de facePlayerWhenStopped
    [Tooltip("Velocidad de rotación suave (Slerp) hacia el jugador cuando está cerca y casi alineada.")]
    public float rotationSpeed = 5.0f;
    [Tooltip("Ángulo (grados) a partir del cual se usarán las animaciones de giro en lugar de Slerp.")]
    public float turnAnimationAngleThreshold = 30.0f;

    // Referencias y Estado Privado
    // private NavMeshAgent agent; // <--- ELIMINADO
    private bool isPlayerClose = false; // Flag: ¿Está el jugador lo suficientemente cerca para interactuar/rotar? Renombrado de isStoppedByPlayer

    // Hashes de parámetros (más eficiente que usar strings)
    private readonly int hashWalk = Animator.StringToHash("Walk");
    private readonly int hashTalking = Animator.StringToHash("Talking");
    private readonly int hashLeftTurn = Animator.StringToHash("LeftTurn");
    private readonly int hashRightTurn = Animator.StringToHash("RightTurn");

    void Start()
    {
        // agent = GetComponent<NavMeshAgent>(); // <--- ELIMINADO
        if (animator == null) animator = GetComponent<Animator>();

        // --- Validaciones Iniciales ---
        bool initializationError = false;
        // if (agent == null) { Debug.LogError("¡Error Crítico! NavMeshAgent no encontrado en " + gameObject.name, this); initializationError = true; } // <--- ELIMINADO
        if (animator == null) { Debug.LogError("¡Error Crítico! Animator no encontrado en " + gameObject.name, this); initializationError = true; }
        if (player == null) {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) { player = playerObj.transform; Debug.Log("Jugador encontrado por Tag.", this); }
            else { Debug.LogError("¡Error Crítico! No se asignó ni encontró al jugador (Tag 'Player') en " + gameObject.name, this); initializationError = true; }
        }
        if (dialogueManager == null) {
             dialogueManager = FindAnyObjectByType<DialogueManager>(); // Usar FindAnyObjectByType en versiones más nuevas
             if (dialogueManager == null) Debug.LogWarning("DialogueManager no asignado ni encontrado en la escena.", this);
             else Debug.Log("DialogueManager encontrado en la escena.", this);
        }
        if (initializationError) { Debug.LogError("Debido a errores críticos, el script GoddessController se desactivará.", this); enabled = false; return; }
        // --- Fin Validaciones ---

        // --- Configuración eliminada ---
        // agent.stoppingDistance = 0.1f;
        // agent.autoBraking = false;
        // agent.updateRotation = true; // La rotación ahora se manejará manualmente si es necesario

        isPlayerClose = false; // Empezar asumiendo que el jugador no está cerca
        // SetForwardDestination(); // <--- ELIMINADO
        UpdateMovementAnimation(); // Asegurar que la animación de caminar esté desactivada al inicio
    }

    void Update()
    {
        // Salir si falta alguna referencia esencial
        if (player == null || animator == null) return; // <--- Eliminada la comprobación de 'agent'

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // --- Actualizar Estado de Proximidad ---
        isPlayerClose = (distanceToPlayer <= interactionDistance);

        // --- Ejecutar Lógica Principal ---
        // UpdateMovementState(distanceToPlayer); // <--- ELIMINADO: Ya no hay gestión de movimiento NavMesh
        HandleDialogueInterruption(distanceToPlayer); // Interrumpe diálogo si jugador lejos
        // UpdateMovementAnimation(); // <--- ELIMINADO de Update: La animación 'Walk' siempre será false, se establece en Start y si se añadiera movimiento nuevo.
        HandleRotationWithTriggers(); // Maneja la rotación (con animaciones de giro o Slerp) cuando el jugador está cerca
    }

    // void UpdateMovementState(float distanceToPlayer) // <--- ELIMINADO: Función entera dependía de NavMeshAgent
    // { ... }

    // void SetForwardDestination() // <--- ELIMINADO: Función entera dependía de NavMeshAgent y NavMesh
    // { ... }

    // Verifica si el diálogo debe interrumpirse por la distancia del jugador
    void HandleDialogueInterruption(float distanceToPlayer)
    {
        // Usar el método IsDialogueActive() del DialogueManager
        if (dialogueManager != null && dialogueManager.IsDialogueActive() && distanceToPlayer > stopTalkingDistance)
        {
            Debug.Log($"Jugador demasiado lejos ({distanceToPlayer:F1}m > {stopTalkingDistance}m). Interrumpiendo diálogo.");
            dialogueManager.HideDialogue(); // Usar HideDialogue para cerrar la UI
            StopTalkingAnimation(); // Detener la animación de hablar también
        }
    }

    // Actualiza la animación de caminar. Como ya no hay movimiento, siempre es false.
    void UpdateMovementAnimation()
    {
         if (animator == null) return;
         // Sin NavMeshAgent, asumimos que no se mueve (a menos que añadas otra lógica)
         animator.SetBool(hashWalk, false);
    }

    // Maneja la rotación cuando está cerca del jugador, usando animaciones de giro o Slerp
    void HandleRotationWithTriggers()
    {
        // Solo rotar si la opción está activa Y si el jugador está cerca
        if (facePlayerWhenClose && isPlayerClose && player != null)
        {
            Vector3 directionToPlayer = player.position - transform.position;
            directionToPlayer.y = 0; // Ignorar diferencia de altura

            // Solo rotar si hay una dirección válida
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                // Calcular el ángulo relativo al frente de la Diosa
                float angle = Vector3.SignedAngle(transform.forward, directionToPlayer.normalized, Vector3.up);

                // Si el ángulo supera el umbral, usar animación de giro
                if (angle > turnAnimationAngleThreshold)
                {
                    animator.SetTrigger(hashRightTurn); // Activar trigger de giro a la derecha
                }
                else if (angle < -turnAnimationAngleThreshold)
                {
                    animator.SetTrigger(hashLeftTurn); // Activar trigger de giro a la izquierda
                }
                // Si el ángulo es pequeño, usar Slerp para un ajuste suave
                else
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }
            }
        }
        // Nota: Ya no hay rotación automática al "moverse" porque no hay NavMeshAgent.
        // Si añades otro sistema de movimiento, tendrás que gestionar la rotación durante el movimiento tú mismo.
    }

    // void StopAgentMovement() // <--- ELIMINADO
    // { ... }

    // void ResumeAgentMovement() // <--- ELIMINADO
    // { ... }

    // Método público para ser llamado externamente e iniciar el diálogo
    public void TriggerDialogue()
    {
        // Validaciones básicas
        if (player == null || dialogueManager == null || animator == null) {
             Debug.LogWarning("Intento de TriggerDialogue fallido: Faltan referencias.");
             return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // No iniciar si el jugador ya está demasiado lejos
        if(distanceToPlayer > stopTalkingDistance) {
             Debug.Log($"No se inicia diálogo. Jugador demasiado lejos ({distanceToPlayer:F1}m > {stopTalkingDistance}m).");
             return;
        }

        // Si estamos cerca del jugador, intentar mirarlo antes de empezar a hablar
        if (isPlayerClose && facePlayerWhenClose) {
             HandleRotationWithTriggers(); // Usar el método que incluye animaciones/slerp
        }

        // Iniciar el diálogo y la animación correspondiente
        Debug.Log("Iniciando diálogo.");
        dialogueManager.ShowNextDialogue(); // Mostrar UI del diálogo
        animator.SetBool(hashTalking, true);  // Activar animación de hablar

        // Programar la *detención de la animación* (no del diálogo en sí)
        CancelInvoke(nameof(StopTalkingAnimation)); // Cancelar llamada previa si existiera
        // Considera ajustar la duración o basarla en el tiempo real del diálogo si es posible
        Invoke(nameof(StopTalkingAnimation), 2.5f);   // Duración de la animación Talking (ajustable)
    }

    // Detiene SOLAMENTE la animación de hablar
    void StopTalkingAnimation()
    {
        if (animator != null) {
            animator.SetBool(hashTalking, false);
        }
    }

    // Limpieza al desactivar el objeto
    void OnDisable() {
        // if(agent != null && agent.isOnNavMesh) { // <--- ELIMINADO
        //     agent.isStopped = true;
        // }
        CancelInvoke(); // Cancelar todas las llamadas Invoke pendientes
    }
}