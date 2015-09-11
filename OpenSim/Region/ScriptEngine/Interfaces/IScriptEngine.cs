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

using log4net;
using System;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Interfaces;
using Amib.Threading;
using OpenSim.Framework;
using System.Collections.Generic;

namespace OpenSim.Region.ScriptEngine.Interfaces
{
    /// <summary>
    /// An interface for a script API module to communicate with
    /// the engine it's running under
    /// </summary>

    public delegate void ScriptRemoved(UUID script);
    public delegate void ObjectRemoved(UUID prim);

    public interface IScriptEngine
    {
        /// <summary>
        /// Queue an event for execution
        /// </summary>
        IScriptWorkItem QueueEventHandler(object parms);

        Scene World { get; }

        IScriptModule ScriptModule { get; }

        /// <summary>
        /// Post an event to a single script
        /// </summary>
        bool PostScriptEvent(UUID itemID, EventParams parms);
        
        /// <summary>
        /// Post event to an entire prim
        /// </summary>
        bool PostObjectEvent(uint localID, EventParams parms);

        DetectParams GetDetectParams(UUID item, int number);
        void SetMinEventDelay(UUID itemID, double delay);
        int GetStartParameter(UUID itemID);

        void SetScriptState(UUID itemID, bool state);
        bool GetScriptState(UUID itemID);
        void SetState(UUID itemID, string newState);
        void ApiResetScript(UUID itemID);
        void ResetScript(UUID itemID);

        IConfig Config { get; }
        IConfigSource ConfigSource { get; }
        string ScriptEngineName { get; }
        IScriptApi GetApi(UUID itemID, string name);

        // These can only be called from inside the script run thread as they directly access script data.
        float GetTotalRuntime(UUID itemID);
        float GetAverageScriptTime(UUID itemID);
        int GetFreeMemory(UUID itemID);
        int GetUsedMemory(UUID itemID);
        int GetMaxMemory(); // same for all scripts

        float GetEventQueueFreeSpacePercentage(UUID itemID);

        void SetTimerEvent(uint localID, UUID itemID, float sec);

        /// <summary>
        /// Sends a return value to a script from a long running system call
        /// </summary>
        /// <param name="itemId">Script Id</param>
        /// <param name="returnVal">The value to return</param>
        /// <param name="delay">The sleep delay before returning the script to operation</param>
        void SysReturn(UUID itemId, object returnVal, int delay);

        void UpdateTouchData(uint localID, DetectParams[] det);

        /// <summary>
        /// Enables or disables all scripts in the specified object
        /// </summary>
        /// <param name="group">The group to enable or disable scripts in</param>
        /// <param name="enabled">If true, all scripts will be enabled, if false all scripts will be disabled</param>
        void ChangeScriptEnabledLandStatus(SceneObjectGroup group, bool enabled);

        /// <summary>
        /// Returns true if scripts can run on the given parcel, false if not
        /// </summary>
        /// <param name="parcel">The parcel to check</param>
        /// <param name="hostPart">The part we're testing</param>
        /// <returns></returns>
        bool ScriptsCanRun(ILandObject parcel, SceneObjectPart hostPart);
    }
}
