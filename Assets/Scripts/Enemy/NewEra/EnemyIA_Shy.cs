// Archivo: Scripts/Enemy/NewEra/EnemyIA_Shy.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyIA_Shy : EnemyBase // Hereda de EnemyBase
{
    // Hashes de animación (asegúrate que estos nombres coincidan con tu Animator)
    private readonly int detectPlayerTriggerAnimHash = Animator.StringToHash("DetectPlayer");
    private readonly int isFleeingBoolAnimHash = Animator.StringToHash("IsFleeing");

    // --- NUEVO: Campo para el sonido de alerta ---
    [Header("Shy Specific Sounds")]
    [Tooltip("Sonido que se reproduce cuando el enemigo se asusta/detecta al jugador.")]
    [SerializeField] private AudioClip alertSound;
    // -----------------------------------------

    // Estado interno
    private float calmDownTimer = 0f;
    private bool isPerformingSurprise = false; // Flag para controlar la secuencia de sorpresa
    private Coroutine surpriseAndFleeCoroutine = null; // Referencia a la corutina
    private bool surpriseAnimationHasFinished = false; // Flag para ser activada por el Animation Event

    protected override void Awake()
    {
        base.Awake(); // Llama al Awake de EnemyBase (importante para inicializar profile, agent, animator, audioSources, etc.)
        if (!enabled) return; // Salir si EnemyBase falló (ej. sin perfil)

        // Validación opcional del sonido de alerta
        if (alertSound == null)
        {
            Debug.LogWarning($"EnemyIA_Shy ({gameObject.name}): No se ha asignado un 'Alert Sound' en el Inspector.", this);
        }
    }

    protected override void Start()
    {
        base.Start(); // Llama al Start de EnemyBase
        if (!enabled) return;
        ChangeState(EnemyState.Wandering); // Estado inicial
    }

    protected override void UpdateAIState()
    {
        // Si está en la secuencia de sorpresa, o neutralizado, o sin perfil, no ejecutar la lógica de IA normal.
        if (isPerformingSurprise || profile == null || currentState == EnemyState.Neutralized) return;

        switch(currentState)
        {
            case EnemyState.Wandering:
            case EnemyState.Idle:
                ExecuteWanderingShy();
                CheckForPlayerOrLight();
                break;

            case EnemyState.Fleeing:
                ExecuteFleeing();
                break;

            default:
                 ChangeState(EnemyState.Wandering);
                 break;
        }
    }

    private void ExecuteWanderingShy()
    {
        if (isPerformingSurprise || agent == null || !agent.enabled || profile == null) return;

        if (agent.isStopped) agent.isStopped = false;
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 randomPos = transform.position + Random.insideUnitSphere * (profile.maxDistanceFromCenter * 0.5f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, profile.maxDistanceFromCenter, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void CheckForPlayerOrLight()
    {
        if (player == null || profile == null || isPerformingSurprise || currentState == EnemyState.Fleeing || currentState == EnemyState.Neutralized) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool playerDetectedBySight = false;

        if (distanceToPlayer < profile.shySettings.detectionRadius)
        {
             Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;
             Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
             if (!Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer))
             {
                 playerDetectedBySight = true;
             }
        }

        bool lightDetected = lightExposureTime > 0.1f;

        if (playerDetectedBySight || lightDetected)
        {
            if (surpriseAndFleeCoroutine == null)
            {
                surpriseAndFleeCoroutine = StartCoroutine(SurpriseAndFleeSequence());
            }
        }
    }

    private IEnumerator SurpriseAndFleeSequence()
    {
        isPerformingSurprise = true;
        surpriseAnimationHasFinished = false;

        // --- NUEVO: Reproducir sonido de alerta ---
        if (alertSound != null && mainAudioSource != null) // Usa mainAudioSource heredado de EnemyBase
        {
            mainAudioSource.PlayOneShot(alertSound);
            //Debug.Log($"{gameObject.name}: Playing Alert Sound!"); // Log para confirmar
        }
        // -----------------------------------------

        // 1. Detener el NavMeshAgent
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // 2. Mirar hacia el jugador (giro rápido/directo)
        if (player != null)
        {
            Vector3 directionToPlayer = player.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                // Intenta una rotación muy rápida. Ajusta el último parámetro (factor de Slerp) si es necesario.
                // Un valor de 1.0f intenta completar el giro en un frame.
                // Si aún no es suficiente, considera 'transform.rotation = targetRotation;'
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1.0f);
                // Pequeña espera opcional para asegurar que la rotación se aplique visualmente antes de la animación
                // yield return new WaitForSeconds(0.05f);
            }
        }

        // 3. Activar la animación de sorpresa en el Animator
        if (animator != null)
        {
            animator.SetTrigger(detectPlayerTriggerAnimHash);
        }
        else
        {
            surpriseAnimationHasFinished = true;
        }

        // 4. Esperar a que el Animation Event llame a OnSurpriseAnimationFinished()
        float waitUntilEventTimeout = Time.time + 2.0f;
        while (!surpriseAnimationHasFinished && Time.time < waitUntilEventTimeout)
        {
            yield return null;
        }

        if (!surpriseAnimationHasFinished)
        {
            //Debug.LogWarning($"{gameObject.name}: Animation Event 'OnSurpriseAnimationFinished' no se disparó o se agotó el tiempo. Procediendo a huir.");
        }

        // 5. Cambiar al estado de IA "Fleeing"
        ChangeState(EnemyState.Fleeing);

        calmDownTimer = profile.shySettings.timeToCalmDown;

        isPerformingSurprise = false;
        surpriseAndFleeCoroutine = null;
    }

    /// <summary>
    /// Método público llamado por un Animation Event al final de la animación "Shy_SurpriseJump".
    /// </summary>
    public void OnSurpriseAnimationFinished()
    {
        surpriseAnimationHasFinished = true;
        //Debug.Log($"{gameObject.name}: Evento OnSurpriseAnimationFinished recibido del Animator.");
    }

    private void ExecuteFleeing()
    {
        if (isPerformingSurprise || agent == null || !agent.enabled || player == null || profile == null || currentState == EnemyState.Neutralized)
        {
             if (agent != null && agent.isOnNavMesh && !isPerformingSurprise && currentState != EnemyState.Neutralized) ChangeState(EnemyState.Wandering);
             return;
        }

        if (agent.speed != profile.shySettings.fleeSpeed) agent.speed = profile.shySettings.fleeSpeed;

        Vector3 dirAwayFromPlayer = (transform.position - player.position).normalized;
        Vector3 fleeTarget = transform.position + dirAwayFromPlayer * profile.shySettings.fleeDistance;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleeTarget, out hit, profile.shySettings.fleeDistance * 0.5f, NavMesh.AllAreas))
        {
             agent.SetDestination(hit.position);
        } else {
             Vector3 randomPos = transform.position + Random.insideUnitSphere * 5f;
              if (NavMesh.SamplePosition(randomPos, out hit, 7f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        bool playerStillVisible = distanceToPlayer < profile.shySettings.detectionRadius &&
                             !Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer);
        bool isStillLit = lightExposureTime > 0.1f;

        if (!playerStillVisible && !isStillLit)
        {
            calmDownTimer -= Time.deltaTime;
            if (calmDownTimer <= 0)
            {
                 ChangeState(EnemyState.Wandering);
            }
        }
        else
        {
            calmDownTimer = profile.shySettings.timeToCalmDown;
        }
    }

     public override void OnLightExposure(Transform lightSource, float intensity)
     {
         base.OnLightExposure(lightSource, intensity);

         if (currentState != EnemyState.Fleeing && currentState != EnemyState.Neutralized && !isPerformingSurprise)
         {
             if (surpriseAndFleeCoroutine == null)
             {
                 surpriseAndFleeCoroutine = StartCoroutine(SurpriseAndFleeSequence());
             }
         }
     }

    protected override void OnEnterState(EnemyState newState)
    {
        base.OnEnterState(newState);
        if (agent == null || !agent.enabled || profile == null) return;

        if (animator != null)
        {
            animator.SetBool(isFleeingBoolAnimHash, newState == EnemyState.Fleeing);
        }

        switch (newState)
        {
            case EnemyState.Wandering:
            case EnemyState.Idle:
                agent.speed = profile.wanderSpeed;
                if (agent.isOnNavMesh) agent.isStopped = false;
                break;

            case EnemyState.Fleeing:
                agent.speed = profile.shySettings.fleeSpeed;
                if (agent.isOnNavMesh) agent.isStopped = false;
                break;
        }
    }
}