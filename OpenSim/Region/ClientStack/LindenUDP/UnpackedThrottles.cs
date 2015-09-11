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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Client throttles not yet packed into a byte array. 
    /// </summary>
    internal class UnpackedThrottles
    {
        private float[] m_unpackedThrottleData;
        private object m_lock = new object();
        private float m_lastMultiplier;
        private byte[] m_packedThrottleData = null;

        public UnpackedThrottles(float[] unpackedThrottleData)
        {
            m_unpackedThrottleData = unpackedThrottleData;
        }

        /// <summary>
        /// Returns the throttles packed with the given multiplier
        /// </summary>
        /// <param name="multiplier">Multiplier for the throttles after packing</param>
        /// <returns>Throttles packed into a byte array</returns>
        public byte[] GetPackedThrottles(float multiplier)
        {
            lock (m_lock)
            {
                //should be the common case
                if (multiplier == m_lastMultiplier && m_packedThrottleData != null)
                {
                    return m_packedThrottleData;
                }

                //otherwise pack and cache
                m_packedThrottleData = new byte[(LLUDPClient.THROTTLE_CATEGORY_COUNT - 1) * 4];
                int i = 0;
                int j = 0;

                while (j < LLUDPClient.THROTTLE_CATEGORY_COUNT - 1)
                {
                    Utils.FloatToBytes(m_unpackedThrottleData[j++] * multiplier, m_packedThrottleData, i);
                    i += 4;
                }

                m_lastMultiplier = multiplier;
                return m_packedThrottleData;
            }
        }

        /// <summary>
        /// Returns the total bandwidth available from the current throttle settings
        /// </summary>
        /// <returns></returns>
        public int GetThrottleTotal()
        {
            int total = 0;
            lock (m_lock)
            {
                foreach (float catTotal in m_unpackedThrottleData)
                {
                    total += (int)catTotal;
                }
            }

            return total;
        }
    }
}
