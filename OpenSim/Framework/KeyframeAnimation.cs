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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenSim.Framework
{
    public class KeyframeAnimation
    {
        public enum Modes
        {
            Forward = 0,
            Loop = 1,
            PingPong = 2,
            Reverse = 3,
        }

        public enum Commands
        {
            Play = 0,
            Stop = 1,
            Pause = 2,
        }

        public enum Data
        {
            Rotation = 1,
            Translation = 2,
            Both = 3
        }

        public KeyframeAnimation()
        {
            CurrentAnimationPosition = 0;
            PingPongForwardMotion = true;
            CurrentMode = Modes.Forward;
            TimeLastTick = 0;
            TimeElapsed = 0;
            TimeList = new TimeSpan[0];
            InitialPosition = Vector3.Zero;
            PositionList = new Vector3[0];
            InitialRotation = Quaternion.Identity;
            RotationList = new Quaternion[0];
        }

        public int CurrentAnimationPosition { get; set; }
        public bool PingPongForwardMotion { get; set; }
        public Modes CurrentMode { get; set; }
        public Commands CurrentCommand { get; set; }
        public int TimeLastTick { get; set; }
        public int TimeElapsed { get; set; }
        public TimeSpan[] TimeList { get; set; }
        public Vector3 InitialPosition { get; set; }
        public Vector3[] PositionList { get; set; }
        public Quaternion InitialRotation { get; set; }
        public Quaternion[] RotationList { get; set; }
    }
}
