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
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using System.Web;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;
using Timer=System.Timers.Timer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;


namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// Common base for the main OpenSimServers (user, grid, inventory, region, etc)
    /// </summary>
    public abstract class BaseOpenSimServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This will control a periodic log printout of the current 'show stats' (if they are active) for this
        /// server.
        /// </summary>
        private Timer m_periodicDiagnosticsTimer = new Timer(60 * 60 * 1000);

        protected CommandConsole m_console;
        protected OpenSimAppender m_consoleAppender;
        protected IAppender m_logFileAppender = null; 

        /// <summary>
        /// Time at which this server was started
        /// </summary>
        protected DateTime m_startuptime;

        /// <summary>
        /// Record the initial startup directory for info purposes
        /// </summary>
        protected string m_startupDirectory = Environment.CurrentDirectory;

        protected string m_pidFile = String.Empty;
        
        /// <summary>
        /// Random uuid for private data 
        /// </summary>
        protected string m_osSecret = String.Empty;

        protected BaseHttpServer m_httpServer;
        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

      
        /// <summary>
        /// Holds the non-viewer statistics collection object for this service/server
        /// </summary>
        protected IStatsCollector m_stats;

        public BaseOpenSimServer()
        {
            m_startuptime = DateTime.Now;

            // Random uuid for private data
            m_osSecret = UUID.Random().ToString();

            m_periodicDiagnosticsTimer.Elapsed += new ElapsedEventHandler(LogDiagnostics);
            m_periodicDiagnosticsTimer.Enabled = true;

            // Add ourselves to thread monitoring.  This thread will go on to become the console listening thread
            Thread.CurrentThread.Name = "ConsoleThread";
            ThreadTracker.Add(Thread.CurrentThread);

            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "LogFileAppender")
                {
                    m_logFileAppender = appender;
                }
            }

            System.Console.CancelKeyPress += new System.ConsoleCancelEventHandler(HandleConsoleCancelEvent);
        }

        protected void HandleConsoleCancelEvent(object sender, ConsoleCancelEventArgs args)
        {
            // The system may be running without a console prompt and cannot issue SHUTDOWN command
            // call shutdown on ctrl-c
            this.Shutdown();
        }
        
        /// <summary>
        /// Must be overriden by child classes for their own server specific startup behaviour.
        /// </summary>
        protected virtual void StartupSpecific()
        {
            if (m_console != null)
            {
                ILoggerRepository repository = LogManager.GetRepository();
                IAppender[] appenders = repository.GetAppenders();

                foreach (IAppender appender in appenders)
                {
                    if (appender.Name == "Console")
                    {
                        m_consoleAppender = (OpenSimAppender)appender;
                        break;
                    }
                }

                if (null == m_consoleAppender)
                {
                    Notice("No appender named Console found (see the log4net config file for this executable)!");
                }
                else
                {
                    m_consoleAppender.Console = m_console;
                    
                    // If there is no threshold set then the threshold is effectively everything.
                    if (null == m_consoleAppender.Threshold)
                        m_consoleAppender.Threshold = Level.All;
                    
                    Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
                }                                          
                
                m_console.Commands.AddCommand("base", false, "quit",
                        "quit",
                        "Quit the application", HandleQuit);

                m_console.Commands.AddCommand("base", false, "shutdown",
                        "shutdown",
                        "Quit the application", HandleQuit);

                m_console.Commands.AddCommand("base", false, "forcegc",
                        "forcegc [ 0|1|2|*|now ]",
                        "Forces an immediate full garbage collection (testing/dev only)", HandleForceGC);

                m_console.Commands.AddCommand("base", false, "set log level",
                        "set log level <level>",
                        "Set the console logging level", HandleLogLevel);

                m_console.Commands.AddCommand("base", false, "show info",
                        "show info",
                        "Show general information", HandleShow);

                m_console.Commands.AddCommand("base", false, "show stats",
                        "show stats",
                        "Show statistics", HandleShow);

                m_console.Commands.AddCommand("base", false, "show threads",
                        "show threads",
                        "Show thread status", HandleShow);

                m_console.Commands.AddCommand("base", false, "show uptime",
                        "show uptime",
                        "Show server uptime", HandleShow);

                m_console.Commands.AddCommand("base", false, "show version",
                        "show version",
                        "Show server version", HandleShow);
            }
        }
        
        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>      
        public virtual void ShutdownSpecific() {}
        
        /// <summary>
        /// Provides a list of help topics that are available.  Overriding classes should append their topics to the
        /// information returned when the base method is called.
        /// </summary>
        /// 
        /// <returns>
        /// A list of strings that represent different help topics on which more information is available
        /// </returns>
        protected virtual List<string> GetHelpTopics() { return new List<string>(); }

        /// <summary>
        /// Print statistics to the logfile, if they are active
        /// </summary>
        protected void LogDiagnostics(object source, ElapsedEventArgs e)
        {
            StringBuilder sb = new StringBuilder("DIAGNOSTICS\n\n");
            sb.Append(GetUptimeReport());

            if (m_stats != null)
            {
                sb.Append(m_stats.Report());
            }

            sb.Append(Environment.NewLine);
            sb.Append(GetThreadsReport());

            m_log.Debug(sb);
        }

        /// <summary>
        /// Get a report about the registered threads in this server.
        /// </summary>
        protected string GetThreadsReport()
        {
            StringBuilder sb = new StringBuilder();

            List<Thread> threads = ThreadTracker.GetThreads();
            if (threads == null)
            {
                sb.Append("Thread tracking is only enabled in DEBUG mode.");
            }
            else
            {
                sb.Append(threads.Count + " threads are being tracked:" + Environment.NewLine);
                foreach (Thread t in threads)
                {
                    if (t.IsAlive)
                    {
                        sb.Append(
                            "ID: " + t.ManagedThreadId + ", Name: " + t.Name + ", Alive: " + t.IsAlive
                            + ", Pri: " + t.Priority + ", State: " + t.ThreadState + Environment.NewLine);
                    }
                    else
                    {
                        try
                        {
                            sb.Append("ID: " + t.ManagedThreadId + ", Name: " + t.Name + ", DEAD" + Environment.NewLine);
                        }
                        catch
                        {
                            sb.Append("THREAD ERROR" + Environment.NewLine);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return a report about the uptime of this server
        /// </summary>
        /// <returns></returns>
        protected string GetUptimeReport()
        {
            StringBuilder sb = new StringBuilder(String.Format("Time now is {0}\n", DateTime.Now));
            sb.Append(String.Format("Server has been running since {0}, {1}\n", m_startuptime.DayOfWeek, m_startuptime));
            sb.Append(String.Format("That is an elapsed time of {0}\n", DateTime.Now - m_startuptime));

            return sb.ToString();
        }

        /// <summary>
        /// Performs initialization of the scene, such as loading configuration from disk.
        /// </summary>
        public virtual void Startup()
        {
            m_log.Info("[STARTUP]: Beginning startup processing");

            StartupSpecific();

            // Report the version number near the end so you can still see it after startup.
            m_log.Info("[STARTUP]: Version: " + VersionInfo.Version + "\n");

            TimeSpan timeTaken = DateTime.Now - m_startuptime;
            m_log.InfoFormat("[STARTUP]: Startup took {0}m {1}s", timeTaken.Minutes, timeTaken.Seconds);
        }

        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>      
        public virtual void Shutdown()
        {
            // Use a status of 64 (0b01000000) to indicate that this "error" is an explicit shutdown and not a real error.
            Shutdown(64);
        }

        /// <summary>
        /// Shutdown the server with the specified exit code.
        /// </summary>
        /// <param name="exitCode">The exit code to be returned once shutdown has completed.</param>
        public void Shutdown(int exitCode)
        {
            ShutdownSpecific();

            m_log.Info("[SHUTDOWN]: Shutdown processing on main thread complete.  Exiting...");
            RemovePIDFile();

            Environment.Exit(exitCode);
        }

        private void HandleQuit(string module, string[] args)
        {
            Shutdown();
        }

        private void HandleForceGC(string module, string[] args)
        {
            // Default is an full (gen2) but NON-forced GC.
            int gen = 2;
            GCCollectionMode mode = GCCollectionMode.Optimized;

            if (args.Length > 1)
            {
                // Any argument implies forced GC.
                mode = GCCollectionMode.Forced;
                switch (args[1].ToLower())
                {
                    case "now":
                    case "*":
                        gen = GC.MaxGeneration;
                        break;
                    case "0":
                    case "1":
                    case "2":
                        gen = Convert.ToInt32(args[1]);
                        break;
                    default:
                        m_log.Warn("Usage: forcegc [ 0|1|2|*|now ]");
                        return;
                }
            }

            GC.Collect(gen, mode, true);
        }

        private void HandleLogLevel(string module, string[] cmd)
        {
            if (null == m_consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }
      
            string rawLevel = cmd[3];
            
            ILoggerRepository repository = LogManager.GetRepository();
            Level consoleLevel = repository.LevelMap[rawLevel];
            
            if (consoleLevel != null)
                m_consoleAppender.Threshold = consoleLevel;
            else
                Notice(
                    String.Format(
                        "{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF",
                        rawLevel));

            Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
        }

        /// <summary>
        /// Show help information
        /// </summary>
        /// <param name="helpArgs"></param>
        protected virtual void ShowHelp(string[] helpArgs)
        {
            Notice(String.Empty);
            
            if (helpArgs.Length == 0)
            {
                Notice("set log level [level] - change the console logging level only.  For example, off or debug.");
                Notice("show info - show server information (e.g. startup path).");

                if (m_stats != null)
                    Notice("show stats - show statistical information for this server");

                Notice("show threads - list tracked threads");
                Notice("show uptime - show server startup time and uptime.");
                Notice("show version - show server version.");
                Notice(String.Empty);

                return;
            }
        }

        public virtual void HandleShow(string module, string[] cmd)
        {            
            List<string> args = new List<string>(cmd);

            args.RemoveAt(0);

            string[] showParams = args.ToArray();

            switch (showParams[0])
            {                       
                case "info":
                    Notice("Version: " + VersionInfo.Version);
                    Notice("Startup directory: " + m_startupDirectory);
                    break;

                case "stats":
                    if (m_stats != null)
                        Notice(m_stats.Report());
                    break;

                case "threads":
                    Notice(GetThreadsReport());
                    break;

                case "uptime":
                    Notice(GetUptimeReport());
                    break;

                case "version":
                    Notice(
                        String.Format(
                            "Version: {0}", VersionInfo.Version));
                    break;
            }
        }

        public virtual void HandleNuke(string module, string[] cmd)
        {
        }

        public virtual void HandleBlacklistOwner(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistCreator(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistName(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistUser(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistRemove(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistClear(string module, string[] cmd)
        {
        }
        public virtual void HandleBlacklistShow(string module, string[] cmd)
        {
        }

        /// <summary>
        /// Console output is only possible if a console has been established.
        /// That is something that cannot be determined within this class. So
        /// all attempts to use the console MUST be verified.
        /// </summary>
        protected void Notice(string msg)
        {
            if (m_console != null)
            {
                m_console.Notice(msg);
            }
        }

        protected void CreatePIDFile(string path)
        {
            try
            {
                string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                FileStream fs = File.Create(path);
                System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                Byte[] buf = enc.GetBytes(pidstring);
                fs.Write(buf, 0, buf.Length);
                fs.Close();
                m_pidFile = path;
            }
            catch (Exception)
            {
            }
        }
        
        public string osSecret {
            // Secret uuid for the simulator
            get { return m_osSecret; }
            
        }

        public string StatReport(OSHttpRequest httpRequest)
        {
            // If we catch a request for "callback", wrap the response in the value for jsonp
            if( httpRequest.QueryString["callback"] != null)
            {
                return httpRequest.QueryString["callback"] + "(" + m_stats.XReport((DateTime.Now - m_startuptime).ToString() , VersionInfo.FullVersion ) + ");";
            } 
            else 
            {
                return m_stats.XReport((DateTime.Now - m_startuptime).ToString() , VersionInfo.FullVersion );
            }
        }
           
        protected void RemovePIDFile()
        {
            if (!String.IsNullOrEmpty(m_pidFile))
            {
                try
                {
                    File.Delete(m_pidFile);
                    m_pidFile = String.Empty;
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
