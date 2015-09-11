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
using OpenMetaverse;

namespace OpenSim.Region.Physics.Manager
{
    /// <summary>
    /// Stores prim specific persistent physics properties
    /// </summary>
    public interface IPhysicsProperties
    {
        /// <summary>
        /// If set, this object wants to know about collisions from the SDK
        /// </summary>
        bool WantsCollisionNotification { get; set; }

        /// <summary>
        /// If set, child prims of this object want to know about collisions from the SDK 
        /// </summary>
        bool ChildrenWantCollisionNotification { get; set; }

        /// <summary>
        /// If set, any collisions that are sent to this prim get forwarded to the
        /// root instead (true by default)
        /// </summary>
        bool PassCollisions { get; set; }

        /// <summary>
        /// Sets a continuous force that is applied to this object
        /// </summary>
        Vector3 Force { get; set; }
        /// <summary>
        /// Sets whether or not the force is applied in the local or global frame
        /// </summary>
        bool ForceIsLocal { get; set; }

        /// <summary>
        /// Sets a continuous rotational force that is applied to this object
        /// </summary>
        Vector3 Torque { get; set; }
        /// <summary>
        /// Sets whether or not the rotational force is applied in the local or global frame
        /// </summary>
        bool TorqueIsLocal { get; set; }

        /// <summary>
        /// Sets this object's buoyancy against gravity
        /// </summary>
        float Buoyancy { get; set; }

        /// <summary>
        /// Sets this object's material
        /// </summary>
        IMaterial Material { get; set; }

        /// <summary>
        /// The target position this object is moving towards
        /// </summary>
        OpenMetaverse.Vector3 MoveTarget { get; set; }

        /// <summary>
        /// Time in seconds to try and achieve a completed move to the given MoveTarget
        /// </summary>
        float MoveTargetTau { get; set; }


        /// <summary>
        /// Target rotation set by llRotLookAt
        /// </summary>
        OpenMetaverse.Quaternion RotLookTarget { get; set; }

        /// <summary>
        /// This seems to act mostly like a damping strength
        /// </summary>
        float RotLookStrength { get; set; }

        /// <summary>
        /// This seems to act mostly like a tau time to reach the target
        /// </summary>
        float RotLookDamping { get; set; }


        /// <summary>
        /// Hover type set by llGroundRepel or llSetHoverHeight
        /// </summary>
        PIDHoverFlag HoverType { get; set; }

        /// <summary>
        /// Height at which to hover above the ground or water
        /// </summary>
        float HoverHeight { get; set; }

        /// <summary>
        /// Time to take to reach the requested hover height
        /// </summary>
        float HoverTau { get; set; }

        /// <summary>
        /// Damping factor
        /// </summary>
        float HoverDamping { get; set; } 


        /// <summary>
        /// Angular velocity target. Used by llTargetOmega
        /// </summary>
        OpenMetaverse.Vector3 AngularVelocityTarget { get; set; }


        /// <summary>
        /// Is this group using volumedetect
        /// </summary>
        bool VolumeDetectActive { get; set; }

        /// <summary>
        /// The current region coords of the grabbed object
        /// </summary>
        OpenMetaverse.Vector3 GrabTarget { get; set; }

        /// <summary>
        /// The grabbed object's tau (time to move to the target)
        /// </summary>
        float GrabTargetTau { get; set; }

        /// <summary>
        /// Specifies whether object grabbing is blocked
        /// </summary>
        bool BlockGrab { get; set; }

        /// <summary>
        /// Specifies which of the respective rotation axes are locked
        /// </summary>
        OpenMetaverse.Vector3 LockedAxes { get; set; }
    }
}
