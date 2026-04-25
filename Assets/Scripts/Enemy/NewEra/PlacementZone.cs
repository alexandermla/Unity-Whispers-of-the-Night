using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))] // Necesita un Collider para definir el área
public class PlacementZone : MonoBehaviour
{
    public enum ZoneType
    {
        Any,        // Zona genérica
        Patrol,     // Para enemigos que patrullan
        Guard,      // Para enemigos que defienden un punto
        Ambush,     // Para enemigos sigilosos o de ataque sorpresa (Shy?)
        HighTraffic // Zonas por donde probablemente pase el jugador
        // Añade más tipos según necesites
    }

    [Tooltip("Tipo estratégico de esta zona.")]
    public ZoneType zoneType = ZoneType.Any;

    [Tooltip("Prioridad de esta zona (ej. 10 = alta, 0 = normal).")]
    public int priority = 0;

    [Tooltip("Lista de IDs de EnemyTypeDefinition permitidos en esta zona (ej. 'HUNTER', 'TANK'). Dejar vacío para permitir cualquiera.")]
    public List<string> allowedEnemyTypeIds; // Usa los IDs que definiste en EnemyTypeDefinition

    private Collider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            // Asegurarse que sea Trigger para no colisionar físicamente
            // Debug.LogWarning($"Collider en PlacementZone '{gameObject.name}' no era Trigger. Cambiando a Trigger.", this);
            // zoneCollider.isTrigger = true; // Opcional: forzarlo o simplemente avisar
        }
    }

    // Método para obtener un punto aleatorio DENTRO de los límites de esta zona
    public Vector3 GetRandomPointInZone()
    {
        if (zoneCollider == null) return transform.position; // Fallback

        Bounds bounds = zoneCollider.bounds;
        // Genera un punto aleatorio dentro del AABB (Axis-Aligned Bounding Box)
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y, // Usar el centro Y como base inicial
            Random.Range(bounds.min.z, bounds.max.z)
        );
        // NavMesh.SamplePosition se usará después para encontrar el suelo
    }

    // --- Gizmo para Visualización ---
    void OnDrawGizmos()
    {
        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null) return;

        Gizmos.color = GetZoneGizmoColor(zoneType);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        // Usar la matriz del objeto para que el Gizmo escale y rote con el objeto
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

        if (zoneCollider is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size); // Dibuja el cubo usando el centro y tamaño locales
        }
        else if (zoneCollider is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius); // Dibuja la esfera usando el centro y radio locales
        }
        // Añadir más tipos de collider si los usas (Capsule, Mesh)

        Gizmos.matrix = oldMatrix; // Restaurar matriz
    }

    // Helper para color del Gizmo
    private Color GetZoneGizmoColor(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Guard: return new Color(1f, 0f, 0f, 0.3f); // Rojo
            case ZoneType.Patrol: return new Color(0f, 1f, 0f, 0.3f); // Verde
            case ZoneType.Ambush: return new Color(0.5f, 0f, 1f, 0.3f); // Púrpura
            case ZoneType.HighTraffic: return new Color(1f, 0.8f, 0f, 0.3f); // Naranja
            case ZoneType.Any:
            default: return new Color(0f, 0.5f, 1f, 0.3f); // Azul
        }
    }
}