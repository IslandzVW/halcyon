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

using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    [ProtoContract]
    public class KeyframeAnimationSnapshot
    {
        [ProtoMember(1)]
        public int CurrentAnimationPosition { get; set; }
        [ProtoMember(2)]
        public bool PingPongForwardMotion { get; set; }
        [ProtoMember(3)]
        public int CurrentMode { get; set; }
        [ProtoMember(4)]
        public int CurrentCommand { get; set; }
        [ProtoMember(5)]
        public int TimeLastTick { get; set; }
        [ProtoMember(6)]
        public int TimeElapsed { get; set; }
        [ProtoMember(7)]
        public TimeSpan[] TimeList { get; set; }
        [ProtoMember(8)]
        public OpenMetaverse.Vector3 InitialPosition { get; set; }
        [ProtoMember(9)]
        public OpenMetaverse.Vector3[] PositionList { get; set; }
        [ProtoMember(10)]
        public OpenMetaverse.Quaternion InitialRotation { get; set; }
        [ProtoMember(11)]
        public OpenMetaverse.Quaternion[] RotationList { get; set; }

        public OpenSim.Framework.KeyframeAnimation ToKeyframeAnimation()
        {
            return new OpenSim.Framework.KeyframeAnimation()
            {
                CurrentAnimationPosition = this.CurrentAnimationPosition,
                CurrentMode = (OpenSim.Framework.KeyframeAnimation.Modes)this.CurrentMode,
                CurrentCommand = (OpenSim.Framework.KeyframeAnimation.Commands)this.CurrentCommand,
                InitialPosition = this.InitialPosition,
                InitialRotation = this.InitialRotation,
                PingPongForwardMotion = this.PingPongForwardMotion,
                PositionList = this.PositionList,
                RotationList = this.RotationList,
                TimeLastTick = this.TimeLastTick,
                TimeElapsed = this.TimeElapsed,
                TimeList = this.TimeList
            };
        }

        public static KeyframeAnimationSnapshot FromKeyframeAnimation(OpenSim.Framework.KeyframeAnimation keyframeAnimation)
        {
            if (keyframeAnimation == null)
                return null;

            return new KeyframeAnimationSnapshot()
            {
                CurrentAnimationPosition = keyframeAnimation.CurrentAnimationPosition,
                CurrentMode = (int)keyframeAnimation.CurrentMode,
                CurrentCommand = (int)keyframeAnimation.CurrentCommand,
                InitialPosition = keyframeAnimation.InitialPosition,
                InitialRotation = keyframeAnimation.InitialRotation,
                PingPongForwardMotion = keyframeAnimation.PingPongForwardMotion,
                PositionList = keyframeAnimation.PositionList,
                RotationList = keyframeAnimation.RotationList,
                TimeLastTick = keyframeAnimation.TimeLastTick,
                TimeElapsed = keyframeAnimation.TimeElapsed,
                TimeList = keyframeAnimation.TimeList
            };
        }

        internal static KeyframeAnimationSnapshot FromBytes(byte[] bytes, int start)
        {
            using (MemoryStream ms = new MemoryStream(bytes, start, bytes.Length - start))
            {
                return ProtoBuf.Serializer.Deserialize<KeyframeAnimationSnapshot>(ms);
            }
        }

        internal byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<KeyframeAnimationSnapshot>(ms, this);
                return ms.ToArray();
            }
        }
    }
}
