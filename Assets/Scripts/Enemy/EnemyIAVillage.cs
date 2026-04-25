using UnityEngine;
using UnityEngine.AI; // Asegúrate que esté
using System.Linq; // Para .All

// Hereda de EnemyBase
public class EnemyIAVillage : EnemyBase
{
    // Parámetros específicos de esta IA que no están en EnemyBase
    [Header("IA Village Settings")]
    [SerializeField] private float fieldOfView = 120f;
    [SerializeField] private float standingAgroRange = 8f;
    [SerializeField] private float crouchAgroRange = 4f;
    [SerializeField] private float lightAgroRange = 15f;
    [SerializeField] private float loseAgroDistanceMultiplier = 1.5f;
    [SerializeField] private float searchDuration = 4.0f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float attackAngleThreshold = 30f;

    [Header("Wandering Specifics")]
    [SerializeField] private float wanderSpeed = 1.5f;
    [SerializeField] private float wanderInterval = 5f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private Transform movementAreaCenter;
    [SerializeField] private float movementAreaRadius = 15f;
    [SerializeField] private float maxDistanceFromCenter = 25f;

    [Header("Combat Specifics")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [Tooltip("El Collider (Trigger) del Hitbox del ataque.")]
    [SerializeField] private Collider attackHitboxCollider;
    [SerializeField] private AudioClip attackSound;

    // Referencias específicas
    private EnemyHitbox enemyHitbox;

    // Estado interno específico
    private float specificAttackTimer = 0f;
    private float wanderTimer = 0f;
    private float waitTimer = 0f;
    private float timeSinceLostSight = 0f;
    private bool isWaiting = false;
    //private Vector3 lastKnownPlayerPosition;
    private Vector3 actualMovementAreaCenter;

    // Hashes específicos (si los hubiera)
    private readonly int attackTriggerAnimHash = Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake(); // Llama al Awake de EnemyBase PRIMERO

        if (movementAreaCenter == null) actualMovementAreaCenter = transform.position;
        else actualMovementAreaCenter = movementAreaCenter.position;

        // Configurar Hitbox específico
        if (attackHitboxCollider != null)
        {
            enemyHitbox = attackHitboxCollider.GetComponent<EnemyHitbox>();
            if (enemyHitbox == null) { Debug.LogError($"Collider {attackHitboxCollider.name} no tiene EnemyHitbox!", this); attackHitboxCollider = null;}
            else {
                // --- CORREGIDO: Asignar a enemyBase ---
                if (enemyHitbox.enemyBase == null) enemyHitbox.enemyBase = this;
                // -------------------------------------
                attackHitboxCollider.enabled = false;
                if (!attackHitboxCollider.isTrigger) Debug.LogWarning($"Collider en {attackHitboxCollider.name} NO es Trigger.", this);
            }
        } else Debug.LogWarning($"Attack Hitbox Collider no asignado en {gameObject.name}. No podrá atacar.", this);

         // Configuración inicial del agente
         if(agent != null) {
             agent.speed = wanderSpeed;
             agent.stoppingDistance = 0.1f;
             agent.autoBraking = true; // Asegurar que esté activo
         }
    }

    protected override void Start()
    {
        base.Start(); // Llama al Start de EnemyBase
        specificAttackTimer = attackCooldown;
        wanderTimer = wanderInterval;
        ChangeState(EnemyState.Wandering);
    }

    // Implementación de la Lógica de IA
    protected override void UpdateAIState()
    {
        if (specificAttackTimer < attackCooldown) specificAttackTimer += Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Wandering: ExecuteWandering(); CheckForPlayerDetection(); break;
            case EnemyState.Chasing: ExecuteChasing(); CheckPlayerVisibility(); break;
            case EnemyState.Searching: ExecuteSearching(); CheckForPlayerDetection(); break;
            case EnemyState.Attacking: ExecuteAttacking(); break;
        }
    }

    // Métodos de Estado Específicos
    private void ExecuteWandering() {
        if (agent == null || !agent.enabled) return; if (agent.isStopped) agent.isStopped = false; if (agent.speed != wanderSpeed) agent.speed = wanderSpeed; if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;
        if (isWaiting) { waitTimer += Time.deltaTime; if (waitTimer >= waitTimeAtPoint) { isWaiting = false; WanderRandomlyWithinArea(); } return; }
        if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath)) { isWaiting = true; waitTimer = 0f; }
    }
    private void WanderRandomlyWithinArea() {
        if (agent == null || !agent.enabled) return; Vector3 randomDirection = Random.insideUnitSphere * movementAreaRadius; Vector3 targetPos = actualMovementAreaCenter + randomDirection; NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, movementAreaRadius * 2, NavMesh.AllAreas)) { agent.SetDestination(hit.position); }
        else { if (NavMesh.SamplePosition(actualMovementAreaCenter, out hit, 1.0f, NavMesh.AllAreas)) { agent.SetDestination(hit.position); } else { Debug.LogError($"Wander failed critically for {gameObject.name}", this); } }
        wanderTimer = 0f; isWaiting = false;
    }
    private void ExecuteChasing() {
        if (agent == null || !agent.enabled || player == null) return; if (agent.isStopped) agent.isStopped = false;
        float distanceFromCenter = Vector3.Distance(transform.position, actualMovementAreaCenter); if (distanceFromCenter > maxDistanceFromCenter) { ChangeState(EnemyState.Wandering); if (agent.enabled) agent.SetDestination(actualMovementAreaCenter); return; }
        if (agent.speed != chaseSpeed) agent.speed = chaseSpeed; if (agent.stoppingDistance != attackRange) agent.stoppingDistance = attackRange;
        if (Vector3.Distance(agent.destination, player.position) > 0.5f) { agent.SetDestination(player.position); }
        base.RotateTowards(player.position); // <-- Usa el método base
        lastKnownPlayerPosition = player.position; timeSinceLostSight = 0f;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position); Vector3 directionToPlayer = player.position - transform.position; directionToPlayer.y = 0; float angleToPlayer = (directionToPlayer.sqrMagnitude > 0.01f) ? Vector3.Angle(transform.forward, directionToPlayer.normalized) : 0f;
        if (distanceToPlayer <= attackRange && specificAttackTimer >= attackCooldown && angleToPlayer <= attackAngleThreshold) { ChangeState(EnemyState.Attacking); }
    }
    private void ExecuteSearching() {
         if (agent == null || !agent.enabled) return; if (agent.isStopped) agent.isStopped = false; if (agent.speed != chaseSpeed) agent.speed = chaseSpeed; if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;
         if (!float.IsNaN(lastKnownPlayerPosition.x) && agent.destination != lastKnownPlayerPosition) { agent.SetDestination(lastKnownPlayerPosition); }
         base.RotateTowards(lastKnownPlayerPosition); // <-- Usa el método base
         timeSinceLostSight += Time.deltaTime;
         if (timeSinceLostSight >= searchDuration || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)) { ChangeState(EnemyState.Wandering); }
    }
    private void ExecuteAttacking() {
         if (agent != null && agent.enabled) { if(!agent.isStopped) agent.isStopped = true; agent.velocity = Vector3.zero; }
         if(player != null) base.RotateTowards(player.position); // <-- Usa el método base
         AttackPlayer();
         ChangeState(EnemyState.Chasing);
    }
    private void AttackPlayer() {
        if (animator != null) animator.SetTrigger(attackTriggerAnimHash);
        if (mainAudioSource != null && attackSound != null) mainAudioSource.PlayOneShot(attackSound);
        specificAttackTimer = 0f;
    }

    // Métodos de Detección
    private void CheckForPlayerDetection() {
        if (player == null || currentState == EnemyState.Neutralized) return; float distanceToPlayer = Vector3.Distance(transform.position, player.position); float currentAgroRange = GetCurrentAgroRange(); if (distanceToPlayer > currentAgroRange) return;
        Vector3 directionToPlayerNorm = (player.position - transform.position).normalized; if(Vector3.Angle(transform.forward, directionToPlayerNorm) > fieldOfView * 0.5f) return;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) return;
        if (currentState == EnemyState.Wandering || currentState == EnemyState.Searching) { ChangeState(EnemyState.Chasing); }
        lastKnownPlayerPosition = player.position; timeSinceLostSight = 0f;
    }
    private void CheckPlayerVisibility() {
        if (player == null) { if(currentState == EnemyState.Chasing) ChangeState(EnemyState.Searching); return; } // Si pierde al jugador, buscar
        float distanceFromCenter = Vector3.Distance(transform.position, actualMovementAreaCenter); if (distanceFromCenter > maxDistanceFromCenter) { ChangeState(EnemyState.Wandering); if (agent.enabled) agent.SetDestination(actualMovementAreaCenter); return; }
        float distanceToPlayer = Vector3.Distance(transform.position, player.position); float loseAgroDistance = GetCurrentAgroRange() * loseAgroDistanceMultiplier; if (distanceToPlayer > loseAgroDistance) { ChangeState(EnemyState.Searching); return; }
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; Vector3 directionToPlayerNorm = (player.position + Vector3.up * 0.5f - rayOrigin).normalized; if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) { ChangeState(EnemyState.Searching); }
        else { timeSinceLostSight = 0f; lastKnownPlayerPosition = player.position; }
    }
    private float GetCurrentAgroRange() {
        // Cache references if possible, FindObjectOfType is slow in Update
        LampSystem lamp = player?.GetComponentInChildren<LampSystem>();
        PlayerController pc = player?.GetComponent<PlayerController>();
        if ((lamp != null && lamp.IsLampOn) || (pc != null && pc.IsRunning())) return lightAgroRange;
        if (pc != null && pc.IsCrouching()) return crouchAgroRange;
        return standingAgroRange;
    }

    // --- RotateTowards se hereda de EnemyBase ---

    // Eventos de Animación Hitbox
    public void AttackHitboxOn() { if (enemyHitbox != null) enemyHitbox.ActivateHitbox(); }
    public void AttackHitboxOff() { if (enemyHitbox != null) enemyHitbox.DeactivateHitbox(); }

    // --- RegisterHit se hereda de EnemyBase ---

    // Lógica de Entrada a Estado (Sobrescribe base para configurar agente)
    protected override void OnEnterState(EnemyState newState) {
        base.OnEnterState(newState); // Llama a lógica base (emisión)
        if (agent == null || !agent.enabled) return;
        switch (newState) {
            case EnemyState.Wandering: agent.speed = wanderSpeed; agent.stoppingDistance = 0.1f; isWaiting = false; break;
            case EnemyState.Chasing: agent.speed = chaseSpeed; agent.stoppingDistance = attackRange; timeSinceLostSight = 0f; break;
            case EnemyState.Searching: agent.speed = chaseSpeed; agent.stoppingDistance = 0.1f; if (!float.IsNaN(lastKnownPlayerPosition.x)) agent.SetDestination(lastKnownPlayerPosition); break;
            // Attacking y Neutralized ya manejan isStopped en sus lógicas o en base.OnEnterState
        }
    }
}