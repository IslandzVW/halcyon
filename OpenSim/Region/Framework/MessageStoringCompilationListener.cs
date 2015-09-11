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

using System.Threading;
using OpenSim.Region.Framework.Interfaces;
using System.Text.RegularExpressions;

namespace OpenSim.Region.Framework
{
    /// <summary>
    /// Stores all messages from a compiler in a list
    /// </summary>
    public class MessageStoringCompilationListener : ICompilationListener, IDisposable
    {
        private List<string> _messages = new List<string>();
        private bool _hasErrors = false;
        private ManualResetEvent _waitEvent = new ManualResetEvent(false);
        private bool _timedOut = false;

        /// <summary>
        /// Default timeout for compilation in ms
        /// </summary>
        public const int DEFAULT_TIMEOUT = 30000;

        public IEnumerable<string> Messages
        {
            get
            {
                return _messages;
            }
        }

        #region ICompilationListener Members

        public void Error(string message)
        {
            _hasErrors = true;
            _messages.Add(FixLineInfo(message));
        }

        public bool HasErrors()
        {
            return _hasErrors;
        }

        public void Info(string message)
        {
            _messages.Add(FixLineInfo(message));
        }

        private string FixLineInfo(string message)
        {
            const string PATTERN = "line ([0-9]+):([0-9]+)";
            Match lineMatch = Regex.Match(message, PATTERN);
            if (lineMatch.Success)
            {
                string withInfoRemoved = Regex.Replace(message, PATTERN, String.Empty);
                return String.Format("({0}, {1}) {2}",
                    Convert.ToInt32(lineMatch.Groups[1].Value) - 1,
                    Convert.ToInt32(lineMatch.Groups[2].Value),
                    withInfoRemoved);
            }

            return message;
        }

        public void CompilationFinished()
        {
            //check for _timedOut to prevent a crash from this object
            //already being disposed
            if (!_timedOut)
            {
                _waitEvent.Set();
            }
        }

        public void WaitForCompilation(int millisecondsTimeout)
        {
            if (!_waitEvent.WaitOne(millisecondsTimeout))
            {
                _timedOut = true;
            }
        }

        #endregion

        public void Dispose()
        {
            _waitEvent.Dispose();
        }
    }
}
