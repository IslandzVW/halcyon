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
using System.Linq;
using OpenMetaverse;
using log4net;
using System.Reflection;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public class UndoState
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Vector3 Position = Vector3.Zero;
        public Vector3 Scale = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity, ParentRotation = Quaternion.Identity;

        public UndoState(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    Position = part.ParentGroup.AbsolutePosition;
                    Rotation = part.RotationOffset;
                    Scale = part.Shape.Scale;
                }
                else
                {
                    Position = part.OffsetPosition;
                    Rotation = part.RotationOffset;
                    ParentRotation = part.ParentGroup.RootPart.RotationOffset;
                    Scale = part.Shape.Scale;
                }
            }
        }

        public bool Compare(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    if (Position == part.ParentGroup.AbsolutePosition && Rotation == part.RotationOffset &&
                        Scale == part.Shape.Scale)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (Position == part.OffsetPosition && Rotation == part.RotationOffset &&
                        Scale == part.Shape.Scale)
                    {
                        return ParentRotation == part.ParentGroup.RootPart.RotationOffset;
                    }
                    else
                        return false;

                }
            }
            return false;
        }

        public void PlaybackState(SceneObjectPart part)
        {
            if (part != null)
            {
                part.Undoing = true;

                if (part.ParentID == 0)
                {
                    if (Position != Vector3.Zero)
                    {
                        part.ParentGroup.AbsolutePosition = Position;
                    }
                    part.RotationOffset = Rotation;
                    if (Scale != Vector3.Zero)
                    {
                        part.Scale = Scale;
                    }

                    foreach (SceneObjectPart child in
                                part.ParentGroup.GetParts().Where(child => child.UUID != part.UUID))
                    {
                        child.Undo(); //No updates here, child undo will do it on their own
                    }
                }
                else
                {
                    if (Position != Vector3.Zero)
                    {
                        part.OffsetPosition = Position;
                    }
                        
                    part.UpdateRotation(Rotation);
                    if (Scale != Vector3.Zero)
                    {
                        part.Resize(Scale);
                    }
                }
                part.Undoing = false;
                part.ScheduleFullUpdate(PrimUpdateFlags.FindBest);
            }
        }

        public void PlayfwdState(SceneObjectPart part)
        {
            if (part != null)
            {
                part.Undoing = true;

                if (part.ParentID == 0)
                {
                    if (Position != Vector3.Zero)
                    {
                        part.ParentGroup.AbsolutePosition = Position;
                    }
                    part.RotationOffset = Rotation;
                    if (Scale != Vector3.Zero)
                    {
                        part.Scale = Scale;
                    }

                    foreach (SceneObjectPart child in
                                part.ParentGroup.GetParts().Where(child => child.UUID != part.UUID))
                    {
                        child.Redo(); //No updates here, child redo will do it on their own
                    }
                }
                else
                {
                    if (Position != Vector3.Zero)
                    {
                        part.OffsetPosition = Position;
                    }

                    part.UpdateRotation(Rotation);
                    if (Scale != Vector3.Zero)
                    {
                        part.Resize(Scale);
                    }
                }
                part.Undoing = false;
                part.ScheduleFullUpdate(PrimUpdateFlags.FindBest);
            }
        }
    }
}
