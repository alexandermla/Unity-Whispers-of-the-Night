using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Necesario para ToList

// Sistema de eventos estático para las estatuas
public static class StatueEvents
{
    // Delegado para el evento de estatua recolectada
    public delegate void StatueCollectedEventHandler(string statueId, Vector3 position);

    // Evento estático de estatua recolectada
    public static event StatueCollectedEventHandler OnStatueCollected;

    // Evento estático de todas las estatuas recolectadas
    public static event Action OnAllStatuesCollected;

    // --- Modificado: Usar array de IDs fijos ---
    private static readonly string[] statueIds = { "BEAR", "MOUSE", "DOG", "RACCOON" };
    // Usar HashSet para búsqueda rápida de las recolectadas
    private static HashSet<string> collectedStatues = new HashSet<string>();
    // ------------------------------------------

    // Total de estatuas requeridas
    private static readonly int TOTAL_STATUES = statueIds.Length; // Basado en el array

    // Método para notificar recolección
    public static void StatueCollected(string statueId, Vector3 position)
    {
        // Validar ID antes de añadir
        if (string.IsNullOrEmpty(statueId) || !statueIds.Contains(statueId) || collectedStatues.Contains(statueId))
        {
            //Debug.LogWarning($"[StatueEvents] Intento de recolectar estatua inválida o ya recolectada: {statueId}");
            return;
        }

        // Añadir a recolectadas
        collectedStatues.Add(statueId);

        Debug.Log($"[StatueEvents] Estatua recolectada: {statueId}. Total: {collectedStatues.Count}/{TOTAL_STATUES}");

        // Disparar evento
        OnStatueCollected?.Invoke(statueId, position);

        // Verificar si se recolectaron todas
        if (collectedStatues.Count >= TOTAL_STATUES)
        {
            Debug.Log("[StatueEvents] ¡Todas las estatuas han sido recolectadas!");
            OnAllStatuesCollected?.Invoke();
        }
    }

    // Método para resetear (útil para pruebas)
    public static void ResetStatues()
    {
        collectedStatues.Clear();
        Debug.Log("[StatueEvents] Reseteo de todas las estatuas");
    }

    // Método para consultar una estatua específica
    public static bool IsStatueCollected(string statueId)
    {
        // Validar ID
        if (string.IsNullOrEmpty(statueId) || !statueIds.Contains(statueId)) return false;
        return collectedStatues.Contains(statueId);
    }

    // --- NUEVO: Método para cargar estado ---
    /// <summary>
    /// Carga el estado de las estatuas recolectadas desde un array booleano.
    /// El orden del array debe coincidir con el orden en statueIds.
    /// </summary>
    /// <param name="collectedArray">Array booleano indicando qué estatuas están recolectadas.</param>
    public static void LoadState(bool[] collectedArray)
    {
        collectedStatues.Clear(); // Limpiar estado actual
        if (collectedArray == null || collectedArray.Length != TOTAL_STATUES)
        {
            Debug.LogError("[StatueEvents] Error al cargar estado: El array es nulo o no tiene la longitud correcta.");
            return;
        }

        for (int i = 0; i < TOTAL_STATUES; i++)
        {
            if (collectedArray[i])
            {
                collectedStatues.Add(statueIds[i]);
            }
        }
        Debug.Log($"[StatueEvents] Estado cargado. Estatuas recolectadas: {collectedStatues.Count}/{TOTAL_STATUES}");

         // Verificar si todas están recolectadas después de cargar
         if (collectedStatues.Count >= TOTAL_STATUES)
         {
             // Disparar evento si no se había disparado ya (útil si se carga un save completo)
             // Podríamos necesitar un flag adicional para evitar disparos múltiples si se carga varias veces.
             // Por ahora, asumimos que LoadState se llama una vez al cargar el juego.
              Debug.Log("[StatueEvents] Todas las estatuas estaban recolectadas en el archivo cargado.");
             // OnAllStatuesCollected?.Invoke(); // Opcional: disparar aquí si es necesario
         }
    }
    // --- FIN NUEVO ---

    // Método para obtener total recolectado
    public static int GetCollectedCount()
    {
        return collectedStatues.Count;
    }

    // Método para obtener lista de estatuas recolectadas
    public static string[] GetCollectedStatues()
    {
        // Convertir HashSet a array
        return collectedStatues.ToArray();
    }
}