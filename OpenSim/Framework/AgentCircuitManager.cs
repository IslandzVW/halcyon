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

using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Manage client circuits
    /// </summary>
    public class AgentCircuitManager
    {
        private Dictionary<uint, AgentCircuitData> AgentCircuits = new Dictionary<uint, AgentCircuitData>();

        public virtual AuthenticateResponse AuthenticateSession(UUID sessionID, UUID agentID, uint circuitcode)
        {
            AgentCircuitData validcircuit = null;

            lock (AgentCircuits)
            {
                if (AgentCircuits.ContainsKey(circuitcode))
                {
                    validcircuit = AgentCircuits[circuitcode];
                }
            }

            AuthenticateResponse user = new AuthenticateResponse();
            if (validcircuit == null)
            {
                //don't have this circuit code in our list
                user.Authorised = false;
                return (user);
            }

            if ((sessionID == validcircuit.SessionID) && (agentID == validcircuit.AgentID))
            {
                user.Authorised = true;
                user.LoginInfo = new Login();
                user.LoginInfo.Agent = agentID;
                user.LoginInfo.Session = sessionID;
                user.LoginInfo.SecureSession = validcircuit.SecureSessionID;
                user.LoginInfo.First = validcircuit.FirstName;
                user.LoginInfo.Last = validcircuit.LastName;
                user.LoginInfo.InventoryFolder = validcircuit.InventoryFolder;
                user.LoginInfo.BaseFolder = validcircuit.BaseFolder;
                user.LoginInfo.StartPos = validcircuit.startpos;
                user.ClientVersion = validcircuit.ClientVersion;
            }
            else
            {
                // Invalid
                user.Authorised = false;
            }

            return (user);
        }

        public virtual AuthenticateResponse AuthenticateSession(UUID sessionID, UUID agentID)
        {
            AgentCircuitData validcircuit = null;

            lock (AgentCircuits)
            {
                validcircuit = AgentCircuits.FirstOrDefault((acd) => acd.Value.SessionID == sessionID).Value;
            }

            AuthenticateResponse user = new AuthenticateResponse();
            if (validcircuit == null)
            {
                //don't have this circuit code in our list
                user.Authorised = false;
                return (user);
            }

            if ((sessionID == validcircuit.SessionID) && (agentID == validcircuit.AgentID))
            {
                user.Authorised = true;
                user.LoginInfo = new Login();
                user.LoginInfo.Agent = agentID;
                user.LoginInfo.Session = sessionID;
                user.LoginInfo.SecureSession = validcircuit.SecureSessionID;
                user.LoginInfo.First = validcircuit.FirstName;
                user.LoginInfo.Last = validcircuit.LastName;
                user.LoginInfo.InventoryFolder = validcircuit.InventoryFolder;
                user.LoginInfo.BaseFolder = validcircuit.BaseFolder;
                user.LoginInfo.StartPos = validcircuit.startpos;
                user.ClientVersion = validcircuit.ClientVersion;
            }
            else
            {
                // Invalid
                user.Authorised = false;
            }

            return (user);
        }

        /// <summary>
        /// Add information about a new circuit so that later on we can authenticate a new client session.
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="agentData"></param>
        public virtual void AddNewCircuit(uint circuitCode, AgentCircuitData agentData)
        {
            lock (AgentCircuits)
            {
                AgentCircuits[circuitCode] = agentData;
            }
        }

        public virtual void RemoveCircuit(uint circuitCode)
        {
            lock (AgentCircuits)
            {
                AgentCircuits.Remove(circuitCode);
            }
        }

        public AgentCircuitData GetAgentCircuitData(uint circuitCode)
        {
            lock (AgentCircuits)
            {
                AgentCircuitData agentCircuit = null;
                AgentCircuits.TryGetValue(circuitCode, out agentCircuit);
                return agentCircuit;
            }
        }
    }
}
