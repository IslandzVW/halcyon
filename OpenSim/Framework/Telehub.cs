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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenSim.Framework
{
    public class Telehub
    {
        /// <summary>
        ///     Name of the teleHUB object
        /// </summary>
        public string Name = String.Empty;

        /// <summary>
        ///     UUID of the teleHUB object
        /// </summary>
        public UUID ObjectUUID = UUID.Zero;

        /// <summary>
        ///     Region UUID
        /// </summary>
        public UUID RegionID = UUID.Zero;

        /// <summary>
        ///     Positions users will spawn at in order of creation
        /// </summary>
        public List<Vector3> SpawnPos = new List<Vector3>();

        /// <summary>
        ///     Position of the telehub in the region
        /// </summary>
        public Vector3 TelehubLoc;

        /// <summary>
        ///     Rotation of the av
        /// </summary>
        public Quaternion TelehubRot;

        public string BuildFromList(List<Vector3> SpawnPos)
        {
            return SpawnPos.Aggregate(String.Empty, (current, Pos) => current + (Pos.ToString() + "\n"));
        }

        public static List<Vector3> BuildToList(string SpawnPos)
        {
            if (String.IsNullOrWhiteSpace(SpawnPos))
                return new List<Vector3>();
            return (from Pos in SpawnPos.Split('\n') where !String.IsNullOrEmpty(Pos) select Vector3.Parse(Pos)).ToList();
        }
    }
}
