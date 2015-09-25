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
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Debugging
{
    internal class ContactDebugManager
    {
        public delegate void DataCallback(IEnumerable<KeyValuePair<PhysX.Actor, int>> data);

        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int SAMPLE_TIME_MS = 250;

        private PhysxScene _scene;
        private uint _contactDebugStartedAt;
        private bool _setShapeDebugNextFrame;
        private Dictionary<PhysX.Actor, int> _contactCounts = new Dictionary<PhysX.Actor, int>();

        public event DataCallback OnDataReady;

        public ContactDebugManager(PhysxScene scene)
        {
            _scene = scene;
        }

        /// <summary>
        /// Begins collecting contact data for all dynamic prims
        /// </summary>
        public void BeginCollectingContactData()
        {
            _contactDebugStartedAt = (uint)Environment.TickCount;
            _contactCounts.Clear();

            _setShapeDebugNextFrame = true;
        }

        /// <summary>
        /// Stops collecting contact data for prims
        /// </summary>
        public void CancelContactDataCollection()
        {
            _contactDebugStartedAt = 0;
            _contactCounts.Clear();

            foreach (var dynPrim in _scene.DynamicPrims)
            {
                dynPrim.StopDebuggingCollisionContactsSync();
            }
        }

        /// <summary>
        /// When contact debugging is enabled, prims will forward all their contact information to this debug
        /// manager instance. 
        /// </summary>
        /// <param name="contactPairHeader"></param>
        /// <param name="pairs"></param>
        public void AnalyzeContactChange(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs)
        {
            if (_contactDebugStartedAt == 0) return;

            if (contactPairHeader.Actors[0] == null || contactPairHeader.Actors[1] == null)
            {
                return;
            }

            foreach (var pair in pairs)
            {
                if ((pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0 || (pair.Events & PhysX.PairFlag.NotifyTouchPersists) != 0)
                {
                    int currCount = pair.ContactCount;
                    if (_contactCounts.TryGetValue(contactPairHeader.Actors[0], out currCount))
                    {
                        currCount += pair.ContactCount;
                    }

                    _contactCounts[contactPairHeader.Actors[0]] = currCount;

                    if (_contactCounts.TryGetValue(contactPairHeader.Actors[1], out currCount))
                    {
                        currCount += pair.ContactCount;
                    }

                    _contactCounts[contactPairHeader.Actors[1]] = currCount;
                }
            }
        }

        /// <summary>
        /// A frame has passed, if enough time has passed, we analyze the contact information and
        /// attempt to disable misbehaving collisions
        /// </summary>
        public void OnFramePassed()
        {
            if (_contactDebugStartedAt == 0) return;

            if (_setShapeDebugNextFrame)
            {
                foreach (var dynPrim in _scene.DynamicPrims)
                {
                    dynPrim.DebugCollisionContactsSync();
                }

                _setShapeDebugNextFrame = false;
                return;
            }


            uint timePassed = (uint)Environment.TickCount - _contactDebugStartedAt;
            if (timePassed > SAMPLE_TIME_MS)
            {
                foreach (var primKvp in _contactCounts)
                {
                    DataCallback callback = OnDataReady;
                    if (callback != null)
                    {
                        callback(_contactCounts);
                    }
                }

                CancelContactDataCollection();
            }
        }
    }
}
