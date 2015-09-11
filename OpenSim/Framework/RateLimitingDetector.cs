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

namespace OpenSim.Framework
{
    /// <summary>
    /// Provides a limit to the number of successive calls that can be made
    /// to a given function or operation without a configurable cool down period
    /// between bursts
    /// </summary>
    public class RateLimitingDetector
    {
        private object _stateLock = new object();
        private int _accumulator = 0;
        private DateTime _lastAccess;

        private int _limit;
        private TimeSpan _coolDown;

        public RateLimitingDetector(int limit, TimeSpan coolDown)
        {
            _limit = limit;
            _coolDown = coolDown;
            _lastAccess = DateTime.Now;
        }

        /// <summary>
        /// Tries to increment the accumulator. If the accumulator has been incremented
        /// beyond the limit and the cooldown period has not yet passed, returns false.
        /// Otherwise returns true
        /// </summary>
        /// <param name="transitioned">Whether or not this pass has just passed the limit</param>
        /// <returns></returns>
        public bool TryPass(out bool transitioned)
        {
            lock (_stateLock)
            {
                if (DateTime.Now - _lastAccess >= _coolDown)
                {
                    _accumulator = 0;
                    _lastAccess = DateTime.Now;
                    transitioned = false;
                    return true;
                }

                _lastAccess = DateTime.Now;
                ++_accumulator;

                if (_accumulator > _limit)
                {
                    if (_accumulator - 1 == _limit)
                    {
                        transitioned = true;
                    }
                    else
                    {
                        transitioned = false;
                    }

                    return false;
                }
            }

            transitioned = false;
            return true;
        }

        /// <summary>
        /// Tries to increment the accumulator. If the accumulator has been incremented
        /// beyond the limit and the cooldown period has not yet passed, returns false.
        /// Otherwise returns true
        /// </summary>
        /// <param name="accumlant">The amount to accumulate by</param>
        /// <param name="transitioned">Whether or not this pass has just passed the limit</param>
        /// <returns></returns>
        public bool TryPass(int accumlant, out bool transitioned)
        {
            lock (_stateLock)
            {
                if (DateTime.Now - _lastAccess >= _coolDown)
                {
                    _accumulator = 0;
                    _lastAccess = DateTime.Now;
                    transitioned = false;
                    return true;
                }

                _lastAccess = DateTime.Now;
                _accumulator += accumlant;

                if (_accumulator > _limit)
                {
                    if (_accumulator - 1 == _limit)
                    {
                        transitioned = true;
                    }
                    else
                    {
                        transitioned = false;
                    }

                    return false;
                }
            }

            transitioned = false;
            return true;
        }
    }
}
