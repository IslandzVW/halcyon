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

// #define TESTING_OFFLINES
#if TESTING_OFFLINES
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Threading;

namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    public class OfflineMessageModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private List<Scene> m_SceneList = new List<Scene>();
        private string m_RestURL = String.Empty;

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (!enabled)
                return;

            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }
            if (cnf != null && cnf.GetString(
                    "OfflineMessageModule", "None") !=
                    "OfflineMessageModule")
            {
                enabled = false;
                return;
            }

            lock (m_SceneList)
            {
                if (m_SceneList.Count == 0)
                {
                    m_RestURL = cnf.GetString("OfflineMessageURL", String.Empty);
                    if (String.IsNullOrEmpty(m_RestURL))
                    {
                        m_log.Error("[OFFLINE MESSAGING]: Module was enabled, but no URL is given, disabling");
                        enabled = false;
                        return;
                    }
                }
                if (!m_SceneList.Contains(scene))
                    m_SceneList.Add(scene);

                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        public void PostInitialize()
        {
            if (!enabled)
                return;

            if (m_SceneList.Count == 0)
                return;

            IMessageTransferModule trans = m_SceneList[0].RequestModuleInterface<IMessageTransferModule>();
            if (trans == null)
            {
                enabled = false;

                lock (m_SceneList)
                {
                    foreach (Scene s in m_SceneList)
                        s.EventManager.OnNewClient -= OnNewClient;

                    m_SceneList.Clear();
                }

                m_log.Error("[OFFLINE MESSAGING]: No message transfer module is enabled. Diabling offline messages");
                return;
            }

            trans.OnUndeliveredMessage += UndeliveredMessage;

            m_log.Debug("[OFFLINE MESSAGING]: Offline messages enabled");
        }

        public string Name
        {
            get { return "OfflineMessageModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
        
        public void Close()
        {
        }

        private Scene FindScene(UUID agentID)
        {
            foreach (Scene s in m_SceneList)
            {
                ScenePresence presence = s.GetScenePresence(agentID);
                if (presence != null && !presence.IsChildAgent)
                    return s;
            }
            return null;
        }

        private IClientAPI FindClient(UUID agentID)
        {
            foreach (Scene s in m_SceneList)
            {
                ScenePresence presence = s.GetScenePresence(agentID);
                if (presence != null && !presence.IsChildAgent)
                    return presence.ControllingClient;
            }
            return null;
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnRetrieveInstantMessages += RetrieveInstantMessages;
        }

#if TESTING_OFFLINES
        // This test code just tests the handling of offline IMs and group notices, in the absence of a web server and offline.php script.
        // It stores the most recent (only) offline message, and where it goes to many recipients, it only stores the last recipient added.
        private List<MemoryStream> m_DummyOfflines = new List<MemoryStream>();

        private void DebugStoreIM(GridInstantMessage im)
        {
            MemoryStream buffer = new MemoryStream();
            Type type = typeof(GridInstantMessage);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                serializer.Serialize(writer, im);
                writer.Flush();
            }
            lock(m_DummyOfflines)
            {
                m_DummyOfflines.Add(buffer);
            }

            // string debug = buffer.ToString();
        }

        private List<GridInstantMessage> DebugFetchIMs()
        {
            List<GridInstantMessage> offlines = new List<GridInstantMessage>();
            lock (m_DummyOfflines)
            {
                foreach (MemoryStream offline in m_DummyOfflines)
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(GridInstantMessage));
                    offline.Seek(0, SeekOrigin.Begin);
                    // we should check im.toAgentID here but for this testing, it will return the last one to anybody.
                    offlines.Add((GridInstantMessage)deserializer.Deserialize(offline));
                }
                m_DummyOfflines.Clear();    // we've fetched these
            }
            return offlines;
        }
#endif

        private void RetrieveInstantMessages(IClientAPI client)
        {
            m_log.DebugFormat("[OFFLINE MESSAGING]: Retrieving stored messages for {0}", client.AgentId);
            List<GridInstantMessage> msglist = null;
            try
            {
#if TESTING_OFFLINES
                msglist = DebugFetchIMs();
#else
                msglist = SynchronousRestObjectPoster.BeginPostObject<UUID, List<GridInstantMessage>>(
                      "POST", m_RestURL + "/RetrieveMessages/", client.AgentId);
#endif
                if (msglist == null) return;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[OFFLINE MESSAGING]: Exception fetching offline IMs for {0}: {1}",
                    client.AgentId.ToString(), e.ToString());
                return;
            }

            //I believe the reason some people are not getting offlines or all
            //offlines is because they are not yet a root agent when this module
            //is called into by the client. What we should do in this case
            //is hook into onmakerootagent and send the messages then.
            //for now a workaround is to loop until we get a scene
            Thread.Sleep(2000);
            const int MAX_SCENE_RETRIES = 10;

            int i = 0;
            Scene s = null;
            for (i = 0; i < MAX_SCENE_RETRIES; i++)
            {
                s = FindScene(client.AgentId);
                if (s != null)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            if (s == null)
            {
                m_log.ErrorFormat("[OFFLINE MESSAGING]: Didnt find active scene for user in {0} s", MAX_SCENE_RETRIES);
                return;
            }

            foreach (GridInstantMessage im in msglist)
            {
                // Send through scene event manager so all modules get a chance
                // to look at this message before it gets delivered.
                //
                // Needed for proper state management for stored group
                // invitations
                //
                try
                {
                    s.EventManager.TriggerIncomingInstantMessage(im);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[OFFLINE MESSAGING]: Exception sending offline IM for {0}: {1}",
                        client.AgentId.ToString(), e);
                    m_log.ErrorFormat("[OFFLINE MESSAGING]: Problem offline IM was type {0} from {1} to group {2}:\n{3}",
                        im.dialog.ToString(), im.fromAgentName, im.fromGroup.ToString(), im.message);
                }
            }
        }

        private void UndeliveredMessage(GridInstantMessage im)
        {
            //this does not appear to mean what the coders here thought.
            //it appears to be the type of message this is, online meaning
            //it is coming from an online agent and headed to an online, or
            //not known to not be online agent
            /*if (im.offline != 0)
            {*/

            if (
                //not an im from the group and not start typing, stoptyping, or request tp
                ((!im.fromGroup) 
                && im.dialog != (byte)InstantMessageDialog.StartTyping 
                && im.dialog != (byte)InstantMessageDialog.StopTyping
                && im.dialog != (byte) InstantMessageDialog.RequestTeleport)
                
                || 
                
                //im from the group and invitation or notice may go through
                (im.fromGroup 
                    && (im.dialog == (byte)InstantMessageDialog.GroupInvitation ||
                        im.dialog == (byte)InstantMessageDialog.GroupNotice))
                )
            {

#if TESTING_OFFLINES
                bool success = true;
                DebugStoreIM(im);
#else
                bool success = SynchronousRestObjectPoster.BeginPostObject<GridInstantMessage, bool>(
                        "POST", m_RestURL + "/SaveMessage/", im);
#endif

                if (im.dialog == (byte)InstantMessageDialog.MessageFromAgent)
                {
                    IClientAPI client = FindClient(new UUID(im.fromAgentID));
                    if (client == null)
                        return;

                    client.SendInstantMessage(new GridInstantMessage(
                            null, new UUID(im.toAgentID),
                            "System", new UUID(im.fromAgentID),
                            (byte)InstantMessageDialog.MessageFromAgent,
                            "User is not logged in. " +
                            (success ? "Message saved." : "Message not saved"),
                            false, new Vector3()));
                }
            }
            //}
        }
    }
}

