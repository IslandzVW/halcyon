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
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using log4net;
using System.Reflection;

namespace OpenSim.Region.Framework.AvatarTransit
{
    /// <summary>
    /// Class in charge of avatars moving between regions
    /// </summary>
    public class AvatarTransitController
    {
        private static readonly ILog _log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Delegate for state changes
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="newStage"></param>
        public delegate Task TransitStateChangedDelegate(ScenePresence sp, TransitStage newStage, IEnumerable<uint> rideOnPrims);

        /// <summary>
        /// Event called when the transit state of any avatar changes
        /// </summary>
        public event TransitStateChangedDelegate OnTransitStateChanged;


        private Scene _scene;
        private Dictionary<UUID, InTransitAvatar> _inTransitAvatars = new Dictionary<UUID, InTransitAvatar>();


        public AvatarTransitController(Scene scene)
        {
            _scene = scene;
        }

        /// <summary>
        /// Returns whether or not the given avatar is in transit to a new region
        /// </summary>
        /// <param name="avatarId"></param>
        /// <returns></returns>
        public bool AvatarIsInTransit(UUID avatarId)
        {
            lock (_inTransitAvatars)
            {
                return _inTransitAvatars.ContainsKey(avatarId);
            }
        }

        /// <summary>
        /// Returns whether or not the given avatar is in transit to a new region riding on a prim
        /// </summary>
        /// <param name="avatarId"></param>
        /// <returns></returns>
        public bool AvatarIsInTransitOnPrim(UUID avatarId)
        {
            lock (_inTransitAvatars)
            {
                InTransitAvatar av;
                if (_inTransitAvatars.TryGetValue(avatarId, out av))
                {
                    if (av.TransitArgs.RideOnPart != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to begin a transit from or to another region
        /// </summary>
        /// <param name="transit"></param>
        /// <returns>Whether or not the transit was started, or one was already in progress</returns>
        public async Task TryBeginTransit(TransitArguments transit)
        {
            InTransitAvatar transitAvatar;
            lock (_inTransitAvatars)
            {
                if (_inTransitAvatars.TryGetValue(transit.UserId, out transitAvatar))
                {
                    throw new AvatarTransitException("Avatar is already in transit");
                }

                transitAvatar = new InTransitAvatar(this, transit);
                _inTransitAvatars.Add(transit.UserId, transitAvatar);
            }

            if (transit.Type == TransitType.OutboundTeleport || transit.Type == TransitType.OutboundCrossing)
            {
                await DoBeginOutboundTransit(transitAvatar);
            }
        }


        /// <summary>
        /// Stores the last error that we saw so that repeat errors dont spam the console
        /// </summary>
        private Tuple<UUID, string> _lastError;

        private async Task DoBeginOutboundTransit(InTransitAvatar transitAvatar)
        {
            Exception transitError = null;
            try
            {
                await BeginOutboundTransit(transitAvatar);
            }
            catch (Exception e)
            {
                var last = _lastError;
                if (last != null && (last.Item1 != transitAvatar.UserId || e.Message != last.Item2))
                {
                    _log.ErrorFormat("[TRANSITCONTROLLER]: Error while sending avatar {0}. {1}", transitAvatar.UserId, e);
                    _lastError = Tuple.Create(transitAvatar.UserId, e.Message);
                }

                try
                {
                    transitAvatar.Rollback();
                }
                catch (Exception er)
                {
                    _log.ErrorFormat("[TRANSITCONTROLLER]: Error while performing rollback of avatar that failed transit. {0}", er);
                }

                transitError = e;
            }

            if (transitError != null)
            {
                try
                {
                    await transitAvatar.TriggerOnTransitStageChanged(TransitStage.SendError, transitAvatar.RideOnPrims);
                }
                catch (Exception e)
                {
                    _log.ErrorFormat("[TRANSITCONTROLLER]: Error while triggering TransitStage.SendError. {0}", e);
                }

                RemoveInTransitAvatar(transitAvatar);

                throw new AvatarTransitException(transitError.Message, transitError);
            }
            else
            {
                try
                {
                    await transitAvatar.TriggerOnTransitStageChanged(TransitStage.SendCompletedSuccess, transitAvatar.RideOnPrims);
                }
                catch (Exception e)
                {
                    _log.ErrorFormat("[TRANSITCONTROLLER]: Error while triggering TransitStage.SendCompletedSuccess. {0}", e);
                }

                RemoveInTransitAvatar(transitAvatar);
            }
        }

        private void RemoveInTransitAvatar(InTransitAvatar transitAvatar)
        {
            lock (_inTransitAvatars)
            {
                _inTransitAvatars.Remove(transitAvatar.UserId);
            }
        }

        /// <summary>
        /// Begins a transit to another region
        /// </summary>
        /// <param name="transit"></param>
        /// <param name="transitAvatar"></param>
        private Task BeginOutboundTransit(InTransitAvatar transitAvatar)
        {
            transitAvatar.ScenePresence = _scene.GetScenePresence(transitAvatar.UserId);
            if (transitAvatar.ScenePresence == null)
            {
                throw new InvalidOperationException(String.Format("Avatar {0} is not in the scene and can not be put in transit", transitAvatar.UserId));
            }

            return transitAvatar.SetNewState(new SendStates.TransitBeginState(transitAvatar));
        }

        /// <summary>
        /// Called by an intransit avatar when its transit stage has changed
        /// </summary>
        /// <param name="inTransitAvatar"></param>
        /// <param name="transitStage"></param>
        internal async Task TriggerOnTransitStageChanged(InTransitAvatar inTransitAvatar, TransitStage transitStage, 
            IEnumerable<uint> rideOnPrims)
        {
            var tscDelegate = this.OnTransitStateChanged;
            if (tscDelegate != null)
            {
                await tscDelegate(inTransitAvatar.ScenePresence, transitStage, rideOnPrims);
            }
        }

        /// <summary>
        /// Called when an avatar has successfully been sent to a neighbor region
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal bool HandleReleaseAgent(UUID id)
        {
            lock (_inTransitAvatars)
            {
                InTransitAvatar transitAvatar;
                if (_inTransitAvatars.TryGetValue(id, out transitAvatar))
                {
                    transitAvatar.AvatarReleased();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Called when a object that is being sat on has completed a transfer
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="success"></param>
        internal void HandleObjectSendResult(UUID userId, bool success)
        {
            lock (_inTransitAvatars)
            {
                InTransitAvatar transitAvatar;
                if (_inTransitAvatars.TryGetValue(userId, out transitAvatar))
                {
                    transitAvatar.HandleRemoteObjectCreationResult(success);
                }
            }
        }
    }
}
