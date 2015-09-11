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
using System.Reflection;
using System.Text;
using OpenSim.Region.Physics.Manager;

namespace InWorldz.PhysxPhysics
{
    public class PhysicsPlugin : IPhysicsPlugin
    {
        private Dictionary<string, PhysicsScene> _scenes = new Dictionary<string,PhysicsScene>();

        #region IPhysicsPlugin Members

        public bool Init()
        {
            Preload();
            return true;
        }

        public PhysicsScene GetScene(string sceneIdentifier)
        {
            //no lock needed, only called during an init
            PhysicsScene scene;
            if (_scenes.TryGetValue(sceneIdentifier, out scene))
            {
                return scene;
            }
            else
            {
                scene = new PhysxScene();
                _scenes.Add(sceneIdentifier, scene);
                
                return scene;
            }
        }

        private static void PreloadMethods(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly |
                                BindingFlags.NonPublic |
                                BindingFlags.Public | BindingFlags.Instance |
                                BindingFlags.Static))
            {
                if (method.IsAbstract)
                    continue;
                if (method.ContainsGenericParameters || method.IsGenericMethod || method.IsGenericMethodDefinition)
                    continue;
                if ((method.Attributes & MethodAttributes.PinvokeImpl) > 0)
                    continue;

                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
                catch
                {
                }
            }
        }

        private static void Preload()
        {
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Material)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Meshing.HacdConvexHull)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(PhysicsProperties)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(InWorldz.PhysxPhysics.Vehicle.DynamicsSimulationData)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(InWorldz.PhysxPhysics.Vehicle.VehicleProperties)].CompileInPlace();

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                PreloadMethods(type);
            }
        }

        public string GetName()
        {
            return "InWorldz.PhysxPhysics";
        }

        public void Dispose()
        {
            foreach (PhysxScene scene in _scenes.Values)
            {

            }
        }

        #endregion
    }
}
