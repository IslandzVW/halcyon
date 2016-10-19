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

namespace OpenSim.Region.Framework.Scenes
{
    [Flags]
    public enum ScriptEvents
    {
        None =                  0,
        attach =                1,
        state_exit =            1 << 1,
        timer =                 1 << 2,
        touch =                 1 << 3,
        collision =             1 << 4,
        collision_end =         1 << 5,
        collision_start =       1 << 6,
        control =               1 << 7,
        dataserver =            1 << 8,
        email =                 1 << 9,
        http_response =         1 << 10,
        land_collision =        1 << 11,
        land_collision_end =    1 << 12,
        land_collision_start =  1 << 13,
        at_target =             1 << 14,
        listen =                1 << 15,
        money =                 1 << 16,
        moving_end =            1 << 17,
        moving_start =          1 << 18,
        not_at_rot_target =     1 << 19,
        not_at_target =         1 << 20,
        touch_start =           1 << 21,
        object_rez =            1 << 22,
        remote_data =           1 << 23,
        changed =               1 << 24,
        run_time_permissions =  1 << 28,
        touch_end =             1 << 29,
        state_entry =           1 << 30,
        at_rot_target =         1 << 31,
        bot_update =            1 << 32,
        transaction_result =    1 << 33,
    }
}
