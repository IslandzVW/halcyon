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

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.Lure
{
    public class LureModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Scene> m_scenes = new List<Scene>();

        private IMessageTransferModule m_TransferModule = null;

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                if (config.Configs["Messaging"].GetString(
                        "LureModule", "LureModule") !=
                        "LureModule")
                    return;
            }

            lock (m_scenes)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnIncomingInstantMessage +=
                            OnGridInstantMessage;
                }
            }
        }

        void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
            client.OnStartLure += OnStartLure;
            client.OnTeleportLureRequest += OnTeleportLureRequest;
        }

        public void PostInitialize()
        {
            m_TransferModule =
                m_scenes[0].RequestModuleInterface<IMessageTransferModule>();

            if (m_TransferModule == null)
                m_log.Error("[INSTANT MESSAGE]: No message transfer module, "+
                "lures will not work!");
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LureModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
        }

        public void OnStartLure(byte lureType, string message, UUID targetid, IClientAPI client)
        {
            if (!(client.Scene is Scene))
                return;

            Scene scene = (Scene)(client.Scene);
            ScenePresence presence = scene.GetScenePresence(client.AgentId);
            Vector3 pos = presence.AbsolutePosition;
            UUID dest = Util.BuildFakeParcelID(
                    scene.RegionInfo.RegionHandle,
                    (uint)pos.X, (uint)pos.Y, (uint)pos.Z);

            m_log.DebugFormat("TP invite with message {0}", message);

            GridInstantMessage m = new GridInstantMessage(scene, client.AgentId,
                    client.Name, targetid,
                    (byte)InstantMessageDialog.RequestTeleport, false,
                    message, dest, false, presence.AbsolutePosition,
                    new Byte[0]);
                    
            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(m,
                    delegate(bool success) { });
            }
        }

        public void OnTeleportLureRequest(UUID lureID, uint teleportFlags, IClientAPI client)
        {
            if (!(client.Scene is Scene))
                return;

            Scene scene = (Scene)(client.Scene);

            ulong handle = 0;
            uint x = 128;
            uint y = 128;
            uint z = 70;

            Util.ParseFakeParcelID(lureID, out handle, out x, out y, out z);

            Vector3 position = new Vector3();
            position.X = (float)x;
            position.Y = (float)y;
            position.Z = (float)z;

            TeleportFlags flags = (TeleportFlags)teleportFlags;

            scene.RequestTeleportLocation(client, handle, position,
                    Vector3.Zero, flags);
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Forward remote teleport requests
            //
            if (msg.dialog != 22)
                return;

            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg,
                    delegate(bool success) { });
            }
        }
    }
}
