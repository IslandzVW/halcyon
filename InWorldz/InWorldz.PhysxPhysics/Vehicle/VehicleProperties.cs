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
using ProtoBuf;
using OpenSim.Region.Physics.Manager.Vehicle;

namespace InWorldz.PhysxPhysics.Vehicle
{
    /// <summary>
    /// Properties that are persisted and passed on crossings for vehicle physics objects
    /// </summary>
    [ProtoContract]
    class VehicleProperties
    {
        /// <summary>
        /// The type of vehicle this is
        /// </summary>
        [ProtoMember(1)]
        public VehicleType Type;

        /// <summary>
        /// Flags specified for the vehicle that control behaviors
        /// </summary>
        [ProtoMember(2, DataFormat=DataFormat.TwosComplement)]
        public VehicleFlags Flags;

        /// <summary>
        /// Floating point type parameter values for this vehicle
        /// </summary>
        [ProtoMember(3)]
        public Dictionary<FloatParams, float> ParamsFloat;

        /// <summary>
        /// Rotation type parameter values for this vehicle
        /// </summary>
        [ProtoMember(4)]
        public Dictionary<RotationParams, OpenMetaverse.Quaternion> ParamsRot;

        /// <summary>
        /// Vector type parameter values for this vehicle
        /// </summary>
        [ProtoMember(5)]
        public Dictionary<VectorParams, OpenMetaverse.Vector3> ParamsVec;

        /// <summary>
        /// All of the dynamic values for this vehicle
        /// </summary>
        [ProtoMember(6)]
        public DynamicsSimulationData Dynamics;

        public VehicleProperties()
        {
            ParamsFloat = new Dictionary<FloatParams, float>();
            ParamsRot = new Dictionary<RotationParams, OpenMetaverse.Quaternion>();
            ParamsVec = new Dictionary<VectorParams, OpenMetaverse.Vector3>();
            // The simulation dynamics are created once (see CheckCreateVehicleProperties)
        }

        internal void Merge(VehicleProperties _props)
        {
            Type = _props.Type;
            Flags = _props.Flags;

            foreach (var item in _props.ParamsFloat)
                ParamsFloat[item.Key] = item.Value;

            foreach (var item in _props.ParamsVec)
                ParamsVec[item.Key] = item.Value;

            foreach (var item in _props.ParamsRot)
                ParamsRot[item.Key] = item.Value;

            // Copy the reference to the one simulation dynamics area.
            Dynamics = _props.Dynamics;
        }
    }
}
