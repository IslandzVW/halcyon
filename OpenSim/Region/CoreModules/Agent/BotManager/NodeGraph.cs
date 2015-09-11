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
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.BotManager
{
    public class NodeGraph
    {
        private readonly object m_lock = new object();

        private int _currentPos = 0;
        public int CurrentPos 
        { 
            get {  lock (m_lock) return _currentPos; }
            set {  lock (m_lock) _currentPos = value; }
        }

        public int NumberOfNodes { get { lock (m_lock) return m_listOfPositions.Count; } }

        /// <summary>
        ///     Loop through the current positions over and over
        /// </summary>
        public bool FollowIndefinitely;

        private TimeSpan m_timeSinceLastChangedPosition = TimeSpan.FromMilliseconds(0);
        private List<Vector3> m_listOfPositions = new List<Vector3>();
        private List<TravelMode> m_listOfStates = new List<TravelMode>();
        private DateTime? m_waitingSince = null;

        #region Add

        public void Add(Vector3 position, TravelMode state)
        {
            lock (m_lock)
            {
                m_listOfPositions.Add(position);
                m_listOfStates.Add(state);
            }
        }

        public void AddRange(IEnumerable<Vector3> positions, IEnumerable<TravelMode> states)
        {
            lock (m_lock)
            {
                m_listOfPositions.AddRange(positions);
                m_listOfStates.AddRange(states);
            }
        }

        #endregion

        #region Clear

        public void Clear()
        {
            lock (m_lock)
            {
                CurrentPos = 0;
                m_listOfPositions.Clear();
                m_listOfStates.Clear();
            }
        }

        #endregion

        public bool GetNextPosition(ScenePresence scenePresence, float closeToRange, TimeSpan diffFromLastFrame, float secondsBeforeForcedTeleport,
                                    out Vector3 position, out TravelMode state, out bool needsToTeleportToPosition, out bool changingNodes)
        {
            changingNodes = false;
            lock (m_lock)
            {
                while(true)
                {
                    //Initialize default values
                    position = Vector3.Zero;
                    state = TravelMode.None;
                    needsToTeleportToPosition = false;

                    //See if we have a valid position to attempt to move to
                    if(CurrentPos < m_listOfPositions.Count)
                    {
                        //Get the position/state and make sure that it is within the region
                        position = m_listOfPositions[CurrentPos];
                        Util.ForceValidRegionXYZ(ref position);
                        state = m_listOfStates[CurrentPos];

                        if (state == TravelMode.Wait)
                        {
                            if (!m_waitingSince.HasValue)//Has no value, so we're just starting to wait
                                m_waitingSince = DateTime.Now;
                            else
                            {
                                double ms = (DateTime.Now - m_waitingSince.Value).TotalMilliseconds;
                                if (ms > (position.X * 1000f))
                                {
                                    //We're done waiting, increment the position counter and go around the loop again
                                    m_waitingSince = null;
                                    CurrentPos++;
                                    changingNodes = true;
                                    m_timeSinceLastChangedPosition = TimeSpan.FromMilliseconds(0);
                                    continue;
                                }
                            }
                            return true;
                        }
                        else
                        {
                            //We are attempting to move somewhere
                            if (scenePresence.IsAtTarget(position, closeToRange))
                            {
                                //We made it to the position, begin proceeding towards the next position 
                                CurrentPos++;
                                changingNodes = true;
                                m_timeSinceLastChangedPosition = TimeSpan.FromMilliseconds(0);
                                continue;
                            }
                            else 
                            {
                                m_timeSinceLastChangedPosition = m_timeSinceLastChangedPosition.Add(diffFromLastFrame);
                                //We need to see if we should teleport to the next node rather than waiting for us to get there
                                if (m_timeSinceLastChangedPosition.TotalMilliseconds > (secondsBeforeForcedTeleport * 1000))
                                {
                                    changingNodes = true;
                                    m_timeSinceLastChangedPosition = TimeSpan.FromMilliseconds(0);
                                    needsToTeleportToPosition = true;
                                }
                            }
                            return true;
                        }
                    }
                    else
                    {
                        //We've finished all the states in the position list
                        m_timeSinceLastChangedPosition = TimeSpan.FromMilliseconds(0);

                        if (m_listOfPositions.Count == 0)//Sanity check
                            return false;

                        if (FollowIndefinitely)
                        {
                            //If we're following indefinitely, then the counter is reset to the beginning and we go around again
                            CurrentPos = 0;
                            changingNodes = true;
                            continue;
                        }
                        return false;
                    }
                }
            }
        }
    }
}
