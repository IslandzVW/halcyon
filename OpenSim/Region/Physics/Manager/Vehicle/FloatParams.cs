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

namespace OpenSim.Region.Physics.Manager.Vehicle
{

    /// <summary>
    /// Enum containing all the lsl vehicle float params (http://wiki.secondlife.com/wiki/LlSetVehicleFloatParam)
    /// 
    /// Please note if you update this enum you should also update the validator below to include the new range(s)
    /// </summary>
    public enum FloatParams
    {
        None = 0,
        VehicleHoverHeight                  = 24,
        VehicleHoverEfficiency              = 25,
        VehicleHoverTimescale               = 26,
        VehicleBuoyancy                     = 27,
        VehicleLinearDeflectionEfficiency   = 28,
        VehicleLinearDeflectionTimescale    = 29,
        VehicleLinearMotorTimescale_        = 30,   // Now a vector param
        VehicleLinearMotorDecayTimescale_   = 31,   // Now a vector param
        VehicleAngularDeflectionEfficiency  = 32,
        VehicleAngularDeflectionTimescale   = 33,
        VehicleAngularMotorTimescale_       = 34,   // Now a vector param
        VehicleAngularMotorDecayTimescale_  = 35,   // Now a vector param
        VehicleVerticalAttractionEfficiency = 36,
        VehicleVerticalAttractionTimescale  = 37,
        VehicleBankingEfficiency            = 38,
        VehicleBankingMix                   = 39,
        VehicleBankingTimescale             = 40,

        VehicleMouselookAzimuth             = 11001,
        VehicleMouselookAltitude            = 11002,
        VehicleBankingAzimuth               = 11003,
        VehicleDisableMotorsAbove           = 11004,
        VehicleDisableMotorsAfter           = 11005,
        VehicleInvertedBankingModifier      = 11006,
    }

    /// <summary>
    /// This is kind of verbose for checking the validity of the enum, but it will be fast at runtime
    /// unlike Enum.IsDefined() ( see: http://stackoverflow.com/questions/13615/validate-enum-values )
    /// </summary>
    public class FloatParamsValidator
    {
        public const int FLOAT_PARAMS_MIN = (int)FloatParams.VehicleHoverHeight;
        public const int FLOAT_PARAMS_MAX = (int)FloatParams.VehicleBankingTimescale;

        public static bool IsValid(int value)
        {
            // These specific values have been promoted to vectors.
            if (value == (int)FloatParams.VehicleLinearMotorTimescale_ || value == (int)FloatParams.VehicleLinearMotorDecayTimescale_ ||
                value == (int)FloatParams.VehicleAngularMotorTimescale_ || value == (int)FloatParams.VehicleAngularMotorDecayTimescale_) return false;

            if (value >= FLOAT_PARAMS_MIN && value <= FLOAT_PARAMS_MAX) return true;
            if (value >= (int)FloatParams.VehicleMouselookAzimuth && value <= (int)FloatParams.VehicleInvertedBankingModifier) return true;

            return false;
        }
    }
}
