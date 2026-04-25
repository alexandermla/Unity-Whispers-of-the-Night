// Archivo: Scripts/Enemy/NewEra/EnemyIA_Screamer.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class EnemyIA_Screamer : EnemyBase // Hereda de EnemyBase
{
    // Los parámetros de configuración ahora están en profile.screamerSettings o profile base

    [Header("Screamer Effects (Visuals)")]
    [Tooltip("Sistema de partículas para el grito. Asignar en el prefab.")]
    [SerializeField] private ParticleSystem screamVFX;
    [Tooltip("Retraso en segundos desde que empieza la animación hasta que suena el grito/efecto.")]
    [SerializeField] private float screamAnimationDelay = 0.5f; // Puede quedarse aquí o ir al perfil

    // Estado interno
    private float screamTimer = 0f;
    private bool isScreaming = false;

    // Hash de animación específico
    private readonly int screamTriggerAnimHash = Animator.StringToHash("Scream"); // Asume animación "Scream"

    protected override void Awake()
    {
        // --- Llamar a base.Awake() PRIMERO ---
        base.Awake();
        if (!enabled) return;
        // -------------------------------------

        if (screamVFX == null) Debug.LogWarning($"Scream VFX no asignado en {gameObject.name}.");

        // Inicializar timer con cooldown del perfil específico
        screamTimer = profile.screamerSettings.screamCooldown;
    }

    protected override void Start()
    {
        // --- Llamar a base.Start() PRIMERO ---
        base.Start();
        if (!enabled) return;
        // -------------------------------------
        ChangeState(EnemyState.Wandering); // Empezar patrullando
    }

    protected override void UpdateAIState()
    {
        if (profile == null || currentState == EnemyState.Neutralized) return;
        if (isScreaming) return; // No hacer nada mientras grita

        // Actualizar cooldown usando valor del perfil
        if (screamTimer < profile.screamerSettings.screamCooldown)
        {
            screamTimer += Time.deltaTime;
        }

        switch(currentState) {
            case EnemyState.Wandering:
            case EnemyState.Idle:
                ExecuteWanderingScreamer();
                CheckForPlayerDetectionScreamer();
                break;
            case EnemyState.Alerting: break; // Controlado por corutina
            default: ChangeState(EnemyState.Wandering); break;
        }
    }

    private void ExecuteWanderingScreamer() {
        if (agent == null || !agent.enabled || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;
        // Usa wanderSpeed del perfil base
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath)) {
             // Usa radio del perfil base
            Vector3 randomPos = transform.position + Random.insideUnitSphere * (profile.maxDistanceFromCenter * 0.7f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, profile.maxDistanceFromCenter, NavMesh.AllAreas)) {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void CheckForPlayerDetectionScreamer() {
         // Usa valores de profile.screamerSettings para detección
         if (player == null || profile == null || isScreaming || screamTimer < profile.screamerSettings.screamCooldown) return;

         float distanceToPlayer = Vector3.Distance(transform.position, player.position);
         if (distanceToPlayer > profile.screamerSettings.detectionRadius) return;

         Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;
         if (Vector3.Angle(transform.forward, directionToPlayerNorm) > profile.screamerSettings.fieldOfView * 0.5f) return;

         Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
         if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) return; // Usa obstacleLayer base

         // Detectado -> Gritar
         StartCoroutine(ScreamSequence());
    }

    private IEnumerator ScreamSequence()
    {
         if (profile == null) yield break;

         isScreaming = true;
         ChangeState(EnemyState.Alerting);
         if (agent != null && agent.enabled) agent.isStopped = true;
         if(player != null) base.RotateTowards(player.position); // Usa método base

         if (animator != null) animator.SetTrigger(screamTriggerAnimHash);
         yield return new WaitForSeconds(screamAnimationDelay); // Usa delay local

         // Usa screamSound del perfil específico
         if (mainAudioSource != null && profile.screamerSettings.screamSound != null)
             mainAudioSource.PlayOneShot(profile.screamerSettings.screamSound);

         if (screamVFX != null) screamVFX.Play(); // Usa VFX local

         AlertNearbyEnemies(); // Llama a la lógica de alerta

         screamTimer = 0f; // Reinicia cooldown
         yield return new WaitForSeconds(1.5f); // Tiempo post-grito

         isScreaming = false;
         ChangeState(EnemyState.Wandering); // Vuelve a patrullar
         if (agent != null && agent.enabled) agent.isStopped = false;
    }

    private void AlertNearbyEnemies() {
        if (player == null || profile == null) return;

        LayerMask enemyMask = LayerMask.GetMask("Enemy"); // Asegúrate que tus otros enemigos estén en esta capa
        float alertRadius = profile.screamerSettings.alertRadius; // Radio desde el perfil
        //Debug.Log($"Screamer buscando enemigos en radio {alertRadius}m"); // Log opcional

        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, alertRadius, enemyMask);

        //Debug.Log($"Encontrados {nearbyColliders.Length} colliders en capa Enemy"); // Log opcional

        foreach (Collider col in nearbyColliders) {
            EnemyBase otherEnemy = col.GetComponentInParent<EnemyBase>(); // Busca también en padres

            // Validar enemigo encontrado
            if (otherEnemy != null && otherEnemy != this && otherEnemy.CurrentState != EnemyState.Neutralized)
            {
                // --- ¡ACCIÓN DE ALERTAR! ---
                // Llama al método ReceiveAlert del otro enemigo, pasándole la posición del jugador
                //Debug.Log($"Screamer ({gameObject.name}) alertando a: {otherEnemy.gameObject.name}");
                otherEnemy.ReceiveAlert(transform.position, player.position); // <--- LÍNEA CLAVE
                // --------------------------
            }
            // else // Log opcional para saber por qué se ignoró un collider
            // {
            //     if (otherEnemy == null) Debug.Log($"Collider {col.name} no tiene EnemyBase.");
            //     else if (otherEnemy == this) Debug.Log($"Collider {col.name} es el propio Screamer.");
            //     else if (otherEnemy.CurrentState == EnemyState.Neutralized) Debug.Log($"Collider {col.name} está Neutralized.");
            // }
        }
    }

    protected override void OnEnterState(EnemyState newState) {
         base.OnEnterState(newState);
         if (agent == null || !agent.enabled || profile == null) return;

         switch (newState) {
             case EnemyState.Wandering:
             case EnemyState.Idle:
                 agent.speed = profile.wanderSpeed;
                 agent.isStopped = false;
                 break;
             case EnemyState.Alerting:
                 agent.isStopped = true;
                 break;
         }
    }
}