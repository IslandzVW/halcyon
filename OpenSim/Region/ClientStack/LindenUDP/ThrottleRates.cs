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
        /// <summary>Drip rate for resent packets</summary>
        public int Resend;
        /// <summary>Drip rate for terrain packets</summary>
        public int Land;
        /// <summary>Drip rate for wind packets</summary>
        public int Wind;
        /// <summary>Drip rate for cloud packets</summary>
        public int Cloud;
        /// <summary>Drip rate for task packets</summary>
        public int Task;
        /// <summary>Drip rate for texture packets</summary>
        public int Texture;
        /// <summary>Drip rate for asset packets</summary>
        public int Asset;
        /// <summary>Drip rate for state packets</summary>
        public int State;
        /// <summary>Drip rate for the parent token bucket</summary>
        public int Total;

        /// <summary>Maximum burst rate for resent packets</summary>
        public int ResendLimit;
        /// <summary>Maximum burst rate for land packets</summary>
        public int LandLimit;
        /// <summary>Maximum burst rate for wind packets</summary>
        public int WindLimit;
        /// <summary>Maximum burst rate for cloud packets</summary>
        public int CloudLimit;
        /// <summary>Maximum burst rate for task (state and transaction) packets</summary>
        public int TaskLimit;
        /// <summary>Maximum burst rate for texture packets</summary>
        public int TextureLimit;
        /// <summary>Maximum burst rate for asset packets</summary>
        public int AssetLimit;
        /// <summary>Maximum burst rate for state packets</summary>
        public int StateLimit;
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

                Resend = throttleConfig.GetInt("resend_default", 12500);
                Land = throttleConfig.GetInt("land_default", 1400);
                Wind = throttleConfig.GetInt("wind_default", 1400);
                Cloud = throttleConfig.GetInt("cloud_default", 1400);
                Task = throttleConfig.GetInt("task_default", 1400);
                Texture = throttleConfig.GetInt("texture_default", 1400);
                Asset = throttleConfig.GetInt("asset_default", 1400);
                State = throttleConfig.GetInt("state_default", 1400);

                ResendLimit = throttleConfig.GetInt("resend_limit", 12500);
                LandLimit = throttleConfig.GetInt("land_limit", 1400);
                WindLimit = throttleConfig.GetInt("wind_limit", 1400);
                CloudLimit = throttleConfig.GetInt("cloud_limit", 1400);
                TaskLimit = throttleConfig.GetInt("task_limit", 1400);
                TextureLimit = throttleConfig.GetInt("texture_limit", 1400);
                AssetLimit = throttleConfig.GetInt("asset_limit", 1400);
                StateLimit = throttleConfig.GetInt("state_limit", 1400);

                Total = Resend + Land + Wind + Cloud + Task + Texture + Asset + State;//= throttleConfig.GetInt("client_throttle_max_bps", 0);
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
