// Archivo: Scripts/Enemy/NewEra/EnemyIA_Hunter.cs
using UnityEngine;
using UnityEngine.AI;

public class EnemyIA_Hunter : EnemyBase // Hereda de EnemyBase (que ahora tiene el perfil)
{
    // Ya no se necesitan los [SerializeField] para parámetros básicos, se leen del profile.

    [Header("Combat Specifics (Hunter)")]
    [Tooltip("El Collider (Trigger) del Hitbox del ataque. Asignar prefab específico del Hunter.")]
    [SerializeField] private Collider attackHitboxCollider;

    // Referencias específicas
    private EnemyHitbox enemyHitbox;

    // Estado interno específico
    private float specificAttackTimer = 0f;
    //private Vector3 lastKnownPlayerPosition;
    private float timeSinceLostSight = 0f;
    private Vector3 actualMovementAreaCenter; // Centro del área de movimiento

    // Hashes de animación específicos del Hunter (si los tuviera además de los base)
    private readonly int attackTriggerAnimHash = Animator.StringToHash("Attack"); // Asume que la animación de ataque se llama "Attack"

    protected override void Awake()
    {
        // --- IMPORTANTE: Llamar a base.Awake() PRIMERO ---
        // Esto inicializa componentes y valida/carga el perfil desde EnemyBase.
        base.Awake();
        // Si base.Awake encontró un error (ej. perfil no asignado), 'enabled' será false.
        if (!enabled) return;
        // --------------------------------------------------

        // Obtener centro del área (si el Hunter lo necesita específicamente)
        // Podrías tener un Transform asignable en el perfil o buscarlo aquí
        Transform movementAreaCenterTF = null; // Ejemplo: buscar un objeto "HunterPatrolCenter"
        if (movementAreaCenterTF == null)
            actualMovementAreaCenter = transform.position; // Usar posición inicial si no hay centro definido
        else
            actualMovementAreaCenter = movementAreaCenterTF.position;

        // Configurar Hitbox (si es diferente al de otros enemigos)
        if (attackHitboxCollider != null)
        {
            enemyHitbox = attackHitboxCollider.GetComponent<EnemyHitbox>();
            if (enemyHitbox == null) { Debug.LogError($"Collider {attackHitboxCollider.name} no tiene EnemyHitbox!", this); attackHitboxCollider = null;}
            else
            {
                 // Asegurarse que el hitbox apunte a ESTE script (EnemyBase)
                 if (enemyHitbox.enemyBase == null) enemyHitbox.enemyBase = this;
                 attackHitboxCollider.enabled = false; // Empezar desactivado
                 if (!attackHitboxCollider.isTrigger) Debug.LogWarning($"El Collider de ataque en {attackHitboxCollider.name} NO es Trigger.", this);
            }
        } else Debug.LogWarning($"Attack Hitbox Collider no asignado en {gameObject.name}. El Hunter no podrá atacar.", this);

         // Ajustar parámetros del NavMeshAgent si el Hunter necesita algo específico
         // que no esté en el perfil o deba sobrescribir el base.Awake
         if(agent != null) {
             // Ejemplo: Forzar mayor aceleración para el Hunter
             agent.acceleration = 12f;
             agent.angularSpeed = 360f; // Más rápido que el base.Awake
             // Nota: agent.speed y agent.stoppingDistance se establecerán en Start/OnEnterState según el perfil
         }
    }

    protected override void Start() {
        // --- Llamar a base.Start() PRIMERO ---
        base.Start();
        if (!enabled) return; // Salir si Awake falló (ej. sin perfil)
        // -------------------------------------

        // Inicializar temporizador de ataque usando el cooldown del perfil
        specificAttackTimer = profile.attackCooldown;

        // Establecer estado inicial (ej. Wandering o quizás Searching para ser más agresivo)
        ChangeState(EnemyState.Wandering);
    }

    /// <summary>
    /// Lógica principal de la IA del Hunter, llamada cada frame desde EnemyBase.Update().
    /// Determina el comportamiento según el estado actual y los valores del perfil.
    /// </summary>
    protected override void UpdateAIState()
    {
        // Salir si no hay perfil o está neutralizado (doble chequeo)
         if (profile == null || currentState == EnemyState.Neutralized) return;

         // Actualizar temporizador de cooldown de ataque
         if (specificAttackTimer < profile.attackCooldown)
         {
             specificAttackTimer += Time.deltaTime;
         }

        // Máquina de estados para el Hunter
        switch (currentState)
        {
            case EnemyState.Wandering:
                ExecuteWanderingHunter();
                CheckForPlayerDetectionHunter(); // Comprobar si ve al jugador
                break;
            case EnemyState.Chasing:
                ExecuteChasingHunter();
                CheckPlayerVisibilityHunter(); // Comprobar si pierde al jugador
                break;
            case EnemyState.Searching:
                ExecuteSearchingHunter();
                CheckForPlayerDetectionHunter(); // Comprobar si vuelve a ver al jugador
                break;
            case EnemyState.Attacking:
                ExecuteAttackingHunter(); // Realizar el ataque
                break;
            // El estado Neutralized es manejado por EnemyBase
            case EnemyState.Neutralized:
                break;
            // Fallback: si está en un estado inesperado, volver a Wandering
            default:
                ChangeState(EnemyState.Wandering);
                break;
        }
    }

    // --- Implementaciones de los Estados Específicos del Hunter ---

    private void ExecuteWanderingHunter() {
        if (agent == null || !agent.enabled || profile == null) return;
        // Asegurar que el agente se mueva
        if (agent.isStopped) agent.isStopped = false;

        // Usar una fracción de la velocidad de persecución para patrullar (ej. 60%)
        float currentWanderSpeed = profile.chaseSpeed * 0.6f;
        if (agent.speed != currentWanderSpeed) agent.speed = currentWanderSpeed;

        // Usar una distancia de parada pequeña al patrullar
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        // Si ha llegado al destino o no tiene ruta, buscar un nuevo punto
        if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath)) {
            WanderRandomlyAggressive(); // Buscar nuevo punto dentro del área
        }
    }

     private void WanderRandomlyAggressive() {
         if (agent == null || !agent.enabled || profile == null) return;

         // Buscar un punto aleatorio dentro del radio máximo desde el centro
         Vector3 randomDirection = Random.insideUnitSphere * (profile.maxDistanceFromCenter * 0.8f); // Usar 80% del radio máximo
         randomDirection += actualMovementAreaCenter; // Sumar al centro del área

         NavMeshHit hit;
         // Buscar la posición válida más cercana en el NavMesh dentro de un rango amplio
         if (NavMesh.SamplePosition(randomDirection, out hit, profile.maxDistanceFromCenter * 1.5f, NavMesh.AllAreas)) {
             agent.SetDestination(hit.position);
         } else {
             // Fallback: si no encuentra un punto, ir al centro del área
             agent.SetDestination(actualMovementAreaCenter);
             Debug.LogWarning($"{gameObject.name} no encontró punto de wander, volviendo al centro.");
         }
     }


    private void ExecuteChasingHunter() {
         if (agent == null || !agent.enabled || player == null || profile == null) return;
        // Asegurar que el agente se mueva
        if (agent.isStopped) agent.isStopped = false;

        // Comprobar si se alejó demasiado del centro
        float distanceFromCenter = Vector3.Distance(transform.position, actualMovementAreaCenter);
        if (distanceFromCenter > profile.maxDistanceFromCenter) {
            ChangeState(EnemyState.Wandering); // Volver a patrullar
            if (agent.enabled) agent.SetDestination(actualMovementAreaCenter); // Ir hacia el centro
            return;
        }

        // Establecer velocidad y distancia de parada para persecución desde el perfil
        if (agent.speed != profile.chaseSpeed) agent.speed = profile.chaseSpeed;
        if (agent.stoppingDistance != profile.attackRange) agent.stoppingDistance = profile.attackRange;

        // Actualizar destino hacia el jugador
        if (Vector3.Distance(agent.destination, player.position) > 0.1f) // Actualizar solo si la posición cambió significativamente
            agent.SetDestination(player.position);

        // Rotar hacia el jugador usando el método base (que usa profile.rotationSpeed)
        base.RotateTowards(player.position);

        // Actualizar última posición conocida y resetear timer de pérdida de visión
        lastKnownPlayerPosition = player.position;
        timeSinceLostSight = 0f;

        // Comprobar si puede atacar
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Vector3 directionToPlayer = player.position - transform.position;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer.normalized);

        if (distanceToPlayer <= profile.attackRange && specificAttackTimer >= profile.attackCooldown && angleToPlayer <= profile.attackAngleThreshold) {
            ChangeState(EnemyState.Attacking);
        }
    }

    private void ExecuteSearchingHunter() {
         if (agent == null || !agent.enabled || profile == null) return;
        // Asegurar que el agente se mueva
        if (agent.isStopped) agent.isStopped = false;

        // Buscar con velocidad alta (velocidad de persecución)
        if (agent.speed != profile.chaseSpeed) agent.speed = profile.chaseSpeed;
        // Usar distancia de parada pequeña al buscar
        if (agent.stoppingDistance != 0.1f) agent.stoppingDistance = 0.1f;

        // Si no tiene destino o no es la última posición conocida, ir hacia allá
        if (!agent.hasPath || Vector3.Distance(agent.destination, lastKnownPlayerPosition) > 0.5f)
        {
             if (!float.IsNaN(lastKnownPlayerPosition.x)) // Asegurar que la posición es válida
             {
                agent.SetDestination(lastKnownPlayerPosition);
             }
        }

        // Rotar hacia la última posición conocida
        if (!float.IsNaN(lastKnownPlayerPosition.x))
             base.RotateTowards(lastKnownPlayerPosition);

        // Incrementar temporizador de búsqueda
        timeSinceLostSight += Time.deltaTime;

        // Si ha pasado el tiempo de búsqueda o ha llegado al destino, volver a patrullar
        if (timeSinceLostSight >= profile.searchDuration || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)) {
            ChangeState(EnemyState.Wandering);
        }
    }

     private void ExecuteAttackingHunter() {
         if (profile == null) return;
         // Detener al agente antes de atacar
         if (agent != null && agent.enabled) {
             if(!agent.isStopped) agent.isStopped = true;
             agent.velocity = Vector3.zero; // Resetear velocidad residual
         }
         // Mirar al jugador mientras ataca
         if(player != null) base.RotateTowards(player.position);

         // Llamar a la lógica de ataque
         AttackHunter();

         // Volver inmediatamente a perseguir después de iniciar el ataque
         // La animación/hitbox se resolverán mientras ya está en estado Chasing
         ChangeState(EnemyState.Chasing);
    }

    /// <summary>
    /// Inicia la animación y el sonido de ataque, activa el hitbox.
    /// </summary>
    private void AttackHunter() {
         if (profile == null) return;

         // Disparar trigger de animación
         if (animator != null) animator.SetTrigger(attackTriggerAnimHash);

         // Reproducir sonido de ataque desde el perfil
         if (mainAudioSource != null && profile.attackSound != null)
             mainAudioSource.PlayOneShot(profile.attackSound);

         // Reiniciar temporizador de cooldown
         specificAttackTimer = 0f;

         // Activar el hitbox (si existe)
         //AttackHitboxOn();
         // Desactivar el hitbox después de un breve tiempo (ej. 0.5s)
         // Idealmente, esto se haría con un Animation Event al final de la animación de ataque.
         //Invoke(nameof(AttackHitboxOff), 0.5f);
    }

     // --- Métodos de Detección (usan valores del perfil) ---

     /// <summary>
     /// Comprueba si el jugador está dentro del rango y campo de visión.
     /// Cambia al estado Chasing si se detecta al jugador.
     /// </summary>
     private void CheckForPlayerDetectionHunter() {
         if (player == null || profile == null || currentState == EnemyState.Neutralized) return;

         float distanceToPlayer = Vector3.Distance(transform.position, player.position);
         float currentAgroRange = GetCurrentAgroRangeHunter(); // Obtiene el rango basado en estado del jugador

         // Salir si está fuera de rango
         if (distanceToPlayer > currentAgroRange) return;

         Vector3 directionToPlayerNorm = (player.position - transform.position).normalized;

         // Salir si está fuera del campo de visión (definido en el perfil)
         if(Vector3.Angle(transform.forward, directionToPlayerNorm) > profile.fieldOfView * 0.5f) return;

         // Lanzar rayo para comprobar obstáculos
         Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; // Origen ligeramente elevado
         if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer))
         {
             // Hay un obstáculo, no se ve al jugador
             return;
         }

         // Si pasa todas las comprobaciones -> Jugador detectado
         if (currentState == EnemyState.Wandering || currentState == EnemyState.Searching) {
             //Debug.Log($"{gameObject.name} detectó al jugador!");
             ChangeState(EnemyState.Chasing);
         }
         // Actualizar última posición conocida y resetear timer (incluso si ya estaba persiguiendo)
         lastKnownPlayerPosition = player.position;
         timeSinceLostSight = 0f;
     }

      /// <summary>
      /// Comprueba si el jugador sigue siendo visible o si se ha alejado demasiado.
      /// Cambia al estado Searching si se pierde al jugador.
      /// </summary>
      private void CheckPlayerVisibilityHunter() {
         // Si no hay jugador o perfil, o si el jugador ya no existe, ir a buscar (o patrullar)
         if (player == null || profile == null) {
             if(currentState == EnemyState.Chasing) ChangeState(EnemyState.Searching);
             return;
         }

         float distanceToPlayer = Vector3.Distance(transform.position, player.position);
         // Usar loseAgroDistanceMultiplier del perfil
         float loseAgroDistance = GetCurrentAgroRangeHunter() * profile.loseAgroDistanceMultiplier;

         // Comprobar si se ha alejado demasiado
         if (distanceToPlayer > loseAgroDistance) {
             //Debug.Log($"{gameObject.name} perdió al jugador por distancia.");
             ChangeState(EnemyState.Searching);
             return;
         }

         // Comprobar línea de visión con Raycast
         Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
         // Apuntar al centro aproximado del jugador
         Vector3 playerTargetPoint = player.position + Vector3.up * 1.0f;
         Vector3 directionToPlayerNorm = (playerTargetPoint - rayOrigin).normalized;

         if (Physics.Raycast(rayOrigin, directionToPlayerNorm, distanceToPlayer, obstacleLayer)) {
             // Hay un obstáculo, jugador perdido de vista
             //Debug.Log($"{gameObject.name} perdió al jugador por obstáculo.");
             ChangeState(EnemyState.Searching);
         }
         else {
             // Jugador todavía visible, resetear timer y actualizar posición
             timeSinceLostSight = 0f;
             lastKnownPlayerPosition = player.position;
         }
     }

     /// <summary>
     /// Calcula el rango de detección actual basado en el estado del jugador y los valores del perfil.
     /// </summary>
     /// <returns>El rango de detección efectivo.</returns>
      private float GetCurrentAgroRangeHunter() {
         // Valor por defecto si no hay jugador o perfil
         if (player == null || profile == null) return profile?.standingAgroRange ?? 8f; // Usa el del perfil o 8 si no hay perfil

         // Intentar obtener componentes del jugador (optimización: cachear si se llama muy frecuente)
         LampSystem lamp = player.GetComponentInChildren<LampSystem>(); // Buscar linterna
         PlayerController pc = player.GetComponent<PlayerController>();       // Buscar controlador

         // Devolver rango según prioridad: Luz/Correr > Normal > Agachado
         if ((lamp != null && lamp.IsLampOn) || (pc != null && pc.IsRunning()))
             return profile.lightAgroRange;
         if (pc != null && pc.IsCrouching())
             return profile.crouchAgroRange;

         return profile.standingAgroRange; // Rango por defecto
     }

      // --- Métodos para Hitbox ---
      // Estos métodos pueden ser llamados por Animation Events en la animación de ataque

     /// <summary>
     /// Activa el collider del hitbox de ataque.
     /// </summary>
     public void AttackHitboxOn() {
         if (enemyHitbox != null)
         {
             enemyHitbox.ActivateHitbox();
             Debug.Log($"{gameObject.name} Attack Hitbox ON");
         }
     }

     /// <summary>
     /// Desactiva el collider del hitbox de ataque.
     /// </summary>
     public void AttackHitboxOff() {
         if (enemyHitbox != null)
         {
             enemyHitbox.DeactivateHitbox();
              //Debug.Log($"{gameObject.name} Attack Hitbox OFF");
         }
     }
     // RegisterHit se hereda de EnemyBase

     // --- Lógica de Entrada a Estados Específica del Hunter ---
     /// <summary>
     /// Sobrescribe el método base para configurar el NavMeshAgent según el perfil al entrar en un estado.
     /// </summary>
      protected override void OnEnterState(EnemyState newState) {
         // --- IMPORTANTE: Llamar a base.OnEnterState PRIMERO ---
         // Esto maneja la lógica común como el color de emisión.
         base.OnEnterState(newState);
         // -----------------------------------------------------

         // Salir si falta agente o perfil
         if (agent == null || !agent.enabled || profile == null) return;

         // Configuración específica del agente para cada estado del Hunter
         switch (newState) {
             case EnemyState.Wandering:
                 agent.speed = profile.chaseSpeed * 0.6f; // Patrullar rápido
                 agent.stoppingDistance = 0.1f;          // Parar cerca del punto de wander
                 agent.isStopped = false;                // Asegurar que se mueva
                 break;
             case EnemyState.Chasing:
                 agent.speed = profile.chaseSpeed;        // Velocidad máxima
                 agent.stoppingDistance = profile.attackRange; // Parar a distancia de ataque
                 timeSinceLostSight = 0f;                 // Resetear timer al empezar a perseguir
                 agent.isStopped = false;                // Asegurar que se mueva
                 break;
             case EnemyState.Searching:
                 agent.speed = profile.chaseSpeed;        // Buscar rápido
                 agent.stoppingDistance = 0.1f;          // Parar cerca de la última posición
                 // Ir a la última posición conocida (si es válida)
                 if (!float.IsNaN(lastKnownPlayerPosition.x))
                 {
                      agent.SetDestination(lastKnownPlayerPosition);
                 }
                 agent.isStopped = false;                // Asegurar que se mueva
                 break;
             case EnemyState.Attacking:
                 // La base ya pone agent.isStopped = true
                 break;
            // Los demás estados (Idle, Fleeing, Alerting, Neutralized) son manejados por la base
            // o no son usados directamente por el Hunter en esta implementación.
         }
     }
}