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
using System.Linq;
using System.Text;
using net.openstack.Providers.Rackspace;
using net.openstack.Core.Domain;
using System.IO;
using OpenMetaverse;
using JSIStudios.SimpleRESTServices.Client;
using net.openstack.Core.Exceptions.Response;
using OpenSim.Framework;
using log4net;
using System.Reflection;

namespace InWorldz.Data.Assets.Stratus
{
    /// <summary>
    /// This class does the actual setup work for the cloudfiles client. These objects are pooled
    /// and used by the worker threads.
    /// </summary>
    internal class CloudFilesAssetWorker
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// How many hex characters to use for the CF container prefix
        /// </summary>
        const int CONTAINER_UUID_PREFIX_LEN = 4;

        private CoreExt.ExtendedCloudFilesProvider _provider;

        public const int DEFAULT_READ_TIMEOUT = 45 * 1000;
        public const int DEFAULT_WRITE_TIMEOUT = 10 * 1000;

        public CloudFilesAssetWorker() : this(DEFAULT_READ_TIMEOUT, DEFAULT_WRITE_TIMEOUT)
        {

        }

        public CloudFilesAssetWorker(int readTimeout, int writeTimeout)
        {
            CloudIdentity identity = new CloudIdentity { Username = Config.Settings.Instance.CFUsername, APIKey = Config.Settings.Instance.CFApiKey };
            IRestService restService = new CoreExt.ExtendedJsonRestServices(readTimeout, writeTimeout);
            _provider = new CoreExt.ExtendedCloudFilesProvider(identity, Config.Settings.Instance.CFDefaultRegion, null, restService);
            
            //warm up
            _provider.GetAccountHeaders(useInternalUrl: Config.Settings.Instance.CFUseInternalURL, region: Config.Settings.Instance.CFDefaultRegion);
        }

        /// <summary>
        /// Calls into the cloud files provider to grab an object and returns a memory stream containing
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public MemoryStream GetAsset(UUID assetId)
        {
            string assetIdStr = assetId.ToString();

            MemoryStream memStream = new MemoryStream();
            try
            {
                this.WarnIfLongOperation("GetObject",
                    () => _provider.GetObject(GenerateContainerName(assetIdStr), GenerateAssetObjectName(assetIdStr),
                            memStream, useInternalUrl: Config.Settings.Instance.CFUseInternalURL, region: Config.Settings.Instance.CFDefaultRegion));

                return memStream;
            }
            catch
            {
                memStream.Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// CF containers are PREFIX_#### where we use the first N chars of the hex representation
        /// of the asset ID to partition the space. The hex alpha chars in the container name are uppercase
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        private static string GenerateContainerName(string assetId)
        {
            return Config.Settings.Instance.CFContainerPrefix + assetId.Substring(0, CONTAINER_UUID_PREFIX_LEN).ToUpper();
        }

        /// <summary>
        /// The object name is defined by the assetId, dashes stripped, with the .asset prefix
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        private static string GenerateAssetObjectName(string assetId)
        {
            return assetId.Replace("-", String.Empty).ToLower() + ".asset";
        }

        private void WarnIfLongOperation(string opName, Action operation)
        {
            const ulong WARNING_TIME = 5000;

            ulong startTime = Util.GetLongTickCount();
            operation();
            ulong time = Util.GetLongTickCount() - startTime;

            if (time >= WARNING_TIME)
            {
                m_log.WarnFormat("[InWorldz.Stratus]: Slow CF operation {0} took {1} ms", opName, time);
            }
        }

        internal MemoryStream StoreAsset(StratusAsset asset)
        {
            MemoryStream stream = new MemoryStream();

            try
            {
                ProtoBuf.Serializer.Serialize<StratusAsset>(stream, asset);
                stream.Position = 0;

                string assetIdStr = asset.Id.ToString();

                Dictionary<string, string> mheaders = this.GenerateStorageHeaders(asset, stream);


                this.WarnIfLongOperation("CreateObject",
                    () => _provider.CreateObject(GenerateContainerName(assetIdStr), stream, GenerateAssetObjectName(assetIdStr),
                            "application/octet-stream", headers: mheaders, useInternalUrl: Config.Settings.Instance.CFUseInternalURL,
                            region: Config.Settings.Instance.CFDefaultRegion)
                );

                stream.Position = 0;

                return stream;
            }
            catch (ResponseException e)
            {
                stream.Dispose();
                if (e.Response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    throw new AssetAlreadyExistsException(String.Format("Asset {0} already exists and can not be overwritten", asset.Id));
                }
                else
                {
                    throw;
                }
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private Dictionary<string, string> GenerateStorageHeaders(StratusAsset asset, MemoryStream stream)
        {
            //the HTTP headers only accept letters and digits
            StringBuilder fixedName = new StringBuilder();
            bool appended = false;
            foreach (char letter in asset.Name)
            {
                char c = (char) (0x000000ff & (uint) letter);
                if (c == 127 || (c < ' ' && c != '\t'))
                {
                    continue;
                }
                else
                {
                    fixedName.Append(letter);
                    appended = true;
                }
            }

            if (!appended) fixedName.Append("empty");

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                {"ETag", OpenSim.Framework.Util.Md5Hash(stream)},
                {"X-Object-Meta-Temp", asset.Temporary ? "1" : "0"},
                {"X-Object-Meta-Local", asset.Local ? "1" : "0"},
                {"X-Object-Meta-Type", asset.Type.ToString()},
                {"X-Object-Meta-Name", fixedName.ToString()}
            };

            if (!Config.Settings.Instance.EnableCFOverwrite)
            {
                headers.Add("If-None-Match", "*");
            }

            stream.Position = 0;

            return headers;
        }

        internal Dictionary<string, string> GetAssetMetadata(UUID assetId)
        {
            string assetIdStr = assetId.ToString();

            return _provider.GetObjectMetaData(GenerateContainerName(assetIdStr), GenerateAssetObjectName(assetIdStr),
                useInternalUrl: Config.Settings.Instance.CFUseInternalURL, region: Config.Settings.Instance.CFDefaultRegion);
        }

        internal void PurgeAsset(UUID assetID)
        {
            string assetIdStr = assetID.ToString();

            this.WarnIfLongOperation("DeleteObject", 
                ()=> _provider.DeleteObject(GenerateContainerName(assetIdStr), GenerateAssetObjectName(assetIdStr),
                        useInternalUrl: Config.Settings.Instance.CFUseInternalURL, region: Config.Settings.Instance.CFDefaultRegion));
        }
    }
}
