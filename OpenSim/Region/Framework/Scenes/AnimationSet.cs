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
using System.Reflection;
using log4net;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    [Serializable]
    public class AnimationSet
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static AvatarAnimations Animations = new AvatarAnimations();

        private OpenSim.Framework.Animation m_defaultAnimation = new OpenSim.Framework.Animation();
        private List<OpenSim.Framework.Animation> m_animations = new List<OpenSim.Framework.Animation>();

        public OpenSim.Framework.Animation DefaultAnimation 
        {
            get { return m_defaultAnimation; } 
        }
        public AnimationSet()
        {
            ResetDefaultAnimation();
        }

        public bool HasAnimation(UUID animID)
        {
            if (m_defaultAnimation.AnimID == animID)
                return true;

            for (int i = 0; i < m_animations.Count; ++i)
            {
                if (m_animations[i].AnimID == animID)
                    return true;
            }

            return false;
        }

        public bool Add(UUID animID, int sequenceNum, UUID objectID)
        {
            lock (m_animations)
            {
                if (!HasAnimation(animID))
                {
                    m_animations.Add(new OpenSim.Framework.Animation(animID, sequenceNum, objectID));
                    return true;
                }
            }
            return false;
        }

        public bool Remove(UUID animID)
        {
            lock (m_animations)
            {
                if (m_defaultAnimation.AnimID == animID)
                {
                    ResetDefaultAnimation();
                    return true;
                }
                else if (HasAnimation(animID))
                {
                    for (int i = 0; i < m_animations.Count; i++)
                    {
                        if (m_animations[i].AnimID == animID)
                        {
                            m_animations.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Clear(bool isSitting)
        {
            if (!isSitting)
                ResetDefaultAnimation();
            m_animations.Clear();
        }

        /// <summary>
        /// The default animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        public bool SetDefaultAnimation(UUID animID, int sequenceNum, UUID objectID)
        {
            bool rc = false;
            lock (m_defaultAnimation)
            {
                if (m_defaultAnimation.AnimID != animID)
                {
                    m_defaultAnimation = new OpenSim.Framework.Animation(animID, sequenceNum, objectID);
                    rc = true;
                }
            }
//            if (!rc) m_log.ErrorFormat("SetDefaultAnimation: Animation '{0}' already set.", animID.ToString());
            return rc;
        }

        protected bool ResetDefaultAnimation()
        {
            return TrySetDefaultAnimation("STAND", 1, UUID.Zero);
        }

        /// <summary>
        /// Set the animation as the default animation if it's known
        /// </summary>
        public bool TrySetDefaultAnimation(string anim, int sequenceNum, UUID objectID)
        {
            if (Animations.AnimsUUID.ContainsKey(anim))
            {
                return SetDefaultAnimation(Animations.AnimsUUID[anim], sequenceNum, objectID);
            }
            m_log.ErrorFormat("TrySetDefaultAnimation: Animation '{0}' not found.", anim);
            return false;
        }

        public void GetArrays(out UUID[] animIDs, out int[] sequenceNums, out UUID[] objectIDs)
        {
            lock (m_animations)
            {
                animIDs = new UUID[m_animations.Count + 1];
                sequenceNums = new int[m_animations.Count + 1];
                objectIDs = new UUID[m_animations.Count + 1];

                animIDs[0] = m_defaultAnimation.AnimID;
                sequenceNums[0] = m_defaultAnimation.SequenceNum;
                objectIDs[0] = m_defaultAnimation.ObjectID;

                for (int i = 0; i < m_animations.Count; ++i)
                {
                    animIDs[i + 1] = m_animations[i].AnimID;
                    sequenceNums[i + 1] = m_animations[i].SequenceNum;
                    objectIDs[i + 1] = m_animations[i].ObjectID;
                }
            }
        }

        public OpenSim.Framework.Animation[] ToArray()
        {
            OpenSim.Framework.Animation[] theArray = new OpenSim.Framework.Animation[m_animations.Count+1];

            if (m_animations == null)
                return theArray;    // let's try to keep known s%^t from happening

            uint i = 0;
            try
            {
                foreach (OpenSim.Framework.Animation anim in m_animations)
                    theArray[i++] = anim;
                theArray[i++] = m_defaultAnimation;
            }
            catch 
            {
                /* S%^t happens. Ignore. */ 
            }
            return theArray;
        }

        public void FromArray(OpenSim.Framework.Animation[] theArray)
        {
            m_animations.Clear();

            if (theArray == null)
                return; // no work to do, leave m_animations as is

            uint i = 0;
            foreach (OpenSim.Framework.Animation anim in theArray)
            {
                if (++i == theArray.Length)
                    m_defaultAnimation = anim;
                else
                    m_animations.Add(anim);
            }
        }
    }
}
