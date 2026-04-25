// Archivo: Scripts/Enemy/NewEra/EnemyIA_Thrower.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyIA_Thrower : EnemyBase // Hereda de EnemyBase
{
    // Los parámetros de configuración ahora están en profile.throwerSettings o profile base

    [Header("Thrower Specifics")]
    [Tooltip("Punto desde donde se instancia el proyectil. Asignar en el prefab.")]
    [SerializeField] private Transform projectileSpawnPoint;

    // Estado interno
    private float specificAttackTimer = 0f;
    private bool isPreparingAttack = false;
    //private Vector3 lastKnownPlayerPosition;

    // Hash de animación específico
    private readonly int throwTriggerAnimHash = Animator.StringToHash("Throw"); // Asume animación "Throw"

    protected override void Awake()
    {
        // --- Llamar a base.Awake() PRIMERO ---
        base.Awake();
        if (!enabled) return;
        // -------------------------------------

        // Validar prefab de proyectil desde el perfil específico
        if (profile.throwerSettings.projectilePrefab == null)
            Debug.LogError($"Projectile Prefab no asignado en el Perfil '{profile.name}' para {gameObject.name}!", this);

        if (projectileSpawnPoint == null) {
            projectileSpawnPoint = transform;
            Debug.LogWarning($"Projectile Spawn Point no asignado en {gameObject.name}. Usando el transform del enemigo.", this);
        }

        // Configuración inicial del agente NavMesh
        if(agent != null) {
            // Usar preferredMinDistance del perfil específico para stopping distance inicial
            agent.stoppingDistance = profile.throwerSettings.preferredMinDistance * 0.9f;
            agent.autoBraking = true;
            // La velocidad inicial (wanderSpeed) ya la establece base.Awake
        }
    }

     protected override void Start() {
        // --- Llamar a base.Start() PRIMERO ---
        base.Start();
        if (!enabled) return;
        // -------------------------------------

        // Inicializar timer de ataque con cooldown de rango del perfil específico
        specificAttackTimer = profile.throwerSettings.rangedAttackCooldown;
        ChangeState(EnemyState.Wandering);
    }

    protected override void UpdateAIState()
    {
        if (profile == null || currentState == EnemyState.Neutralized) return;
        if (isPreparingAttack) return; // No hacer nada mientras prepara ataque

        // Actualizar cooldown de ataque de rango usando valor del perfil
        if (specificAttackTimer < profile.throwerSettings.rangedAttackCooldown)
             specificAttackTimer += Time.deltaTime;

        switch(currentState) {
            case EnemyState.Wandering:
            case EnemyState.Idle:
                ExecuteWanderingThrower();
                CheckForPlayerDetectionThrower();
                break;
            case EnemyState.Chasing: // Mantener distancia
                ExecuteMaintainDistance();
                CheckForAttackConditions();
                CheckPlayerVisibilityThrower();
                break;
            case EnemyState.Fleeing: // Jugador demasiado cerca
                ExecuteFleeing();
                CheckForPlayerDetectionThrower();
                break;
            case EnemyState.Attacking: /* Controlado por Corutina */ break;
            case EnemyState.Searching:
                ExecuteSearchingThrower();
                CheckForPlayerDetectionThrower();
                break;
            default: ChangeState(EnemyState.Wandering); break;
        }
    }

    private void ExecuteWanderingThrower() {
        if (agent == null || !agent.enabled || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;
        // Usa wanderSpeed del perfil base
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        if (!agent.pathPending && agent.remainingDistance < 0.5f) {
            // Usa radio del perfil base
            Vector3 randomPos = transform.position + Random.insideUnitSphere * (profile.maxDistanceFromCenter * 0.6f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, profile.maxDistanceFromCenter, NavMesh.AllAreas)) {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void CheckForPlayerDetectionThrower() {
        if (player == null || profile == null || currentState == EnemyState.Neutralized || isPreparingAttack) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float currentAgroRange = GetCurrentAgroRangeThrower(); // Usa rangos base del perfil

        if (distanceToPlayer > currentAgroRange) return;

        Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, directionToPlayerNorm) > profile.fieldOfView * 0.5f) return; // Usa FoV base

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) return; // Usa obstacleLayer base

        lastKnownPlayerPosition = player.position;

        if (currentState == EnemyState.Wandering || currentState == EnemyState.Idle || currentState == EnemyState.Searching) {
            // Usa fleeDistanceThreshold del perfil específico
            if (distanceToPlayer < profile.throwerSettings.fleeDistanceThreshold) {
                ChangeState(EnemyState.Fleeing);
            } else {
                ChangeState(EnemyState.Chasing); // Mantener distancia
            }
        }
         else if (currentState == EnemyState.Fleeing && distanceToPlayer >= profile.throwerSettings.fleeDistanceThreshold)
        {
            ChangeState(EnemyState.Chasing); // Dejar de huir
        }
     }

    private void ExecuteMaintainDistance() {
        if (agent == null || !agent.enabled || player == null || profile == null) {
            ChangeState(EnemyState.Wandering); return;
        }
        if (agent.isStopped) agent.isStopped = false;

        // Usa wanderSpeed base para maniobrar
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        // Usa preferredMinDistance del perfil específico para stopping distance
        float targetStoppingDist = profile.throwerSettings.preferredMinDistance * 0.9f;
        if (agent.stoppingDistance != targetStoppingDist) agent.stoppingDistance = targetStoppingDist;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Usa preferredMin/Max Distance del perfil específico
        if (distanceToPlayer < profile.throwerSettings.preferredMinDistance) {
            Vector3 dirAwayFromPlayer = (transform.position - player.position).normalized;
            Vector3 targetPos = transform.position + dirAwayFromPlayer * 5f;
            NavMeshHit hit;
            if(NavMesh.SamplePosition(targetPos, out hit, 6f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            base.RotateTowards(player.position); // Usa rotación base
        }
        else if (distanceToPlayer > profile.throwerSettings.preferredMaxDistance) {
            agent.SetDestination(player.position);
            base.RotateTowards(player.position);
        }
        else { // Dentro del rango
            if (!agent.isStopped) agent.isStopped = true;
            agent.velocity = Vector3.zero;
            base.RotateTowards(player.position);
        }
    }

    private void ExecuteFleeing() {
        if (agent == null || !agent.enabled || player == null || profile == null) {
            ChangeState(EnemyState.Wandering); return;
        }
        if (agent.isStopped) agent.isStopped = false;

        // Usa wanderSpeed base y fleeSpeedMultiplier del perfil específico
        float currentFleeSpeed = profile.wanderSpeed * profile.throwerSettings.fleeSpeedMultiplier;
        if (agent.speed != currentFleeSpeed) agent.speed = currentFleeSpeed;
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        // Usa fleeDistanceThreshold del perfil específico
        if(distanceToPlayer > profile.throwerSettings.fleeDistanceThreshold * 1.2f) {
            ChangeState(EnemyState.Chasing); // Volver a mantener distancia
            return;
        }

        Vector3 dirAwayFromPlayer = (transform.position - player.position).normalized;
        Vector3 fleeTarget = transform.position + dirAwayFromPlayer * 10f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleeTarget, out hit, 12f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    private void ExecuteSearchingThrower() {
        if (agent == null || !agent.enabled || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;
        // Usa wanderSpeed base
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        if (agent.stoppingDistance != 0.5f) agent.stoppingDistance = 0.5f;

        if (!float.IsNaN(lastKnownPlayerPosition.x) && (!agent.hasPath || Vector3.Distance(agent.destination, lastKnownPlayerPosition) > 0.5f)) {
            agent.SetDestination(lastKnownPlayerPosition);
        }
        if (!float.IsNaN(lastKnownPlayerPosition.x)) base.RotateTowards(lastKnownPlayerPosition);

        // Vuelve a wander al llegar (podría usar searchDuration base si se implementa timer)
        if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance) {
            ChangeState(EnemyState.Wandering);
        }
    }

    private void CheckForAttackConditions() {
        // Usa valores de profile.throwerSettings
        if (player == null || profile == null || specificAttackTimer < profile.throwerSettings.rangedAttackCooldown || isPreparingAttack) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer >= profile.throwerSettings.preferredMinDistance && distanceToPlayer <= profile.throwerSettings.preferredMaxDistance)
        {
            Vector3 directionToPlayer = player.position - transform.position;
            directionToPlayer.y = 0;
            float angleToPlayer = (directionToPlayer.sqrMagnitude > 0.01f) ? Vector3.Angle(transform.forward, directionToPlayer.normalized) : 0f;

            if (angleToPlayer <= profile.throwerSettings.rangedAttackAngleThreshold)
            {
                Vector3 rayOrigin = projectileSpawnPoint.position;
                Vector3 targetPoint = player.position + Vector3.up * 0.8f;
                Vector3 directionToTarget = (targetPoint - rayOrigin).normalized;

                if (!Physics.Raycast(rayOrigin, directionToTarget, distanceToPlayer, obstacleLayer)) { // Usa obstacleLayer base
                    StartCoroutine(RangedAttackSequence());
                }
            }
        }
    }

    private void CheckPlayerVisibilityThrower() {
        // Usa rangos y loseMultiplier del perfil base
        if (player == null || profile == null) { if(currentState != EnemyState.Searching && currentState != EnemyState.Wandering) ChangeState(EnemyState.Searching); return; }
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float loseAgroDist = GetCurrentAgroRangeThrower() * profile.loseAgroDistanceMultiplier;
        if (distanceToPlayer > loseAgroDist) { ChangeState(EnemyState.Searching); return; }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 directionToPlayerNorm = (player.position + Vector3.up * 0.5f - rayOrigin).normalized;
        if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) { // Usa obstacleLayer base
            if (currentState != EnemyState.Searching) ChangeState(EnemyState.Searching);
        }
        else {
            lastKnownPlayerPosition = player.position;
            if (currentState == EnemyState.Searching) {
                // Usa fleeDistanceThreshold del perfil específico
                 if (distanceToPlayer < profile.throwerSettings.fleeDistanceThreshold) ChangeState(EnemyState.Fleeing);
                 else ChangeState(EnemyState.Chasing);
            }
        }
    }

     private float GetCurrentAgroRangeThrower() {
         // Usa rangos del perfil base
         if (player == null || profile == null) return profile?.standingAgroRange ?? 8f;
         LampSystem lamp = player.GetComponentInChildren<LampSystem>();
         PlayerController pc = player.GetComponent<PlayerController>();
         if ((lamp != null && lamp.IsLampOn) || (pc != null && pc.IsRunning())) return profile.lightAgroRange;
         if (pc != null && pc.IsCrouching()) return profile.crouchAgroRange;
         return profile.standingAgroRange;
     }

    private IEnumerator RangedAttackSequence()
    {
        if (profile == null) yield break;

        isPreparingAttack = true;
        ChangeState(EnemyState.Attacking);
        if (agent != null && agent.enabled) agent.isStopped = true;
        Vector3 targetLookPos = player != null ? player.position : lastKnownPlayerPosition;
        if (!float.IsNaN(targetLookPos.x)) base.RotateTowards(targetLookPos);

        if (animator != null) animator.SetTrigger(throwTriggerAnimHash);
        // Usa rangedAttackWindUpTime del perfil específico
        yield return new WaitForSeconds(profile.throwerSettings.rangedAttackWindUpTime);

        if (profile.throwerSettings.projectilePrefab != null && projectileSpawnPoint != null && player != null && currentState != EnemyState.Neutralized) {
             Vector3 spawnPos = projectileSpawnPoint.position;
             Vector3 playerVel = Vector3.zero;
             CharacterController playerCC = player.GetComponent<CharacterController>();
             if(playerCC != null) playerVel = playerCC.velocity;
             // Usa projectileSpeed del perfil específico
             float timeToTarget = Vector3.Distance(spawnPos, player.position) / profile.throwerSettings.projectileSpeed;
             Vector3 predictedTargetPos = (player.position + Vector3.up * 0.8f) + (playerVel * timeToTarget * 0.7f);
             Vector3 direction = (predictedTargetPos - spawnPos).normalized;

             GameObject projectileGO = Instantiate(profile.throwerSettings.projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
             Projectile projectileScript = projectileGO.GetComponent<Projectile>();

             if (projectileScript != null)
             {
                 // Usa projectileSpeed y attackDamage del perfil
                 projectileScript.Initialize(direction, profile.throwerSettings.projectileSpeed, profile.attackDamage, this.gameObject);
             }
             else {
                 Rigidbody rb = projectileGO.GetComponent<Rigidbody>();
                 if (rb != null) { rb.linearVelocity = direction * profile.throwerSettings.projectileSpeed; }
                 Destroy(projectileGO, 5f);
             }

             // Usa throwSound del perfil específico
             if(mainAudioSource != null && profile.throwerSettings.throwSound != null)
                 mainAudioSource.PlayOneShot(profile.throwerSettings.throwSound);
        }

        specificAttackTimer = 0f;
        isPreparingAttack = false;
        CheckPlayerVisibilityThrower();
        if (currentState == EnemyState.Attacking) ChangeState(EnemyState.Chasing);
        if (agent != null && agent.enabled && currentState != EnemyState.Attacking) agent.isStopped = false;
    }

    protected override void OnEnterState(EnemyState newState) {
         base.OnEnterState(newState);
         if (agent == null || !agent.enabled || profile == null) return;

         switch (newState) {
             case EnemyState.Wandering:
             case EnemyState.Idle:
                 agent.speed = profile.wanderSpeed;
                 agent.stoppingDistance = 0.1f;
                 agent.isStopped = false;
                 break;
             case EnemyState.Chasing: // Mantener distancia
                 agent.speed = profile.wanderSpeed;
                 // Usa preferredMinDistance del perfil específico
                 agent.stoppingDistance = profile.throwerSettings.preferredMinDistance * 0.9f;
                 agent.isStopped = false;
                 break;
             case EnemyState.Fleeing:
                 // Usa wanderSpeed base y fleeSpeedMultiplier específico
                 agent.speed = profile.wanderSpeed * profile.throwerSettings.fleeSpeedMultiplier;
                 agent.stoppingDistance = 0.1f;
                 agent.isStopped = false;
                 break;
             case EnemyState.Searching:
                 agent.speed = profile.wanderSpeed;
                 agent.stoppingDistance = 0.5f;
                 if (!float.IsNaN(lastKnownPlayerPosition.x)) agent.SetDestination(lastKnownPlayerPosition);
                 agent.isStopped = false;
                 break;
             case EnemyState.Attacking: break; // Base detiene agente
         }
    }
}