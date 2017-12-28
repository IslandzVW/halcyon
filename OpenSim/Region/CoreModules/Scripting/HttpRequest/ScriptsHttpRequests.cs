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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Text.RegularExpressions;

/*****************************************************
 *
 * ScriptsHttpRequests
 *
 * Implements the llHttpRequest and http_response
 * callback.
 *
 * Some stuff was already in LSLLongCmdHandler, and then
 * there was this file with a stub class in it.  So,
 * I am moving some of the objects and functions out of
 * LSLLongCmdHandler, such as the HttpRequestClass, the
 * start and stop methods, and setting up pending and
 * completed queues.  These are processed in the
 * LSLLongCmdHandler polling loop.  Similiar to the
 * XMLRPCModule, since that seems to work.
 *
 * //TODO
 *
 * HTTPS support
 *
 * Configurable timeout?
 * Configurable max response size?
 * Configurable
 *
 * **************************************************/

namespace OpenSim.Region.CoreModules.Scripting.HttpRequest
{
    public class HttpRequestModule : IRegionModule, IHttpRequestModule
    {
        private Amib.Threading.SmartThreadPool _threadPool;
        private Amib.Threading.SmartThreadPool _slowPool;
        private readonly object m_httpListLock = new object();
        private const int HTTP_TIMEOUT = 60 * 1000;          // 60 seconds
        private const string m_name = "HttpScriptRequests";

        /// <summary>
        /// The maximum number of inflight requests for the entire region before we start
        /// returning null request IDs
        /// </summary>
        private const int MAX_REQUEST_QUEUE_SIZE = 200;

        /// <summary>
        /// The maximum number of slow HTTP requests we allow in the queue
        /// </summary>
        private const int MAX_SLOW_REQUEST_QUEUE_SIZE = 50;

        /// <summary>
        /// The maximum number of inflight requests for a single object before we start
        /// returning null request IDs
        /// </summary>
        private const int MAX_SINGLE_OBJECT_QUEUE_SIZE = 10;

        /// <summary>
        /// The maximum number of threads to use for normal priority requests
        /// </summary>
        private const int MAX_NORMAL_THREADS = 24;

        /// <summary>
        /// The maximum number of threads to use for known slow requests
        /// </summary>
        private const int MAX_SLOW_THREADS = 8;

        /// <summary>
        /// The amount of time above which we consider a script to be slow (ms)
        /// </summary>
        private const int SLOW_SCRIPT_TIME = 3000;

        /// <summary>
        /// The amount of time above which we consider a script to be normal (ms)
        /// Objects that respond faster than this time will have their priorities raised
        /// if their priority is currently below normal
        /// </summary>
        private const int NORMAL_SCRIPT_TIME = 1000;

        /// <summary>
        /// The number of seconds we wait before checking the priority list for expired objects
        /// </summary>
        private const int PRIORITY_MAINT_SECS = 300;

        /// <summary>
        /// The number of seconds we remember the lower priority of misbehaving scripts
        /// </summary>
        private const int PRIORITY_TIMEOUT_SECS = 600;

        private string m_proxyurl = String.Empty;
        private string m_proxyexcepts = String.Empty;

        // <request id, HttpRequestClass>
        private Dictionary<UUID, HttpRequestObject> m_pendingRequests;
        private Queue<HttpRequestObject> m_completedRequests;
        private Scene m_scene;

        /// <summary>
        /// Tracks the number of http requests per script
        /// </summary>
        private Dictionary<UUID, short> m_objectQueueSize;

        /// <summary>
        /// A debugging log reporting level, 0 is off.  See HttpRequestConsoleCommand().
        /// </summary>
        private int m_debugLevel = 0;

        /// <summary>
        /// Priorities for HTTP calls coming from an object based on the history of its behavior
        /// </summary>
        private Dictionary<UUID, TimestampedItem<Amib.Threading.WorkItemPriority>> m_objectPriorities;

        /// <summary>
        /// The last time in ticks that we maintained the priority list
        /// </summary>
        private ulong m_lastPriorityMaintenance;

        /// <summary>
        /// The number of low priority jobs that are currently queued
        /// </summary>
        private int m_numLowPriorityQueued;


        public HttpRequestModule()
        {
            ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
        }

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IHttpRequestModule>(this);
            m_proxyurl = config.Configs["Startup"].GetString("HttpProxy");
            m_proxyexcepts = config.Configs["Startup"].GetString("HttpProxyExceptions");
            m_pendingRequests = new Dictionary<UUID, HttpRequestObject>();
            m_completedRequests = new Queue<HttpRequestObject>();
            m_objectQueueSize = new Dictionary<UUID, short>();
            m_objectPriorities = new Dictionary<UUID, TimestampedItem<Amib.Threading.WorkItemPriority>>();
            m_lastPriorityMaintenance = Util.GetLongTickCount();

            _threadPool = new Amib.Threading.SmartThreadPool(Amib.Threading.SmartThreadPool.DefaultIdleTimeout, MAX_NORMAL_THREADS, 2);
            _threadPool.Name = "HttpRequestWorkerNormal";

            _slowPool = new Amib.Threading.SmartThreadPool(Amib.Threading.SmartThreadPool.DefaultIdleTimeout, MAX_SLOW_THREADS, 0);
            _slowPool.Name = "HttpRequestWorkerSlow";

            ReadBlacklistFromConfig(config.Configs["HttpRequest"]);


            MainConsole.Instance.Commands.AddCommand(
                "Comms",
                true,
                "httprequest queuelength",
                "httprequest queuelength",
                "Report on the current size of the request queue.",
                "Displays the current size of the request queue.",
                HttpRequestConsoleCommand);
            MainConsole.Instance.Commands.AddCommand(
                "Comms",
                true,
                "httprequest debug",
                "httprequest debug <debuglevel>",
                "Enable/disable debugging of the request queue.",
                "Enable/disable debugging of the request queue.",
                HttpRequestConsoleCommand);
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region IHttpRequestModule Members

        public int OutstandingRequests
        {
            get { return m_pendingRequests.Count; }
        }

        public float RequestQueueFreeSpacePercentage
        {
            get
            {
                if (OutstandingRequests >= MAX_REQUEST_QUEUE_SIZE)
                    return 0.0f;    // No space
                else
                    return 1.0f - (float)OutstandingRequests / MAX_REQUEST_QUEUE_SIZE;
            }
        }

        // Called only from HTTP_CUSTOM_HEADER.
        private bool ScriptCanChangeHeader(string key)
        {
            string targetKey = key.ToLower();

            // Content-type must be set via HTTP_MIMETYPE
            if (targetKey == "content-type")
                return false;

            // These are reserved for internal use only.
            // if (targetKey == "user-agent")   // SL allows appending multi-line text on the end of the URL to achieve this.
            //    return false;                 // instead we'll just allow it normally.
            if (targetKey.StartsWith("x-secondlife"))
                return false;

            return true;
        }

        public UUID StartHttpRequest(UUID sogId, uint localID, UUID itemID, string url, string[] parms, Dictionary<string, string> headers, string body)
        {
            //if there are already too many requests globally, reject this one
            if (RequestQueueFreeSpacePercentage == 0.0f) return UUID.Zero;

            // fast exit for the common case of scripter error passing an empty URL
            if (String.IsNullOrEmpty(url)) return UUID.Zero;

            if (BlockedByBlacklist(url)) return UUID.Zero;

            UUID reqID = UUID.Random();
            HttpRequestObject htc = new HttpRequestObject();

            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            //
            // Parameters are expected in {key, value, ... , key, value}
            try
            {
                int i = 0;
                while (i < parms.Length)
                {
                    switch (Int32.Parse(parms[i++]))
                    {
                        case (int)HttpRequestConstants.HTTP_METHOD:
                            // This one was validated to be one of the valid values for method in LSLSystemAPI.cs
                            htc.HttpMethod = parms[i++];
                            break;

                        case (int)HttpRequestConstants.HTTP_MIMETYPE:
                            htc.HttpMIMEType = parms[i++];
                            break;

                        case (int)HttpRequestConstants.HTTP_BODY_MAXLENGTH:
                            htc.HttpBodyMaxLength = int.Parse(parms[i++]);
                            break;

                        case (int)HttpRequestConstants.HTTP_VERIFY_CERT:
                            htc.HttpVerifyCert = (int.Parse(parms[i++]) != 0);
                            break;

                        case (int)HttpRequestConstants.HTTP_VERBOSE_THROTTLE:
                            htc.HttpVerboseThrottle = (int.Parse(parms[i++]) != 0);
                            break;

                        case (int)HttpRequestConstants.HTTP_CUSTOM_HEADER:
                            string key = parms[i++];
                            string value = parms[i++];
                            // The script is not allowed to override some of the headers.
                            if (ScriptCanChangeHeader(key))
                            {
                                if (headers.ContainsKey(key))
                                {
                                    // In SL, duplicate headers add to the existing header after a comma+space
                                    headers[key] += ", " + value;
                                }
                                else
                                {
                                    headers.Add(key, value);
                                }
                            }
                            break;

                        case (int)HttpRequestConstants.HTTP_PRAGMA_NO_CACHE:
                            // What if there is more than one pragma header defined?
                            bool noCache = (int.Parse(parms[i++]) != 0);
                            if (noCache == true)
                                headers.Add("Pragma", "no-cache");
                            else if (headers.ContainsKey("Pragma"))
                                headers.Remove("Pragma");
                            break;

                        default:
                            // Invalid Parameter Type.  Log an error?
                            // Fail the request by returning a Null Key
                            return UUID.Zero;
                    }
                }
            }
            catch (System.IndexOutOfRangeException)
            {
                // They passed us junk, we stepped past the end of the array.
                // We'll fail the request by returning a NULL_KEY
                return UUID.Zero;
            }

            if (m_debugLevel >= 1)
                MainConsole.Instance.OutputFormat("httprequest: LocalID={0} URL={1}", localID, url);

            htc.LocalID = localID;
            htc.ItemID = itemID;
            htc.SogID = sogId;
            htc.Url = url;
            htc.ReqID = reqID;
            htc.HttpTimeout = HTTP_TIMEOUT;
            htc.OutboundBody = body;
            htc.ResponseHeaders = headers;
            htc.proxyurl = m_proxyurl;
            htc.proxyexcepts = m_proxyexcepts;

            Amib.Threading.WorkItemPriority prio;
            lock (m_httpListLock)
            {
                //if there are already too many requests for this script running, reject this one
                if (!TryIncrementObjectQueue(sogId))
                {
                    return UUID.Zero;
                }

                prio = GetObjectPriority(sogId);
                if (prio == Amib.Threading.WorkItemPriority.Lowest)
                {
                    if (m_numLowPriorityQueued < MAX_SLOW_REQUEST_QUEUE_SIZE)
                    {
                        htc.IsLowPriority = true;
                        ++m_numLowPriorityQueued;
                    }
                    else
                    {
                        DecrementObjectQueue(sogId);
                        return UUID.Zero;
                    }
                }

                m_pendingRequests.Add(reqID, htc);
            }

            if (prio > Amib.Threading.WorkItemPriority.Lowest)
            {
                _threadPool.QueueWorkItem(() => this.ProcessRequest(htc));
            }
            else
            {
                _slowPool.QueueWorkItem(() => this.ProcessRequest(htc));
            }

            return reqID;
        }

        private bool TryIncrementObjectQueue(UUID sogId)
        {
            short size;
            if (m_objectQueueSize.TryGetValue(sogId, out size))
            {
                if (size >= MAX_SINGLE_OBJECT_QUEUE_SIZE)
                {
                    return false;
                }
                else
                {
                    m_objectQueueSize[sogId] = ++size;
                }
            }
            else
            {
                m_objectQueueSize.Add(sogId, 1);
            }

            return true;
        }

        private void ProcessRequest(HttpRequestObject req)
        {
            req.Process();

            lock (m_httpListLock)
            {
                m_pendingRequests.Remove(req.ReqID);
                m_completedRequests.Enqueue(req);

                DecrementObjectQueue(req.SogID);
                AdjustObjectPriority(req);
                MaintainPriorityQueue();
            }
        }

        private void MaintainPriorityQueue()
        {
            if (Util.GetLongTickCount() - m_lastPriorityMaintenance > PRIORITY_MAINT_SECS)
            {
                List<UUID> expiredObjects = new List<UUID>();
                foreach (var kvp in m_objectPriorities)
                {
                    if (kvp.Value.ElapsedSeconds >= PRIORITY_TIMEOUT_SECS)
                    {
                        expiredObjects.Add(kvp.Key);
                    }
                }

                foreach (var id in expiredObjects)
                {
                    m_objectPriorities.Remove(id);
                }

                m_lastPriorityMaintenance = Util.GetLongTickCount();
            }
        }

        /// <summary>
        /// Based on the time it took a request to run, we adjust the priority
        /// of future requests sent by an object
        /// </summary>
        /// <param name="req"></param>
        private void AdjustObjectPriority(HttpRequestObject req)
        {
            Amib.Threading.WorkItemPriority prio = GetObjectPriority(req.SogID);

            if (req.RequestDuration >= SLOW_SCRIPT_TIME)
            {
                if (prio > Amib.Threading.WorkItemPriority.Lowest)
                {
                    prio--;
                    SetObjectPriority(req.SogID, prio);
                }
            }
            else if (req.RequestDuration < NORMAL_SCRIPT_TIME)
            {
                if (prio < Amib.Threading.WorkItemPriority.Normal)
                {
                    prio++;
                    SetObjectPriority(req.SogID, prio);
                }
            }
            else
            {
                //just ping the timestamp if it exists
                UpdatePriorityTimestamp(req.SogID);
            }

            //also if this request was low priority, maintain the counts
            --m_numLowPriorityQueued;
        }

        private void UpdatePriorityTimestamp(UUID sogId)
        {
            TimestampedItem<Amib.Threading.WorkItemPriority> prio;
            if (m_objectPriorities.TryGetValue(sogId, out prio))
            {
                prio.ResetTimestamp();
            }
        }

        private void SetObjectPriority(UUID sogId, Amib.Threading.WorkItemPriority newPrio)
        {
            TimestampedItem<Amib.Threading.WorkItemPriority> prio;
            if (m_objectPriorities.TryGetValue(sogId, out prio))
            {
                prio.Item = newPrio;
                prio.ResetTimestamp();
            }
            else
            {
                m_objectPriorities[sogId] = new TimestampedItem<Amib.Threading.WorkItemPriority>(newPrio);
            }
        }

        private Amib.Threading.WorkItemPriority GetObjectPriority(UUID sogId)
        {
            TimestampedItem<Amib.Threading.WorkItemPriority> prio;
            if (! m_objectPriorities.TryGetValue(sogId, out prio))
            {
                return Amib.Threading.WorkItemPriority.Normal;
            }

            return prio.Item;
        }

        private void DecrementObjectQueue(UUID sogId)
        {
            short size;
            if (m_objectQueueSize.TryGetValue(sogId, out size))
            {
                --size;
                if (size == 0)
                {
                    m_objectQueueSize.Remove(sogId);
                }
                else
                {
                    m_objectQueueSize[sogId] = size;
                }
            }
        }

        public void StopHttpRequest(uint m_localID, UUID m_itemID)
        {
            if (m_pendingRequests != null)
            {
                lock (m_httpListLock)
                {
                    m_objectQueueSize.Remove(m_itemID);
                }
            }

            //The code below has been commented out because it doesn't work
            //the problem is that m_itemID is not being used as the key for
            //m_pendingRequests. m_pendingRequests key is the randomly generated
            //request id generated in StartHttpRequestAbove

            //TODO: Actually abort requests that haven't been started yet

            /*
            if (m_pendingRequests != null)
            {
                lock (HttpListLock)
                {
                    HttpRequestClass tmpReq;
                    if (m_pendingRequests.TryGetValue(m_itemID, out tmpReq))
                    {
                        m_pendingRequests.Remove(m_itemID);
                    }
                }
            }
             */
        }

        /*
        * TODO
        * Not sure how important ordering is is here - the next first
        * one completed in the list is returned, based soley on its list
        * position, not the order in which the request was started or
        * finsihed.  I thought about setting up a queue for this, but
        * it will need some refactoring and this works 'enough' right now
        */

        public IServiceRequest GetNextCompletedRequest()
        {
            lock (m_httpListLock)
            {
                if (m_completedRequests.Count == 0) return null;
                else return m_completedRequests.Dequeue();
            }
        }

        #endregion

        protected void HttpRequestConsoleCommand(string module, string[] args)
        {
            if (args.Length < 2)
                return;
            if (args[1] == "queuelength")
            {
                MainConsole.Instance.OutputFormat(
                    "Region {0} httprequest queue contains {1} requests",
                    m_scene.RegionInfo.RegionName,
                    OutstandingRequests);
                return;
            }
            if (args[1] == "debug")
            {
                if (args.Length >= 3)
                    m_debugLevel = Convert.ToInt32(args[2]);
                MainConsole.Instance.OutputFormat("httprequest debug level is {0}", m_debugLevel);
                return;
            }
        }    
    
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            HttpWebRequest Request = (HttpWebRequest) sender;

            if (Request.Headers.Get("NoVerifyCert") != null)
                return true;
            
              // If the certificate is a valid, signed certificate, return true.
              if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                return true;

              // If there are errors in the certificate chain, look at each error to determine the cause.
              if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) != 0)
              {
                if (chain != null && chain.ChainStatus != null)
                {
                      foreach (System.Security.Cryptography.X509Certificates.X509ChainStatus status in chain.ChainStatus)
                      {
                        if ((certificate.Subject == certificate.Issuer) &&
                               (status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot))
                        {
                              // Self-signed certificates with an untrusted root are valid. 
                              continue;
                        }
                        else
                        {
                              if (status.Status != System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError)
                              {
                                // If there are any other errors in the certificate chain, the certificate is invalid,
                                 // so the method returns false.
                                return false;
                              }
                        }
                      }
                }

                // When processing reaches this line, the only errors in the certificate chain are 
                // untrusted root errors for self-signed certificates. These certificates are valid
                // for default Exchange server installations, so return true.
                return true;
              }
              else
              {
                 // In all other cases, return false.
                return false;
              }
        }

        #region Blacklist Checks

        private List<string> _hostsBlacklist = new List<string>();
        private List<int> _portsBlacklist = new List<int>();
        private List<KeyValuePair<string, int>> _hostnameAndPortBlacklist = new List<KeyValuePair<string, int>>();

        /// <summary>
        /// Reads the blacklist config from the given configuration file
        /// </summary>
        /// <param name="config"></param>
        private void ReadBlacklistFromConfig(IConfig config)
        {
            try
            {
                if (config == null)
                    return;

                string hostBlacklist = config.GetString("HostBlacklist", String.Empty);
                string portBlacklist = config.GetString("PortBlacklist", String.Empty);
                string hostnameAndPortBlacklist = config.GetString("HostnameAndPortBlacklist", String.Empty);

                if (!string.IsNullOrEmpty(hostBlacklist))
                {
                    string[] hosts = hostBlacklist.Split(',');
                    foreach (string host in hosts)
                        _hostsBlacklist.Add(WildcardToRegex(host));
                }

                if (!string.IsNullOrEmpty(portBlacklist))
                {
                    string[] ports = portBlacklist.Split(',');
                    foreach (string port in ports)
                        _portsBlacklist.Add(int.Parse(port));
                }

                if (!string.IsNullOrEmpty(hostnameAndPortBlacklist))
                {
                    string[] hosts = hostnameAndPortBlacklist.Split(',');
                    foreach (string host in hosts)
                    {
                        string[] url = host.Split(':');
                        _hostnameAndPortBlacklist.Add(new KeyValuePair<string, int>(WildcardToRegex(url[0]), int.Parse(url[1])));
                    }
                }

                /*
                Tests with config as follows
                HostBlacklist = "10.0.0.1,20.0.0.*,google.com"
                PortBlacklist = "8010,8020"
                HostnameAndPortBlacklist = "192.168.1.*:80,yahoo.com:1234"
                
                bool blocked;
                blocked = BlockedByBlacklist("http://10.0.0.1:1234"); //true
                blocked = BlockedByBlacklist("http://10.0.0.2:1234"); //false
                blocked = BlockedByBlacklist("http://20.0.0.2:1234"); //true
                blocked = BlockedByBlacklist("http://20.0.0.1:1234"); //true
                blocked = BlockedByBlacklist("http://1.2.3.4:1234"); //false
                blocked = BlockedByBlacklist("http://1.2.3.4:8010"); //true
                blocked = BlockedByBlacklist("http://1.2.3.4:8020"); //true
                blocked = BlockedByBlacklist("http://192.168.1.1:8080"); //false
                blocked = BlockedByBlacklist("http://192.168.1.1:80"); //true
                blocked = BlockedByBlacklist("http://192.168.1.1/test.html"); 
                blocked = BlockedByBlacklist("http://google.com/test.html");//true
                blocked = BlockedByBlacklist("http://yahoo.com/test.html");//false
                blocked = BlockedByBlacklist("http://yahoo.com:1234/test.html");//true
                */
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Output("[ScriptsHttpRequest]: Failed to parse blacklist config: " + ex.ToString());
            }
        }

        /// <summary>
        /// Checks to make sure that a url is allowed by the blacklist
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool BlockedByBlacklist(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                if (uri.Scheme != "http" && uri.Scheme != "https") return true;

                //Port check
                if (_portsBlacklist.Contains(uri.Port))
                    return true;

                //Hostname check
                if (_hostsBlacklist.Any((h) => Regex.IsMatch(uri.Host, h)))
                    return true;

                //Hostname+port check
                if (_hostnameAndPortBlacklist.Any((kvp) => Regex.IsMatch(uri.Host, kvp.Key) && kvp.Value == uri.Port))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Output("[ScriptsHttpRequest]: Failed to parse URL for blacklist check '" + url + "': " + ex.ToString());
            }
            return true;
        }

        /// <summary>
        /// Converts a normal wildcard (e.g. 192.168.1.*) to a proper regular expression (^192\.168\.1\..*$)
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        private string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                       + "$";
        }

        #endregion
    }
}
