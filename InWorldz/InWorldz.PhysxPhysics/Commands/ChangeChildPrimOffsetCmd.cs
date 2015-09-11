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
using OpenSim.Framework;

namespace InWorldz.PhysxPhysics.Commands
{
    internal class ChangeChildPrimOffsetCmd : IMultiPrimCommand
    {
        private PhysxPrim _parent;
        private PhysxPrim _child;
        private OpenMetaverse.Vector3 _newOffset;
        private OpenMetaverse.Quaternion _rotOffset;

        private List<PhysxPrim> _affectedPrims;
        private int _delayCount = 0;

        public ChangeChildPrimOffsetCmd(PhysxPrim parent, PhysxPrim child, OpenMetaverse.Vector3 newOffset,
            OpenMetaverse.Quaternion rotOffset)
        {
            Util.ThrowIfNull(parent, "parent");
            Util.ThrowIfNull(child, "child");

            _parent = parent;
            _child = child;
            _newOffset = newOffset;
            _rotOffset = rotOffset;
            _affectedPrims = new List<PhysxPrim> { parent, child };
        }

        public void Execute(PhysxScene scene)
        {
            if (_child.Disposed || _parent.Disposed) return;

            _parent.ChildPrimOffsetChangedSync(_child, _newOffset, _rotOffset);
        }

        public bool AffectsMultiplePrims()
        {
            return true;
        }

        public PhysxPrim GetTargetPrim()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PhysxPrim> GetTargetPrims()
        {
            return _affectedPrims;
        }

        public bool RemoveWaitAndCheckReady()
        {
            if (--_delayCount <= 0) return true;

            return false;
        }

        public void AddDelay()
        {
            _delayCount++;
        }


        public bool IsCullable
        {
            get { return false; }
        }
    }
}
