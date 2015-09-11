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
using ProtoBuf;
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Messages
{
    /// <summary>
    /// An animation currently executing on an avatar in a packed
    /// format suitable for transmission
    /// </summary>
    [ProtoContract]
    public class PackedAnimation
    {
        [ProtoMember(1)]
        public Guid AnimID;

        [ProtoMember(2)]
        public int SequenceNum;

        [ProtoMember(3)]
        public Guid ObjectID;

        internal static PackedAnimation[] FromAnimations(Animation[] animations)
        {
            PackedAnimation[] anims = new PackedAnimation[animations.Length];

            for (int i = 0; i < animations.Length; i++)
            {
                Animation anim = animations[i];
                anims[i] = new PackedAnimation { AnimID = anim.AnimID.Guid, ObjectID = anim.ObjectID.Guid, SequenceNum = anim.SequenceNum };
            }

            return anims;
        }

        internal static Animation[] ToAnimations(PackedAnimation[] packedAnimation)
        {
            Animation[] anims = new Animation[packedAnimation.Length];

            for (int i = 0; i < packedAnimation.Length; i++)
            {
                PackedAnimation packed = packedAnimation[i];
                anims[i] = new Animation { AnimID = new UUID(packed.AnimID), ObjectID = new UUID(packed.ObjectID), SequenceNum = packed.SequenceNum };
            }

            return anims;
        }
    }
}
