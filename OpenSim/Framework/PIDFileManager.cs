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

using System.Diagnostics;
using System.IO;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// Creates a file with this processes PID and status as the given pidfile or, if null, processname.pid
    /// </summary>
    public class PIDFileManager
    {
        public enum Status
        {
            Init = 0,
            Starting,
            Running
        }

        public string PidFile { get; private set; }

        public PIDFileManager(string pidFile)
        {
            var thisProcess = Process.GetCurrentProcess();

            PidFile = pidFile;
            if (string.IsNullOrWhiteSpace(pidFile))
            {
                PidFile = $"{Path.GetFileName(thisProcess.MainModule.FileName)}.pid";
            }

            this.SetStatus(Status.Init);
        }

        public void SetStatus(Status status)
        {
            var pid = Process.GetCurrentProcess().Id;

            using (FileStream pidFile = File.OpenWrite(PidFile))
            {
                var pidInfo = $"{((int)status)} {pid}";
                var utf8bytes = Encoding.UTF8.GetBytes(pidInfo);

                pidFile.Write(utf8bytes, 0, utf8bytes.Length);
            }
        }
    }
}
