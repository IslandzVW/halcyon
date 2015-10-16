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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse.Assets;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    public class J2KDecoderModule : IRegionModule, IJ2KDecoder
    {
        #region IRegionModule Members

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Cached Decoded Layers
        /// </summary>
        private bool OpenJpegFail = false;
        private J2KDecodeFileCache fCache;

        /// <summary>
        /// List of client methods to notify of results of decode
        /// </summary>
        private readonly Dictionary<UUID, List<DecodedCallback>> m_notifyList = new Dictionary<UUID, List<DecodedCallback>>();

        public J2KDecoderModule()
        {
        }

        public void Initialize(Scene scene, IConfigSource source)
        {
            bool useFileCache = true;
            IConfig myConfig = source.Configs["J2KDecoder"];

            if (myConfig == null)
                return;

            if (myConfig.GetString("J2KDecoderModule", String.Empty) != Name)
                return;

            useFileCache = myConfig.GetBoolean("J2KDecoderFileCacheEnabled", true);

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

        public string Name
        {
            get { return "J2KDecoderModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region IJ2KDecoder Members


        public void BeginDecode(UUID AssetId, byte[] assetData, DecodedCallback decodedReturn)
        {
            // Dummy for if decoding fails.
            OpenJPEG.J2KLayerInfo[] result = new OpenJPEG.J2KLayerInfo[0];

            // Check if it's cached
            bool cached = false;
            
            lock (fCache)
            {
                cached = fCache.TryLoadCacheForAsset(AssetId, out result);
            }

            // If it's cached, return the cached results
            if (cached)
            {
                decodedReturn(AssetId, result);
            }
            else
            {
                // not cached, so we need to decode it
                // Add to notify list and start decoding.
                // Next request for this asset while it's decoding will only be added to the notify list
                // once this is decoded, requests will be served from the cache and all clients in the notifylist will be updated
                bool decode = false;
                lock (m_notifyList)
                {
                    if (m_notifyList.ContainsKey(AssetId))
                    {
                        m_notifyList[AssetId].Add(decodedReturn);
                    }
                    else
                    {
                        List<DecodedCallback> notifylist = new List<DecodedCallback>();
                        notifylist.Add(decodedReturn);
                        m_notifyList.Add(AssetId, notifylist);
                        decode = true;
                    }
                }

                // Do Decode!
                if (decode)
                {
                    Decode(AssetId, assetData);
                }
            }
        }
        
        public bool Decode(UUID assetID, byte[] j2kData)
        {
            OpenJPEG.J2KLayerInfo[] layers;
            return Decode(assetID, j2kData, out layers);
        }

        public bool Decode(UUID assetID, byte[] j2kData, out OpenJPEG.J2KLayerInfo[] layers)
        {
            bool decodedSuccessfully = true;
            layers = new OpenJPEG.J2KLayerInfo[0]; // Dummy result for if it fails.  Informs that there's only full quality

            if (! OpenJpegFail)
            {  
                lock (fCache)
                {
                    decodedSuccessfully = fCache.TryLoadCacheForAsset(assetID, out layers);
                }
                
                if (!decodedSuccessfully)
                    decodedSuccessfully = DoJ2KDecode(assetID, j2kData, out layers);
            }

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
        
        #endregion

        /// <summary>
        /// Decode Jpeg2000 Asset Data
        /// </summary>
        /// <param name="AssetId">UUID of Asset</param>
        /// <param name="j2kdata">Byte Array Asset Data </param>
        private bool DoJ2KDecode(UUID assetID, byte[] j2kdata, out OpenJPEG.J2KLayerInfo[] layers)
        {
            int DecodeTime = 0;
            DecodeTime = Environment.TickCount;
            bool decodedSuccessfully = true;
            
            layers = new OpenJPEG.J2KLayerInfo[0]; // Dummy result for if it fails.  Informs that there's only full quality

            try
            {

                AssetTexture texture = new AssetTexture(assetID, j2kdata);
                bool sane = false;

                if (texture.DecodeLayerBoundaries())
                {
                    sane = true;

                    // Sanity check all of the layers
                    for (int i = 0; i < texture.LayerInfo.Length; i++)
                    {
                        if (texture.LayerInfo[i].End > texture.AssetData.Length)
                        {
                            sane = false;
                            break;
                        }
                    }
                }

                if (sane)
                {
                    m_log.InfoFormat("[J2KDecoderModule]: {0} Decode Time: {1}", Environment.TickCount - DecodeTime, assetID); 
                    layers = texture.LayerInfo;
                }
                else
                {
                    m_log.Warn("[J2KDecoderModule]: Failed to decode layer data for texture " + assetID + ", guessing sane defaults");
                    decodedSuccessfully = false;
                    
                    // Layer decoding completely failed. Guess at sane defaults for the layer boundaries
                    layers = CreateDefaultLayers(j2kdata.Length);
                }
                
                fCache.SaveCacheForAsset(assetID, layers); 
                texture = null; // dereference and dispose of ManagedImage
            }
            catch (DllNotFoundException)
            {
                m_log.Error(
                    "[J2KDecoderModule]: OpenJpeg is not installed properly. Decoding disabled!  This will slow down texture performance!  Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
                OpenJpegFail = true;
                decodedSuccessfully = false;
            }
            catch (Exception ex)
            {
                m_log.WarnFormat(
                    "[J2KDecoderModule]: JPEG2000 texture decoding threw an exception for {0}, {1}",
                    assetID, ex);
                decodedSuccessfully = false;
            }

            return (decodedSuccessfully);
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
