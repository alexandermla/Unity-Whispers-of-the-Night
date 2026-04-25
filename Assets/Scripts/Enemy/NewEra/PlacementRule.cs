using UnityEngine;
using System.Collections.Generic;

[System.Serializable] // Para que se muestre en el Inspector dentro de PlacementProfile
public class PlacementRule
{
    [Tooltip("Descripción de la regla para fácil identificación.")]
    public string ruleDescription = "New Rule";

    [Tooltip("El tipo de enemigo al que aplica esta regla.")]
    public EnemyTypeDefinition enemyType;

    [Tooltip("Cuántos enemigos de este tipo colocar usando esta regla.")]
    public int count = 1;

    [Header("Targeting Strategy")]
    [Tooltip("Tipo de zona donde preferentemente se aplicará esta regla.")]
    public PlacementZone.ZoneType targetZoneType = PlacementZone.ZoneType.Any; // Usa el Enum de PlacementZone

    [Tooltip("Tag de un StrategicPoint específico al que apuntar (ej. 'Fountain', 'Statue', 'PlayerStart'). Dejar vacío para no apuntar a uno específico.")]
    public string targetStrategicPointTag = "";

    [Tooltip("Distancia mínima deseada al punto estratégico (si se especificó uno).")]
    public float minDistanceFromStrategicPoint = 0f;
    [Tooltip("Distancia máxima deseada al punto estratégico (si se especificó uno).")]
    public float maxDistanceFromStrategicPoint = 10f;

    [Header("Constraints")]
    [Tooltip("Distancia mínima a mantener de *otros* enemigos ya colocados.")]
    public float minDistanceToOtherEnemies = 2.0f;

    [Tooltip("¿Requiere línea de visión directa al punto estratégico seleccionado? (Costoso)")]
    public bool requireLineOfSightToTarget = false;

    [Tooltip("Prioridad de esta regla (más alta se procesa antes).")]
    public int priority = 0; // Opcional para ordenar reglas
}