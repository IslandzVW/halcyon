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

/* Original code: Tedd Hansen */
using System;
using System.IO;
using System.Reflection;
using log4net;

namespace OpenSim.Region.Framework.Scenes.Scripting
{
    public class ScriptEngineLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public IScriptEngineInterface LoadScriptEngine(string EngineName)
        {
            IScriptEngineInterface ret = null;
            try
            {
                ret =
                    LoadAndInitAssembly(
                        Path.Combine("ScriptEngines", "OpenSim.Region.ScriptEngine." + EngineName + ".dll"),
                        "OpenSim.Region.ScriptEngine." + EngineName + ".ScriptEngine");
            }
            catch (Exception e)
            {
                m_log.Error("[ScriptEngine]: " +
                            "Error loading assembly \"" + EngineName + "\": " + e.Message + ", " +
                            e.StackTrace.ToString());
            }
            return ret;
        }

        /// <summary>
        /// Does actual loading and initialization of script Assembly. Can throw many kinds of errors, depending on what problem was run across during loading.
        /// </summary>
        /// <param name="file_name">File name of script assembly (.dll)</param>
        /// <param name="namespace_class">The namespace and type in the assembly to initialize and return</param>
        /// <returns></returns>
        private IScriptEngineInterface LoadAndInitAssembly(string file_name, string namespace_class)
        {
            //Common.SendToDebug("Loading ScriptEngine Assembly " + FileName);

            // Load .Net Assembly (.dll), initialize and return it

            // All error checking is expected to be handled by caller.

            Assembly a = Assembly.LoadFrom(file_name);

            Type t = a.GetType(namespace_class, true);

            IScriptEngineInterface ret = (IScriptEngineInterface) Activator.CreateInstance(t);

            return ret;
        }
    }
}
