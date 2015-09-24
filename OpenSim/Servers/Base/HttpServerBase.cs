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
using System.Threading;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using log4net;
using Nini.Config;

namespace OpenSim.Servers.Base
{
    public class HttpServerBase : ServicesServerBase
    {
        // Logger
        //
        //private static readonly ILog m_log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType);

        // The http server instance
        //
        protected BaseHttpServer m_HttpServer = null;

        public IHttpServer HttpServer
        {
            get { return m_HttpServer; }
        }

        // Handle all the automagical stuff
        //
        public HttpServerBase(string prompt, string[] args) : base(prompt, args)
        {
        }

        protected override void ReadConfig()
        {
            IConfig networkConfig = m_Config.Configs["Network"];

            if (networkConfig == null)
            {
                System.Console.WriteLine("Section 'Network' not found, server can't start");
                Thread.CurrentThread.Abort();
            }

            uint port = (uint)networkConfig.GetInt("port", 0);

            if (port == 0)
            {
                System.Console.WriteLine("Port number not specified or 0, server can't start");
                Thread.CurrentThread.Abort();
            }

            bool ssl_main = networkConfig.GetBoolean("https_main",false);
            BaseHttpServer httpServer = null;

            //
            // This is where to make the servers:
            //
            //
            // Make the base server according to the port, etc.
            // ADD: Possibility to make main server ssl
            // Then, check for https settings and ADD a server to
            // m_Servers
            //
            if ( !ssl_main )
            {
                httpServer = new BaseHttpServer(port, null);
            }
#if false
            else
            {
                string cert_path = networkConfig.GetString("cert_path", String.Empty);
                if (cert_path == String.Empty)
                {
                    System.Console.WriteLine("Path to X509 certificate is missing, server can't start.");
                    Thread.CurrentThread.Abort();
                }
                string cert_pass = networkConfig.GetString("cert_pass", String.Empty);
                if (cert_pass == String.Empty)
                {
                    System.Console.WriteLine("Password for X509 certificate is missing, server can't start.");
                    Thread.CurrentThread.Abort();
                }

                httpServer = new BaseHttpServer(port, ssl_main, cert_path, cert_pass);
            } 
#endif

            MainServer.AddHttpServer(httpServer);
            MainServer.Instance = httpServer;

#if false
            // If https_listener = true, then add an ssl listener on the https_port...
            if (ssl_listener == true)
            {

                uint https_port = (uint)networkConfig.GetInt("https_port", 0);

                string cert_path = networkConfig.GetString("cert_path", String.Empty);
                if (cert_path == String.Empty)
                {
                    System.Console.WriteLine("Path to X509 certificate is missing, server can't start.");
                    Thread.CurrentThread.Abort();
                }
                string cert_pass = networkConfig.GetString("cert_pass", String.Empty);
                if (cert_pass == String.Empty)
                {
                    System.Console.WriteLine("Password for X509 certificate is missing, server can't start.");
                    Thread.CurrentThread.Abort();
                }

                MainServer.AddHttpServer(new BaseHttpServer(https_port, ssl_listener, cert_path, cert_pass));
            } 
#endif
        }

        protected override void Initialise()
        {
            foreach (BaseHttpServer s in MainServer.Servers.Values)
                s.Start();

            MainServer.RegisterHttpConsoleCommands(MainConsole.Instance);
        }
    }
}
