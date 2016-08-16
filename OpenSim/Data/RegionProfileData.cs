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
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    /// <summary>
    /// A class which contains information known to the grid server about a region
    /// </summary>
    //[Serializable]
    public class RegionProfileData
    {
        /// <summary>
        /// The name of the region
        /// </summary>
        public string regionName = String.Empty;

        /// <summary>
        /// A 64-bit number combining map position into a (mostly) unique ID
        /// </summary>
        public ulong regionHandle;

        /// <summary>
        /// OGS/OpenSim Specific ID for a region
        /// </summary>
        public UUID UUID;

        /// <summary>
        /// Coordinates of the region
        /// </summary>
        public uint regionLocX;
        public uint regionLocY;
        public uint regionLocZ; // Reserved (round-robin, layers, etc)

        /// <summary>
        /// Authentication secrets
        /// </summary>
        /// <remarks>Not very secure, needs improvement.</remarks>
        public string regionSendKey = String.Empty;
        public string regionRecvKey = String.Empty;
        public string regionSecret = String.Empty;

        /// <summary>
        /// Whether the region is online
        /// </summary>
        public bool regionOnline;

        /// <summary>
        /// Information about the server that the region is currently hosted on
        /// </summary>
        public string serverHostName = String.Empty;
        public uint serverPort;

        public uint httpPort;
        public uint remotingPort;


        /// <summary>
        /// Preformatted URI containing the unresolved external host name and specified port, terminating in a slash.
        /// </summary>
        /// <remarks>See serverHostName and httpPort, but also alternates such as OutsideIP and OutsideIpOrResolvedHostname if you are wanting to make a fast direct connection that doesn't have to go through a DNS resolution at some point later.</remarks>
        public string httpServerURI
        {
            get { return "http://" + serverHostName + ":" + httpPort + "/"; }
        }

        /// <summary>
        /// Set of optional overrides. Can be used to create non-eulicidean spaces.
        /// </summary>
        public ulong regionNorthOverrideHandle;
        public ulong regionSouthOverrideHandle;
        public ulong regionEastOverrideHandle;
        public ulong regionWestOverrideHandle;

        /// <summary>
        /// Optional: URI Location of the region database
        /// </summary>
        /// <remarks>Used for floating sim pools where the region data is not nessecarily coupled to a specific server</remarks>
        public string regionDataURI = String.Empty;

        /// <summary>
        /// Region Asset Details
        /// </summary>
        public string regionAssetURI = String.Empty;

        public string regionAssetSendKey = String.Empty;
        public string regionAssetRecvKey = String.Empty;

        /// <summary>
        /// Region Userserver Details
        /// </summary>
        public string regionUserURI = String.Empty;

        public string regionUserSendKey = String.Empty;
        public string regionUserRecvKey = String.Empty;

        /// <summary>
        /// Region Map Texture Asset
        /// </summary>
        public UUID regionMapTextureID = new UUID("00000000-0000-1111-9999-000000000006");

        /// <summary>
        /// this particular mod to the file provides support within the spec for RegionProfileData for the
        /// owner_uuid for the region
        /// </summary>
        public UUID owner_uuid = UUID.Zero;

        /// <summary>
        /// OGS/OpenSim Specific original ID for a region after move/split
        /// </summary>
        public UUID originUUID;

        /// <summary>
        /// The Maturity rating of the region
        /// </summary>
        public uint maturity;

        public ProductRulesUse product;   // Unknown, Full, Ocean, Scenic, Plus

        private string _outsideIp;

        /// <summary>
        /// Returns what we consider to be the outside, as in public-facing, IP address for this region
        /// </summary>
        public string OutsideIP
        {
            get 
            {
                return _outsideIp;
            }

            set
            {
                _outsideIp = value;
            }
        }

        /// <summary>
        /// Returns either the explicitly set Outside IP address or the resolved public hostname
        /// </summary>
        public string OutsideIpOrResolvedHostname
        {
            get
            {
                if (_outsideIp != null)
                {
                    return _outsideIp;
                }

                //if the public ip address is not set, we'll resolve the external hostname
                //this allows
                return Util.GetHostFromDNS(serverHostName).ToString();
            }
        }

        public byte AccessLevel
        {
            get { return Util.ConvertMaturityToAccessLevel(maturity); }
        }


        public RegionInfo ToRegionInfo()
        {
            return RegionInfo.Create(UUID, regionName, regionLocX, regionLocY, serverHostName, httpPort, serverPort, remotingPort, _outsideIp);
        }

        public static RegionProfileData FromRegionInfo(RegionInfo regionInfo)
        {
            if (regionInfo == null)
            {
                return null;
            }

            return Create(regionInfo.RegionID, regionInfo.RegionName, regionInfo.RegionLocX,
                          regionInfo.RegionLocY, regionInfo.ExternalHostName,
                          (uint) regionInfo.ExternalEndPoint.Port, regionInfo.HttpPort, regionInfo.RemotingPort, 
                          regionInfo.AccessLevel, regionInfo.Product, regionInfo.OutsideIP);
        }

        public static RegionProfileData Create(UUID regionID, string regionName, uint locX, uint locY, string externalHostName, 
            uint regionPort, uint httpPort, uint remotingPort, byte access, ProductRulesUse product, string outsideIP)
        {
            RegionProfileData regionProfile;
            regionProfile = new RegionProfileData();
            regionProfile.regionLocX = locX;
            regionProfile.regionLocY = locY;
            regionProfile.regionHandle =
                Utils.UIntsToLong((regionProfile.regionLocX * Constants.RegionSize),
                                  (regionProfile.regionLocY*Constants.RegionSize));
            regionProfile.serverHostName = externalHostName;
            regionProfile.serverPort = regionPort;
            regionProfile.httpPort = httpPort;
            regionProfile.remotingPort = remotingPort;

            regionProfile.UUID = regionID;
            regionProfile.regionName = regionName;
            regionProfile.maturity = access;
            regionProfile.product = product;
            regionProfile._outsideIp = outsideIP;

            return regionProfile;
        }
    }
}
