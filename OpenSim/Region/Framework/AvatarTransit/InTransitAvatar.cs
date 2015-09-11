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
using System.Threading.Tasks;
using OpenMetaverse;
using System.Threading;

namespace OpenSim.Region.Framework.AvatarTransit
{
    /// <summary>
    /// Avatar that is currently in transit to this region or another
    /// </summary>
    public class InTransitAvatar
    {
        private AvatarTransitController _controller;
        private TransitArguments _transitArgs;
        private ITransitState _currentState;
        private List<uint> _rideOnPrims;
        private ManualResetEventSlim _releaseEvent;
        private ManualResetEventSlim _objectCreationEvent;
        private bool _objectCreationSuccess;

        /// <summary>
        /// The arguments that begun this avatar transit
        /// </summary>
        public TransitArguments TransitArgs
        {
            get
            {
                return _transitArgs;
            }
        }

        /// <summary>
        /// Convienience for TransitArgs.UserId
        /// </summary>
        public UUID UserId { get { return _transitArgs.UserId; } }

        /// <summary>
        /// For an outbound avatar, this will be set by the transit manager
        /// </summary>
        public Scenes.ScenePresence ScenePresence { get; set; }

        /// <summary>
        /// The localids of the prims that this user is riding on while crossing into/out of a region
        /// </summary>
        public IEnumerable<uint> RideOnPrims
        {
            get { return _rideOnPrims; }
        }


        public InTransitAvatar(AvatarTransitController controller, TransitArguments transit)
        {
            _controller = controller;
            _transitArgs = transit;

            //collect the IDs of the prims we're riding on
            if (transit.RideOnGroup != null)
            {
                var primIds = transit.RideOnGroup.GetParts().Select<Scenes.SceneObjectPart, uint>(
                    (Scenes.SceneObjectPart part) => { return part.LocalId; });

                _rideOnPrims = new List<uint>(primIds);
            }

            if (transit.Type == TransitType.OutboundCrossing ||
                transit.Type == TransitType.OutboundTeleport)
            {
                _releaseEvent = new ManualResetEventSlim();
            }

            if (transit.Type == TransitType.OutboundCrossing &&
                _transitArgs.RideOnPart != null)
            {
                _objectCreationEvent = new ManualResetEventSlim();
            }
        }

        /// <summary>
        /// Sets a new transit state for this avatar and calls its stateentry event
        /// </summary>
        /// <param name="transitState"></param>
        internal async Task SetNewState(ITransitState transitState)
        {
            _currentState = transitState;
            await transitState.StateEntry();
        }

        /// <summary>
        /// Called by a transit state's StateEntry to inform the avatar that its state has changed
        /// </summary>
        /// <param name="transitStage"></param>
        internal async Task TriggerOnTransitStageChanged(TransitStage transitStage, IEnumerable<uint> rideOnPrims)
        {
            await _controller.TriggerOnTransitStageChanged(this, transitStage, rideOnPrims);
        }

        /// <summary>
        /// Called when the avatar has been released by a destination region
        /// </summary>
        internal void AvatarReleased()
        {
            _releaseEvent.Set();
        }

        /// <summary>
        /// Waits for the destination region to release the agent in this region
        /// </summary>
        /// <returns></returns>
        internal Task WaitForRelease()
        {
            const int RELEASE_TIMEOUT = 10000;
            var task = new Task(() =>
                {
                    if (!_releaseEvent.Wait(RELEASE_TIMEOUT))
                    {
                        throw new SendStates.SendAvatarException(
                            String.Format("Timout waiting for avatar {0} to become root on destination",
                            ScenePresence.Name)); 
                    }
                }
            );

            task.Start();

            return task;
        }

        internal void Rollback()
        {
            _currentState.Rollback();
        }

        /// <summary>
        /// Waits for the prim we're riding on to be created
        /// </summary>
        /// <returns></returns>
        internal Task<bool> WaitForRemoteObjectCreation()
        {
            var task = new Task<bool>(() =>
            {
                _objectCreationEvent.Wait();
                _objectCreationEvent.Dispose();

                return _objectCreationSuccess;
            }
            );

            task.Start();

            return task;
        }

        /// <summary>
        /// Called when we know if the object we're sitting on for a crossing has been created
        /// on the remote region
        /// </summary>
        /// <param name="success"></param>
        internal void HandleRemoteObjectCreationResult(bool success)
        {
            _objectCreationSuccess = success;
            _objectCreationEvent.Set();
        }
    }
}
