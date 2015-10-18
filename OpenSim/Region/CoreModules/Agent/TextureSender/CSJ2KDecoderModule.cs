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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Reflection;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using System.Threading;
using log4net;
using OpenMetaverse.Imaging;
using CSJ2K;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Region.CoreModules.Agent.TextureSender;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    public class CSJ2KDecoderModule : IRegionModule, IJ2KDecoder
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Cache for decoded layer data</summary>
        private J2KDecodeFileCache fCache;
        /// <summary>List of client methods to notify of results of decode</summary>
        private readonly Dictionary<UUID, List<DecodedCallback>> m_notifyList = new Dictionary<UUID, List<DecodedCallback>>();
        /// <summary>Reference to a scene (doesn't matter which one as long as it can load the cache module)</summary>
        private Scene m_scene;

        #region IRegionModule

        public CSJ2KDecoderModule()
        {
        }

        public string Name { get { return "CSJ2KDecoderModule"; } }
        public bool IsSharedModule { get { return true; } }

        public void Initialize(Scene scene, IConfigSource source)
        {
            if (m_scene == null)
                m_scene = scene;

            bool useFileCache = false; 
            IConfig myConfig = source.Configs["J2KDecoder"]; 

            if (myConfig != null)
            {
                if (myConfig.GetString("J2KDecoderModule", Name) != Name)
                    return;

                useFileCache = myConfig.GetBoolean("J2KDecoderFileCacheEnabled", false);
            }
            else
            {
                m_log.DebugFormat("[J2K DECODER MODULE] No decoder specified, Using defaults");
            }


            m_log.DebugFormat("[J2K DECODER MODULE] Using {0} decoder. File Cache is {1}",
                Name, (useFileCache ? "enabled" : "disabled"));

            fCache = new J2KDecodeFileCache(useFileCache, J2KDecodeFileCache.CacheFolder);
            scene.RegisterModuleInterface<IJ2KDecoder>(this);
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        #endregion IRegionModule

        #region IJ2KDecoder

        public void BeginDecode(UUID assetID, byte[] j2kData, DecodedCallback callback)
        {
            OpenJPEG.J2KLayerInfo[] result;
            bool decodedSuccessfully = false;

            lock (fCache)
            {
                decodedSuccessfully = fCache.TryLoadCacheForAsset(assetID, out result);
            }

            if (decodedSuccessfully)
            {
                callback(assetID, result);
                return;
            }

            // Not cached, we need to decode it.
            // Add to notify list and start decoding.
            // Next request for this asset while it's decoding will only be added to the notify list
            // once this is decoded, requests will be served from the cache and all clients in the notifylist will be updated
            bool decode = false;
            lock (m_notifyList)
            {
                if (m_notifyList.ContainsKey(assetID))
                {
                    m_notifyList[assetID].Add(callback);
                }
                else
                {
                    List<DecodedCallback> notifylist = new List<DecodedCallback>();
                    notifylist.Add(callback);
                    m_notifyList.Add(assetID, notifylist);
                    decode = true;
                }
            }

            // Do Decode!
            if (decode)
                Decode(assetID, j2kData);
        }

        public bool Decode(UUID assetID, byte[] j2kData)
        {
            OpenJPEG.J2KLayerInfo[] layers;
            return Decode(assetID, j2kData, out layers);
        }

        public bool Decode(UUID assetID, byte[] j2kData, out OpenJPEG.J2KLayerInfo[] layers)
        {
            bool decodedSuccessfully = true;

            lock (fCache)
            {
                decodedSuccessfully = fCache.TryLoadCacheForAsset(assetID, out layers);
            }

            if (!decodedSuccessfully)
                decodedSuccessfully = DoJ2KDecode(assetID, j2kData, out layers);

            // Notify Interested Parties
            lock (m_notifyList)
            {
                if (m_notifyList.ContainsKey(assetID))
                {
                    foreach (DecodedCallback d in m_notifyList[assetID])
                    {
                        if (d != null)
                            d.DynamicInvoke(assetID, layers);
                    }
                    m_notifyList.Remove(assetID);
                }
            }

            return (decodedSuccessfully);
        }

        #endregion IJ2KDecoder

        /// <summary>
        /// Decode Jpeg2000 Asset Data
        /// </summary>
        /// <param name="assetID">UUID of Asset</param>
        /// <param name="j2kData">JPEG2000 data</param>
        /// <param name="layers">layer data</param>
        /// <param name="components">number of components</param>
        /// <returns>true if decode was successful.  false otherwise.</returns>
        private bool DoJ2KDecode(UUID assetID, byte[] j2kData, out OpenJPEG.J2KLayerInfo[] layers)
        {
            bool decodedSuccessfully = true;
            int DecodeTime = Environment.TickCount;
            layers = new OpenJPEG.J2KLayerInfo[0]; // Dummy result for if it fails.  Informs that there's only full quality

            try
            {
                using (MemoryStream ms = new MemoryStream(j2kData))
                {
                    List<int> layerStarts = CSJ2K.J2kImage.GetLayerBoundaries(ms);

                    if (layerStarts != null && layerStarts.Count > 0)
                    {
                        layers = new OpenJPEG.J2KLayerInfo[layerStarts.Count];

                        for (int i = 0; i < layerStarts.Count; i++)
                        {
                            OpenJPEG.J2KLayerInfo layer = new OpenJPEG.J2KLayerInfo();

                            if (i == 0)
                                layer.Start = 0;
                            else
                                layer.Start = layerStarts[i];

                            if (i == layerStarts.Count - 1)
                                layer.End = j2kData.Length;
                            else
                                layer.End = layerStarts[i + 1] - 1;

                            layers[i] = layer;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("[J2KDecoderModule]: CSJ2K threw an exception decoding texture " + assetID + ": " + ex.Message);
                decodedSuccessfully = false;
            }

            if (layers.Length == 0)
            {
                m_log.Warn("[J2KDecoderModule]: Failed to decode layer data for texture " + assetID + ", guessing sane defaults");

                // Layer decoding completely failed. Guess at sane defaults for the layer boundaries
                layers = CreateDefaultLayers(j2kData.Length);
                decodedSuccessfully = false;
            }
            else
            {
                int elapsed = Environment.TickCount - DecodeTime;
                if (elapsed >= 50)
                    m_log.InfoFormat("[J2KDecoderModule]: {0} Decode Time: {1}", elapsed, assetID);
                // Cache Decoded layers
                fCache.SaveCacheForAsset(assetID, layers);
            }

            return decodedSuccessfully;
        }

        private OpenJPEG.J2KLayerInfo[] CreateDefaultLayers(int j2kLength)
        {
            OpenJPEG.J2KLayerInfo[] layers = new OpenJPEG.J2KLayerInfo[5];

            for (int i = 0; i < layers.Length; i++)
                layers[i] = new OpenJPEG.J2KLayerInfo();

            // These default layer sizes are based on a small sampling of real-world texture data
            // with extra padding thrown in for good measure. This is a worst case fallback plan
            // and may not gracefully handle all real world data
            layers[0].Start = 0;
            layers[1].Start = (int)((float)j2kLength * 0.02f);
            layers[2].Start = (int)((float)j2kLength * 0.05f);
            layers[3].Start = (int)((float)j2kLength * 0.20f);
            layers[4].Start = (int)((float)j2kLength * 0.50f);

            layers[0].End = layers[1].Start - 1;
            layers[1].End = layers[2].Start - 1;
            layers[2].End = layers[3].Start - 1;
            layers[3].End = layers[4].Start - 1;
            layers[4].End = j2kLength;

            return layers;
        }
    }
}
