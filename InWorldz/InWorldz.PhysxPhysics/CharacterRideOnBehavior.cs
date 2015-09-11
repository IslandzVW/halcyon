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

namespace InWorldz.PhysxPhysics
{
    class CharacterRideOnBehavior : PhysX.ControllerBehaviorCallback
    {
        /// <summary>
        /// The amount of time that a user must stay standing on top of an object before
        /// </summary>
        private const uint MIN_TIME_TO_STICK_TO_OBJECT = 1000;

        private uint _timeBeganStandingOnPrim;
        private PhysxPrim _standingOnPrim;

        public void AvatarStandingOn(PhysxPrim prim)
        {
            if (_standingOnPrim != prim)
            {
                _timeBeganStandingOnPrim = (uint)Environment.TickCount;
                _standingOnPrim = prim;
            }
        }

        public void AvatarNotStandingOnPrim()
        {
            _standingOnPrim = null;
            _timeBeganStandingOnPrim = 0;
        }

        internal void AvatarKicked(PhysxPrim physxPrim)
        {
            if (_standingOnPrim == physxPrim)
            {
                AvatarNotStandingOnPrim();
            }
        }

        public override PhysX.ControllerBehaviorFlag GetBehaviorFlags(PhysX.Shape shape)
        {
            if (IsRideOnPrim(shape))
            {
                return PhysX.ControllerBehaviorFlag.CctCanRideOnObject;
            }
            else
            {
                return 0;
            }
        }

        private bool IsRideOnPrim(PhysX.Shape shape)
        {
            if (_standingOnPrim == null)
            {
                return false;
            }

            PhysxPrim shapePrim = shape.Actor.UserData as PhysxPrim;

            return IsRideOnPrim(shapePrim);
        }

        public bool IsRideOnPrim(PhysxPrim shapePrim)
        {
            if (shapePrim == _standingOnPrim && (uint)Environment.TickCount - _timeBeganStandingOnPrim > MIN_TIME_TO_STICK_TO_OBJECT)
            {
                return true;
            }

            return false;
        }

        public override PhysX.ControllerBehaviorFlag GetBehaviorFlags(PhysX.Controller controller)
        {
            return 0;
        }
    }
}
