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

using System;
using System.Collections.Generic;
using System.Text;
using Nini.Config;
using log4net;
using OpenSim.Data.SimpleDB;

namespace OpenSim.Region.OptionalModules.Avatar.FlexiGroups
{
    class ProviderFactory
    {
        public static IGroupDataProvider GetProviderFromConfigName(ILog log, IConfig groupsConfig, string configName)
        {
            switch (configName)
            {
                case "XmlRpc":
                    string ServiceURL = groupsConfig.GetString("XmlRpcServiceURL");
                    bool DisableKeepAlive = groupsConfig.GetBoolean("XmlRpcDisableKeepAlive", false);

                    string ServiceReadKey = groupsConfig.GetString("XmlRpcServiceReadKey", String.Empty);
                    string ServiceWriteKey = groupsConfig.GetString("XmlRpcServiceWriteKey", String.Empty);

                    log.InfoFormat("[GROUPS]: XmlRpc Service URL set to: {0}", ServiceURL);

                    return new XmlRpcGroupDataProvider(ServiceURL, DisableKeepAlive, ServiceReadKey, ServiceWriteKey);


                case "Native":
                    string dbType = groupsConfig.GetString("NativeProviderDBType");
                    string connStr = groupsConfig.GetString("NativeProviderConnString");

                    ConnectionFactory connFactory = new ConnectionFactory(dbType, connStr);
                    return new NativeGroupDataProvider(connFactory);

            }

            return null;
        }
    }
}
