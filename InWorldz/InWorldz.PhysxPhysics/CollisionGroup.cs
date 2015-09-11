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

namespace InWorldz.PhysxPhysics
{
    /// <summary>
    /// Flags that represent collision groups and the types that they collide with
    /// </summary>
    [Flags]
    internal enum CollisionGroupFlag
    {
        None =              0,
        Normal =            (1 << 0),
        PhysicalPhantom =   (1 << 1),
        Ground =            (1 << 2),
        Character =         (1 << 3),
        Trigger =           (1 << 4)
    }

    internal class CollisionGroup
    {
        static public readonly Dictionary<CollisionGroupFlag, CollisionGroupFlag> CollidesWith
            = new Dictionary<CollisionGroupFlag, CollisionGroupFlag>
        {
            {CollisionGroupFlag.Normal, CollisionGroupFlag.Normal | CollisionGroupFlag.Ground | CollisionGroupFlag.Character},
            {CollisionGroupFlag.PhysicalPhantom, CollisionGroupFlag.Ground},
            {CollisionGroupFlag.Character, CollisionGroupFlag.Normal | CollisionGroupFlag.Ground | CollisionGroupFlag.Character },
            {CollisionGroupFlag.Ground, CollisionGroupFlag.Normal | CollisionGroupFlag.Character | CollisionGroupFlag.PhysicalPhantom },
            {CollisionGroupFlag.Trigger, CollisionGroupFlag.None}
        };

        public static void SetCollisionGroup(CollisionGroupFlag group, IEnumerable<PhysX.Shape> shapes)
        {
            foreach (var shape in shapes)
            {
                SetCollisionGroup(group, shape);
            }
        }

        public static void SetCollisionGroup(CollisionGroupFlag group, PhysX.Shape shape)
        {
            PhysX.FilterData newFilterData = CollisionGroup.GetFilterData(shape.SimulationFilterData.Word0,
                shape.SimulationFilterData.Word1, group);

            shape.SimulationFilterData = newFilterData;
            shape.QueryFilterData = newFilterData;
        }

        public static PhysX.FilterData GetFilterData(uint word0, uint word1, CollisionGroupFlag group)
        {
            return new PhysX.FilterData(word0,
                    word1,
                    (uint)group,
                    (uint)CollisionGroup.CollidesWith[group]);
        }
    }
}
