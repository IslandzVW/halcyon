/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenSim.Region.Physics.Manager
{
    /// <summary>
    /// What changes should be applied from the given description
    /// </summary>
    [Flags]
    public enum MaterialChanges
    {
        None = 0,
        Density = 1,
        Friction = 2,
        Restitution = 4,
        GravityMultiplier = 8,
        All = Density | Friction | Restitution | GravityMultiplier
    }

    /// <summary>
    /// A physics material that a prim can be made up of
    /// </summary>
    public interface IMaterial
    {
        /// <summary>
        /// Indicates the OMV material number the given material is a preset/static 
        /// material. Otherwise set to -1 if this is a custom material
        /// </summary>
        int MaterialPreset { get; }

        /// <summary>
        /// The density of the material in kg/m^3
        /// </summary>
        float Density { get; }

        /// <summary>
        /// Static friction
        /// </summary>
        float StaticFriction { get; }
        /// <summary>
        /// Dynamic friction
        /// </summary>
        float DynamicFriction { get; }
        /// <summary>
        /// Restitution (bounce)
        /// </summary>
        float Restitution { get; }
        /// <summary>
        /// Gravity Multiplier
        /// </summary>
        float GravityMultiplier { get; }
    }
}
