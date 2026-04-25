// Archivo: Scripts/Enemy/NewEra/EnemyAIProfile.cs
using UnityEngine;
using UnityEngine.AI; // Necesario para NavMeshAgent settings si los incluyes

// Esto permite crear instancias de este objeto desde el menú Assets de Unity
[CreateAssetMenu(fileName = "New Enemy Profile", menuName = "Enemy Profiles/AI Profile")]
public class EnemyAIProfile : ScriptableObject
{
    [Header("Identification")]
    public string enemyType = "Default"; // Ej. "Hunter", "Tank", "Shy"

    [Header("Base Stats (from EnemyBase)")]
    [Tooltip("Tiempo en segundos de exposición a la luz directa para neutralizar al enemigo.")]
    public float exposureTimeToKill = 3.5f;
    [Tooltip("Material base opcional. Si no se asigna, usará el de disolución o el del renderer.")]
    public Material baseMaterial;
    [Tooltip("Material opcional para cuando el enemigo está fijado por el jugador.")]
    public Material lockedOnMaterial;
    [Tooltip("Color de emisión (HDR) cuando el enemigo está inactivo o patrullando.")]
    [ColorUsage(true, true)] public Color idleEmissionColor = Color.black;
    [Tooltip("Color de emisión (HDR) cuando el enemigo está alerta, persiguiendo o atacando.")]
    [ColorUsage(true, true)] public Color alertEmissionColor = new Color(1f, 0.1f, 0.1f) * 2.0f;

    [Header("Movement & Navigation")]
    [Tooltip("Velocidad de rotación hacia el objetivo.")]
    public float rotationSpeed = 10f;
    [Tooltip("Velocidad al patrullar o buscar (si no está persiguiendo activamente).")]
    public float wanderSpeed = 1.5f;
    [Tooltip("Velocidad al perseguir al jugador.")]
    public float chaseSpeed = 3.5f;
    // Opcional: Añadir aquí configuraciones de NavMeshAgent si varían mucho
    // public float agentAcceleration = 8f;
    // public float agentAngularSpeed = 120f;
    // public float agentStoppingDistanceWander = 0.1f;

    [Header("Detection & Targeting")]
    [Tooltip("Ángulo de visión frontal del enemigo (en grados).")]
    public float fieldOfView = 120f;
    [Tooltip("Distancia a la que detecta al jugador si este está de pie o caminando.")]
    public float standingAgroRange = 8f;
    [Tooltip("Distancia a la que detecta al jugador si este está agachado.")]
    public float crouchAgroRange = 4f;
    [Tooltip("Distancia a la que detecta al jugador si este tiene la linterna encendida o corre.")]
    public float lightAgroRange = 15f;
    [Tooltip("Multiplicador sobre el rango de agro actual para determinar cuándo pierde el objetivo si no lo ve.")]
    public float loseAgroDistanceMultiplier = 1.5f;
    [Tooltip("Tiempo (en segundos) que el enemigo busca en la última posición conocida del jugador antes de volver a patrullar.")]
    public float searchDuration = 4.0f;
    [Tooltip("Distancia máxima del punto central de su área a la que puede alejarse persiguiendo.")]
    public float maxDistanceFromCenter = 25f;

    [Header("Combat")]
    [Tooltip("Distancia a la que el enemigo puede iniciar un ataque cuerpo a cuerpo.")]
    public float attackRange = 1.8f;
    [Tooltip("Tiempo (en segundos) de espera entre ataques.")]
    public float attackCooldown = 2.0f;
    [Tooltip("Ángulo máximo (en grados) respecto al jugador para poder iniciar un ataque.")]
    public float attackAngleThreshold = 30f;
    [Tooltip("Daño que inflige el ataque (si aplica).")]
    public float attackDamage = 10f;
    [Tooltip("Sonido que se reproduce al atacar.")]
    public AudioClip attackSound;
    // public GameObject attackHitboxPrefab; // Si el hitbox varía por perfil

    [Header("Light Damage Audio")] // <--- NUEVA SECCIÓN
    [Tooltip("El clip de audio que suena mientras el enemigo recibe daño de la luz (ej. sonido de quemado/siseo).")]
    public AudioClip lightDamageSoundClip;
    [Tooltip("Volumen máximo que alcanzará este sonido justo antes de que el enemigo sea neutralizado (0 a 1).")]
    [Range(0f, 1f)]
    public float lightDamageMaxVolume = 0.8f;

    [Header("Specific Behaviors (Defaults or placeholders)")]
    [Tooltip("Parámetros específicos para el enemigo 'Thrower'.")]
    public ThrowerSpecifics throwerSettings;
    [Tooltip("Parámetros específicos para el enemigo 'Screamer'.")]
    public ScreamerSpecifics screamerSettings;
    [Tooltip("Parámetros específicos para el enemigo 'Shy'.")]
    public ShySpecifics shySettings;
    // Añade aquí más structs para otros tipos si tienen muchos parámetros únicos

}

// --- Structs para parámetros específicos (opcional pero organizado) ---
[System.Serializable]
public struct ThrowerSpecifics
{
    public float preferredMinDistance;
    public float preferredMaxDistance;
    public float fleeDistanceThreshold;
    public float fleeSpeedMultiplier;
    public GameObject projectilePrefab;
    public float projectileSpeed;
    public float rangedAttackCooldown;
    public float rangedAttackAngleThreshold;
    public float rangedAttackWindUpTime;
    public AudioClip throwSound;
}

[System.Serializable]
public struct ScreamerSpecifics
{
    public float detectionRadius;
    public float fieldOfView; // Puede ser diferente al general
    public float alertRadius;
    public float alertDuration;
    public float screamCooldown;
    public AudioClip screamSound;
    // public ParticleSystem screamVFXPrefab; // Es mejor dejar los VFX en el prefab del enemigo
}

[System.Serializable]
public struct ShySpecifics
{
    public float detectionRadius;
    public float fleeDistance;
    public float fleeSpeed; // Renombrado desde shyFleeSpeed para consistencia
    public float timeToCalmDown;
}