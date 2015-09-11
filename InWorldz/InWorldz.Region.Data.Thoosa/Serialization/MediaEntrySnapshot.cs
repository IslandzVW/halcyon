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

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// Snapshot of an OpenMetaverse.MediaEntry
    /// </summary>
    [ProtoContract]
    public class MediaEntrySnapshot
    {
        [ProtoMember(1)]
        public bool AutoLoop;

        [ProtoMember(2)]
        public bool AutoPlay;

        [ProtoMember(3)]
        public bool AutoScale;

        [ProtoMember(4)]
        public bool AutoZoom;

        [ProtoMember(5)]
        public OpenMetaverse.MediaPermission ControlPermissions;

        [ProtoMember(6)]
        public OpenMetaverse.MediaControls Controls;

        [ProtoMember(7)]
        public string CurrentURL;

        [ProtoMember(8)]
        public bool EnableAlterntiveImage;

        [ProtoMember(9)]
        public bool EnableWhiteList;

        [ProtoMember(10)]
        public int Height;

        [ProtoMember(11)]
        public string HomeURL;

        [ProtoMember(12)]
        public bool InteractOnFirstClick;

        [ProtoMember(13)]
        public OpenMetaverse.MediaPermission InteractPermissions;

        [ProtoMember(14)]
        public string[] WhiteList;

        [ProtoMember(15)]
        public int Width;

        public static MediaEntrySnapshot FromMediaEntry(OpenMetaverse.MediaEntry ent)
        {
            if (ent == null) return null;

            MediaEntrySnapshot snap = new MediaEntrySnapshot
            {
                AutoLoop = ent.AutoLoop,
                AutoPlay = ent.AutoPlay,
                AutoScale = ent.AutoScale,
                AutoZoom = ent.AutoZoom,
                ControlPermissions = ent.ControlPermissions,
                Controls = ent.Controls,
                CurrentURL = ent.CurrentURL,
                EnableAlterntiveImage = ent.EnableAlterntiveImage,
                EnableWhiteList = ent.EnableWhiteList,
                Height = ent.Height,
                HomeURL  = ent.HomeURL,
                InteractOnFirstClick = ent.InteractOnFirstClick,
                InteractPermissions = ent.InteractPermissions,
                WhiteList = ent.WhiteList,
                Width = ent.Width
            };

            return snap;
        }

        public OpenMetaverse.MediaEntry ToMediaEntry()
        {
            return new OpenMetaverse.MediaEntry
            {
                AutoLoop = this.AutoLoop,
                AutoPlay = this.AutoPlay,
                AutoScale = this.AutoScale,
                AutoZoom = this.AutoZoom,
                ControlPermissions = this.ControlPermissions,
                Controls  = this.Controls,
                CurrentURL = this.CurrentURL,
                EnableAlterntiveImage = this.EnableAlterntiveImage,
                EnableWhiteList = this.EnableWhiteList,
                Height = this.Height,
                HomeURL = this.HomeURL,
                InteractOnFirstClick = this.InteractOnFirstClick,
                InteractPermissions = this.InteractPermissions,
                WhiteList = this.WhiteList,
                Width = this.Width
            };
        }

        public static MediaEntrySnapshot[] SnapshotArrayFromList(OpenSim.Framework.PrimitiveBaseShape.PrimMedia mediaList)
        {
            if (mediaList == null) return null;

            MediaEntrySnapshot[] snapList = new MediaEntrySnapshot[mediaList.Count];

            for (int index=0; index<mediaList.Count; index++)
            {
                snapList[index] = MediaEntrySnapshot.FromMediaEntry(mediaList[index]);
            }

            return snapList;
        }

        public static OpenSim.Framework.PrimitiveBaseShape.PrimMedia SnapshotArrayToList(MediaEntrySnapshot[] snapList)
        {
            if (snapList == null) return null;

            var mediaList = new OpenSim.Framework.PrimitiveBaseShape.PrimMedia(snapList.Length);
            mediaList.New(snapList.Length);
            int index = 0;
            foreach (var snap in snapList)
            {
                mediaList[index++] = (snap != null) ? snap.ToMediaEntry() : null;
            }

            return mediaList;
        }
    }
}
