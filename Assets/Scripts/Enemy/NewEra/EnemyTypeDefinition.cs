using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Type", menuName = "Enemy Placement/Enemy Type Definition")]
public class EnemyTypeDefinition : ScriptableObject
{
    [Tooltip("Identificador único para este tipo de enemigo (ej. 'HUNTER', 'TANK').")]
    public string enemyTypeId;

    [Tooltip("El Prefab del enemigo que se instanciará.")]
    public GameObject enemyPrefab;

    [Tooltip("El perfil de IA (EnemyAIProfile) asociado a este enemigo. Opcional, pero útil si necesitas consultar datos de IA durante la colocación.")]
    public EnemyAIProfile aiProfile; // Asigna el profile (HunterProfile, TankProfile, etc.) aquí

    // Puedes añadir más datos relevantes para la colocación aquí si es necesario
    // public float placementRadius = 0.5f; // Radio para evitar solapamientos
    // public bool requireClearLineOfSight = false; // ¿Necesita línea de visión a algún punto?
}