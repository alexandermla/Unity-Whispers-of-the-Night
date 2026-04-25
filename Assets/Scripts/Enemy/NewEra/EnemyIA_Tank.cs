using UnityEngine;
using UnityEngine.AI;

public class EnemyIA_Tank : EnemyBase // Hereda de EnemyBase (que ahora tiene el perfil)
{
    [Header("Tank Combat Specifics")]
    [Tooltip("El Collider (Trigger) del Hitbox del ataque. Asignar prefab específico del Tank.")]
    [SerializeField] private Collider attackHitboxCollider;

    [Header("Tank Smart Navigation")]
    [Tooltip("Cada cuánto recalcula el destino durante la persecución. Valores bajos reaccionan mejor, pero cuestan más CPU.")]
    [SerializeField] private float chaseRepathInterval = 0.2f;

    [Tooltip("Tiempo mínimo casi sin moverse antes de considerar que el Tank está atascado.")]
    [SerializeField] private float stuckCheckTime = 0.75f;

    [Tooltip("Distancia mínima que debería avanzar durante la ventana de comprobación para no considerarse atascado.")]
    [SerializeField] private float stuckMinProgress = 0.25f;

    [Tooltip("Radio usado para buscar puntos alternativos alrededor del jugador o del Tank cuando se atasca.")]
    [SerializeField] private float unstuckSearchRadius = 3.5f;

    [Tooltip("Tiempo que el Tank puede seguir persiguiendo aunque un obstáculo tape la visión directa del jugador.")]
    [SerializeField] private float chaseMemoryTime = 2.0f;

    [Tooltip("Distancia por delante del jugador usada para anticipar su movimiento si tiene Rigidbody.")]
    [SerializeField] private float predictionLeadTime = 0.35f;

    [Tooltip("Distancia máxima entre el destino ideal y un punto válido del NavMesh.")]
    [SerializeField] private float navMeshSampleRadius = 2.0f;

    // Referencias y estado interno
    private EnemyHitbox enemyHitbox;
    private float specificAttackTimer = 0f;
    private float wanderTimer = 0f; // Timer para decidir cuándo buscar nuevo punto de wander
    private float waitTimer = 0f; // Timer para esperar en el punto de wander
    private float timeSinceLostSight = 0f;
    private bool isWaiting = false; // Flag para indicar si está esperando en un punto de wander
    //private Vector3 lastKnownPlayerPosition;
    private Vector3 actualMovementAreaCenter; // Centro del área de movimiento

    // Navegación inteligente del Tank
    private float chaseRepathTimer = 0f;
    private float stuckTimer = 0f;
    private float recoveryTimer = 0f;
    private Vector3 lastProgressPosition;
    private Vector3 currentSmartDestination;
    private Rigidbody playerRigidbody;

    // Hashes de Animación
    private readonly int attackTriggerAnimHash = Animator.StringToHash("Attack"); // Asume animación "Attack"

    protected override void Awake()
    {
        // --- IMPORTANTE: Llamar a base.Awake() PRIMERO ---
        base.Awake();
        if (!enabled) return; // Salir si base.Awake falló (ej. sin perfil)
        // --------------------------------------------------

        // Obtener centro del área
        Transform movementAreaCenterTF = null;
        if (movementAreaCenterTF == null && transform.parent != null) movementAreaCenterTF = transform.parent;
        if (movementAreaCenterTF == null) actualMovementAreaCenter = transform.position;
        else actualMovementAreaCenter = movementAreaCenterTF.position;

        // Configurar Hitbox específico del Tank
        if (attackHitboxCollider != null)
        {
            enemyHitbox = attackHitboxCollider.GetComponent<EnemyHitbox>();
            if (enemyHitbox == null) { Debug.LogError($"Collider {attackHitboxCollider.name} no tiene EnemyHitbox!", this); attackHitboxCollider = null; }
            else
            {
                if (enemyHitbox.enemyBase == null) enemyHitbox.enemyBase = this;
                attackHitboxCollider.enabled = false;
                if (!attackHitboxCollider.isTrigger) Debug.LogWarning($"Collider de ataque en {attackHitboxCollider.name} NO es Trigger.", this);
            }
        }
        else Debug.LogWarning($"Attack Hitbox Collider no asignado en {gameObject.name}. El Tank no podrá atacar.", this);

        // Configuración específica del NavMeshAgent para el Tank
        if (agent != null)
        {
            agent.acceleration = 4f; // Valor fijo o leer de profile si lo añades
            agent.autoBraking = true;
            agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }
    }

    protected override void Start()
    {
        // --- Llamar a base.Start() PRIMERO ---
        base.Start();
        if (!enabled) return;
        // -------------------------------------

        playerRigidbody = player != null ? player.GetComponent<Rigidbody>() : null;
        lastProgressPosition = transform.position;
        currentSmartDestination = transform.position;

        // Inicializar temporizador de ataque desde el perfil
        specificAttackTimer = profile.attackCooldown;
        ChangeState(EnemyState.Wandering);
    }

    protected override void UpdateAIState()
    {
        if (profile == null || currentState == EnemyState.Neutralized) return;
        if (specificAttackTimer < profile.attackCooldown) specificAttackTimer += Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Wandering: ExecuteWanderingTank(); CheckForPlayerDetectionTank(); break;
            case EnemyState.Chasing: ExecuteChasingTank(); CheckPlayerVisibilityTank(); break;
            case EnemyState.Searching: ExecuteSearchingTank(); CheckForPlayerDetectionTank(); break;
            case EnemyState.Attacking: ExecuteAttackingTank(); break;
            case EnemyState.Neutralized: break;
            default: ChangeState(EnemyState.Wandering); break;
        }
    }

    private void ExecuteWanderingTank()
    {
        if (agent == null || !agent.enabled || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed;
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= 2f)
            { // Tiempo de espera (podría ir al perfil)
                isWaiting = false;
                WanderRandomlyTank();
            }
            return;
        }
        if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath))
        {
            isWaiting = true;
            waitTimer = 0f;
        }
    }

    private void WanderRandomlyTank()
    {
        if (agent == null || !agent.enabled || profile == null) return;
        Vector3 randomDirection = Random.insideUnitSphere * profile.maxDistanceFromCenter; // Usar radio del perfil
        randomDirection.y = 0f;
        Vector3 targetPos = actualMovementAreaCenter + randomDirection;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, profile.maxDistanceFromCenter * 1.5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(actualMovementAreaCenter);
        }
        isWaiting = false;
    }

    private void ExecuteChasingTank()
    {
        if (agent == null || !agent.enabled || player == null || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;

        float distanceFromCenter = Vector3.Distance(transform.position, actualMovementAreaCenter);
        if (distanceFromCenter > profile.maxDistanceFromCenter)
        { // Usar valor del perfil
            ChangeState(EnemyState.Wandering);
            if (agent.enabled) SetSmartDestination(actualMovementAreaCenter, true);
            return;
        }

        if (agent.speed != profile.chaseSpeed) agent.speed = profile.chaseSpeed; // Usar valor del perfil
        if (agent.stoppingDistance != profile.attackRange) agent.stoppingDistance = profile.attackRange; // Usar valor del perfil

        chaseRepathTimer -= Time.deltaTime;
        recoveryTimer -= Time.deltaTime;

        Vector3 targetPosition = GetPredictedPlayerPosition();
        if (chaseRepathTimer <= 0f || Vector3.Distance(currentSmartDestination, targetPosition) > 0.75f || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            SetSmartDestination(targetPosition, false);
            chaseRepathTimer = chaseRepathInterval;
        }

        if (IsLikelyStuck())
        {
            TrySetUnstuckDestination(targetPosition);
        }

        base.RotateTowards(player.position);

        if (HasLineOfSightToPlayer())
        {
            lastKnownPlayerPosition = player.position;
            timeSinceLostSight = 0f;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Vector3 directionToPlayer = player.position - transform.position;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer.normalized);

        // Usar valores del perfil para condición de ataque
        if (distanceToPlayer <= profile.attackRange && specificAttackTimer >= profile.attackCooldown && angleToPlayer <= profile.attackAngleThreshold && HasLineOfSightToPlayer())
        {
            ChangeState(EnemyState.Attacking);
        }
    }

    private void ExecuteSearchingTank()
    {
        if (agent == null || !agent.enabled || profile == null) return;
        if (agent.isStopped) agent.isStopped = false;
        if (agent.speed != profile.wanderSpeed) agent.speed = profile.wanderSpeed; // Buscar lento
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        if (!float.IsNaN(lastKnownPlayerPosition.x) && (!agent.hasPath || Vector3.Distance(agent.destination, lastKnownPlayerPosition) > 0.5f))
        {
            SetSmartDestination(lastKnownPlayerPosition, true);
        }
        if (!float.IsNaN(lastKnownPlayerPosition.x)) base.RotateTowards(lastKnownPlayerPosition);

        timeSinceLostSight += Time.deltaTime;
        // Usar searchDuration del perfil
        if (timeSinceLostSight >= profile.searchDuration || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance))
        {
            ChangeState(EnemyState.Wandering);
        }
    }

    private void ExecuteAttackingTank()
    {
        if (profile == null) return;
        if (agent != null && agent.enabled) { if (!agent.isStopped) agent.isStopped = true; agent.velocity = Vector3.zero; }
        if (player != null) base.RotateTowards(player.position);
        AttackTank();
        ChangeState(EnemyState.Chasing);
    }

    private void AttackTank()
    {
        if (profile == null) return;
        if (animator != null) animator.SetTrigger(attackTriggerAnimHash);
        // Usar attackSound del perfil
        if (mainAudioSource != null && profile.attackSound != null) mainAudioSource.PlayOneShot(profile.attackSound);
        specificAttackTimer = 0f;
        //AttackHitboxOn();
        //Invoke(nameof(AttackHitboxOff), 0.7f); // Duración hitbox (podría ir al perfil)
    }

    private void CheckForPlayerDetectionTank()
    {
        if (player == null || profile == null || currentState == EnemyState.Neutralized) return;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float currentAgroRange = GetCurrentAgroRangeTank();
        if (distanceToPlayer > currentAgroRange) return;
        Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, directionToPlayerNorm) > profile.fieldOfView * 0.5f) return; // Usa FoV del perfil
        if (!HasLineOfSightToPlayer()) return;

        if (currentState == EnemyState.Wandering || currentState == EnemyState.Searching)
        {
            ChangeState(EnemyState.Chasing);
        }
        lastKnownPlayerPosition = player.position;
        timeSinceLostSight = 0f;
    }

    private void CheckPlayerVisibilityTank()
    {
        if (player == null || profile == null) { if (currentState == EnemyState.Chasing) ChangeState(EnemyState.Searching); return; }
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        // Usa loseAgroDistanceMultiplier del perfil
        float loseAgroDistance = GetCurrentAgroRangeTank() * profile.loseAgroDistanceMultiplier;
        if (distanceToPlayer > loseAgroDistance) { ChangeState(EnemyState.Searching); return; }

        if (HasLineOfSightToPlayer())
        {
            timeSinceLostSight = 0f;
            lastKnownPlayerPosition = player.position;
        }
        else
        {
            timeSinceLostSight += Time.deltaTime;
            if (timeSinceLostSight >= chaseMemoryTime)
            {
                ChangeState(EnemyState.Searching);
            }
        }
    }

    private float GetCurrentAgroRangeTank()
    {
        if (player == null || profile == null) return profile?.standingAgroRange ?? 8f;
        LampSystem lamp = player.GetComponentInChildren<LampSystem>();
        PlayerController pc = player.GetComponent<PlayerController>();
        // Usa rangos del perfil
        if ((lamp != null && lamp.IsLampOn) || (pc != null && pc.IsRunning())) return profile.lightAgroRange;
        if (pc != null && pc.IsCrouching()) return profile.crouchAgroRange;
        return profile.standingAgroRange;
    }

    private Vector3 GetPredictedPlayerPosition()
    {
        if (player == null) return transform.position;
        Vector3 predictedPosition = player.position;
        if (playerRigidbody != null)
        {
            Vector3 horizontalVelocity = playerRigidbody.linearVelocity;
            horizontalVelocity.y = 0f;
            predictedPosition += horizontalVelocity * predictionLeadTime;
        }
        return predictedPosition;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 playerTargetPoint = player.position + Vector3.up * 1.0f;
        Vector3 directionToPlayer = playerTargetPoint - rayOrigin;
        float distanceToPlayer = directionToPlayer.magnitude;
        if (distanceToPlayer <= 0.01f) return true;
        return !Physics.Raycast(rayOrigin, directionToPlayer.normalized, distanceToPlayer, obstacleLayer);
    }

    private bool SetSmartDestination(Vector3 desiredDestination, bool force)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (!force && Vector3.Distance(currentSmartDestination, desiredDestination) <= 0.2f && agent.hasPath) return true;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(desiredDestination, out hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            if (!NavMesh.SamplePosition(desiredDestination, out hit, unstuckSearchRadius, NavMesh.AllAreas)) return false;
        }

        NavMeshPath path = new NavMeshPath();
        bool hasUsablePath = agent.CalculatePath(hit.position, path) && path.status != NavMeshPathStatus.PathInvalid;
        if (!hasUsablePath) return false;

        agent.SetPath(path);
        currentSmartDestination = hit.position;
        return true;
    }

    private bool IsLikelyStuck()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending) return false;
        if (recoveryTimer > 0f) return false;

        bool shouldBeMoving = currentState == EnemyState.Chasing && agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.25f;
        if (!shouldBeMoving)
        {
            stuckTimer = 0f;
            lastProgressPosition = transform.position;
            return false;
        }

        float progress = Vector3.Distance(transform.position, lastProgressPosition);
        bool lowProgress = progress < stuckMinProgress;
        bool lowVelocity = agent.velocity.sqrMagnitude < 0.04f;

        if (lowProgress && lowVelocity)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
            lastProgressPosition = transform.position;
        }

        return stuckTimer >= stuckCheckTime;
    }

    private void TrySetUnstuckDestination(Vector3 targetPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3[] candidateDirections = new Vector3[] {
             transform.right,
             -transform.right,
             transform.forward,
             -transform.forward,
             (targetPosition - transform.position).normalized,
             Quaternion.Euler(0f, 45f, 0f) * (targetPosition - transform.position).normalized,
             Quaternion.Euler(0f, -45f, 0f) * (targetPosition - transform.position).normalized
         };

        float bestScore = float.PositiveInfinity;
        Vector3 bestDestination = Vector3.zero;
        bool found = false;

        for (int i = 0; i < candidateDirections.Length; i++)
        {
            Vector3 dir = candidateDirections[i];
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) continue;
            dir.Normalize();

            for (int step = 1; step <= 3; step++)
            {
                Vector3 candidate = transform.position + dir * (unstuckSearchRadius * step * 0.5f);
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, navMeshSampleRadius, NavMesh.AllAreas)) continue;

                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status == NavMeshPathStatus.PathInvalid) continue;

                float score = Vector3.Distance(hit.position, targetPosition);
                if (path.status == NavMeshPathStatus.PathPartial) score += 5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestDestination = hit.position;
                    found = true;
                }
            }
        }

        if (found)
        {
            agent.ResetPath();
            agent.SetDestination(bestDestination);
            currentSmartDestination = bestDestination;
            recoveryTimer = 0.65f;
            stuckTimer = 0f;
            lastProgressPosition = transform.position;
        }
        else
        {
            agent.ResetPath();
            SetSmartDestination(targetPosition, true);
            recoveryTimer = 0.65f;
            stuckTimer = 0f;
            lastProgressPosition = transform.position;
        }
    }

    public void AttackHitboxOn() { if (enemyHitbox != null) enemyHitbox.ActivateHitbox(); }
    public void AttackHitboxOff() { if (enemyHitbox != null) enemyHitbox.DeactivateHitbox(); }

    protected override void OnEnterState(EnemyState newState)
    {
        base.OnEnterState(newState);
        if (agent == null || !agent.enabled || profile == null) return;

        switch (newState)
        {
            case EnemyState.Wandering:
                agent.speed = profile.wanderSpeed;
                agent.stoppingDistance = 0.1f;
                isWaiting = false;
                agent.isStopped = false;
                stuckTimer = 0f;
                recoveryTimer = 0f;
                lastProgressPosition = transform.position;
                break;
            case EnemyState.Chasing:
                agent.speed = profile.chaseSpeed;
                agent.stoppingDistance = profile.attackRange;
                timeSinceLostSight = 0f;
                chaseRepathTimer = 0f;
                stuckTimer = 0f;
                recoveryTimer = 0f;
                lastProgressPosition = transform.position;
                agent.isStopped = false;
                break;
            case EnemyState.Searching:
                agent.speed = profile.wanderSpeed;
                agent.stoppingDistance = 0.1f;
                stuckTimer = 0f;
                recoveryTimer = 0f;
                if (!float.IsNaN(lastKnownPlayerPosition.x)) SetSmartDestination(lastKnownPlayerPosition, true);
                agent.isStopped = false;
                break;
            case EnemyState.Attacking: break; // Base ya detiene agente
        }
    }
}
