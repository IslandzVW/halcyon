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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;

using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using System.IO;
using System.IO.Compression;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class EventQueueGetModule : IEventQueue, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Debug level.
        /// </value>
        public int DebugLevel { get; set; }

        protected Scene m_Scene;
        private const int m_timeout = 15 * 1000;            // 15 seconds

        private Dictionary<UUID, EventQueueGetRequestHandler> queues = 
            new Dictionary<UUID, EventQueueGetRequestHandler>();

        #region INonSharedRegionModule methods

        public virtual void Initialize(IConfigSource config)
        {
            MainConsole.Instance.Commands.AddCommand(
                "Comms",
                false,
                "debug eq",
                "debug eq [0|1]",
                "Turn on event queue debugging",
                "debug eq 1 will turn on event queue debugging.  This will log all outgoing event queue messages to clients.\n"
                    + "debug eq 0 will turn off event queue debugging.",
                HandleDebugEq);
        }

        public void PostInitialize()
        {
        }

        public virtual void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_Scene = scene;
        }

        public void RemoveRegion(Scene scene)
        {
            m_Scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_Scene.EventManager.OnDeregisterCaps -= OnDeregisterCaps;
        }

        public void RegionLoaded(Scene scene)
        {
            m_Scene.RegisterModuleInterface<IEventQueue>(this);

            m_Scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_Scene.EventManager.OnDeregisterCaps += OnDeregisterCaps;
        }

        public virtual string Name
        {
            get { return "EventQueueGetModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        protected void HandleDebugEq(string module, string[] args)
        {
            int debugLevel;

            if (!(args.Length == 3 && int.TryParse(args[2], out debugLevel)))
            {
                MainConsole.Instance.OutputFormat("Usage: debug eq [0|1]");
            }
            else
            {
                DebugLevel = debugLevel;
                MainConsole.Instance.OutputFormat(
                    "Set event queue debug level to {0} in {1}", DebugLevel, m_Scene.RegionInfo.RegionName);

                lock (queues)
                {
                    foreach (EventQueueGetRequestHandler handler in new List<EventQueueGetRequestHandler>(queues.Values))
                    {
                        handler.DebugLevel = debugLevel;
                    }
                }
            }
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Register caps for client {0} in region {1}", agentID, m_Scene.RegionInfo.RegionName);

            lock (queues)
            {
                EventQueueGetRequestHandler queue;
                if (queues.TryGetValue(agentID, out queue) == true)
                {
                    // shouldn't happen
                    m_log.ErrorFormat("[EVENTQUEUE]: OnRegisterCaps and an EQ already exists for client {0} in region {1}!", agentID, m_Scene.RegionInfo.RegionName);
                }
                else
                {
                    string capsUrl = "/CAPS/EQG/" + UUID.Random().ToString() + "/";
                    queue = new EventQueueGetRequestHandler("POST", capsUrl, agentID, caps, DebugLevel);
                    queues.Add(agentID, queue);
                }

                caps.RegisterHandler("EventQueueGet", queue);
            }
        }

        public void OnDeregisterCaps(UUID AgentID, Caps caps)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Deregister caps for client {0} in region {1}", AgentID, m_Scene.RegionInfo.RegionName);

            lock (queues)
            {
                caps.DeregisterHandler("EventQueueGet"); 

                // If there is a posted request signal it.  We'll send a 404 in the handler
                EventQueueGetRequestHandler queue;
                if (queues.TryGetValue(AgentID, out queue) == true)
                {
                    queues.Remove(AgentID);
                    queue.DumpQueue();
                }
            }
        }

        #region IEventQueue Members

        public bool Enqueue(OSD ev, UUID avatarID)
        {
            return EnqueueWithPriority(ev, avatarID, false);
        }

        private bool EnqueueWithPriority(OSD ev, UUID avatarID, bool highPriority)
        {
            EventQueueGetRequestHandler queue = null;

            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            lock (queues)
            {
                if (queues.TryGetValue(avatarID, out queue) != true)
                {
                    m_log.ErrorFormat("[EVENTQUEUE]: Could not enqueue request for {0}, queue for avatar not found", avatarID);
                    return (false);
                }
            }

            queue.Enqueue(ev, highPriority);
            return (true);
        }

        public void DisableSimulator(ulong handle, UUID avatarID)
        {
            OSD item = EventQueueHelper.DisableSimulator(handle);
            Enqueue(item, avatarID);
        }

        public virtual bool EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID)
        {
            OSD item = EventQueueHelper.EnableSimulator(handle, endPoint);
            return EnqueueWithPriority(item, avatarID, true);
        }

        public virtual bool EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint, string capsPath)
        {
            OSD item = EventQueueHelper.EstablishAgentCommunication(avatarID, endPoint.ToString(), capsPath);
            return EnqueueWithPriority(item, avatarID, true);
        }

        public virtual bool TeleportFinishEvent(ulong regionHandle, byte simAccess,
                                        IPEndPoint regionExternalEndPoint,
                                        uint locationID, uint flags, string capsURL,
                                        UUID avatarID)
        {
            OSD item = EventQueueHelper.TeleportFinishEvent(regionHandle, simAccess, regionExternalEndPoint,
                                                            locationID, flags, capsURL, avatarID);
            return EnqueueWithPriority(item, avatarID, true);
        }

        public virtual bool CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                                IPEndPoint newRegionExternalEndPoint,
                                string capsURL, UUID avatarID, UUID sessionID)
        {
            OSD item = EventQueueHelper.CrossRegion(handle, pos, lookAt, newRegionExternalEndPoint,
                                                    capsURL, avatarID, sessionID);
            return EnqueueWithPriority(item, avatarID, true);
        }

        public void ChatterboxInvitation(UUID sessionID, string sessionName,
                                         UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                         uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                         uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            OSD item = EventQueueHelper.ChatterboxInvitation(sessionID, sessionName, fromAgent, message, toAgent, fromName, dialog,
                                                             timeStamp, offline, parentEstateID, position, ttl, transactionID,
                                                             fromGroup, binaryBucket);
            Enqueue(item, toAgent);
            //m_log.InfoFormat("########### eq ChatterboxInvitation #############\n{0}", item);
        }

        public void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID fromAgent, UUID toAgent, bool canVoiceChat,
                                                      bool isModerator, bool textMute, byte dialog)
        {
            OSD item = EventQueueHelper.ChatterBoxSessionAgentListUpdates(sessionID, fromAgent, canVoiceChat,
                                                                          isModerator, textMute, dialog);
            Enqueue(item, toAgent);
            //m_log.InfoFormat("########### eq ChatterBoxSessionAgentListUpdates #############\n{0}", item);
        }

        public void ParcelProperties(ParcelPropertiesMessage parcelPropertiesMessage, UUID avatarID)
        {
            OSD item = EventQueueHelper.ParcelProperties(parcelPropertiesMessage);
            Enqueue(item, avatarID);
        }

        public void GroupMembership(AgentGroupDataUpdatePacket groupUpdate, UUID avatarID)
        {
            OSD item = EventQueueHelper.GroupMembership(groupUpdate);
            Enqueue(item, avatarID);
        }

        public void ScriptRunning(UUID objectID, UUID itemID, bool running, bool mono, UUID avatarID)
        {
            OSD item = EventQueueHelper.ScriptRunningReplyEvent(objectID, itemID, running, mono);
            Enqueue(item, avatarID);
        }

        public void QueryReply(PlacesReplyPacket groupUpdate, UUID avatarID)
        {
            OSD item = EventQueueHelper.PlacesQuery(groupUpdate);
            Enqueue(item, avatarID);
        }

        public OSD BuildEvent(string eventName, OSD eventBody)
        {
            return EventQueueHelper.BuildEvent(eventName, eventBody);
        }

        public void PartPhysicsProperties(uint localID, byte physhapetype,
                        float density, float friction, float bounce, float gravmod, UUID avatarID)
        {
            OSD item = EventQueueHelper.PartPhysicsProperties(localID, physhapetype,
                        density, friction, bounce, gravmod);
            Enqueue(item, avatarID);
        }

        #endregion


        protected class EventQueueGetRequestHandler : BaseRequestHandler, IAsyncRequestHandler
        {
            private object m_eqLock; 
            private readonly UUID m_agentID;
            private readonly Caps m_Caps;
            private Queue<OSD> m_highPriorityItems;
            private Queue<OSD> m_Items;
            private AsyncHttpRequest m_Request;
            private int m_SequenceNumber;

            public EventQueueGetRequestHandler(string httpMethod, string path, UUID agentID, Caps caps, int debugLevel) 
                : base(httpMethod, path, "EventQueueGetHandler", "AsyncRequest")
            {
                m_eqLock = new object();
                m_agentID = agentID;
                m_Caps = caps;
                m_highPriorityItems = new Queue<OSD>();
                m_Items = new Queue<OSD>();
                Random rnd = new Random(Environment.TickCount);
                m_SequenceNumber = rnd.Next(30000000);
                DebugLevel = debugLevel;
            }
            
            public int DebugLevel { get; set; }

            public bool isQueueValid
            {
                get { return (m_Items != null); }
            }

            private void TimeoutHandler(AsyncHttpRequest pRequest)
            {
                UUID sessionid = pRequest.AgentID;
                Hashtable eventsToSend = null;

                lock (m_eqLock)
                {
                    if (isQueueValid == false)
                        return;

                    if (m_Request == pRequest)
                    {
                        m_Request = null;
                        eventsToSend = GetEvents(pRequest);
                    }
                }

                if (eventsToSend != null)
                    pRequest.SendResponse(eventsToSend);
            }

            public void Handle(IHttpServer server, string path, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                AsyncHttpRequest pendingRequest = null;
                AsyncHttpRequest eventQueueRequest = 
                    new AsyncHttpRequest(server, httpRequest, httpResponse, m_agentID, TimeoutHandler, m_timeout);
                Hashtable responseToSend = null;

                lock (m_eqLock)
                {
                    if (!isQueueValid)
                    {
                        m_log.ErrorFormat("[EVENTQUEUE]: HandleRequest for {0} failed, isQueueValid == false", m_agentID);
                        m_Request = null;
                        return;
                    }

                    pendingRequest = m_Request;
                    m_Request = eventQueueRequest;

                    if ((pendingRequest == null) && (m_highPriorityItems.Count > 0 || m_Items.Count > 0))
                    {
                        pendingRequest = m_Request;
                        m_Request = null;
                        responseToSend = GetEvents(pendingRequest);
                    }
                }

                if (responseToSend != null)
                {
                    pendingRequest.SendResponse(responseToSend);
                }
            }

            public void Enqueue(OSD ev, bool highPriority)
            {
                AsyncHttpRequest pendingRequest = null;
                Hashtable responseToSend = null;

                lock (m_eqLock)
                {
                    if (isQueueValid == false)
                    {
                        m_log.ErrorFormat("[EVENTQUEUE]: Unable to enqueue new event. isQueueValid == false");
                        return;
                    }

                    if (highPriority)
                    {
                        m_highPriorityItems.Enqueue(ev);
                    }
                    else
                    {
                        m_Items.Enqueue(ev);
                    }

                    if (m_Request != null)
                    {
                        pendingRequest = m_Request;
                        m_Request = null;
                        responseToSend = GetEvents(pendingRequest);
                    }
                }

                if (responseToSend != null)
                {
                    pendingRequest.SendResponse(responseToSend);
                }
            }

            public void DumpQueue()
            {
                AsyncHttpRequest pendingRequest = null;
                Hashtable responseToSend = null;

                lock (m_eqLock)
                {
                    if (isQueueValid == true)
                    {
                        m_highPriorityItems.Clear();
                        m_highPriorityItems = null;
                        m_Items.Clear();
                        m_Items = null;
                    }

                    // If a request is pending signal it.  The response handler will send a 404
                    if (m_Request != null)
                    {
                        pendingRequest = m_Request;
                        m_Request = null;
                        responseToSend = GetEvents(pendingRequest);
                    }
                }

                if (responseToSend != null)
                {
                    pendingRequest.SendResponse(responseToSend);
                }
            }

            public Hashtable GetEvents(AsyncHttpRequest pRequest)
            {
                Hashtable responsedata = new Hashtable();

                lock (m_eqLock) // should already be locked on entry to this function
                {
                    // If the queue is gone we're being cleaned up.  Send a 404
                    if (isQueueValid == false)
                    {
                        responsedata["int_response_code"] = 404;
                        responsedata["content_type"] = "text/plain";
                        responsedata["str_response_string"] = "Not Found";
                        responsedata["error_status_text"] = "Not Found";

                        return responsedata;
                    }

                    if (m_highPriorityItems.Count == 0 && m_Items.Count == 0)
                    {
                        /*
                        ** The format of this response is important. If its not setup this way the client will
                        ** stop polling for new events.  http://wiki.secondlife.com/wiki/EventQueueGet
                        */
                        responsedata["int_response_code"] = 502;
                        responsedata["content_type"] = "text/plain";
                        responsedata["str_response_string"] = "Upstream error: ";
                        responsedata["error_status_text"] = "Upstream error:";
                        responsedata["http_protocol_version"] = "1.0";

                        if (DebugLevel > 0)
                        {
                            m_log.DebugFormat(
                                "[EVENT QUEUE GET MODULE]: Eq TIMEOUT/502 to client {0} in region {1}",
                                m_agentID, 
                                m_Caps.RegionName);
                        }

                        return responsedata;
                    }

                    // Return the events we have queued
                    OSDArray array = new OSDArray();

                    //if there are no high priority items, then dequeue the normal
                    //priority items
                    if (!DequeueIntoArray(m_highPriorityItems, array))
                    {
                        DequeueIntoArray(m_Items, array);
                    }

                    // m_log.ErrorFormat("[EVENTQUEUE]: Responding with {0} events to viewer.", array.Count.ToString());

                    OSDMap events = new OSDMap();
                    events.Add("events", array);
                    events.Add("id", new OSDInteger(m_SequenceNumber));

                    responsedata["int_response_code"] = 200;
                    responsedata["content_type"] = "application/xml";
                    var eventData = OSDParser.SerializeLLSDXmlString(events);
                    responsedata["str_response_string"] = eventData;

                    //warn at 512kB of uncompressed data
                    const int WARN_THRESHOLD = 524288;
                    if (eventData.Length > WARN_THRESHOLD)
                    {
                        m_log.WarnFormat("[EVENTQUEUE]: Very large message being enqueued. Size: {0}, Items{1}. Contents: ",
                            eventData.Length, array.Count);

                        foreach (var e in array)
                        {
                            OSDMap evt = (OSDMap)e;
                            m_log.WarnFormat("[EVENTQUEUE]: {0}", evt["message"]);
                        }
                    }

                    return responsedata;
                }
            }

            private bool DequeueIntoArray(Queue<OSD> queue, OSDArray array)
            {
                OSD element = null;
                bool hadItems = queue.Count > 0;

                while (queue.Count > 0)
                    {
                    element = queue.Dequeue();

                        array.Add(element);
                        m_SequenceNumber++;

                        if ((DebugLevel > 0) && (element is OSDMap))
                        {
                            OSDMap ev = (OSDMap)element;
                            m_log.DebugFormat(
                                "[EVENT QUEUE GET MODULE]: Eq OUT {0} to client {1} in region {2}",
                                    ev["message"],
                                m_agentID,
                                    m_Caps.RegionName);
                        }
                    }

                return hadItems;
                }
            }
        }
}
