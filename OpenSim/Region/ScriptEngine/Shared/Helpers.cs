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
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.ScriptEngine.Shared
{
    public class DetectParams
    {
        public DetectParams()
        {
            Key = UUID.Zero;
            OffsetPos = new OpenMetaverse.Vector3();
            LinkNum = 0;
            Group = UUID.Zero;
            Name = String.Empty;
            Owner = UUID.Zero;
            Position = new OpenMetaverse.Vector3();
            Rotation = new OpenMetaverse.Quaternion();
            Type = 0;
            Velocity = new OpenMetaverse.Vector3();
            initializeSurfaceTouch();
        }

        public UUID Key;
        /// <summary>
        /// This is the UUID of a bot that is having sensor/listen events
        /// called on it
        /// </summary>
        public UUID BotID;
        public OpenMetaverse.Vector3 OffsetPos;
        public int LinkNum;
        public UUID Group;
        public string Name;
        public UUID Owner;
        public OpenMetaverse.Vector3 Position;
        public OpenMetaverse.Quaternion Rotation;
        public int Type;
        public OpenMetaverse.Vector3 Velocity;

        private OpenMetaverse.Vector3 touchST;
        public OpenMetaverse.Vector3 TouchST { get { return touchST; } }

        private OpenMetaverse.Vector3 touchNormal;
        public OpenMetaverse.Vector3 TouchNormal { get { return touchNormal; } }

        private OpenMetaverse.Vector3 touchBinormal;
        public OpenMetaverse.Vector3 TouchBinormal { get { return touchBinormal; } }

        private OpenMetaverse.Vector3 touchPos;
        public OpenMetaverse.Vector3 TouchPos { get { return touchPos; } }

        private OpenMetaverse.Vector3 touchUV;
        public OpenMetaverse.Vector3 TouchUV { get { return touchUV; } }

        private int touchFace;
        public int TouchFace { get { return touchFace; } }

        // This can be done in two places including the constructor
        // so be carefull what gets added here
        private void initializeSurfaceTouch()
        {
            touchST = new OpenMetaverse.Vector3(-1.0f, -1.0f, 0.0f);
            touchNormal = new OpenMetaverse.Vector3();
            touchBinormal = new OpenMetaverse.Vector3();
            touchPos = new OpenMetaverse.Vector3();
            touchUV = new OpenMetaverse.Vector3(-1.0f, -1.0f, 0.0f);
            touchFace = -1;
        }

        /*
         * Set up the surface touch detected values
         */
        public SurfaceTouchEventArgs SurfaceTouchArgs
        {
            set
            {
                if (value == null)
                {
                    // Initialize to defaults if no value
                    initializeSurfaceTouch();
                }
                else
                {
                    // Set the values from the touch data provided by the client
                    touchST = value.STCoord;
                    touchUV = value.UVCoord;
                    touchNormal = value.Normal;
                    touchBinormal = value.Binormal;
                    touchPos = value.Position;
                    touchFace = value.FaceIndex;
                }
            }
        }

        public void Populate(Scene scene)
        {
            SceneObjectPart part = scene.GetSceneObjectPart(Key);
            if (part == null) // Avatar, maybe?
            {
                ScenePresence presence = scene.GetScenePresence(Key);
                if (presence == null)
                    return;

                Name = presence.Firstname + " " + presence.Lastname;
                Owner = Key;

                Position = presence.AbsolutePosition;
                Rotation = presence.Rotation;
                Velocity = presence.Velocity;

                Type = 0x01; // Avatar
                if (presence.Velocity != Vector3.Zero)
                    Type |= 0x02; // Active

                Group = presence.ControllingClient.ActiveGroupId;
            }
            else //object
            {
                part = part.ParentGroup.RootPart; // We detect objects only

                LinkNum = 0; // Not relevant

                Group = part.GroupID;
                Name = part.Name;
                Owner = part.OwnerID;
                if (part.Velocity == Vector3.Zero)
                    Type = 0x04; // Passive
                else
                    Type = 0x02; // Passive

                foreach (SceneObjectPart p in part.ParentGroup.GetParts())
                {
                    if (p.Inventory.ContainsScripts())
                    {
                        Type |= 0x08; // Scripted
                        break;
                    }
                }

                Position = part.AbsolutePosition;

                Quaternion wr = part.ParentGroup.GroupRotation;
                Rotation = wr;
                Velocity = part.Velocity;
            }
        }

        public static DetectParams FromDetectedObject(DetectedObject detobj)
        {
            DetectParams parms = new DetectParams();
            parms.Key = detobj.keyUUID;
            parms.Group = detobj.groupUUID;
            parms.LinkNum = detobj.linkNum;
            parms.Name = detobj.nameStr;
            parms.Owner = detobj.ownerUUID;
            parms.Position = detobj.posVector;
            parms.Rotation = detobj.rotQuat;
            parms.Type = detobj.colliderType;
            parms.Velocity = detobj.velVector;

            return parms;
        }
    }

    /// <summary>
    /// Holds all the data required to execute a scripting event.
    /// </summary>
    public class EventParams
    {
        public EventParams(string eventName, Object[] eventParams, DetectParams[] detectParams)
        {
            EventName = eventName;
            Params = eventParams;
            DetectParams = detectParams;
        }

        public string EventName;
        public Object[] Params;
        public DetectParams[] DetectParams;
    }
}
