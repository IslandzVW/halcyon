/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public enum WindConstants : int
    {
        WindSpeedDefault = 0,   // LSL WIND_SPEED_DEFAULT
        WindSpeedFixed   = 1,   // LSL WIND_SPEED_FIXED
        WindSpeedTurbulence = 2,    // LSL WIND_SPEED_TERRAIN_TURBULENCE
    }
    public interface IWindModule : IRegionModule
    {
        
        /// <summary>
        /// Retrieves the current wind speed at the given Region Coordinates
        /// </summary>
        Vector3 WindSpeed(int x, int y, int z);

        /// <summary>
        /// Sets (or unsets) the current wind speed at the given Region Coordinates
        /// </summary>
        void WindSet(int type, Vector3 loc, Vector3 speed);

        /// <summary>
        /// Set Wind Plugin Parameter
        /// </summary>
        void WindParamSet(string plugin, string param, float value);

        /// <summary>
        /// Get Wind Plugin Parameter
        /// </summary>
        float WindParamGet(string plugin, string param);

        /// <summary>
        /// Current active wind model plugin or String.Empty
        /// </summary>
        string WindActiveModelPluginName { get; }
    }
}
