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

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes.Serialization;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace InWorldz.Region.Data.Thoosa
{
    public class ThoosaModule : ISharedRegionModule
    {
        private static readonly log4net.ILog m_log
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Engines.SerializationEngine _engine;

        public void PostInitialize()
        {
        }

        public string Name
        {
            get { return "InWorldz.Region.Data.Thoosa"; }
        }

        public System.Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialize(Nini.Config.IConfigSource source)
        {
            var cfg = source.Configs["InWorldz.Thoosa"];
            if (cfg == null || cfg.GetBoolean("enable_serialization_engine", true))
            {
                _engine = new Engines.SerializationEngine();
                ProviderRegistry.Instance.RegisterInterface<ISerializationEngine>(_engine);

                Preload();

                m_log.InfoFormat("[InWorldz.Thoosa]: Serialization engine enabled");
            }
            else
            {
                m_log.InfoFormat("[InWorldz.Thoosa]: Serialization engine disabled");
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
            //preload
            new Serialization.SceneObjectGroupSnapshot();
            new Serialization.SceneObjectPartSnapshot();
            new Serialization.PrimShapeSnapshot();

            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.CoalescedObjectSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.SceneObjectGroupSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.SceneObjectPartSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.PrimShapeSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.MediaEntrySnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.TaskInventoryItemSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.TaskInventorySnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.KeyframeAnimationSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Serialization.ItemPermissionBlockSnapshot)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(OpenMetaverse.Vector3)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(OpenMetaverse.Quaternion)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Dictionary<System.Guid, byte[]>)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(OpenMetaverse.PrimFlags)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(OpenSim.Framework.ProfileShape)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(OpenSim.Framework.HollowShape)].CompileInPlace();
            ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(System.Tuple<Serialization.SceneObjectGroupSnapshot, Serialization.ItemPermissionBlockSnapshot>)].CompileInPlace();
            
            

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                PreloadMethods(type);
            }
        }

        public void Close()
        {
        }

        public void AddRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
        }

        public void RemoveRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
        }

        public void RegionLoaded(OpenSim.Region.Framework.Scenes.Scene scene)
        {
        }
    }
}
