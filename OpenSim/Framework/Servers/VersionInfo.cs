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

namespace OpenSim
{
    using System;
    using System.IO;
    using System.Reflection;
   
    public class VersionInfo
    {
        /// <value>
        /// This is the InWorldz Halcyon version string.  Change this if you are releasing a new InWorldz version.
        /// This should be changed with every significant release.
        /// </value>
        public readonly static string LabelVersion = "0.9.18";

        /// <summary>
        /// This is name of the software product (separate from the grid it is running on).
        /// This should not be changed.
        /// </summary>
        public readonly static string LabelName = "Halcyon";

        /// <summary>
        /// The Revision number is just a 6-digit YYMMDD date. 
        /// To update this, force a full rebuild before delivering a new product.
        /// </summary>
        private static string _revision = null;
        public static string Revision
        {
            get
            {
                if (_revision == null)
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    System.DateTime exeDate = System.IO.File.GetLastWriteTime(exePath);
                    _revision = exeDate.ToString("yyMMdd");
                }
                return _revision;
            }
        }

        /// <summary>
        /// This is the short version, such as "Halcyon 1.2.3", without revision info.
        /// </summary>
        public static string ShortVersion
        {
            get
            {
                return LabelName + " " + LabelVersion;
            }
        }

        /// <summary>
        /// This is the full version, with revision info, such as "Halcyon 1.2.3 R151131".
        /// This is the one requested by most of the software, and passed in RegionInfo to viewers.
        /// </summary>
        private static string _version = null;
        public static string Version
        {
            get {
                if (_version == null)
                {
                    _version = ShortVersion + " R" + Revision;
                }
                return _version;
            }
        }
        
        /// <value>
        /// This is the external interface version.  It is separate from the externally-visible version info.
        /// 
        /// This version number should be increased by 1 every time a code change makes the previous revision incompatible
        /// with the new revision.  This will usually be due to interregion or grid facing interface changes.
        /// 
        /// Changes which are compatible with an older revision (e.g. older revisions experience degraded functionality
        /// but not outright failure) do not need a version number increment.
        /// 
        /// Having this version number allows the grid service to reject connections from regions running a version
        /// of the code that is too old. 
        ///   
        /// </value>
        public readonly static int MajorInterfaceVersion = 4;
    }
}
