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

using OpenSim.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Connection
{
    /// <summary>
    /// Object representing the whole of an avatar connection. This object
    /// encapsulates the CAPS as well as UDP communication object that makes
    /// up an avatar's connection to the simulator
    /// </summary>
    public class AvatarConnection
    {
        /// <summary>
        /// This is the circuit data we get passed from an initial contact from another
        /// simulator or the login service
        /// </summary>
        private AgentCircuitData _circuitData;

        /// <summary>
        /// Our control over user caps.
        /// </summary>
        private ICapsControl _capsControl;

        /// <summary>
        /// The state of this connection
        /// </summary>
        private AvatarConnectionState _state;

        /// <summary>
        /// Why this connection is established
        /// </summary>
        private EstablishedBy _establishedReason;

        /// <summary>
        /// Our UDP circuit. May be null if not yet set up
        /// </summary>
        private IClientAPI _udpCircuit;

        /// <summary>
        /// Whether or not this connection is currently terminating
        /// </summary>
        private bool _terminating = false;

        /// <summary>
        /// Delegate called for connection terminations
        /// </summary>
        /// <param name="conn">The connection that was terminated</param>
        public delegate void ConnectionTerminated(AvatarConnection conn);

        /// <summary>
        /// Event called when a connection is terminated
        /// </summary>
        public event ConnectionTerminated OnConnectionTerminated;


        /// <summary>
        /// This is the circuit data we get passed from an initial contact from another
        /// simulator or the login service
        /// </summary>
        public AgentCircuitData CircuitData
        {
            get
            {
                return _circuitData;
            }
        }

        /// <summary>
        /// The state of this connection
        /// </summary>
        public AvatarConnectionState State
        {
            get
            {
                return _state;
            }
        }

        /// <summary>
        /// The udp cicuit attached to this connection
        /// </summary>
        public IClientAPI UdpCircuit
        {
            get
            {
                return _udpCircuit;
            }
        }

        /// <summary>
        /// The SP we're connected with
        /// </summary>
        public Scenes.ScenePresence ScenePresence { get; set; }

        public AvatarConnection(AgentCircuitData circuitData, EstablishedBy reason)
        {
            _circuitData = circuitData;
            _state = AvatarConnectionState.UDPCircuitWait;
            _establishedReason = reason;
        }

        /// <summary>
        /// Attempts to attach a UDP circuit to this connection. 
        /// </summary>
        /// <param name="udpCircuit">The circuit to attach</param>
        /// <exception cref=""
        public void AttachUdpCircuit(IClientAPI udpCircuit)
        {
            if (_udpCircuit != null)
            {
                var ex = new AttachUdpCircuitException(String.Format("UDP circuit already exists for {0}. New code: {1}, Existing code: {2}",
                    _circuitData.AgentID, _udpCircuit.CircuitCode, udpCircuit.CircuitCode));

                ex.CircuitAlreadyExisted = true;

                if (udpCircuit.CircuitCode == _circuitData.CircuitCode &&
                    udpCircuit.SessionId == _circuitData.SessionID)
                {
                    ex.ExistingCircuitMatched = true;
                }

                throw ex;
            }

            if (udpCircuit.CircuitCode != _circuitData.CircuitCode)
            {
                throw new AttachUdpCircuitException(String.Format("UDP circuit code doesnt match expected code for {0}. Expected: {1}, Actual {2}",
                    _circuitData.AgentID, _circuitData.CircuitCode, udpCircuit.CircuitCode));
            }

            if (udpCircuit.SessionId != _circuitData.SessionID)
            {
                throw new AttachUdpCircuitException(String.Format("UDP session ID doesnt match expected ID for {0}. Expected: {1}, Actual {2}",
                    _circuitData.AgentID, _circuitData.SessionID, udpCircuit.SessionId));
            }

            _udpCircuit = udpCircuit;
            _udpCircuit.OnConnectionClosed += _udpCircuit_OnConnectionClosed;
            _udpCircuit.OnSetThrottles += _udpCircuit_OnSetThrottles;
            _state = AvatarConnectionState.Established;
        }

        void _udpCircuit_OnSetThrottles(int bwMax)
        {
            //fire this off on a secondary thread. it is not order critical and if
            //aperture is hung this can hang
            Util.FireAndForget((obj) => { this.SetRootBandwidth(bwMax); });
        }

        void _udpCircuit_OnConnectionClosed(IClientAPI obj)
        {
            this.Terminate(false);
        }

        /// <summary>
        /// Called by the transit manager when this avatar has entered a new transit stage
        /// </summary>
        /// <param name="stage"></param>
        public async Task TransitStateChange(AvatarTransit.TransitStage stage, IEnumerable<uint> rideOnPrimIds)
        {
            if (stage == AvatarTransit.TransitStage.SendBegin)
            {
                var pa = ScenePresence.PhysicsActor;

                if (pa != null)
                {
                    pa.Suspend();
                }

                if (_capsControl != null) _capsControl.PauseTraffic();
                await _udpCircuit.PauseUpdatesAndFlush();
            }
            else if (stage == AvatarTransit.TransitStage.SendCompletedSuccess || stage == AvatarTransit.TransitStage.SendError)
            {
                if (_capsControl != null) _capsControl.ResumeTraffic();
                if (stage == AvatarTransit.TransitStage.SendCompletedSuccess)
                    _udpCircuit.ResumeUpdates(rideOnPrimIds);
                else
                    _udpCircuit.ResumeUpdates(null);    // don't kill object update if not leaving region

                if (stage == AvatarTransit.TransitStage.SendError)
                {
                    var pa = ScenePresence.PhysicsActor;

                    if (pa != null)
                    {
                        pa.Resume(false, null);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns a caps control to this connection
        /// </summary>
        /// <param name="capsControl"></param>
        internal void SetCapsControl(ICapsControl capsControl)
        {
            _capsControl = capsControl;
        }

        /// <summary>
        /// Terminates this user connection. Terminates UDP and tears down caps handlers and waits for the 
        /// teardown to complete
        /// </summary>
        public void Terminate(bool waitForClose)
        {
            if (!_terminating)
            {
                _terminating = true;

                if (_udpCircuit != null) _udpCircuit.Close();
                if (_capsControl != null) _capsControl.Teardown();

                if (_udpCircuit != null && waitForClose)
                {
                    _udpCircuit.WaitForClose();
                }

                var terminatedHandler = OnConnectionTerminated;
                if (terminatedHandler != null)
                {
                    terminatedHandler(this);
                }
            }
            else
            {
                if (_udpCircuit != null && waitForClose)
                {
                    _udpCircuit.WaitForClose();
                }
            }
        }

        /// <summary>
        /// Sets the bandwidth available. We will assign this number to our caps
        /// </summary>
        /// <param name="bytesPerSecond"></param>
        internal void SetRootBandwidth(int bytesPerSecond)
        {
            if (_capsControl != null) _capsControl.SetMaxBandwidth(bytesPerSecond);
        }
    }
}
