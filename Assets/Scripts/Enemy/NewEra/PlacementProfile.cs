using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Placement Profile", menuName = "Enemy Placement/Placement Profile")]
public class PlacementProfile : ScriptableObject
{
    [Tooltip("Descripción del perfil (ej. 'Nivel 1 - Fácil', 'Bosque Emboscada').")]
    public string profileDescription = "Default Profile";

    [Tooltip("Lista de reglas de colocación que definen este perfil.")]
    public List<PlacementRule> placementRules = new List<PlacementRule>();

    [Header("Global Settings (Overrides)")]
    [Tooltip("Número máximo total de enemigos permitidos por este perfil (0 = sin límite global, se basa en la suma de las reglas).")]
    public int maxTotalEnemies = 0;

    [Tooltip("Distancia mínima global entre cualquier enemigo colocado por este perfil (se aplica además de las reglas individuales).")]
    public float globalMinDistanceBetweenEnemies = 1.0f;
}