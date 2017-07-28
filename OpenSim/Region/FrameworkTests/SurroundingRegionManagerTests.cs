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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using KellermanSoftware.CompareNetObjects;
using System.Threading;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.FrameworkTests
{
    [TestFixture]
    class SurroundingRegionManagerTests
    {
        private Scene mockScene;
        private SurroundingRegionManager srm;

        private Dictionary<NeighborStateChangeType, int> stateChangeCounts = new Dictionary<NeighborStateChangeType, int>();

        [SetUp]
        public void Setup()
        {
            mockScene = InWorldz.Testing.SceneHelper.CreateScene(9000, 1000, 1000);
            srm = mockScene.SurroundingRegions;
            srm.OnNeighborStateChange += srm_OnNeighborStateChange;
        }

        void srm_OnNeighborStateChange(SimpleRegionInfo neighbor, NeighborStateChangeType changeType)
        {
            if (stateChangeCounts.ContainsKey(changeType))
            {
                stateChangeCounts[changeType]++;
            }
            else
            {
                stateChangeCounts[changeType] = 1;
            }
            
        }

        [TearDown]
        public void TearDown()
        {
            InWorldz.Testing.SceneHelper.TearDownScene(mockScene);
        }

        public static void SendCreateRegionMessage(uint xloc, uint yloc, ushort receiverServerPort, ushort regionServerport)
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = regionServerport,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = xloc,
                RegionLocY = yloc,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:" + receiverServerPort + "/region2/regionup");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            var response = req.GetResponse();
            response.Dispose();
        }

        public static void SendRegionDownMessage(uint xloc, uint yloc, ushort receiverServerPort, ushort regionServerport)
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = regionServerport,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = xloc,
                RegionLocY = yloc,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:" + receiverServerPort + "/region2/regiondown");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            var response = req.GetResponse();
            response.Dispose();
        }

        [Test]
        public void TestAddRegion()
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = 9000,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = 1100,
                RegionLocY = 1000,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/regionup");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            var response = req.GetResponse();
            Assert.AreEqual(2, response.ContentLength);
            StreamReader sr = new StreamReader(response.GetResponseStream());
            Assert.AreEqual("OK", sr.ReadToEnd());

            var neighbors = srm.GetNeighborsSnapshot();
            Assert.AreEqual(1, neighbors.Count);

            CompareObjects.Equals((SimpleRegionInfo)info, neighbors[0].RegionInfo);

            Assert.AreEqual(1, stateChangeCounts[NeighborStateChangeType.NeighborUp]);
            stateChangeCounts.Clear();

        }

        [Test]
        public void TestRemoveRegion()
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = 9000,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = 1100,
                RegionLocY = 1000,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/regionup");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            var response = req.GetResponse();
            Assert.AreEqual(2, response.ContentLength);
            StreamReader sr = new StreamReader(response.GetResponseStream());
            Assert.AreEqual("OK", sr.ReadToEnd());

            var neighbors = srm.GetNeighborsSnapshot();
            Assert.AreEqual(1, neighbors.Count);

            CompareObjects.Equals((SimpleRegionInfo)info, neighbors[0].RegionInfo);

            response.Dispose();

            req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/regiondown");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            response = req.GetResponse();
            Assert.AreEqual(2, response.ContentLength);
            sr = new StreamReader(response.GetResponseStream());
            Assert.AreEqual("OK", sr.ReadToEnd());

            neighbors = srm.GetNeighborsSnapshot();
            Assert.AreEqual(0, neighbors.Count);

            response.Dispose();

            Assert.AreEqual(1, stateChangeCounts[NeighborStateChangeType.NeighborDown]);
            stateChangeCounts.Clear();
        }

        [Test]
        public void TestRegionPing()
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = 9000,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = 1100,
                RegionLocY = 1000,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/regionup");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            var response = req.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            response.Dispose();

            var neighbors = srm.GetNeighborsSnapshot();
            var firstPing = neighbors[0].LastPingReceivedOn;

            Thread.Sleep(1000);

            req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/heartbeat");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("key");
            req.Timeout = 10000;
            req.Method = "POST";

            serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();

            response = req.GetResponse();
            Assert.AreEqual(2, response.ContentLength);
            sr = new StreamReader(response.GetResponseStream());
            Assert.AreEqual("OK", sr.ReadToEnd());

            neighbors = srm.GetNeighborsSnapshot();
            Assert.AreEqual(1, neighbors.Count);

            Assert.Greater(neighbors[0].LastPingReceivedOn, firstPing);

            response.Dispose();

            Assert.AreEqual(1, stateChangeCounts[NeighborStateChangeType.NeighborPing]);
            stateChangeCounts.Clear();
        }

        [Test]
        public void TestUnauthorized()
        {
            RegionInfo info = new RegionInfo
            {
                ExternalHostName = "localhost",
                InternalEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                HttpPort = 9000,
                OutsideIP = "127.0.0.1",
                RegionID = UUID.Random(),
                RegionLocX = 1100,
                RegionLocY = 1000,
            };

            WebRequest req = HttpWebRequest.Create("http://127.0.0.1:9000/region2/regionup");
            req.Headers["authorization"] = Util.GenerateHttpAuthorization("BADPASSWORD");
            req.Timeout = 10000;
            req.Method = "POST";

            byte[] serRegInfo = OSDParser.SerializeLLSDBinary(info.PackRegionInfoData());

            var rs = req.GetRequestStream();
            rs.Write(serRegInfo, 0, serRegInfo.Length);
            rs.Flush();


            Assert.Throws<System.Net.WebException>(() =>
                {
                    var response = req.GetResponse();
                });
        }

        [Test]
        public void TestQuerySurroundingRegions256()
        {
            // U = us at 1000, 1000
            //   N N N
            //   N U N
            //   N N N
            
            //top row
            SendCreateRegionMessage(999, 999, 9000, 9001);
            SendCreateRegionMessage(1000, 999, 9000, 9001);
            SendCreateRegionMessage(1001, 999, 9000, 9001);

            //middle row
            SendCreateRegionMessage(999, 1000, 9000, 9001);
            SendCreateRegionMessage(1001, 1000, 9000, 9001);
            
            //bottom row
            SendCreateRegionMessage(999, 1001, 9000, 9001);
            SendCreateRegionMessage(1000, 1001, 9000, 9001);
            SendCreateRegionMessage(1001, 1001, 9000, 9001);

            Assert.NotNull(srm.GetKnownNeighborAt(999, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 1000));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 1000));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 1001));

            //try a surrounding query
            Assert.AreEqual(8, srm.GetKnownNeighborsWithinClientDD(1, 2).Count);
            Assert.AreEqual(8, srm.GetKnownNeighborsWithinClientDD(256, 2).Count);
        }

        [Test]
        public void TestQuerySurroundingRegions512()
        {
            // U = us at 1000, 1000
            // X X X X X
            // X N N N X
            // X N U N X
            // X N N N X
            // X X X X X

            //top row
            SendCreateRegionMessage(998, 998, 9000, 9001);
            SendCreateRegionMessage(999, 998, 9000, 9001);
            SendCreateRegionMessage(1000, 998, 9000, 9001);
            SendCreateRegionMessage(1001, 998, 9000, 9001);
            SendCreateRegionMessage(1002, 998, 9000, 9001);

            //second row
            SendCreateRegionMessage(998, 999, 9000, 9001);
            SendCreateRegionMessage(999, 999, 9000, 9001);
            SendCreateRegionMessage(1000, 999, 9000, 9001);
            SendCreateRegionMessage(1001, 999, 9000, 9001);
            SendCreateRegionMessage(1002, 999, 9000, 9001);

            //middle row
            SendCreateRegionMessage(998, 1000, 9000, 9001);
            SendCreateRegionMessage(999, 1000, 9000, 9001);
            SendCreateRegionMessage(1001, 1000, 9000, 9001);
            SendCreateRegionMessage(1002, 1000, 9000, 9001);

            //an extra to try to trip up the limiter
            SendCreateRegionMessage(1003, 1000, 9000, 9001);

            //second to last row
            SendCreateRegionMessage(998, 1001, 9000, 9001);
            SendCreateRegionMessage(999, 1001, 9000, 9001);
            SendCreateRegionMessage(1000, 1001, 9000, 9001);
            SendCreateRegionMessage(1001, 1001, 9000, 9001);
            SendCreateRegionMessage(1002, 1001, 9000, 9001);

            //last row
            SendCreateRegionMessage(998, 1002, 9000, 9001);
            SendCreateRegionMessage(999, 1002, 9000, 9001);
            SendCreateRegionMessage(1000, 1002, 9000, 9001);
            SendCreateRegionMessage(1001, 1002, 9000, 9001);
            SendCreateRegionMessage(1002, 1002, 9000, 9001);



            //top row
            Assert.NotNull(srm.GetKnownNeighborAt(998, 998));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 998));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 998));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 998));
            Assert.NotNull(srm.GetKnownNeighborAt(1002, 998));

            //second row
            Assert.NotNull(srm.GetKnownNeighborAt(998, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 999));
            Assert.NotNull(srm.GetKnownNeighborAt(1002, 999));

            //middle row
            Assert.NotNull(srm.GetKnownNeighborAt(998, 1000));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 1000));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 1000));
            Assert.NotNull(srm.GetKnownNeighborAt(1002, 1000));

            //second to last row
            Assert.NotNull(srm.GetKnownNeighborAt(998, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 1001));
            Assert.NotNull(srm.GetKnownNeighborAt(1002, 1001));

            //last row
            Assert.NotNull(srm.GetKnownNeighborAt(998, 1002));
            Assert.NotNull(srm.GetKnownNeighborAt(999, 1002));
            Assert.NotNull(srm.GetKnownNeighborAt(1000, 1002));
            Assert.NotNull(srm.GetKnownNeighborAt(1001, 1002));
            Assert.NotNull(srm.GetKnownNeighborAt(1002, 1002));



            //try a surrounding query
            Assert.AreEqual(8, srm.GetKnownNeighborsWithinClientDD(1, 2).Count);
            Assert.AreEqual(8, srm.GetKnownNeighborsWithinClientDD(256, 2).Count);
            Assert.AreEqual(24, srm.GetKnownNeighborsWithinClientDD(257, 2).Count);
            Assert.AreEqual(24, srm.GetKnownNeighborsWithinClientDD(512, 2).Count);
            Assert.AreEqual(24, srm.GetKnownNeighborsWithinClientDD(1024, 2).Count);
        }
    }
}
