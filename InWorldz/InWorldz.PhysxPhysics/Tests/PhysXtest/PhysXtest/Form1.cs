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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace PhysXtest
{
    public partial class Form1 : Form
    {
        PhysX.Material material;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private PhysX.RigidDynamic CreateBox(PhysX.Scene scene, int offset)
        {
            const float HEIGHT = 20.0f;

            var rigid = scene.Physics.CreateRigidDynamic();
            var shape = rigid.CreateShape(new PhysX.BoxGeometry(1.0f, 1.0f, 1.0f), material);
            
            rigid.GlobalPose = PhysX.Math.Matrix.Translation(130f, 130f, HEIGHT + offset);

            return rigid;
        }

        private PhysX.RigidDynamic CreateSphere(PhysX.Scene scene, int offset)
        {
            const float HEIGHT = 20.0f;

            var rigid = scene.Physics.CreateRigidDynamic();
            var shape = rigid.CreateShape(new PhysX.SphereGeometry(1.0f), material);

            rigid.GlobalPose = PhysX.Math.Matrix.Translation(128f, 128f, HEIGHT + offset);
            rigid.AngularDamping = 0.2f;
            rigid.LinearDamping = 0.2f;

            return rigid;
        }

        static Random rand = new Random();

        private static PhysX.HeightFieldSample[] CreateSampleGrid(int rows, int columns)
        {
            var samples = new PhysX.HeightFieldSample[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var sample = new PhysX.HeightFieldSample()
                    {
                        
                        Height = 0//(short)rand.Next(10)
                        
                    };

                    samples[r * columns + c] = sample;
                }
            }

            return samples;
        }

        public PhysX.RigidStatic CreateTriangle(PhysX.Scene scene)
        {
            List<PhysX.Math.Vector3> Vertices = new List<PhysX.Math.Vector3>();
            List<int> Indices = new List<int>();


            PhysX.TriangleMeshDesc TriangleMeshDesc = new PhysX.TriangleMeshDesc()
            {
                Triangles = new int[12] { 0, 1, 2,   0, 3, 1,  3, 4, 1,  3, 5, 4 },
                Points = new PhysX.Math.Vector3[6] { 
                    new PhysX.Math.Vector3 { X = 0, Y = 0, Z = 0 },
                    new PhysX.Math.Vector3 { X = 1, Y = 1, Z = 0 },
                    new PhysX.Math.Vector3 { X = 0, Y = 1, Z = 0 },  

                    new PhysX.Math.Vector3 { X = 1, Y = 0, Z = 0 },
                    new PhysX.Math.Vector3 { X = 2, Y = 1, Z = 0 },
                    new PhysX.Math.Vector3 { X = 2, Y = 0, Z = 0 },
                }
            };


            MemoryStream ms = new MemoryStream();
            PhysX.Cooking cook = scene.Physics.CreateCooking();
            cook.CookTriangleMesh(TriangleMeshDesc, ms);
            cook.Dispose();

            ms.Position = 0;

            PhysX.TriangleMesh triangleMesh = scene.Physics.CreateTriangleMesh(ms);
            PhysX.TriangleMeshGeometry triangleMeshShapeDesc = new PhysX.TriangleMeshGeometry(triangleMesh);

            //PhysX.Math.Matrix.RotationYawPitchRoll(0f, (float)Math.PI / 2, 0f) * PhysX.Math.Matrix.Translation(0f, 0f, 0f)
            var hfActor = scene.Physics.CreateRigidStatic();
            hfActor.CreateShape(triangleMeshShapeDesc, scene.Physics.CreateMaterial(0.75f, 0.75f, 0.1f));

            return hfActor;
        }

        private PhysX.RigidStatic CreateGround(PhysX.Scene scene)
        {
            var hfGeom = this.CreateHfGeom(scene);

            return this.CreateGround(scene, hfGeom);
            //return CreateTriangle(scene);
        }

        private PhysX.HeightFieldGeometry CreateHfGeom(PhysX.Scene scene)
        {
            const int rows = 256, columns = 256;
            var samples = CreateSampleGrid(rows, columns);

            var heightFieldDesc = new PhysX.HeightFieldDesc()
            {
                NumberOfRows = rows,
                NumberOfColumns = columns,
                Samples = samples
            };

            PhysX.HeightField heightField = scene.Physics.CreateHeightField(heightFieldDesc);

            PhysX.HeightFieldGeometry hfGeom = new PhysX.HeightFieldGeometry(heightField, 0, 1.0f, 1.0f, 1.0f);

            return hfGeom;
        }

        private PhysX.RigidStatic CreateGround(PhysX.Scene scene, PhysX.HeightFieldGeometry hfGeom)
        {
            var hfActor = scene.Physics.CreateRigidStatic();
            hfActor.CreateShape(hfGeom, material);

            hfActor.GlobalPose = PhysX.Math.Matrix.RotationYawPitchRoll(0f, (float)Math.PI / 2, 0f) * PhysX.Math.Matrix.Translation(0f, 256 - 1, 0f);

            return hfActor;
            //return CreateTriangle(scene);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PhysX.Physics phys = new PhysX.Physics();

            PhysX.SceneDesc desc = new PhysX.SceneDesc();
            desc.Gravity = new PhysX.Math.Vector3(0f, 0f, -9.8f);

            PhysX.Scene scene = phys.CreateScene(desc);
            material = scene.Physics.CreateMaterial(0.5f, 0.5f, 0.1f);

            var conn = phys.ConnectToRemoteDebugger("localhost", null, null, null, PhysX.RemoteDebuggerConnectionFlags.Debug);

            /*scene.SetVisualizationParameter(PhysX.VisualizationParameter.Scale, 1.0f);
            scene.SetVisualizationParameter(PhysX.VisualizationParameter.CollisionShapes, true);
            scene.SetVisualizationParameter(PhysX.VisualizationParameter.JointLocalFrames, true);
            scene.SetVisualizationParameter(PhysX.VisualizationParameter.JointLimits, true);
            scene.SetVisualizationParameter(PhysX.VisualizationParameter.ParticleSystemPosition, true);
            scene.SetVisualizationParameter(PhysX.VisualizationParameter.ActorAxes, true);*/

            var hfGeom = this.CreateHfGeom(scene);

            while (true)
            {
                PhysX.RigidDynamic sphere = CreateSphere(scene, 0);
                sphere.Dispose();
                PhysX.RigidStatic ground = CreateGround(scene, hfGeom);
                ground.Dispose();
                GC.Collect();
            }

            scene.AddActor(CreateGround(scene));

            /*PhysX.RigidDynamic box1 = null;
            for (int i = 0; i < 10; i += 2)
            {
                PhysX.RigidDynamic mahBox = CreateBox(scene, i);
                scene.AddActor(mahBox);

                if (i == 0)
                {
                    box1 = mahBox;
                    ((PhysX.Actor)box1).Flags = PhysX.ActorFlag.DisableGravity;
                }
            }*/

            
            for (int i = 0; i < 10; i += 2)
            {
                PhysX.RigidDynamic mahBox = CreateSphere(scene, i);
                scene.AddActor(mahBox);
            }

            Stopwatch sw = new Stopwatch();
            while (true)
            {
                sw.Start();
                scene.Simulate(0.025f);
                scene.FetchResults(true);

                sw.Stop();

                int sleep = 25 - (int)sw.ElapsedMilliseconds;
                if (sleep < 0) sleep = 0;
                System.Threading.Thread.Sleep(sleep);
                sw.Reset();
                //label1.Text = DecomposeToPosition(mahBox.GlobalPose).ToString();
                this.Update();
            }
        }

        public static PhysX.Math.Vector3 DecomposeToPosition(PhysX.Math.Matrix matrix)
        {
            return new PhysX.Math.Vector3(matrix.M41, matrix.M42, matrix.M43);
        }
    }
}
