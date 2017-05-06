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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.ServiceConnectors.Interregion;
using OpenSim.Region.Framework.Scenes;

namespace InWorldz.Testing
{
    public class SceneHelper
    {
        public static Scene CreateScene(ushort httpPort, uint xloc, uint yloc)
        {
            //2130706433 = 127.0.0.1
            BaseHttpServer server = new BaseHttpServer(httpPort, new System.Net.IPAddress(2130706433));
            var commsManager = new OpenSim.Framework.Communications.CommunicationsManager(new OpenSim.Framework.NetworkServersInfo(), server,
                    new AssetCache(), new LibraryRootFolder(".", "Library"));
            var gridSvc = new SceneCommunicationService(commsManager);
            var regionInfo =  new OpenSim.Framework.RegionInfo(xloc, yloc,
                    new System.Net.IPEndPoint(new System.Net.IPAddress(2130706433), 9001),
                    "localhost");

            var restComms = new RESTInterregionComms();
            gridSvc.UnitTest_SetCommsOut(restComms);
            Scene scene = new Scene(regionInfo, commsManager, gridSvc);

            restComms.Initialize_Unittest(scene);

            server.Start();

            

            return scene;
        }

        public static void TearDownScene(Scene scene)
        {
            ((BaseHttpServer)scene.CommsManager.HttpServer).Stop();
        }
    }
}
