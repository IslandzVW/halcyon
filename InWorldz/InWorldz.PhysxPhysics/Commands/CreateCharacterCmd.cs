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
using System.Threading;

namespace InWorldz.PhysxPhysics.Commands
{
    internal class CreateCharacterCmd : CharacterCommand, IDisposable
    {
        private float _height;
        private float _radius;
        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Quaternion _rotation;
        private bool _flying;
        private OpenMetaverse.Vector3 _initialVelocity;

        public readonly ManualResetEventSlim FinshedEvent = new ManualResetEventSlim(false);

        public PhysxCharacter FinalActor;

        public CreateCharacterCmd(float height, float radius, OpenMetaverse.Vector3 pos,
            OpenMetaverse.Quaternion rot, bool flying, OpenMetaverse.Vector3 initialVelocity)
        {
            _height = height;
            _radius = radius;
            _position = pos;
            _rotation = rot;
            _flying = flying;
            _initialVelocity = initialVelocity;
        }

        public override void Execute(PhysxScene scene)
        {
            FinalActor = new PhysxCharacter(scene, _height, _radius, _position, _rotation, _flying, _initialVelocity);
            scene.AddCharacterSync(FinalActor);
            FinshedEvent.Set();
        }

        public void Dispose()
        {
            FinshedEvent.Dispose();
        }
    }
}
