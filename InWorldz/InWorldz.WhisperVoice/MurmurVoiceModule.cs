/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Copyright 2009 Brian Becker <bjbdragon@gmail.com>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation; either version 2 of 
 * the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

/// Original source at https://github.com/vgaessler/whisper_server
using System;
using System.IO;
using System.Web;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using Murmur;

namespace MurmurVoice
{
    #region Callback Classes

    public class MetaCallbackImpl : MetaCallbackDisp_
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public MetaCallbackImpl() { }
        public override void started(ServerPrx srv, Ice.Current current) { m_log.Info("[MurmurVoice] Server started."); }
        public override void stopped(ServerPrx srv, Ice.Current current) { m_log.Info("[MurmurVoice] Server stopped."); }
    }

    public class ServerCallbackImpl : ServerCallbackDisp_
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ServerManager m_manager;

        public ServerCallbackImpl(ServerManager manager)
        {
            m_log.Debug("[MurmurVoice] Create ServerCallbackImpl");

            m_manager = manager;
        }

        public void AddUserToChan(User state, int channel)
        {
            m_log.DebugFormat("[MurmurVoice] AddUserToChan {0} {1} {2} {3}", state.name, state.userid, state.session, channel);

            if (state.channel != channel)
            {
                state.channel = channel;
                m_manager.Server.setState(state);
            }
        }

        public override void userConnected(User state, Ice.Current current)
        {
            m_log.DebugFormat("[MurmurVoice] User connected {0} {1} {2}", state.name, state.userid, state.session);

            if ((state.userid < 0) && (state.session < 0))
            {
                try
                {
                    m_log.DebugFormat("[MurmurVoice] Kicked user {0} {1}", state.name, state.session);
                    m_manager.Server.kickUser(state.session, "This server requires registration to connect.");
                }
                catch (InvalidSessionException)
                {
                    m_log.DebugFormat("[MurmurVoice] Couldn't kick session {0} {1}", state.name, state.session);
                }
                return;
            }

            Agent agent = m_manager.Agent.Get(state.name);

            if (agent != null)
            {
                agent.userid = state.userid;
                agent.session = state.session;
                AddUserToChan(state, agent.channel);
            }
        }

        public override void userDisconnected(User state, Ice.Current current)
        {
            Agent agent = m_manager.Agent.Get(state.name);

            m_log.DebugFormat("[MurmurVoice] User disconnected {0} {1} {2}", state.name, state.userid, state.session);

            if (agent != null)
            {
                agent.session = -1;
                m_manager.Agent.RemoveAgent(agent.uuid);
            }
        }

        public override void userStateChanged(User state, Ice.Current current) { }
        public override void channelCreated(Channel state, Ice.Current current) { }
        public override void channelRemoved(Channel state, Ice.Current current) { }
        public override void channelStateChanged(Channel state, Ice.Current current) { }
    }

    #endregion

    #region Management Classes

    public class ServerManager : IDisposable
    {
        private ServerPrx m_server;
        private AgentManager m_agent_manager;
        private ChannelManager m_channel_manager;
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AgentManager Agent
        {
            get { return m_agent_manager; }
        }

        public ChannelManager Channel
        {
            get { return m_channel_manager; }
        }

        public ServerPrx Server
        {
            get { return m_server; }
        }

        public ServerManager(ServerPrx server, string channel)
        {
            m_server = server;

            // Create the Agent Manager
            m_agent_manager = new AgentManager(m_server);
            m_channel_manager = new ChannelManager(m_server, channel);
        }

        public void ListInLog()
        {
            m_agent_manager.ListInLog();
            m_channel_manager.ListInLog();
        }

        public void Dispose() { }

        public void Close()
        {
            m_agent_manager.Close();
            m_channel_manager.Close();
        }
    }

    public class ChannelManager
    {
        private Dictionary<string, int> chan_ids = new Dictionary<string, int>();
        private ServerPrx m_server;
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        int parent_chan;

        public ChannelManager(ServerPrx server, string channel)
        {
            m_server = server;

            // Update list of channels
            lock (chan_ids)
                foreach (var child in m_server.getTree().children)
                    chan_ids[child.c.name] = child.c.id;

            // Set channel if it was found, create it if it wasn't
            lock (chan_ids)
                if (chan_ids.ContainsKey(channel))
                    parent_chan = chan_ids[channel];
                else
                    parent_chan = m_server.addChannel(channel, 0);

            // Set permissions on channels
            Murmur.ACL[] acls = new Murmur.ACL[1];
            acls[0] = new Murmur.ACL();
            acls[0].group = "all";
            acls[0].applyHere = true;
            acls[0].applySubs = true;
            acls[0].inherited = false;
            acls[0].userid = -1;
            acls[0].allow = Murmur.PermissionSpeak.value;
            acls[0].deny = Murmur.PermissionEnter.value;

            m_log.DebugFormat("[MurmurVoice] Setting ACLs on channel");
            m_server.setACL(parent_chan, acls, (new List<Murmur.Group>()).ToArray(), true);
        }

        public int GetOrCreate(string name)
        {
            lock (chan_ids)
            {
                if (chan_ids.ContainsKey(name))
                    return chan_ids[name];
                m_log.DebugFormat("[MurmurVoice] Channel '{0}' not found. Creating.", name);
                return chan_ids[name] = m_server.addChannel(name, parent_chan);
            }
        }

        public void Remove(string name)
        {
            int channelID = 0;
            lock (chan_ids)
            {
                if (chan_ids.TryGetValue(name, out channelID))
                    chan_ids.Remove(name);
                else
                    return;
            }
            // m_server.removeChannel(channelID);
        }

        public void ListInLog()
        {
            // Channels
            lock (chan_ids)
            {
                foreach(var channel in chan_ids)
                {
                    m_log.InfoFormat("Channel:  name {0}  Murmur id {1}", channel.Key, channel.Value);
                }
            }
        }

        public void Dispose() { }

        public void Close()
        {
            lock (chan_ids)
            {
                /* foreach(int channel in chan_ids.Values)
                {
                    try
                    {
                        m_server.removeChannel(channel);
                    }
                    catch
                    {
                    }
                } */
                chan_ids.Clear();
            }
        }
    }

    public class Agent
    {
        public int channel = -1;
        public int session = -1;
        public int userid = -1;
        public UUID uuid;
        public string pass;

        public Agent(UUID uuid, Scene scene)
        {
            this.uuid = uuid;

            // Random passwords only work for single standalone regions.
            // this.pass = "u" + UUID.Random().ToString().Replace("-", "").Substring(0, 16);

            // This works for multiple regions and in grid mode, but it is not a safe password, because the
            // password can be calculated based on the UUID. It is not advised to use such unsafe passwords.
            this.pass = "u" + Convert.ToBase64String(uuid.GetBytes()).Replace('+', '-').Replace('/', '_').Substring(2, 16);
        }

        /* public Agent(UUID uuid, Scene scene)
        {
            this.uuid = uuid;
            this.pass = "";

            if (scene != null)
            {
                ScenePresence avatar = scene.GetScenePresence(uuid);

                if (avatar != null)
                {
                    // Password based on the Id0 secret client identifier
                    // More secure, but can cause problems when users use multiple viewers or computers
                    AgentCircuitData agentCircuit = scene.AuthenticateHandler.GetAgentCircuitData(avatar.ControllingClient.CircuitCode);
                    this.pass = "u" + agentCircuit.Id0.Substring(2, 16);
                }
                else
                {
                    throw new Exception(String.Format("[MurmurVoice] Could not find scene presence to get Id0 of {0}", uuid));
                }
            }
        } */

        public string name
        {
            get { return Agent.Name(uuid); }
        }

        public static string Name(UUID uuid)
        {
            return "x" + Convert.ToBase64String(uuid.GetBytes()).Replace('+', '-').Replace('/', '_');
        }

        public Dictionary<UserInfo, string> user_info
        {
            get
            {
                Dictionary<UserInfo, string> user_info = new Dictionary<UserInfo, string>();
                user_info[UserInfo.UserName] = this.name;
                user_info[UserInfo.UserPassword] = this.pass;
                return user_info;
            }
        }
    }

    public class AgentManager
    {
        private Dictionary<string, Agent> name_to_agent = new Dictionary<string, Agent>();
        private ServerPrx m_server;
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AgentManager(ServerPrx server)
        {
            m_server = server;
        }

        public Agent Get(UUID uuid)
        {
            string name = Agent.Name(uuid);
            lock (name_to_agent)
                if (name_to_agent.ContainsKey(name))
                    return name_to_agent[name];
                else
                {
                    return null;
                }
        }

        public Agent GetOrCreate(UUID uuid, Scene scene)
        {
            string name = Agent.Name(uuid);
            lock (name_to_agent)
                if (name_to_agent.ContainsKey(name))
                    return (name_to_agent[name].session < 0 ? null : name_to_agent[name]);
                else
                {
                    Agent a = Add(uuid, scene);
                    return a;
                }
        }

        public void RemoveAgent(UUID uuid)
        {
            Agent user = new Agent(uuid, null);

            m_log.DebugFormat("[MurmurVoice] Removing registered user {0}", user.name);

            lock (name_to_agent)
                if (name_to_agent.ContainsKey(user.name))
                    name_to_agent.Remove(user.name);
        }

        private Agent Add(UUID uuid, Scene scene)
        {
            Agent agent = new Agent(uuid, scene);

            Dictionary<int, User> users = m_server.getUsers();

            foreach (var v in users)
            {
                User user = v.Value;

                if (user.name == agent.name)
                {
                    m_log.DebugFormat("[MurmurVoice] Found previously registered user {0} {1} {2} {3}", user.name, user.userid, user.session, user.channel);

                    if ((user.userid >= 0) && (user.session >= 0))
                    {
                        // Reuse Murmur User
                        m_log.DebugFormat("[MurmurVoice] Reusing previously registered user {0} {1} {2} {3}", user.name, user.userid, user.session, user.channel);

                        agent.userid = user.userid;
                        agent.session = user.session;
                        agent.channel = user.channel;

                        lock (name_to_agent)
                            name_to_agent[agent.name] = agent;

                        // Handle password changes
                        m_server.updateRegistration(agent.userid, agent.user_info);

                        return agent;
                    }
                    else
                    {
                        if (user.userid >= 0)
                        {
                            agent.userid = user.userid;

                            lock (name_to_agent)
                                name_to_agent[agent.name] = agent;

                            // Handle password changes
                            m_server.updateRegistration(agent.userid, agent.user_info);

                            return agent;
                        }
                    }
                    break;
                }
            }

            lock (name_to_agent)
                name_to_agent[agent.name] = agent;

            try
            {
                int r = m_server.registerUser(agent.user_info);
                if (r >= 0) agent.userid = r;
            }
            catch (Murmur.InvalidUserException)
            {
                m_log.Warn("[MurmurVoice] InvalidUserException; continuing to recover later");
            }

            m_log.DebugFormat("[MurmurVoice] Registered {0} (uid {1}) identified by {2}", agent.uuid.ToString(), agent.userid, agent.pass);

            return agent;
        }

        public Agent Get(string name)
        {
            lock (name_to_agent)
            {
                if (!name_to_agent.ContainsKey(name))
                    return null;
                return name_to_agent[name];
            }
        }

        public void ListInLog()
        {
            // Agents
            lock (name_to_agent)
            {
                foreach (var v in name_to_agent)
                {
                    Agent agent = v.Value;

                    m_log.InfoFormat("Agent:  UUID {0}  userid {1}  session {2}  channel {3}  password {4}",
                                      agent.uuid, agent.userid, agent.session, agent.channel, agent.pass);
                }
            }
        }

        public void Close() { }
    }

    #endregion

    #region Thread Classes

    public class KeepAlive
    {
        public bool running = true;
        public ServerPrx m_server;
        public KeepAlive(ServerPrx prx)
        {
            m_server = prx;
        }

        public void StartPinging()
        {
            if (running)
            {
                m_server.ice_ping();
                Thread.Sleep(100);
            }
        }
    }

    #endregion

    #region Mumble Voice Module

    //[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MurmurVoice")]

    public class MurmurVoiceModule : ISharedRegionModule
    {
        // Infrastructure
        private IConfig m_config;
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<Scene> m_scenes = new List<Scene> ();

        // ICE
        private static ServerPrx m_server;
        private static Ice.ObjectAdapter m_adapter;
        private static KeepAlive m_keepalive;
        private static Thread m_keepalive_t;

        // Capability strings
        private static readonly string m_parcelVoiceInfoRequestPath = "0107/";
        private static readonly string m_provisionVoiceAccountRequestPath = "0108/";
        private static readonly string m_chatSessionRequestPath = "0109/";

        // Configuration
        private static string m_murmurd_host;
        private static int m_murmurd_port;
        private static string m_murmurd_ice;
        private static int m_server_id;
        private static string m_server_version = "";
        private static bool m_glacier_enabled;
        private static string m_glacier_ice;
        private static string m_glacier_user;
        private static string m_glacier_pass;
        private static string m_murmur_ice_cb;
        private static string m_channel_name;
        private static Dictionary<UUID, ServerManager> m_managers = new Dictionary<UUID, ServerManager>();
        private static Dictionary<UUID, ServerCallbackImpl> m_servercallbacks = new Dictionary<UUID, ServerCallbackImpl>();
        private static bool m_enabled = false;
        private static bool m_started = false; //Have we connected to the Murmur server yet?

        private ServerManager GetServerManager(Scene scene)
        {
            if (m_managers.ContainsKey(scene.RegionInfo.RegionID))
                return m_managers[scene.RegionInfo.RegionID];
            return null;
        }

        private void AddServerManager(Scene scene, ServerManager manager)
        {
            m_managers[scene.RegionInfo.RegionID] = manager;
        }

        private ServerCallbackImpl GetServerCallback(Scene scene)
        {
            if (m_servercallbacks.ContainsKey(scene.RegionInfo.RegionID))
                return m_servercallbacks[scene.RegionInfo.RegionID];
            return null;
        }

        private void AddServerCallback(Scene scene, ServerCallbackImpl serverCallbackImpl)
        {
            m_servercallbacks[scene.RegionInfo.RegionID] = serverCallbackImpl;
        }

        private void RemoveServerCallback(Scene scene)
        {
            if (m_servercallbacks.ContainsKey(scene.RegionInfo.RegionID))
                m_servercallbacks.Remove(scene.RegionInfo.RegionID);
        }

        public void Initialise(IConfigSource config)
        {
            m_config = config.Configs["MurmurVoice"];

            if (null == m_config)
            {
                m_log.Info("[MurmurVoice] no config found, plugin disabled");
                return;
            }

            if (!m_config.GetBoolean("enabled", false))
            {
                m_log.Info("[MurmurVoice] plugin disabled by configuration");
                return;
            }

            m_murmurd_host = m_config.GetString("murmur_host", String.Empty);
            m_server_id = m_config.GetInt("murmur_sid", 1);
            m_server_version = m_config.GetString("server_version", String.Empty);
            m_murmurd_ice = "Meta:" + m_config.GetString("murmur_ice", String.Empty);
            m_murmur_ice_cb = m_config.GetString("murmur_ice_cb", "tcp -h 127.0.0.1");

            m_channel_name = m_config.GetString("channel_name", "Channel").Replace(" ","_");

            m_glacier_enabled = m_config.GetBoolean("glacier", false);
            m_glacier_ice = m_config.GetString("glacier_ice", String.Empty);
            m_glacier_user = m_config.GetString("glacier_user", "admin");
            m_glacier_pass = m_config.GetString("glacier_pass", "password");

            // Admin interface required values
            if (String.IsNullOrEmpty(m_murmurd_ice) ||
                String.IsNullOrEmpty(m_murmurd_host) )
            {
                m_log.Error("[MurmurVoice] plugin disabled: incomplete configuration");
                return;
            }

            m_log.Info("[MurmurVoice] enabled");
            m_enabled = true;
        }

        public void Initialize(Scene scene)
        {
            try
            {
                if (!m_enabled) return;

                if (!m_started)
                {
                    m_started = true;

                    scene.AddCommand(this, "mumble report", "mumble report",
                        "Returns mumble report", MumbleReport);

                    scene.AddCommand(this, "mumble unregister", "mumble unregister <userid>",
                        "Unregister User by userid", UnregisterUser);

                    scene.AddCommand(this, "mumble remove", "mumble remove <UUID>",
                        "Remove Agent by UUID", RemoveAgent);

                    Ice.Communicator comm = Ice.Util.initialize();

                    /*
                    if (m_glacier_enabled)
                    {
                        m_router = RouterPrxHelper.uncheckedCast(comm.stringToProxy(m_glacier_ice));
                        comm.setDefaultRouter(m_router);
                        m_router.createSession(m_glacier_user, m_glacier_pass);
                    }
                    */

                    MetaPrx meta = MetaPrxHelper.checkedCast(comm.stringToProxy(m_murmurd_ice));

                    // Create the adapter
                    comm.getProperties().setProperty("Ice.PrintAdapterReady", "0");
                    if (m_glacier_enabled)
                        m_adapter = comm.createObjectAdapterWithRouter("Callback.Client", comm.getDefaultRouter() );
                    else
                        m_adapter = comm.createObjectAdapterWithEndpoints("Callback.Client", m_murmur_ice_cb);
                    m_adapter.activate();

                    // Create identity and callback for Metaserver
                    Ice.Identity metaCallbackIdent = new Ice.Identity();
                    metaCallbackIdent.name = "metaCallback";
                    //if (m_router != null)
                    //    metaCallbackIdent.category = m_router.getCategoryForClient();
                    MetaCallbackPrx meta_callback = MetaCallbackPrxHelper.checkedCast(m_adapter.add(new MetaCallbackImpl(), metaCallbackIdent));
                    meta.addCallback(meta_callback);

                    m_log.InfoFormat("[MurmurVoice] using murmur server ice '{0}'", m_murmurd_ice);

                    // create a server and figure out the port name
                    Dictionary<string,string> defaults = meta.getDefaultConf();
                    m_server = ServerPrxHelper.checkedCast(meta.getServer(m_server_id));

                    // start thread to ping glacier2 router and/or determine if con$
                    m_keepalive = new KeepAlive(m_server);
                    ThreadStart ka_d = new ThreadStart(m_keepalive.StartPinging);
                    m_keepalive_t = new Thread(ka_d);
                    m_keepalive_t.Start();

                    // first check the conf for a port, if not then use server id and default port to find the right one.
                    string conf_port = m_server.getConf("port");
                    if(!String.IsNullOrEmpty(conf_port))
                        m_murmurd_port = Convert.ToInt32(conf_port);
                    else
                        m_murmurd_port = Convert.ToInt32(defaults["port"])+m_server_id-1;

                    try
                    {
                        m_server.start();
                    }
                    catch
                    {
                    }

                    m_log.Info("[MurmurVoice] started");
                }

                // starts the server and gets a callback
                ServerManager manager = new ServerManager(m_server, m_channel_name);

                // Create identity and callback for this current server
                AddServerCallback(scene, new ServerCallbackImpl(manager));
                AddServerManager(scene, manager);

                Ice.Identity serverCallbackIdent = new Ice.Identity();
                serverCallbackIdent.name = "serverCallback_" + scene.RegionInfo.RegionName.Replace(" ","_");
                //if (m_router != null)
                //    serverCallbackIdent.category = m_router.getCategoryForClient();

                m_server.addCallback(ServerCallbackPrxHelper.checkedCast(m_adapter.add(GetServerCallback(scene), serverCallbackIdent)));

                // Show information on console for debugging purposes
                m_log.InfoFormat("[MurmurVoice] using murmur server '{0}:{1}', sid '{2}'", m_murmurd_host, m_murmurd_port, m_server_id);
                m_log.Info("[MurmurVoice] plugin enabled");
                m_enabled = true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MurmurVoice] plugin initialization failed: {0}", e.ToString());
                return;
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                lock (m_scenes) m_scenes.Add(scene);

                Initialize(scene);

                scene.EventManager.OnMakeChildAgent += MakeChildAgent;
                scene.EventManager.OnRemovePresence += RemovePresence;
                scene.EventManager.OnClientClosed += ClientClosed;
                scene.EventManager.OnRegisterCaps += delegate(UUID agentID, Caps caps)
                {
                    OnRegisterCaps(scene, agentID, caps);
                };
            }
        }

        public void MakeChildAgent(ScenePresence avatar)
        {
            m_log.DebugFormat("[MurmurVoice]: MakeChildAgent {0} {1}", avatar.ControllingClient.AgentId, avatar.Scene.RegionInfo.RegionName);

            GetServerManager(avatar.Scene).Agent.RemoveAgent(avatar.ControllingClient.AgentId);
        }

        public void RemovePresence(UUID agentID)
        {
            m_log.DebugFormat("[MurmurVoice]: RemovePresence {0}", agentID);

            foreach (Scene scene in m_scenes)
            {
                GetServerManager(scene).Agent.RemoveAgent(agentID);
            }
        }

        public void ClientClosed(UUID agentID, Scene scene)
        {
            m_log.DebugFormat("[MurmurVoice]: ClientClosed {0}", agentID);

            ScenePresence avatar = scene.GetScenePresence(agentID);

            if ((avatar == null) || (avatar.IsChildAgent)) return;

            GetServerManager(scene).Agent.RemoveAgent(agentID);
        }

        // Called to indicate that all loadable modules have now been added
        public void RegionLoaded(Scene scene)
        {
            // Do nothing.
        }

        // Called to indicate that the region is going away.
        public void RemoveRegion(Scene scene)
        {
            if (m_enabled)
            {
                lock (m_scenes) m_scenes.Remove(scene);

                scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
                scene.EventManager.OnRemovePresence -= RemovePresence;
                scene.EventManager.OnClientClosed -= ClientClosed;

                GetServerManager(scene).Close();
                GetServerManager(scene).Dispose();
            }
        }

        public void PostInitialise()
        {
            // Do nothing.
        }

        public void Close()
        {
            if (m_enabled)
            {
                m_keepalive.running = false;
                m_keepalive_t.Abort();

                m_server.stop();
            }
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "MurmurVoiceModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        private string ChannelName(Scene scene, LandData land)
        {
            // Create parcel voice channel. If no parcel exists, then the voice channel ID is the same
            // as the directory ID. Otherwise, it reflects the parcel's ID.
            
            if (land.LocalID != 1 && (land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) == 0)
            {
                m_log.DebugFormat("[MurmurVoice]: Region: parcel \"{0}:{1}\": parcel id {2}  {3}", 
                                  scene.RegionInfo.RegionName, land.Name, land.LocalID, land.GlobalID.ToString().Replace("-",""));
                return land.GlobalID.ToString().Replace("-","");
            }
            else
            {
                m_log.DebugFormat("[MurmurVoice]: Region: parcel \"{0}:{1}\": parcel id {2}  {3}", 
                                  scene.RegionInfo.RegionName, scene.RegionInfo.RegionName, land.LocalID, scene.RegionInfo.RegionID.ToString().Replace("-",""));
                return scene.RegionInfo.RegionID.ToString().Replace("-","");
            }
        }

        // OnRegisterCaps is invoked via the scene.EventManager
        // everytime OpenSim hands out capabilities to a client
        // (login, region crossing). We contribute two capabilities to
        // the set of capabilities handed back to the client:
        // ProvisionVoiceAccountRequest and ParcelVoiceInfoRequest.
        // 
        // ProvisionVoiceAccountRequest allows the client to obtain
        // the voice account credentials for the avatar it is
        // controlling (e.g., user name, password, etc).
        // 
        // ParcelVoiceInfoRequest is invoked whenever the client
        // changes from one region or parcel to another.
        //
        // Note that OnRegisterCaps is called here via a closure
        // delegate containing the scene of the respective region (see
        // Initialise()).
        public void OnRegisterCaps(Scene scene, UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[MurmurVoice] OnRegisterCaps: agentID {0} caps {1}", agentID, caps);

            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("ProvisionVoiceAccountRequest",
                                 new RestStreamHandler("POST", capsBase + m_provisionVoiceAccountRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ProvisionVoiceAccountRequest(scene, request, path, param,
                                                                                               agentID);
                                                       }));
            caps.RegisterHandler("ParcelVoiceInfoRequest",
                                 new RestStreamHandler("POST", capsBase + m_parcelVoiceInfoRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ParcelVoiceInfoRequest(scene, request, path, param,
                                                                                         agentID);
                                                       }));
            caps.RegisterHandler("ChatSessionRequest",
                                 new RestStreamHandler("POST", capsBase + m_chatSessionRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ChatSessionRequest(scene, request, path, param,
                                                                                     agentID);
                                                       }));

            // For Naali Viewer
            caps.RegisterHandler("mumble_server_info",
                                 new RestStreamHandler("GET", capsBase + m_chatSessionRequestPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return RestGetMumbleServerInfo(scene, request, path, param, httpRequest, httpResponse);
                                                       }));
        }

        /// Callback for a client request for Voice Account Details.
        public string ProvisionVoiceAccountRequest(Scene scene, string request, string path, string param,
                                                   UUID agentID)
        {
            try
            {
                m_log.Debug("[MurmurVoice] Calling ProvisionVoiceAccountRequest...");

                if (scene == null) throw new Exception("[MurmurVoice] Invalid scene.");

                // Wait for scene presence
                int retry = 0;
                ScenePresence avatar = scene.GetScenePresence(agentID);
                while (avatar == null)
                {
                    if (++retry > 100)
                        throw new Exception(String.Format("region \"{0}\": agent ID \"{1}\": wait for scene presence timed out",
                                            scene.RegionInfo.RegionName, agentID));

                    Thread.Sleep(100);
                    avatar = scene.GetScenePresence(agentID);
                }

                Agent agent = new Agent(agentID, scene);

                LLSDVoiceAccountResponse voiceAccountResponse =
                    new LLSDVoiceAccountResponse(agent.name, agent.pass, m_murmurd_host, 
                        String.Format("tcp://{0}:{1}", m_murmurd_host, m_murmurd_port)
                );

                string r = LLSDHelpers.SerialiseLLSDReply(voiceAccountResponse);
                m_log.DebugFormat("[MurmurVoice] VoiceAccount: {0}", r);
                return r;
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[MurmurVoice] {0} failed", e.ToString());
                return "<llsd><undef /></llsd>";
            }
        }

        /// Callback for a client request for ParcelVoiceInfo
        public string ParcelVoiceInfoRequest(Scene scene, string request, string path, string param,
                                             UUID agentID)
        {
            m_log.Debug("[MurmurVoice] Calling ParcelVoiceInfoRequest...");
            try
            {
                ScenePresence avatar = scene.GetScenePresence(agentID);

                string channel_uri = String.Empty;

                if (null == scene.LandChannel) 
                    throw new Exception(String.Format("region \"{0}\": avatar \"{1}\": land data not yet available",
                                                      scene.RegionInfo.RegionName, avatar.Name));

                // get channel_uri: check first whether estate
                // settings allow voice, then whether parcel allows
                // voice, if all do retrieve or obtain the parcel
                // voice channel
                LandData land = scene.GetLandData(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                if (null == land) 
                    throw new Exception(String.Format("region \"{0}\": avatar \"{1}\": land data not yet available",
                                                      scene.RegionInfo.RegionName, avatar.Name));

                m_log.DebugFormat("[MurmurVoice] region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": request: {4}, path: {5}, param: {6}",
                                  scene.RegionInfo.RegionName, land.Name, land.LocalID, avatar.Name, request, path, param);

                if (((land.Flags & (uint)ParcelFlags.AllowVoiceChat) > 0) && scene.RegionInfo.EstateSettings.AllowVoice)
                {
                    ServerManager manager = GetServerManager(scene);
                    Agent agent = manager.Agent.GetOrCreate(agentID, scene);

                    if (agent == null)
                    {
                        m_log.ErrorFormat("[MurmurVoice] Agent not connected {0}", agentID);

                        return "<llsd><undef /></llsd>";
                    }

                    agent.channel = manager.Channel.GetOrCreate(ChannelName(scene, land));

                    // Wait for session connect
                    int retry = 0;
                    while (agent.session < 0)
                    {
                        if (++retry > 50)
                        {
                            m_log.ErrorFormat("[MurmurVoice] Connecting failed {0} (uid {1}) identified by {2}", agent.uuid.ToString(), agent.userid, agent.pass);

                            return "<llsd><undef /></llsd>";
                        }

                        Thread.Sleep(200);
                    }

                    // Host/port pair for voice server
                    channel_uri = String.Format("{0}:{1}", m_murmurd_host, m_murmurd_port);

                    Murmur.User state = manager.Server.getState(agent.session);
                    GetServerCallback(scene).AddUserToChan(state, agent.channel);

                    m_log.DebugFormat("[MurmurVoice] {0}", channel_uri);
                }
                else
                {
                    m_log.DebugFormat("[MurmurVoice] Voice not enabled.");
                }

                Hashtable creds = new Hashtable();
                creds["channel_uri"] = channel_uri;

                LLSDParcelVoiceInfoResponse parcelVoiceInfo = new LLSDParcelVoiceInfoResponse(scene.RegionInfo.RegionName, land.LocalID, creds);
                string r = LLSDHelpers.SerialiseLLSDReply(parcelVoiceInfo);
                m_log.DebugFormat("[MurmurVoice] Parcel: {0}", r);

                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MurmurVoice] Exception: " + e.ToString());
                return "<llsd><undef /></llsd>";
            }
        }

        /// Callback for a client request for a private chat channel
        public string ChatSessionRequest(Scene scene, string request, string path, string param,
                                         UUID agentID)
        {
            ScenePresence avatar = scene.GetScenePresence(agentID);
            string avatarName = avatar.Name;

            m_log.DebugFormat("[MurmurVoice] Chat Session: avatar \"{0}\": request: {1}, path: {2}, param: {3}",
                              avatarName, request, path, param);
            return "<llsd>true</llsd>";
        }

        /// <summary>
        /// Returns information about a mumble server via a REST Request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param">A string representing the sim's UUID</param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns>Information about the mumble server in http response headers</returns>
        public string RestGetMumbleServerInfo(Scene scene, string request, string path, string param,
                                       OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (m_murmurd_host == null)
            {
                httpResponse.StatusCode = 404;
                httpResponse.StatusDescription = "Not Found";

                string message = "[MurmurVoice]: Server info request from " + httpRequest.RemoteIPEndPoint.Address + ". Cannot send response, module is not configured properly.";
                m_log.Warn(message);
                return "Mumble server info is not available.";
            }
            if (httpRequest.Headers.GetValues("avatar_uuid") == null)
            {
                httpResponse.StatusCode = 400;
                httpResponse.StatusDescription = "Bad Request";

                string message = "[MurmurVoice]: Invalid server info request from " + httpRequest.RemoteIPEndPoint.Address + "";
                m_log.Warn(message);
                return "avatar_uuid header is missing";
            }
                
            string avatar_uuid = httpRequest.Headers.GetValues("avatar_uuid")[0];
            string responseBody = String.Empty;
            UUID avatarId;
            if (UUID.TryParse(avatar_uuid, out avatarId))
            {
                if (scene == null) throw new Exception("[MurmurVoice] Invalid scene.");

                ServerManager manager = GetServerManager(scene);
                Agent agent = manager.Agent.GetOrCreate(avatarId, scene);

                string channel_uri;

                ScenePresence avatar = scene.GetScenePresence(avatarId);
                
                // get channel_uri: check first whether estate
                // settings allow voice, then whether parcel allows
                // voice, if all do retrieve or obtain the parcel
                // voice channel
                LandData land = scene.GetLandData(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                if (null == land) 
                    throw new Exception(String.Format("region \"{0}\": avatar \"{1}\": land data not yet available",
                                                      scene.RegionInfo.RegionName, avatar.Name));

                m_log.DebugFormat("[MurmurVoice] region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": request: {4}, path: {5}, param: {6}",
                                  scene.RegionInfo.RegionName, land.Name, land.LocalID, avatar.Name, request, path, param);

                if (((land.Flags & (uint)ParcelFlags.AllowVoiceChat) > 0) && scene.RegionInfo.EstateSettings.AllowVoice)
                {
                    agent.channel = manager.Channel.GetOrCreate(ChannelName(scene, land));

                    // Host/port pair for voice server
                    channel_uri = String.Format("{0}:{1}", m_murmurd_host, m_murmurd_port);

                    if (agent.session > 0)
                    {
                        Murmur.User state = manager.Server.getState(agent.session);
                        GetServerCallback(scene).AddUserToChan(state, agent.channel);
                    }

                    m_log.InfoFormat("[MurmurVoice] {0}", channel_uri);
                }
                else
                {
                    m_log.DebugFormat("[MurmurVoice] Voice not enabled.");
                    channel_uri = "";
                }
                string m_context = "Mumble voice system";

                httpResponse.AddHeader("Mumble-Server", m_murmurd_host);
                httpResponse.AddHeader("Mumble-Version", m_server_version);
                httpResponse.AddHeader("Mumble-Channel", channel_uri);
                httpResponse.AddHeader("Mumble-User", avatar_uuid);
                httpResponse.AddHeader("Mumble-Password", agent.pass);
                httpResponse.AddHeader("Mumble-Avatar-Id", avatar_uuid);
                httpResponse.AddHeader("Mumble-Context-Id", m_context);

                responseBody += "Mumble-Server: " + m_murmurd_host + "\n";
                responseBody += "Mumble-Version: " + m_server_version + "\n";
                responseBody += "Mumble-Channel: " + channel_uri + "\n";
                responseBody += "Mumble-User: " + avatar_uuid + "\n";
                responseBody += "Mumble-Password: " + agent.pass + "\n";
                responseBody += "Mumble-Avatar-Id: " + avatar_uuid + "\n";
                responseBody += "Mumble-Context-Id: " + m_context + "\n";

                string log_message = "[MurmurVoice]: Server info request handled for " + httpRequest.RemoteIPEndPoint.Address + "";
                m_log.Info(log_message);
            }
            else
            {
                httpResponse.StatusCode = 400;
                httpResponse.StatusDescription = "Bad Request";

                m_log.Warn("[MurmurVoice]: Could not parse avatar uuid from request");
                return "could not parse avatar_uuid header";
            }

            return responseBody;
        }

        private void MumbleReport(string module, string[] args)
        {
            foreach(var v in m_managers)
            {
                UUID regionID = v.Key;
                ServerManager manager = v.Value;

                m_log.InfoFormat("REGION {0}", regionID);
                manager.ListInLog();
            }

            // Murmur Users
            m_log.Info("MURMUR SERVER");

            Dictionary<int, User> users = m_server.getUsers();

            foreach (var v in users)
            {
                User user = v.Value;

                m_log.InfoFormat("User:  name {0}  userid {1}  session {2}  channel {3}",
                                  user.name, user.userid, user.session, user.channel);
            }
        }

        private void UnregisterUser(string module, string[] args)
        {
            if (args.Length == 3)
            {
                int userid = Convert.ToInt32(args[2]);

                m_server.unregisterUser(userid);
            }
            else
            {
                m_log.Info("[MurmurVoice]: Usage: mumble unregister <userid>");
            }
        }

        private void RemoveAgent(string module, string[] args)
        {
            if (args.Length == 3)
            {
                UUID agentID = UUID.Zero;

                if (!UUID.TryParse(args[2], out agentID))
                {
                    m_log.Info("[MurmurVoice]: Error bad UUID format!");
                    return;
                }

                foreach(var v in m_managers)
                {
                    ServerManager manager = v.Value;
                    manager.Agent.RemoveAgent(agentID);
                }
            }
            else
            {
                m_log.Info("[MurmurVoice]: Usage: mumble remove <UUID>");
            }
        }
    }

    #endregion
}
