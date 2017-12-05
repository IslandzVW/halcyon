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

namespace InWorldz.Data.Assets.Stratus.Config
{
    /// <summary>
    /// Singleton for stratus settings
    /// </summary>
    public class Settings
    {
        private static readonly Settings _inst = new Settings();
        public static Settings Instance
        {
            get
            {
                return _inst;
            }
        }

        public bool Enabled { get; set; }
        public bool CFSupport { get; set; }
        public bool LegacySupport { get; set; }
        public string WriteTarget { get; set; }
        public string WhipURL { get; set; }
        public string CFDefaultRegion { get; set; }
        public string CFUsername { get; set; }
        public string CFApiKey { get; set; }
        public string CFContainerPrefix { get; set; }
        public bool CFUseInternalURL { get; set; }
        public int CFWorkerThreads { get; set; }
        public bool CFUseCache { get; set; }
        public int CFCacheSize { get; set; }
        public bool DisableWritebackCache { get; set; }
        public bool EnableCFOverwrite { get; set; }
        public bool UseAsyncStore { get; set; }

        //Settings that are for use during unit tests only
        public bool UnitTest_ThrowTimeout { get; set; }
        public bool UnitTest_DeleteOldCacheFilesImmediately { get; set; }
    }
}
