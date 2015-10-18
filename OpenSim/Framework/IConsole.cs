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
 *     * Neither the name of the OpenSimulator Project nor the
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

using System.Collections.Generic;

namespace OpenSim.Framework
{
    public interface IConsole
    {
        object ConsoleScene { get; set; }

        /// <summary>
        /// The default prompt text.
        /// </summary>
        string DefaultPrompt { get; set; }

        /// <summary>
        /// Sends a warning to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Warn(string format, params object[] args);

        /// <summary>
        /// Sends a warning to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Warn(string sender, string format, params object[] args);

        /// <summary>
        /// Sends a notice to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Notice(string format, params object[] args);

        /// <summary>
        /// Sends a notice to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Notice(string sender, string format, params object[] args);

        /// <summary>
        /// Sends an error to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Error(string format, params object[] args);

        /// <summary>
        /// Sends an error to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Error(string sender, string format, params object[] args);

        /// <summary>
        /// Sends a status message to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Status(string format, params object[] args);

        /// <summary>
        /// Sends a status message to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        void Status(string sender, string format, params object[] args);

        void Debug(string format, params object[] args);

        void Debug(string sender, string format, params object[] args);

        void LockOutput();
        void UnlockOutput();

        void Output(string text);
        void Output(string text, string level);
        void OutputFormat(string format, params object[] components);

        string CmdPrompt(string p);
        string CmdPrompt(string p, string def);
        string CmdPrompt(string p, List<char> excludedCharacters);
        string CmdPrompt(string p, string def, List<char> excludedCharacters);

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        string CmdPrompt(string prompt, string defaultresponse, List<string> options);

        // Displays a prompt and waits for the user to enter a string, then returns that string
        // (Done with no echo and suitable for passwords)
        string PasswdPrompt(string p);
        string ReadLine(string p, bool isCommand, bool e);
    }
}
