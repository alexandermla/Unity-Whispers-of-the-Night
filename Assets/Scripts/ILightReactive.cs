using UnityEngine;

namespace WoN.Interfaces
{
    /// <summary>
    /// Interface for objects that react to light, such as enemies or interactive elements
    /// </summary>
    public interface ILightReactive
    {
        /// <summary>
        /// Called when the object is exposed to light from a light source
        /// </summary>
        /// <param name="lightSource">The transform of the light source</param>
        /// <param name="intensity">The intensity of the light exposure (0-1)</param>
        void OnLightExposure(Transform lightSource = null, float intensity = 1.0f);
    }
}