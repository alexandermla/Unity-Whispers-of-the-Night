using UnityEngine;

public class StrategicPoint : MonoBehaviour
{
    // Usa el Tag del GameObject para identificar el tipo de punto
    // (ej. "Fountain", "Statue", "PlayerStart", "Objective")

    [Tooltip("Prioridad o importancia de este punto (0 = normal).")]
    public int priority = 0;

    // --- Gizmo para Visualización ---
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f); // Esfera pequeña amarilla
        // Opcional: Mostrar el Tag como texto
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"Tag: {gameObject.tag}\nPrio: {priority}");
        #endif
    }
}