using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyPlacementManager : MonoBehaviour
{
    [Header("Configuration Profile")]
    [Tooltip("El perfil de colocación que define qué y cómo colocar.")]
    [SerializeField] private PlacementProfile currentProfile;

    [Header("Scene References (Auto-find or Assign)")]
    [Tooltip("Lista de todas las zonas de colocación en la escena. Si está vacía, intentará encontrarlas por componente.")]
    [SerializeField] private List<PlacementZone> placementZones;
    [Tooltip("Lista de todos los puntos estratégicos en la escena. Si está vacía, intentará encontrarlos por componente.")]
    [SerializeField] private List<StrategicPoint> strategicPoints;
    [Tooltip("Capa(s) que se consideran obstáculos para la colocación y línea de visión.")]
    [SerializeField] private LayerMask placementObstacleLayer;

    [Header("Placement Parameters")]
    [Tooltip("Radio de búsqueda alrededor de un punto candidato para encontrar una posición válida en el NavMesh.")]
    [SerializeField] private float navMeshSampleRadius = 2.0f;
    [Tooltip("Radio para comprobar si hay obstáculos cerca del punto de spawn final.")]
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    [Tooltip("Máximo número de intentos para encontrar una posición válida para un solo enemigo.")]
    [SerializeField] private int maxPlacementAttemptsPerEnemy = 20;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // Contenedor para los enemigos instanciados por este manager
    private Transform enemyContainer;
    private const string ENEMY_CONTAINER_NAME = "--- Managed Enemies ---";

    // Lista interna de posiciones ocupadas para chequeos rápidos
    private List<Vector3> spawnedEnemyPositions = new List<Vector3>();

    void Start()
    {
        // Auto-buscar referencias si no están asignadas
        if (placementZones == null || placementZones.Count == 0)
        {
            placementZones = FindObjectsByType<PlacementZone>(FindObjectsSortMode.InstanceID).ToList();
            Debug.Log($"Encontradas {placementZones.Count} PlacementZones en la escena.");
        }
        if (strategicPoints == null || strategicPoints.Count == 0)
        {
            strategicPoints = FindObjectsByType<StrategicPoint>(FindObjectsSortMode.InstanceID).ToList();
            Debug.Log($"Encontrados {strategicPoints.Count} StrategicPoints en la escena.");
        }

        // Crear contenedor para los enemigos
        GameObject containerGO = GameObject.Find(ENEMY_CONTAINER_NAME);
        if (containerGO == null)
        {
            containerGO = new GameObject(ENEMY_CONTAINER_NAME);
        }
        enemyContainer = containerGO.transform;

        // Opcional: Colocar enemigos automáticamente al iniciar
        // PlaceEnemies();
    }

    [ContextMenu("Place Enemies Now (Using Profile)")]
    public void PlaceEnemies()
    {
        if (currentProfile == null)
        {
            Debug.LogError("EnemyPlacementManager: ¡No se ha asignado un Placement Profile!", this);
            return;
        }

        // 0. Limpiar enemigos gestionados anteriormente
        ClearExistingManagedEnemies();
        spawnedEnemyPositions.Clear();

        Debug.Log($"Iniciando colocación usando perfil: {currentProfile.profileDescription}");

        int totalEnemiesPlaced = 0;
        int totalEnemiesToPlace = currentProfile.maxTotalEnemies > 0
                                    ? currentProfile.maxTotalEnemies
                                    : currentProfile.placementRules.Sum(rule => rule.count); // Suma de todas las reglas si no hay límite global

        // Ordenar reglas por prioridad (opcional)
        List<PlacementRule> sortedRules = currentProfile.placementRules.OrderByDescending(rule => rule.priority).ToList();

        // 1. Iterar sobre las reglas del perfil
        foreach (PlacementRule rule in sortedRules)
        {
            if (rule.enemyType == null || rule.enemyType.enemyPrefab == null)
            {
                Debug.LogWarning($"Regla '{rule.ruleDescription}' omitida: EnemyType o Prefab no asignado.");
                continue;
            }

            int placedForRule = 0;
            int attemptsForRule = 0;
            int maxAttemptsForRule = rule.count * maxPlacementAttemptsPerEnemy;

            // Colocar la cantidad definida por la regla
            while (placedForRule < rule.count && attemptsForRule < maxAttemptsForRule)
            {
                 // Salir si ya alcanzamos el límite global del perfil
                 if (currentProfile.maxTotalEnemies > 0 && totalEnemiesPlaced >= currentProfile.maxTotalEnemies)
                 {
                     Debug.Log("Límite global de enemigos alcanzado por el perfil.");
                     break; // Salir del bucle while de esta regla
                 }

                attemptsForRule++;

                // 2. Encontrar una posición candidata según la regla
                if (TryFindStrategicSpawnPoint(rule, out Vector3 spawnPosition))
                {
                    // 3. Instanciar el enemigo
                    // TODO: Determinar rotación (ej. mirar hacia punto estratégico, aleatoria)
                    Quaternion spawnRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    GameObject spawnedEnemy = Instantiate(rule.enemyType.enemyPrefab, spawnPosition, spawnRotation, enemyContainer);
                    // Opcional: configurar algo en el enemigo recién instanciado si es necesario

                    spawnedEnemyPositions.Add(spawnPosition);
                    placedForRule++;
                    totalEnemiesPlaced++;
                     //Debug.Log($"Regla '{rule.ruleDescription}': Enemigo {placedForRule}/{rule.count} ({rule.enemyType.enemyTypeId}) colocado en {spawnPosition}");
                }
                 // Si falla en encontrar un punto, el bucle while continúa hasta agotar intentos
            } // Fin while (placedForRule < rule.count)

            if (placedForRule < rule.count)
            {
                Debug.LogWarning($"Regla '{rule.ruleDescription}': Solo se pudieron colocar {placedForRule} de {rule.count} enemigos. Intentos: {attemptsForRule}.");
            }
             // Salir del foreach si ya alcanzamos el límite global del perfil
            if (currentProfile.maxTotalEnemies > 0 && totalEnemiesPlaced >= currentProfile.maxTotalEnemies)
            {
                break; // Salir del bucle foreach de las reglas
            }

        } // Fin foreach (PlacementRule rule in sortedRules)

        Debug.Log($"Colocación finalizada. Total enemigos colocados: {totalEnemiesPlaced}. Posiciones: {spawnedEnemyPositions.Count}");
    }

    // --- Lógica Central de Búsqueda Estratégica ---
    private bool TryFindStrategicSpawnPoint(PlacementRule rule, out Vector3 foundPosition)
    {
        List<PlacementZone> candidateZones = GetCandidateZones(rule);
        StrategicPoint targetPoint = GetTargetStrategicPoint(rule);

        // Barajar zonas para añadir aleatoriedad si hay varias candidatas
        candidateZones = candidateZones.OrderBy(z => Random.value).ToList();

        foreach (PlacementZone zone in candidateZones)
        {
            // Intentar encontrar un punto válido dentro de esta zona que cumpla la regla
            if (TryFindValidPointInZone(zone, rule, targetPoint, out foundPosition))
            {
                return true; // ¡Encontrado!
            }
        }

        // Si no se encontró en ninguna zona candidata
        Debug.LogWarning($"No se encontró posición válida para la regla '{rule.ruleDescription}' después de buscar en {candidateZones.Count} zonas.");
        foundPosition = Vector3.zero;
        return false;
    }

    // Intenta encontrar un punto específico dentro de UNA zona que cumpla la regla
    private bool TryFindValidPointInZone(PlacementZone zone, PlacementRule rule, StrategicPoint targetPoint, out Vector3 foundPosition)
    {
        int attemptsInZone = 10; // Intentos por encontrar un punto dentro de ESTA zona

        for (int i = 0; i < attemptsInZone; i++)
        {
            Vector3 randomPointInZone = zone.GetRandomPointInZone();

            if (NavMesh.SamplePosition(randomPointInZone, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Vector3 candidatePosition = hit.position;

                // Aplicar chequeos de validez (distancia a otros, obstáculos, distancia a punto estratégico, LoS)
                if (IsStrategicPositionValid(candidatePosition, rule, targetPoint))
                {
                    foundPosition = candidatePosition;
                    return true;
                }
            }
        }

        foundPosition = Vector3.zero;
        return false;
    }

    // --- Funciones de Filtrado y Selección Estratégica (IMPLEMENTAR TU LÓGICA AQUÍ) ---

    // Filtra las zonas que cumplen los criterios iniciales de la regla
    private List<PlacementZone> GetCandidateZones(PlacementRule rule)
    {
        List<PlacementZone> potentialZones;

        // 1. Filtrar por ZoneType de la regla
        if (rule.targetZoneType == PlacementZone.ZoneType.Any)
        {
            potentialZones = new List<PlacementZone>(this.placementZones); // Copia de todas las zonas
        }
        else
        {
            potentialZones = this.placementZones.Where(z => z.zoneType == rule.targetZoneType).ToList();
        }

        // 2. Filtrar por si la zona permite este tipo de enemigo
        potentialZones = potentialZones.Where(z =>
            z.allowedEnemyTypeIds == null ||
            z.allowedEnemyTypeIds.Count == 0 ||
            z.allowedEnemyTypeIds.Contains(rule.enemyType.enemyTypeId)
        ).ToList();

        // 3. Si hay un punto estratégico objetivo, priorizar zonas que lo contengan o estén cerca (lógica simple)
        if (!string.IsNullOrEmpty(rule.targetStrategicPointTag))
        {
            StrategicPoint targetSp = GetTargetStrategicPoint(rule);
            if (targetSp != null)
            {
                // Ordenar por distancia al punto estratégico (más cercanas primero)
                // y luego por la prioridad de la zona
                potentialZones = potentialZones.OrderBy(z => Vector3.Distance(z.transform.position, targetSp.transform.position))
                                            .ThenByDescending(z => z.priority)
                                            .ToList();
            }
            else
            {
                // Si no se encontró el punto, solo ordenar por prioridad de zona
                potentialZones = potentialZones.OrderByDescending(z => z.priority).ToList();
            }
        }
        else
        {
            // Si no hay punto estratégico, solo ordenar por prioridad de zona
            potentialZones = potentialZones.OrderByDescending(z => z.priority).ToList();
        }


        if (potentialZones.Count == 0) {
            Debug.LogWarning($"No se encontraron zonas candidatas para la regla '{rule.ruleDescription}' después de filtros iniciales. Intentando con todas las zonas que permitan el tipo '{rule.enemyType.enemyTypeId}'.");
            // Fallback más amplio: cualquier zona que permita el tipo de enemigo, ordenada por prioridad
            potentialZones = this.placementZones.Where(z =>
                z.allowedEnemyTypeIds == null ||
                z.allowedEnemyTypeIds.Count == 0 ||
                z.allowedEnemyTypeIds.Contains(rule.enemyType.enemyTypeId))
                .OrderByDescending(z => z.priority)
                .ToList();

            if(potentialZones.Count == 0) {
                Debug.LogError($"¡FALLBACK CRÍTICO! No se encontraron zonas para la regla '{rule.ruleDescription}'. Usando TODAS las zonas. Revisa configuración.");
                potentialZones = new List<PlacementZone>(this.placementZones); // Último recurso
            }
        }
        return potentialZones;
    }

    // Encuentra el punto estratégico si la regla lo especifica
    private StrategicPoint GetTargetStrategicPoint(PlacementRule rule)
    {
        if (string.IsNullOrEmpty(rule.targetStrategicPointTag))
        {
            return null;
        }
        // Busca el primer punto estratégico que coincida con el tag y tenga la mayor prioridad
        // Si hay varios con el mismo tag y misma prioridad, tomará el primero que encuentre.
        return strategicPoints
            .Where(p => p.CompareTag(rule.targetStrategicPointTag))
            .OrderByDescending(p => p.priority)
            .FirstOrDefault(); // Devuelve el primero o null si no hay
    }

    // --- Chequeo Final de Validez Estratégica (IMPLEMENTAR TU LÓGICA AQUÍ) ---
    private bool IsStrategicPositionValid(Vector3 position, PlacementRule rule, StrategicPoint targetPoint)
    {
        // 1. Chequeo de distancia mínima global Y de la regla
        float minDistanceToEnemies = Mathf.Max(currentProfile.globalMinDistanceBetweenEnemies, rule.minDistanceToOtherEnemies);
        foreach (Vector3 existingPos in spawnedEnemyPositions)
        {
            if (Vector3.Distance(position, existingPos) < minDistanceToEnemies)
            {
                // Debug.Log($"Posición {position} inválida: Muy cerca de otro enemigo ({Vector3.Distance(position, existingPos):F1}m < {minDistanceToEnemies:F1}m).");
                return false;
            }
        }

        // 2. Chequeo de obstáculos en el punto de spawn (usa el radio del enemigo como aproximación)
        // Asumimos que los enemigos tienen un radio aproximado para el chequeo
        float enemyRadiusCheck = (rule.enemyType.enemyPrefab != null && rule.enemyType.enemyPrefab.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent)) ? agent.radius : 0.5f; // Obtener radio del NavMeshAgent o default
        if (Physics.CheckSphere(position + Vector3.up * enemyRadiusCheck, enemyRadiusCheck, placementObstacleLayer))
        {
            // Debug.Log($"Posición {position} inválida: Obstruida por LayerMask (CheckSphere).");
            return false;
        }

        // 3. Chequeo de distancia al punto estratégico (si aplica)
        if (targetPoint != null)
        {
            float distanceToTarget = Vector3.Distance(position, targetPoint.transform.position);
            if (distanceToTarget < rule.minDistanceFromStrategicPoint || distanceToTarget > rule.maxDistanceFromStrategicPoint)
            {
                // Debug.Log($"Posición {position} inválida para Regla '{rule.ruleDescription}': Distancia a {targetPoint.tag} ({distanceToTarget:F1}m) fuera de rango [{rule.minDistanceFromStrategicPoint:F1}m - {rule.maxDistanceFromStrategicPoint:F1}m].");
                return false;
            }

            // 4. Chequeo de Línea de Visión (costoso, usar con moderación)
            if (rule.requireLineOfSightToTarget)
            {
                Vector3 targetCheckPos = targetPoint.transform.position + Vector3.up * 0.5f;
                Vector3 originCheckPos = position + Vector3.up * 0.5f; // Punto medio del enemigo
                if (Physics.Linecast(originCheckPos, targetCheckPos, placementObstacleLayer))
                {
                    // Debug.Log($"Posición {position} inválida: Sin línea de visión a {targetPoint.tag}.");
                    return false;
                }
            }
        }
        return true;
    }

    // --- Limpieza ---
    private void ClearExistingManagedEnemies()
    {
        if (enemyContainer == null) return;

        // Destruye todos los hijos del contenedor
        foreach (Transform child in enemyContainer)
        {
            // Podrías añadir un chequeo adicional si otros objetos pudieran
            // terminar accidentalmente dentro del contenedor
            if (child != null && child.GetComponent<EnemyBase>() != null) // Asegúrate que sea un enemigo
            {
                Destroy(child.gameObject);
            }
        }
         Debug.Log("Enemigos gestionados anteriores eliminados.");
    }

    // --- Gizmos para Visualización ---
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Dibuja las posiciones de los enemigos colocados
        Gizmos.color = Color.magenta;
        foreach (Vector3 pos in spawnedEnemyPositions)
        {
            Gizmos.DrawSphere(pos, 0.4f); // Esfera magenta donde se colocó un enemigo
        }

        // Dibuja información sobre las zonas (ya lo hace PlacementZone.cs)
        // Dibuja información sobre los puntos (ya lo hace StrategicPoint.cs)
    }
}