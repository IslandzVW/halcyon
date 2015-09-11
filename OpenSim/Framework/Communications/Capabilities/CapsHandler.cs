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

using OpenSim.Framework.Servers.HttpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Framework.Communications.Capabilities
{
    /// <summary>
    /// Represents a registered CAPS handler
    /// </summary>
    public class CapsHandler
    {
        /// <summary>
        /// The name of this capability handler
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Is this CAP handled outside of our builtin server?
        /// </summary>
        public bool HandlerIsExternal { get { return ExternalHandlerURL != null; } }

        /// <summary>
        /// Whether or not this capshandler supports caps control
        /// </summary>
        public bool SupportsControl { get; set; }

        /// <summary>
        /// Returns the requesthandler that will respond to requests to this CAP
        /// </summary>
        public IRequestHandler RequestHandler { get; set; }

        /// <summary>
        /// The path to an external handler for this CAP
        /// </summary>
        public string ExternalHandlerURL { get; set; }

        /// <summary>
        /// Action to execute when the cap should pause traffic
        /// </summary>
        public Action PauseTrafficHandler { get; set; }

        /// <summary>
        /// Action to execute when the cap should resume traffic
        /// </summary>
        public Action ResumeTrafficHandler { get; set; }

        /// <summary>
        /// Action to execute when the cap should resume traffic
        /// </summary>
        public Action<int> MaxBandwidthHandler { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="handler"></param>
        /// <param name="supportsControl"></param>
        /// <param name="handlerExternal"></param>
        public CapsHandler(string name, IRequestHandler handler, bool supportsControl)
        {
            Name = name;
            RequestHandler = handler;
            SupportsControl = supportsControl;
        }

        public CapsHandler(string name, string handlerUrl, bool supportsControl)
        {
            Name = name;
            ExternalHandlerURL = handlerUrl;
            SupportsControl = supportsControl;
        }

        /// <summary>
        /// Called by caps when it would like us to pause traffic to the client
        /// </summary>
        public void PauseTraffic()
        {
            if (PauseTrafficHandler != null)
            {
                PauseTrafficHandler();
            }
        }

        /// <summary>
        /// Called by caps when it would like us to resume traffic to the client
        /// </summary>
        public void ResumeTraffic()
        {
            if (ResumeTrafficHandler != null)
            {
                ResumeTrafficHandler();
            }
        }

        /// <summary>
        /// Called by caps when it wants us to set a maximum bandwidth for a user
        /// </summary>
        /// <param name="maxBw"></param>
        public void SetMaxBandwidth(int maxBw)
        {
            if (MaxBandwidthHandler != null)
            {
                MaxBandwidthHandler(maxBw);
            }
        }
    }
}
