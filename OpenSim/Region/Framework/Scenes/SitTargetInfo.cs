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
using System.Drawing;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Physics.Manager;
using OpenSim.Framework.Geom;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OpenSim.Region.Framework.Scenes
{
    public class SitTargetInfo
    {
        private bool m_isActive = false;
        private Vector3 m_offset = Vector3.Zero;
        private Quaternion m_rotation = Quaternion.Identity;
        private SceneObjectPart m_part = null;
        private ScenePresence m_sitter = null;

        public static readonly SitTargetInfo None = new SitTargetInfo();

        #region Properties

        public Vector3 Offset
        {
            get { return m_offset; }
        }

        public Quaternion Rotation
        {
            get { return m_rotation; }
        }

        public bool IsActive
        {
            get { return m_isActive; }
        }

        public ScenePresence Sitter
        {
            get { return m_sitter; }
            set { m_sitter = value; }
        }

        public SceneObjectPart Seat
        {
            get { return m_part; }
            set { m_part = value; }
        }

        public bool IsZero
        {
            get { return (m_offset == Vector3.Zero) && (m_rotation == Quaternion.Identity);  }
        }

        public bool HasSitter
        {
            get { return (m_sitter != null);  }
        }
        #endregion

        #region Constructor(s)
        public SitTargetInfo()
        {
            m_offset = Vector3.Zero;
            m_rotation = Quaternion.Identity;
            m_isActive = false;
            m_part = null;
            m_sitter = null;
        }

        public SitTargetInfo(SceneObjectPart part, bool isEnabled, Vector3 pos, Quaternion rot)
        {
            m_isActive = isEnabled;
            m_offset = pos;
            m_rotation = rot;
            m_part = part;
        }
        #endregion

        #region Methods
        public void SeatAvatar(ScenePresence avatar)
        {
            m_sitter = avatar;
        }

        public void CopyTo(SitTargetInfo sitInfo)
        {
            sitInfo.m_isActive = this.m_isActive;
            sitInfo.m_offset = this.m_offset;
            sitInfo.m_rotation = this.m_rotation;
            sitInfo.m_part = this.m_part;
            sitInfo.m_sitter = this.m_sitter;
        }
        #endregion
    }
}
