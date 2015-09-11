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

namespace OpenSim.Region.Framework.Scenes
{
    public struct SceneObjectPartPhysicsSummary
    {
        public enum ChangeFlags
        {
            NoChange = 0,
            NeedsPhysicsShapeChanged = (1 << 0),
            NeedsDynamicActorChanged = (1 << 1),
            IsPhantomChanged = (1 << 2),
            IsVolumeDetectChanged = (1 << 3),
            IsFlexiChanged = (1 << 4)
        }

        public bool NeedsPhysicsShape;
        public bool NeedsDynamicActor;
        public bool IsPhantom;
        public bool IsVolumeDetect;
        public bool IsFlexi;

        static public ChangeFlags Compare(SceneObjectPartPhysicsSummary first, SceneObjectPartPhysicsSummary second)
        {
            ChangeFlags flags = ChangeFlags.NoChange;

            if (first.NeedsPhysicsShape != second.NeedsPhysicsShape)
            {
                flags |= ChangeFlags.NeedsPhysicsShapeChanged;
            }

            if (first.NeedsDynamicActor != second.NeedsDynamicActor)
            {
                flags |= ChangeFlags.NeedsDynamicActorChanged;
            }

            if (first.IsPhantom != second.IsPhantom)
            {
                flags |= ChangeFlags.IsPhantomChanged;
            }

            if (first.IsVolumeDetect != second.IsVolumeDetect)
            {
                flags |= ChangeFlags.IsVolumeDetectChanged;
            }

            if (first.IsFlexi != second.IsFlexi)
            {
                flags |= ChangeFlags.IsFlexiChanged;
            }

            return flags;
        }

        static public SceneObjectPartPhysicsSummary SummaryFromParams(bool isFlexi, bool isAttachment,
            bool isVolumeDetect, bool isPhysical, bool isPhantom)
        {
            SceneObjectPartPhysicsSummary summary = new SceneObjectPartPhysicsSummary();
            summary.IsFlexi = isFlexi;

            if (isAttachment)
            {
                summary.IsVolumeDetect = false;
                summary.NeedsDynamicActor = false;
                summary.IsPhantom = true;
                summary.NeedsPhysicsShape = false;
            }
            else if (summary.IsFlexi)
            {
                summary.IsVolumeDetect = false;
                summary.NeedsDynamicActor = false;
                summary.NeedsPhysicsShape = false;
                summary.IsPhantom = true;
            }
            else if (isVolumeDetect)
            {
                //if VolumeDetection is set, the phantom flag is locally ignored
                summary.IsPhantom = false;
                summary.NeedsDynamicActor = false;
                summary.NeedsPhysicsShape = true;
                summary.IsVolumeDetect = true;
            }
            else
            {
                summary.NeedsDynamicActor = isPhysical;
                summary.IsPhantom = isPhantom;
                summary.NeedsPhysicsShape = summary.NeedsDynamicActor || (!summary.IsPhantom && !isAttachment);
            }

            return summary;
        }

        public override string ToString()
        {
            return String.Format("<SceneObjectPartPhysicsSummary> NeedsPhysicsShape: {0}, NeedsDynamicActor: {1}, IsPhantom: {2}, IsVolumeDetect: {3}, IsFlexi: {4}",
                new object[] { NeedsPhysicsShape, NeedsDynamicActor, IsPhantom, IsVolumeDetect, IsFlexi });
        }
    }
}
