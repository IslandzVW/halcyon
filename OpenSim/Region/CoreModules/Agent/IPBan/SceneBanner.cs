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

using System.Collections.Generic;
using System.Net;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.IPBan
{
    internal class SceneBanner
    {
                private static readonly log4net.ILog m_log
                    = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private List<string> bans;
        public SceneBanner(SceneBase scene, List<string> banList)
        {
            scene.EventManager.OnClientConnect += EventManager_OnClientConnect;

            bans = banList;
        }

        void EventManager_OnClientConnect(IClientCore client)
        {
            IClientIPEndpoint ipEndpoint;
            if(client.TryGet(out ipEndpoint))
            {
                IPAddress end = ipEndpoint.EndPoint;

                foreach (string ban in bans)
                {
                    if (end.ToString().StartsWith(ban))
                    {
                        client.Disconnect("Banned - network \"" + ban + "\" is not allowed to connect to this server.");
                        m_log.Warn("[IPBAN] Disconnected '" + end + "' due to '" + ban + "' ban.");
                        return;
                    }
                }

                //m_log.InfoFormat("[IPBAN] User \"{0}\" not in any ban lists. Allowing connection.", end);
            }
        }
    }
}
