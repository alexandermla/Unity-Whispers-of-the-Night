// ILockOnVisuals.cs
using UnityEngine;

/// <summary>
/// Interfaz para enemigos u objetos que pueden cambiar su apariencia visual al ser fijados.
/// </summary>
public interface ILockOnVisuals
{
    /// <summary>
    /// Actualiza el material del objeto para indicar el estado de Lock-On.
    /// </summary>
    /// <param name="isLockedOn">True si está fijado, false si no.</param>
    void SetLockOnMaterial(bool isLockedOn);
}