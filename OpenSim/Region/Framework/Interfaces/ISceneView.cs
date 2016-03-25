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

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ISceneViewModule
    {
        /// <summary>
        /// Is culling enabled?
        /// </summary>
        bool UseCulling { get; set; }

        /// <summary>
        /// Create a scene view for the given presence
        /// </summary>
        /// <param name="presence"></param>
        /// <returns></returns>
        ISceneView CreateSceneView(ScenePresence presence);
    }

    public interface ISceneView
    {
        /// <summary>
        /// Is culling enabled?
        /// </summary>
        bool UseCulling { get; set; }

        /// <summary>
        /// The distance before culling checks will be run again
        /// </summary>
        float DistanceBeforeCullingRequired { get; set; }

        /// <summary>
        /// Check to see whether any entities have now entered the agent's draw distance
        /// (Only works when UseCulling is enabled)
        /// </summary>
        void CheckForDistantEntitiesToShow();

        /// <summary>
        /// Checks whether any attachments have changed for the given presence, and whether they should be sent to our agent
        /// </summary>
        /// <param name="presence"></param>
        void CheckWhetherAttachmentsHaveChanged(ScenePresence presence);

        /// <summary>
        /// Send updates to the client about prims which have been placed on the update queue.  We don't
        /// necessarily send updates for all the parts on the queue, e.g. if an updates with a more recent
        /// timestamp has already been sent.
        /// 
        /// SHOULD ONLY BE CALLED WITHIN THE SCENE LOOP
        /// </summary>
        void SendPrimUpdates();

        /// <summary>
        /// Add the part to the queue of parts for which we need to send an update to the client
        /// 
        /// THIS METHOD SHOULD ONLY BE CALLED FROM WITHIN THE SCENE LOOP!!
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updateFlags"></param>
        void QueuePartForUpdate(SceneObjectPart sceneObjectPart, PrimUpdateFlags updateFlags);

        /// <summary>
        /// Sends a full part update to this client
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updateFlags"></param>
        void SendPartUpdate(SceneObjectPart part, PrimUpdateFlags updateFlags);

        /// <summary>
        /// Sends a full group update to this client
        /// </summary>
        /// <param name="sceneObjectGroup"></param>
        /// <param name="updateFlags"></param>
        void SendGroupUpdate(SceneObjectGroup sceneObjectGroup, PrimUpdateFlags updateFlags);

        /// <summary>
        /// Informs the SceneView that the given patch has been modified and must be resent
        /// </summary>
        /// <param name="serialized"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void TerrainPatchUpdated(float[] serialized, int x, int y);

        /// <summary>
        /// Whether the client requires the entire scene to be sent to them once they have fully loaded
        /// </summary>
        bool NeedsFullSceneUpdate { get; set; }

        /// <summary>
        /// Sends kill packets if the given object is within the draw distance of the avatar
        /// </summary>
        /// <param name="grp"></param>
        /// <param name="localIds"></param>
        void SendKillObjects(SceneObjectGroup grp, List<uint> localIds);

        /// <summary>
        /// Tell *ALL* agents about this agent
        /// </summary>
        void SendInitialFullUpdateToAllClients();

        /// <summary>
        /// Sends a full update for our client to all clients in the scene
        /// </summary>
        void SendFullUpdateToAllClients();

        /// <summary>
        /// Tell other client about this avatar (The client previously didn't know or had outdated details about this avatar)
        /// </summary>
        /// <param name="remoteAvatar"></param>
        void SendFullUpdateToOtherClient(ScenePresence m_presence);

        /// <summary>
        /// Tell our client about other client's avatar (includes appearance and full object update)
        /// </summary>
        /// <param name="m_presence"></param>
        void SendPresenceToUs(ScenePresence otherClient);

        /// <summary>
        /// Removes this presence from the currently seen list
        /// </summary>
        /// <param name="presence"></param>
        void ClearFromScene(ScenePresence presence);

        /// <summary>
        /// Clears all presences from the currently seen list
        /// </summary>
        void ClearScene();

        /// <summary>
        /// Clears all presence and tracking information for this scene view
        /// </summary>
        void ClearAllTracking();

        /// <summary>
        /// Send a terse update for an avatar if they are within draw distance
        /// </summary>
        /// <param name="scenePresence"></param>
        void SendAvatarTerseUpdate(ScenePresence scenePresence);
    }
}
