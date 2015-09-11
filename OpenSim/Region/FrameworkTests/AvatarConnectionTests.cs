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
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSim.Region.Framework.Scenes;
using InWorldz.Testing;
using OpenSim.Framework;
using System.Threading;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Connection;
using System.Collections;
using OpenMetaverse;

namespace OpenSim.Region.FrameworkTests
{
    [TestFixture]
    public class AvatarConnectionTests
    {
        [Test]
        public void TestConnectionCanOnlyAttachSingleUDP()
        {
            AvatarConnection conn = new AvatarConnection(new AgentCircuitData(), EstablishedBy.Login);
            conn.AttachUdpCircuit(new MockClientAPI());

            Assert.Throws<AttachUdpCircuitException>(() => conn.AttachUdpCircuit(new MockClientAPI()));
        }

        [Test]
        public void TestConnectionManagerAllowsSingleUserInstance()
        {
            UUID agentId = UUID.Random();

            OpenSim.Region.Framework.AvatarTransit.AvatarTransitController transitCont
                = new Framework.AvatarTransit.AvatarTransitController(null);

            AvatarConnectionManager mgr = new AvatarConnectionManager(new MockCapsModule(), transitCont);
            mgr.NewConnection(new AgentCircuitData { AgentID = agentId }, EstablishedBy.Login);

            Assert.Throws<ConnectionAlreadyEstablishedException>(() =>
                mgr.NewConnection(new AgentCircuitData { AgentID = agentId }, EstablishedBy.Login));
        }

        [Test]
        public void TestAuthorized()
        {
            UUID agentId = UUID.Random();
            UUID sessionId = UUID.Random();
            uint circuitCode = 5;

            OpenSim.Region.Framework.AvatarTransit.AvatarTransitController transitCont
                = new Framework.AvatarTransit.AvatarTransitController(null);

            AvatarConnectionManager mgr = new AvatarConnectionManager(new MockCapsModule(), transitCont);
            mgr.NewConnection(new AgentCircuitData { AgentID = agentId, SessionID = sessionId, CircuitCode = circuitCode }, EstablishedBy.Login);

            Assert.IsTrue(mgr.IsAuthorized(agentId, sessionId, circuitCode));
        }

        [Test]
        public void TestExistsAfterInsertion()
        {
            UUID agentId = UUID.Random();
            UUID sessionId = UUID.Random();
            uint circuitCode = 5;

            OpenSim.Region.Framework.AvatarTransit.AvatarTransitController transitCont
                = new Framework.AvatarTransit.AvatarTransitController(null);

            AvatarConnectionManager mgr = new AvatarConnectionManager(new MockCapsModule(), transitCont);
            mgr.NewConnection(new AgentCircuitData { AgentID = agentId, SessionID = sessionId, CircuitCode = circuitCode }, EstablishedBy.Login);

            Assert.IsNotNull(mgr.GetConnection(agentId));
        }

        [Test]
        public void TestRemoveConnection()
        {
            UUID agentId = UUID.Random();
            UUID sessionId = UUID.Random();
            uint circuitCode = 5;

            OpenSim.Region.Framework.AvatarTransit.AvatarTransitController transitCont
                = new Framework.AvatarTransit.AvatarTransitController(null);

            AvatarConnectionManager mgr = new AvatarConnectionManager(new MockCapsModule(), transitCont);
            var conn = mgr.NewConnection(new AgentCircuitData { AgentID = agentId, SessionID = sessionId, CircuitCode = circuitCode }, EstablishedBy.Login);

            var clientApi = new MockClientAPI();
            clientApi.SessionId = sessionId;
            clientApi.AgentId = agentId;
            clientApi.CircuitCode = circuitCode;
            conn.AttachUdpCircuit(clientApi);

            conn.Terminate(false);

            Assert.IsNull(mgr.GetConnection(agentId));
        }
    }
}
