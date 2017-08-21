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
using OpenSim.Framework;
using Nini.Config;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Holds drip rates and maximum burst rates for throttling with hierarchical
    /// token buckets. The maximum burst rates set here are hard limits and can
    /// not be overridden by client requests
    /// </summary>
    public sealed class ThrottleRates
    {
        // Viewer rates in Kbps for a total bandwidth value of 1000 kbps:
        // BW_PRESET_1000[] = { 100, 100,  20,  20, 310, 310, 140 };
        // We'll use the same (but store in bytes not Kbits).

        /// <summary>Drip rate for resent packets</summary>
        public int Resend = 12500;  // 100Kbps / 8b per B
        /// <summary>Drip rate for terrain packets</summary>
        public int Land = 12500;    // 100Kbps / 8b per B
        /// <summary>Drip rate for wind packets</summary>
        public int Wind = 2500;     //  20Kbps / 8b per B
        /// <summary>Drip rate for cloud packets</summary>
        public int Cloud = 2500;    //  20Kbps / 8b per B
        /// <summary>Drip rate for task packets</summary>
        public int Task = 38750;    // 310Kbps / 8b per B
        /// <summary>Drip rate for texture packets</summary>
        public int Texture = 38750; // 310Kbps / 8b per B
        /// <summary>Drip rate for asset packets</summary>
        public int Asset = 17500;   // 140Kbps / 8b per B
        /// <summary>Drip rate for state packets</summary>
        public int State = 31000;    // 80% of Task
        /// <summary>Drip rate for the parent token bucket</summary>
        public int Total;

        /// <summary>Maximum burst rate for resent packets</summary>
        public int ResendLimit = 12500;
        /// <summary>Maximum burst rate for land packets</summary>
        public int LandLimit = 1400;
        /// <summary>Maximum burst rate for wind packets</summary>
        public int WindLimit = 1400;
        /// <summary>Maximum burst rate for cloud packets</summary>
        public int CloudLimit = 1400;
        /// <summary>Maximum burst rate for task (state and transaction) packets</summary>
        public int TaskLimit = 1400;
        /// <summary>Maximum burst rate for texture packets</summary>
        public int TextureLimit = 1400;
        /// <summary>Maximum burst rate for asset packets</summary>
        public int AssetLimit = 1400;
        /// <summary>Maximum burst rate for state packets</summary>
        public int StateLimit = 1400;
        /// <summary>Burst rate for the parent token bucket</summary>
        public int TotalLimit;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="config">Config source to load defaults from</param>
        public ThrottleRates(IConfigSource config)
        {
            try
            {
                IConfig throttleConfig = config.Configs["ClientStack.LindenUDP"];

                if (throttleConfig != null)
                {
                    Resend = throttleConfig.GetInt("resend_default", Resend);
                    Land = throttleConfig.GetInt("land_default", Land);
                    Wind = throttleConfig.GetInt("wind_default", Wind);
                    Cloud = throttleConfig.GetInt("cloud_default", Cloud);
                    Task = throttleConfig.GetInt("task_default", Task);
                    Texture = throttleConfig.GetInt("texture_default", Texture);
                    Asset = throttleConfig.GetInt("asset_default", Asset);
                    State = throttleConfig.GetInt("state_default", State);

                    ResendLimit = throttleConfig.GetInt("resend_limit", ResendLimit);
                    LandLimit = throttleConfig.GetInt("land_limit", LandLimit);
                    WindLimit = throttleConfig.GetInt("wind_limit", WindLimit);
                    CloudLimit = throttleConfig.GetInt("cloud_limit", CloudLimit);
                    TaskLimit = throttleConfig.GetInt("task_limit", TaskLimit);
                    TextureLimit = throttleConfig.GetInt("texture_limit", TextureLimit);
                    AssetLimit = throttleConfig.GetInt("asset_limit", AssetLimit);
                    StateLimit = throttleConfig.GetInt("state_limit", StateLimit);
                }

                Total = Resend + Land + Wind + Cloud + Task + Texture + Asset + State;
                TotalLimit = Total;
            }
            catch (Exception) { }
        }

        public int GetRate(ThrottleOutPacketType type)
        {
            switch (type)
            {
                case ThrottleOutPacketType.Resend:
                    return Resend;
                case ThrottleOutPacketType.Land:
                    return Land;
                case ThrottleOutPacketType.Wind:
                    return Wind;
                case ThrottleOutPacketType.Cloud:
                    return Cloud;
                case ThrottleOutPacketType.Task:
                    return Task;
                case ThrottleOutPacketType.Texture:
                    return Texture;
                case ThrottleOutPacketType.Asset:
                    return Asset;
                case ThrottleOutPacketType.State:
                    return State;
                case ThrottleOutPacketType.Unknown:
                default:
                    return 0;
            }
        }

        public int GetLimit(ThrottleOutPacketType type)
        {
            switch (type)
            {
                case ThrottleOutPacketType.Resend:
                    return ResendLimit;
                case ThrottleOutPacketType.Land:
                    return LandLimit;
                case ThrottleOutPacketType.Wind:
                    return WindLimit;
                case ThrottleOutPacketType.Cloud:
                    return CloudLimit;
                case ThrottleOutPacketType.Task:
                    return TaskLimit;
                case ThrottleOutPacketType.Texture:
                    return TextureLimit;
                case ThrottleOutPacketType.Asset:
                    return AssetLimit;
                case ThrottleOutPacketType.State:
                    return StateLimit;
                case ThrottleOutPacketType.Unknown:
                default:
                    return 0;
            }
        }
    }
}
