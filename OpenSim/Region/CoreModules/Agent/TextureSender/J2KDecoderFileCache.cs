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
    public class J2KDecodeFileCache
    {
        private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType); 

        private readonly string m_cacheDecodeFolder;
        private bool enabled = true;

        /// <summary>
        ///   Temporarily holds deserialized layer data information in memory
        /// </summary>
        private readonly ExpiringCache<UUID, OpenJPEG.J2KLayerInfo[]> m_decodedCache =
            new ExpiringCache<UUID, OpenJPEG.J2KLayerInfo[]>();

        /// <summary>
        /// Creates a new instance of a file cache
        /// </summary>
        /// <param name="pFolder">base folder for the cache.  Will be created if it doesn't exist</param>
        public J2KDecodeFileCache(bool enabled, string pFolder)
        {
            this.enabled = enabled;
            m_cacheDecodeFolder = pFolder;

            if ((enabled == true) && (Directory.Exists(pFolder) == false))
            {
                Createj2KCacheFolder(pFolder);
            }
        }
        
        public static string CacheFolder 
        { 
            get { return Util.dataDir() + "/j2kDecodeCache"; } 
        }
         
        /// <summary>
        /// Save Layers to Disk Cache
        /// </summary>
        /// <param name="AssetId">Asset to Save the layers. Used int he file name by default</param>
        /// <param name="Layers">The Layer Data from OpenJpeg</param>
        /// <returns></returns>
        public bool SaveCacheForAsset(UUID AssetId, OpenJPEG.J2KLayerInfo[] Layers)
        {
            if (Layers.Length > 0)
            {
                m_decodedCache.AddOrUpdate(AssetId, Layers, TimeSpan.FromMinutes(10));

                if (enabled == true)
                {
                    using (FileStream fsCache =
                        new FileStream(String.Format("{0}/{1}", m_cacheDecodeFolder, FileNameFromAssetId(AssetId)),
                                       FileMode.Create))
                    {
                        using (StreamWriter fsSWCache = new StreamWriter(fsCache))
                        {
                            StringBuilder stringResult = new StringBuilder();
                            string strEnd = "\n";
                            for (int i = 0; i < Layers.Length; i++)
                            {
                                if (i == (Layers.Length - 1))
                                    strEnd = String.Empty;

                                stringResult.AppendFormat("{0}|{1}|{2}{3}", Layers[i].Start, Layers[i].End, Layers[i].End - Layers[i].Start, strEnd);
                            }
                            fsSWCache.Write(stringResult.ToString());
                            fsSWCache.Close();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        
        /// <summary>
        /// Loads the Layer data from the disk cache
        /// Returns true if load succeeded
        /// </summary>
        /// <param name="AssetId">AssetId that we're checking the cache for</param>
        /// <param name="Layers">out layers to save to</param>
        /// <returns>true if load succeeded</returns>
        public bool TryLoadCacheForAsset(UUID AssetId, out OpenJPEG.J2KLayerInfo[] Layers)
        {
            // Check if it's cached in memory
            if (m_decodedCache.TryGetValue(AssetId, out Layers))
            {
                return true;
            }
            
            // Look for it in the file cache
            string filename = String.Format("{0}/{1}", m_cacheDecodeFolder, FileNameFromAssetId(AssetId));
            Layers = new OpenJPEG.J2KLayerInfo[0];

            if ((File.Exists(filename) == false) || (enabled == false))
                return false;

            string readResult = String.Empty;

            try
            {
                using (FileStream fsCachefile = new FileStream(filename, FileMode.Open))
                {

                    using (StreamReader sr = new StreamReader(fsCachefile))
                    {
                        readResult = sr.ReadToEnd();

                        sr.Close();
                    }
                }

            }
            catch (IOException ioe)
            {
                if (ioe is PathTooLongException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. Path is too long.");
                }
                else if (ioe is DirectoryNotFoundException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. Cache Directory does not exist!");
                    enabled = false;
                }
                else
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. IO Exception.");
                }
                return false;

            }
            catch (UnauthorizedAccessException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Read failed. UnauthorizedAccessException Exception. Do you have the proper permissions on this file?");
                return false;
            }
            catch (ArgumentException ae)
            {
                if (ae is ArgumentNullException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. No Filename provided");
                }
                else
                {
                    m_log.Error(
                   "[J2KDecodeCache]: Cache Read failed. Filname was invalid");
                }
                return false;
            }
            catch (NotSupportedException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Read failed, not supported. Cache disabled!");
                enabled = false;

                return false;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[J2KDecodeCache]: Cache Read failed, unknown exception.  Error: {0}",
                    e.ToString());
                return false;
            }

            string[] lines = readResult.Split('\n');

            if (lines.Length <= 0)
                return false;

            Layers = new OpenJPEG.J2KLayerInfo[lines.Length];
            
            for (int i = 0; i < lines.Length; i++)
            {
                string[] elements = lines[i].Split('|');
                if (elements.Length == 3)
                {
                    int element1, element2;

                    try
                    {
                        element1 = Convert.ToInt32(elements[0]);
                        element2 = Convert.ToInt32(elements[1]);
                    }
                    catch (FormatException)
                    {
                        m_log.WarnFormat("[J2KDecodeCache]: Cache Read failed with ErrorConvert for {0}", AssetId);
                        Layers = new OpenJPEG.J2KLayerInfo[0];
                        return false;
                    }

                    Layers[i] = new OpenJPEG.J2KLayerInfo();
                    Layers[i].Start = element1;
                    Layers[i].End = element2;

                }
                else
                {
                    // reading failed
                    m_log.WarnFormat("[J2KDecodeCache]: Cache Read failed for {0}", AssetId);
                    Layers = new OpenJPEG.J2KLayerInfo[0];
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Routine which converts assetid to file name
        /// </summary>
        /// <param name="AssetId">asset id of the image</param>
        /// <returns>string filename</returns>
        public string FileNameFromAssetId(UUID AssetId)
        {
            return String.Format("j2kCache_{0}.cache", AssetId);
        }

        /// <summary>
        /// Creates the Cache Folder
        /// </summary>
        /// <param name="pFolder">Folder to Create</param>
        public void Createj2KCacheFolder(string pFolder)
        {
            try
            {
                Directory.CreateDirectory(pFolder);
            }
            catch (IOException ioe)
            {
                if (ioe is PathTooLongException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the path to the cache folder is too long.  Cache disabled!");
                }
                else if (ioe is DirectoryNotFoundException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the supplied base of the directory folder does not exist.  Cache disabled!");
                }
                else
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an IO Exception.  Cache disabled!");
                }
                enabled = false;

            }
            catch (UnauthorizedAccessException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an UnauthorizedAccessException Exception.  Cache disabled!");
                enabled = false;
            }
            catch (ArgumentException ae)
            {
                if (ae is ArgumentNullException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the folder provided is invalid!  Cache disabled!");
                }
                else
                {
                    m_log.Error(
                   "[J2KDecodeCache]: Cache Directory does not exist and create failed because no cache folder was provided!  Cache disabled!");
                }
                enabled = false;
            }
            catch (NotSupportedException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because it's not supported.  Cache disabled!");
                enabled = false;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an unknown exception.  Cache disabled!  Error: {0}",
                    e.ToString());
                enabled = false;
            }
        }
    }
}
