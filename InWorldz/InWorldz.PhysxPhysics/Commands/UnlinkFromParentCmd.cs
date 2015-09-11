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
    internal class UnlinkFromParentCmd : IMultiPrimCommand
    {
        private PhysxPrim _child;
        private PhysxPrim _parent;
        private OpenMetaverse.Vector3 _newWorldPosition;
        private OpenMetaverse.Quaternion _newWorldRotation;
        private PhysxPrim[] _targetPrims;

        private int _delayCount = 0;

        public UnlinkFromParentCmd(PhysxPrim child, PhysxPrim parent, OpenMetaverse.Vector3 newWorldPosition, 
            OpenMetaverse.Quaternion newWorldRotation)
        {
            Util.ThrowIfNull(parent, "parent");
            Util.ThrowIfNull(child, "child");

            _child = child;
            _parent = parent;
            _newWorldPosition = newWorldPosition;
            _newWorldRotation = newWorldRotation;

            _targetPrims = new PhysxPrim[] { child, parent };
        }

        public bool AffectsMultiplePrims()
        {
            return true;
        }

        public PhysxPrim GetTargetPrim()
        {
            return null;
        }

        public bool RemoveWaitAndCheckReady()
        {
            if (--_delayCount <= 0) return true;
            return false;
        }

        public void Execute(PhysxScene scene)
        {
            if (_child.Disposed || _parent == null || _parent.Disposed || _child.Parent != _parent) return;

            _child.UnlinkFromParent(_newWorldPosition, _newWorldRotation);
        }


        public bool IsCullable
        {
            get { return false; }
        }

        #region IMultiPrimCommand Members

        public void AddDelay()
        {
            _delayCount++;
        }

        public IEnumerable<PhysxPrim> GetTargetPrims()
        {
            return _targetPrims;
        }

        #endregion
    }
}
