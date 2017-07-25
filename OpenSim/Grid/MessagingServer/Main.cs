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
using System.IO;
using System.Net;
using System.Reflection;
using System.Timers;
using System.Threading.Tasks;

using log4net;
using log4net.Config;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;
using OpenSim.Grid.MessagingServer.Modules;
using System.Threading;

namespace OpenSim.Grid.MessagingServer
{
    /// <summary>
    /// </summary>
    public class OpenMessage_Main : BaseOpenSimServer, IGridServiceCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig Cfg;
        private MessageService msgsvc;

        private MessageRegionModule m_regionModule;
        private InterMessageUserServerModule m_userServerModule;

        private UserDataBaseService m_userDataBaseService;

        private ManualResetEvent Terminating = new ManualResetEvent(false);

        private InWorldz.RemoteAdmin.RemoteAdmin m_radmin;

        public static void Main(string[] args)
        {
            // Please note that if you are changing something in this function you should check to see if you need to change the other server's Main functions as well.

            // Under any circumstance other than an explicit exit the exit code should be 1.
            Environment.ExitCode = 1;

            ServicePointManager.DefaultConnectionLimit = 12;

            // Add the arguments supplied when running the application to the configuration
            var configSource = new ArgvConfigSource(args);

            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);
            configSource.Alias.AddAlias("Yes", true);
            configSource.Alias.AddAlias("No", false);

            configSource.AddSwitch("Startup", "background");
            configSource.AddSwitch("Startup", "pidfile");

            m_log.Info("[SERVER]: Launching MessagingServer...");

            var pidFile = new PIDFileManager(configSource.Configs["Startup"].GetString("pidfile", string.Empty));
            XmlConfigurator.Configure();

            OpenMessage_Main messageserver = new OpenMessage_Main();

            pidFile.SetStatus(PIDFileManager.Status.Starting);
            messageserver.Startup();

            pidFile.SetStatus(PIDFileManager.Status.Running);
            messageserver.Work(configSource.Configs["Startup"].GetBoolean("background", false));
        }

        public OpenMessage_Main()
        {
            m_console = new LocalConsole("Messaging");
            MainConsole.Instance = m_console;
        }

        private void Work(bool background)
        {
            if (background)
            {
                Terminating.WaitOne();
                Terminating.Close();
            }
            else
            {
                m_console.Notice("Enter help for a list of commands\n");

                while (true)
                {
                    m_console.Prompt();
                }
            }
        }

        private void registerWithUserServer()
        {
        retry:

            if (m_userServerModule.registerWithUserServer())
            {
                m_log.Info("[SERVER]: Starting HTTP process");
                m_httpServer = new BaseHttpServer(Cfg.HttpPort, null);

                m_httpServer.AddXmlRPCHandler("login_to_simulator", msgsvc.UserLoggedOn);
                m_httpServer.AddXmlRPCHandler("logout_of_simulator", msgsvc.UserLoggedOff);
                m_httpServer.AddXmlRPCHandler("get_presence_info_bulk", msgsvc.GetPresenceInfoBulk);
                m_httpServer.AddXmlRPCHandler("process_region_shutdown", msgsvc.ProcessRegionShutdown);
                m_httpServer.AddXmlRPCHandler("agent_location", msgsvc.AgentLocation);
                m_httpServer.AddXmlRPCHandler("agent_leaving", msgsvc.AgentLeaving);
                m_httpServer.AddXmlRPCHandler("region_startup", m_regionModule.RegionStartup);
                m_httpServer.AddXmlRPCHandler("region_shutdown", m_regionModule.RegionShutdown);

                // New Style
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("login_to_simulator"), msgsvc.UserLoggedOn));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("logout_of_simulator"), msgsvc.UserLoggedOff));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_presence_info_bulk"), msgsvc.GetPresenceInfoBulk));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("process_region_shutdown"), msgsvc.ProcessRegionShutdown));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("agent_location"), msgsvc.AgentLocation));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("agent_leaving"), msgsvc.AgentLeaving));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("region_startup"), m_regionModule.RegionStartup));
                m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("region_shutdown"), m_regionModule.RegionShutdown));

                m_radmin = new InWorldz.RemoteAdmin.RemoteAdmin(Cfg.SSLPublicCertFile);
                m_radmin.AddCommand("MessagingService", "Shutdown", MessagingServerShutdownHandler);
                m_radmin.AddHandler(m_httpServer);

                m_httpServer.Start();

                m_log.Info("[SERVER]: Userserver registration was successful");
            }
            else
            {
                m_log.Error("[STARTUP]: Unable to connect to User Server, retrying in 5 seconds");
                System.Threading.Thread.Sleep(5000);
                goto retry;
            }

        }

        public object MessagingServerShutdownHandler(IList args, IPEndPoint remoteClient)
        {
            m_radmin.CheckSessionValid(new UUID((string)args[0]));

            try
            {
                int delay = (int)args[1];
                string message;

                if (delay > 0)
                    message = "Server is going down in " + delay.ToString() + " second(s).";
                else
                    message = "Server is going down now.";

                m_log.DebugFormat("[RADMIN] Shutdown: {0}", message);

                // Perform shutdown
                if (delay > 0)
                    System.Threading.Thread.Sleep(delay * 1000);

                // Do this on a new thread so the actual shutdown call returns successfully.
                Task.Factory.StartNew(Shutdown);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[RADMIN] Shutdown: failed: {0}", e.Message);
                m_log.DebugFormat("[RADMIN] Shutdown: failed: {0}", e.ToString());
                throw;
            }

            m_log.Info("[RADMIN]: Shutdown Administrator Request complete");
            return true;
        }

        private void deregisterFromUserServer()
        {
            m_userServerModule.deregisterWithUserServer();
            if (m_httpServer != null)
            {
                // try a completely fresh registration, with fresh handlers, too
                m_httpServer.Stop();
                m_httpServer = null;
            }
            m_console.Notice("[SERVER]: Deregistered from userserver.");
        }

        protected override void StartupSpecific()
        {
            Cfg = new MessageServerConfig("MESSAGING SERVER", (Path.Combine(Util.configDir(), "MessagingServer_Config.xml")));

            m_userDataBaseService = new UserDataBaseService();
            m_userDataBaseService.AddPlugin(Cfg.DatabaseProvider, Cfg.DatabaseConnect);

            //Register the database access service so modules can fetch it
            // RegisterInterface<UserDataBaseService>(m_userDataBaseService);

            m_userServerModule = new InterMessageUserServerModule(Cfg, this);
            m_userServerModule.Initialize();

            msgsvc = new MessageService(Cfg, this, m_userDataBaseService);
            msgsvc.Initialize();

            m_regionModule = new MessageRegionModule(Cfg, this);
            m_regionModule.Initialize();

            registerWithUserServer();

            m_userServerModule.PostInitialize();
            msgsvc.PostInitialize();
            m_regionModule.PostInitialize();

            m_log.Info("[SERVER]: Messageserver 0.5 - Startup complete");

            base.StartupSpecific();

            m_console.Commands.AddCommand("messageserver", false, "clear cache",
                    "clear cache",
                    "Clear presence cache", HandleClearCache);

            m_console.Commands.AddCommand("messageserver", false, "register",
                    "register",
                    "Re-register with user server(s)", HandleRegister);
        }

        private void HandleClearCache(string module, string[] cmd)
        {
            int entries = m_regionModule.ClearRegionCache();
            m_console.Notice("Region cache cleared! Cleared " +
                    entries.ToString() + " entries");
        }

        private void HandleRegister(string module, string[] cmd)
        {
            deregisterFromUserServer();
            registerWithUserServer();
        }

        public override void ShutdownSpecific()
        {
            Terminating.Set();
            m_userServerModule.deregisterWithUserServer();
        }

        #region IUGAIMCore
        protected Dictionary<Type, object> m_moduleInterfaces = new Dictionary<Type, object>();

        /// <summary>
        /// Register an Module interface.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iface"></param>
        public void RegisterInterface<T>(T iface)
        {
            lock (m_moduleInterfaces)
            {
                if (!m_moduleInterfaces.ContainsKey(typeof(T)))
                {
                    m_moduleInterfaces.Add(typeof(T), iface);
                }
            }
        }

        public bool TryGet<T>(out T iface)
        {
            if (m_moduleInterfaces.ContainsKey(typeof(T)))
            {
                iface = (T)m_moduleInterfaces[typeof(T)];
                return true;
            }
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return (T)m_moduleInterfaces[typeof(T)];
        }

        public BaseHttpServer GetHttpServer()
        {
            return m_httpServer;
        }
        #endregion
    }
}
