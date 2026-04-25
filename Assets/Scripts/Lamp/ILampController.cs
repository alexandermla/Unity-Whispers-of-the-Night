using UnityEngine;

namespace WoN.Interfaces
{
    /// <summary>
    /// Interface for controlling a lamp or flashlight system
    /// </summary>
    public interface ILampController
    {
        /// <summary>
        /// Gets whether the lamp is currently turned on
        /// </summary>
        bool IsLampOn { get; }
        
        /// <summary>
        /// Gets the current energy level of the lamp (0-1)
        /// </summary>
        float CurrentEnergyPercentage { get; }
        
        /// <summary>
        /// Gets the maximum energy capacity of the lamp
        /// </summary>
        float GetMaxEnergy();
        
        /// <summary>
        /// Gets the maximum distance the light reaches
        /// </summary>
        float GetMaxLightDistance();
        
        /// <summary>
        /// Gets the angle of the flashlight beam
        /// </summary>
        float GetFlashlightAngle();
        
        /// <summary>
        /// Sets the lamp to active or inactive
        /// </summary>
        /// <param name="active">Whether the lamp should be active</param>
        void SetLampActive(bool active);
        
        /// <summary>
        /// Sets the visibility of the lamp's energy UI
        /// </summary>
        /// <param name="visible">Whether the energy UI should be visible</param>
        void SetEnergyUIVisible(bool visible);
    }
}