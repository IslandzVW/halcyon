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
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Debugging
{
    internal class PhysxErrorCallback : PhysX.ErrorCallback
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int MAX_MESSAGES_BEFORE_NOTIFICATION = 20;

        private string lastMessage;
        private int lastMessageRepeat;

        public override void ReportError(PhysX.ErrorCode errorCode, string message, string file, int lineNumber)
        {
            string errorMessage = String.Format("[InWorldz.PhysxPhysics] PhysX ERROR: Code: {0}  Message: {1} ({2}:{3})",
                new object[] { errorCode, message, file, lineNumber });

            if (errorMessage == lastMessage && lastMessageRepeat < MAX_MESSAGES_BEFORE_NOTIFICATION)
            {
                lastMessageRepeat++;
                return;
            }
            else
            {
                if (lastMessageRepeat != 0)
                {
                    m_log.ErrorFormat("[InWorldz.PhysxPhysics] PhysX ERROR: (Last physics message repeats {0} times)", lastMessageRepeat);
                    lastMessageRepeat = 0;
                }

                lastMessage = errorMessage;
            }

            m_log.ErrorFormat(errorMessage);

            if (Settings.Instance.ThrowOnSdkError)
            {
                throw new PhysxSdkException(errorMessage);
            }
        }
    }
}
