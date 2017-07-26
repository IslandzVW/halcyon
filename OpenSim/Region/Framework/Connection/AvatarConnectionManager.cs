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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Connection
{
    /// <summary>
    /// Manages connections from the viewer through all stages of connection 
    /// from setup, to UDP establishment, to teardown
    /// </summary>
    public class AvatarConnectionManager
    {
        /// <summary>
        /// Our reference to the caps module for new connection init
        /// </summary>
        private ICapabilitiesModule _capsModule;

        /// <summary>
        /// Connections by user id
        /// </summary>
        private Dictionary<UUID, AvatarConnection> _connectionsByUserId = new Dictionary<UUID, AvatarConnection>();

        /// <summary>
        /// Connections by endpoint
        /// </summary>
        private Dictionary<System.Net.IPEndPoint, AvatarConnection> _connectionsByEndpoint = new Dictionary<System.Net.IPEndPoint, AvatarConnection>();

        /// <summary>
        /// We must know a transit controller to be able to adjust connection properties depending on the transit state
        /// </summary>
        private AvatarTransit.AvatarTransitController _transitController;

        public AvatarConnectionManager(ICapabilitiesModule capsModule, AvatarTransit.AvatarTransitController controller)
        {
            _capsModule = capsModule;
            _transitController = controller;

            _transitController.OnTransitStateChanged += _transitController_OnTransitStateChanged;
        }

        private async Task _transitController_OnTransitStateChanged(Scenes.ScenePresence sp, AvatarTransit.TransitStage newStage, 
            IEnumerable<uint> rideOnPrims)
        {
            AvatarConnection conn;
            lock (_connectionsByUserId)
            {
                _connectionsByUserId.TryGetValue(sp.UUID, out conn);
            }

            if (conn != null)
            {
                await conn.TransitStateChange(newStage, rideOnPrims);
            }
            else
            {
                throw new InvalidOperationException("OnTransitStateChanged called for unknown connection");
            }
        }

        /// <summary>
        /// Returns an AvatarConnection if the given user is established or in the process of being established
        /// </summary>
        /// <param name="userId"></param>
        public AvatarConnection GetConnection(UUID userId)
        {
            lock (_connectionsByUserId)
            {
                AvatarConnection conn;
                _connectionsByUserId.TryGetValue(userId, out conn);

                return conn;
            }
        }

        /// <summary>
        /// Returns an AvatarConnection if the given user is established on UDP
        /// </summary>
        /// <param name="userId"></param>
        public AvatarConnection GetConnection(System.Net.IPEndPoint userEp)
        {
            lock (_connectionsByUserId)
            {
                AvatarConnection conn;
                _connectionsByEndpoint.TryGetValue(userEp, out conn);

                return conn;
            }
        }

        /// <summary>
        /// Returns a list of all connections we know about
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AvatarConnection> GetAllConnections()
        {
            List<AvatarConnection> connections;
            lock (_connectionsByUserId)
            {
                connections = new List<AvatarConnection>(_connectionsByEndpoint.Values);
            }

            return connections;
        }

        /// <summary>
        /// Creates a new connection for the given circuit. If a connection already exists for the user, 
        /// an exception is thrown
        /// </summary>
        /// <param name="circuitData">Circuit data describing the inbound connection</param>
        /// <returns>A new connection object</returns>
        /// <exception cref="ConnectionAlreadyEstablishedException">If the user is already connected</exception>
        public AvatarConnection NewConnection(AgentCircuitData circuitData, EstablishedBy reason)
        {
            AvatarConnection conn;
            lock (_connectionsByUserId)
            {
                if (_connectionsByUserId.TryGetValue(circuitData.AgentID, out conn))
                {
                    throw new ConnectionAlreadyEstablishedException(String.Format("Connection for user {0} is already established", circuitData.AgentID), conn);
                }
                else
                {
                    conn = new AvatarConnection(circuitData, reason);
                    conn.OnConnectionTerminated += conn_OnConnectionTerminated;
                    _connectionsByUserId[circuitData.AgentID] = conn;
                }
            }

            ICapsControl capsControl = _capsModule.CreateCaps(circuitData.AgentID, circuitData.CapsPath);
            conn.SetCapsControl(capsControl);

            return conn;
        }

        /// <summary>
        /// Handler for connection termination
        /// </summary>
        /// <param name="conn"></param>
        private void conn_OnConnectionTerminated(AvatarConnection conn)
        {
            lock (_connectionsByUserId)
            {
                _connectionsByUserId.Remove(conn.CircuitData.AgentID);

                if (conn.UdpCircuit != null)
                {
                    _connectionsByEndpoint.Remove(conn.UdpCircuit.RemoteEndPoint);
                }
            }
        }

        /// <summary>
        /// Returs whether or not the given client is authorized to connect
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="sessionId"></param>
        /// <param name="circuitCode"></param>
        /// <returns></returns>
        public bool IsAuthorized(UUID userId, UUID sessionId, uint circuitCode)
        {
            lock (_connectionsByUserId)
            {
                AvatarConnection conn;
                if (_connectionsByUserId.TryGetValue(userId, out conn))
                {
                    return conn.CircuitData.SessionID == sessionId && conn.CircuitData.CircuitCode == circuitCode;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Returs whether or not the given client is authorized to connect
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public bool IsAuthorized(UUID userId, UUID sessionId)
        {
            lock (_connectionsByUserId)
            {
                AvatarConnection conn;
                if (_connectionsByUserId.TryGetValue(userId, out conn))
                {
                    return conn.CircuitData.SessionID == sessionId;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Tries to attach the given UDP circuit to the AvatarConnection
        /// </summary>
        /// <param name="udpCircuit"></param>
        /// <returns></returns>
        public void TryAttachUdpCircuit(IClientAPI udpCircuit)
        {
            lock (_connectionsByUserId)
            {
                AvatarConnection conn;
                if (_connectionsByUserId.TryGetValue(udpCircuit.AgentId, out conn))
                {
                    conn.AttachUdpCircuit(udpCircuit);
                    udpCircuit.AfterAttachedToConnection(conn.CircuitData);

                    if (udpCircuit.RemoteEndPoint.Port != 0)
                        _connectionsByEndpoint[udpCircuit.RemoteEndPoint] = conn;
                }
                else
                {
                    throw new AttachUdpCircuitException(String.Format("Could not attach UDP ciruit for user {0}. User has no managed connections", udpCircuit.AgentId));
                }
            }
        }
    }
}
