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
using OpenSim.Region.Physics.Manager.Vehicle;
using OpenSim.Framework;
using OpenMetaverse;
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Vehicle
{
    public static class Mathz
    {
        // -1 if <0, +1 if >=0
        public static int PosNeg(float f)
        {
            int sign = Math.Sign(f);
            if (sign == 0) return 1;
            return sign;
        }
    }

    /// <summary>   
    /// The internal limits to various vehicle parameters
    /// </summary>
    internal class VehicleLimits
    {
        // The interaction between these limits are nonintuitive. Test all cases before settling on any changes.
        //
        public const float MinPhysicsTimestep     = 0.0156f;
        public const float MinPhysicsForce        = 0.001f;                 // Minimum force not treated as zero by PhysX.
        public const int   NumTimeStepsPerRayCast = 8;
        public const int   NumTimeStepsPerWindChk = 8;

        // Threshold value below which activity is considered ended
        // or effectively zero. Most of these values are tuned to be above the squirm point; that is, setting them much lower often
        // causes the object to squirm continuously. This is mostly caused by interactions with the internal limits of PhysX, with floating
        // point inaccuracies and with quantization errors introduced by a discrete time simulation.
        public const float ThresholdLinearMotorDeltaV   = 0.005f;
        public const float ThresholdAngularMotorDeltaV  = Utils.PI/512;
        public const float ThresholdLinearFrictionDeltaV  = 0.002f;         // Speed change below which friction simulation ends.
        public const float ThresholdAngularFrictionDeltaV = 0.004f;         // Speed change below which friction simulation ends.
        public const float ThresholdDeflectionSpeed     = 0.2f;             // Speed at which deflection is disabled, CAUTION: much lower and the object squirms.
        public const float ThresholdDeflectionAngle     = Utils.PI/64.0f;   // Magic number for angular deflection dead zone.
        public const float ThresholdStictionFactor      = 0.002f;
        public const float ThresholdAngularMotorEngaged = 0.9f;             // Time in seconds that the angular motor is considered recently engaged.
        public const float ThresholdLinearMotorEngaged  = 1.0f;             // Time in seconds that the linear motor is considered recently engaged.
        public const float ThresholdLinearMotorUnstuck  = 0.025f;           // Velocity the motor is considered not stalled.
        public const float ThresholdAttractorAngle      = Utils.PI/256.0f;  // Magic number for angular attraction dead zone.
        public const float ThresholdOverturnAngle       = Utils.PI * 0.60f; // Max angle at which vehicle is considered overturned.
        public const float ThresholdHoverDragHeight     = 0.3f;             // Max hover height over ground when a boat is considered dragging.
        public const float ThresholdDelayFubar          = 0.06f;            // Time in seconds to delay motor decay (fixes bad legacy scripts).
        public const float ThresholdBankAngle           = Utils.PI/128.0f;  // Angle below which no bank to turn conversion occurs.
        public const float ThresholdMouselookAngle      = Utils.PI/32.0f;   // Angle below which mouselook steer/bank actions do not occur.
        public const float ThresholdInverseCrossover    = -2.010203f;       // The sentinel value that indicates a velocity reduction crosses over to an inverse rise.

        // Maximum values for various forces and time scales.
        // Some of these numbers are set for SL compatibility (SL:)
        public const float MaxLegacyLinearVelocity  = 30.0f;                // SL: Used for bank-to-turn computations.
        public const float MaxLegacyAngularVelocity = Utils.PI;             // SL: Used for bank-to-turn computations;
        public const float MaxLinearVelocity    = 200.0f;                   // The real speed limit
        public const float MaxLinearOffset      = 100.0f;
        public const float MaxAngularVelocity   = Utils.PI*4.0f;            // SL: Note that this has to stay at 4PI because of badly written freebie scripts
        public const float MaxDecayTimescale    = 120.0f;                   // SL:   
        public const float MaxAttractTimescale  = 500.0f;                   // SL:
        public const float MaxHoverTimescale    = 300.0f;                   // SL:
        public const float MaxTimescale         = 1000.0f;                  // SL:
        public const float MaxBankingSpeed      = 10.0f;                    // SL: Magic number for banking speed effect.
        public const float MaxGroundPenetration = -0.5f;
        public const float MinRegionHeight      = Constants.REGION_MINIMUM_Z; // -128.0f;
        public const float MaxRegionHeight      = Constants.REGION_MAXIMUM_Z; //10000.0f;
        public const float MaxAttractDormancy   = 1.5f;                     // Time in seconds in which staying in the sweet spot triggers object sleep.

        // Enablement switches, useful for debugging specific features (all should be 'true' in production).
        public static Boolean DoAngularDeflection   = true;
        public static Boolean DoLinearDeflection    = true;
        public static Boolean DoMotors              = true;
        public static Boolean DoAngularFriction     = true;
        public static Boolean DoLinearFriction      = true;
        public static Boolean DoVerticalAttractor   = true;
        public static Boolean DoBanking             = true;
        public static Boolean DoSpikeDetection      = true;

        // Debugging switches - they enable console messages to help debug specific features
        // (all should be 'false' in production).
        public static Boolean DebugPrintParams      = false;
        public static Boolean DebugTimestep         = false;
        public static Boolean DebugSpikeDetection   = false;
        public static Boolean DebugVehicleChange    = false;
        public static Boolean DebugAngular          = false;
        public static Boolean DebugBanking          = false;
        public static Boolean DebugBlendedZ         = false;
        public static Boolean DebugLinear           = false;
        public static Boolean DebugAttractor        = false;
        public static Boolean DebugRayCast          = false;
        public static Boolean DebugWind             = false;
        public static Boolean DebugAngularFriction  = false;
        public static Boolean DebugLinearFriction   = false;
        public static Boolean DebugAngularMotor     = false;
        public static Boolean DebugLinearMotor      = false;
        public static Boolean DebugDeflection       = false;
        public static Boolean DebugRegionChange     = false;
    };

    /// <summary>
    /// The entrypoint to the vehicle dynamics simulation
    /// </summary>
    internal class VehicleDynamics
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Our actual physics actor
        /// </summary>
        private PhysxPrim _actor;

        /// <summary>
        /// We shadow the vehicle properties here so that we don't have to take a lock every
        /// time we need access to the props
        /// </summary>
        private VehicleProperties _props;

        /// <summary>
        /// The physx physics object
        /// </summary>
        private PhysX.Physics _physics;

        /// <summary>
        /// The physx scene
        /// </summary>
        private PhysxScene _scene;
    
        /// <summary>
        /// This is the region's ID used to determine if the instance
        /// is running in a different region.
        /// </summary>
        private UUID _regionId;

        /// <summary>
        /// The linear and angular motors of the vehicle
        /// </summary>
        private VehicleMotor _motor;

        /// <summary>
        /// The running statistics for world angular velocities
        /// </summary>
        private RunningStat _angularstatsX;
        private RunningStat _angularstatsY;
        private RunningStat _angularstatsZ;

        /// <summary>
        /// The running statistics for world linear velocities
        /// </summary>
        private RunningStat _linearstatsX;
        private RunningStat _linearstatsY;
        private RunningStat _linearstatsZ;


        /// <summary>
        /// These are common computed values used for the current simulation step.
        /// They are ephemeral and need not be stored in the persistent vehicle properties.
        /// </summary>
        private OpenMetaverse.Quaternion vframe;
        private OpenMetaverse.Quaternion rotation;
        private OpenMetaverse.Vector3 worldAngularVel;
        private OpenMetaverse.Vector3 worldLinearVel;
        private OpenMetaverse.Vector3 localAngularVel;
        private OpenMetaverse.Vector3 localLinearVel;

        public VehicleDynamics(PhysxPrim physxPrim, VehicleProperties shadowProps, PhysX.Physics physics, PhysxScene scene)
        {
            _angularstatsX = new RunningStat();
            _angularstatsY = new RunningStat();
            _angularstatsZ = new RunningStat();
            _linearstatsX  = new RunningStat();
            _linearstatsY  = new RunningStat();
            _linearstatsZ  = new RunningStat();

            _actor = physxPrim;
            _props = new VehicleProperties();
            _physics = physics;
            _scene = scene;
            _motor = new VehicleMotor(ref _actor, ref _props, ref _physics, ref _scene);

            // Preset the defaults and new common keys. This ensures scripts with older versions of saved state
            // do not crash the simulator since the new keys do not exist in the old state.
            _props.Type = VehicleType.None;
            SetVehicleDefaults(_props);

            // Merge the new properties.
            _props.Merge(shadowProps);
            SetVehicleDefaultActions();

            if (VehicleLimits.DebugVehicleChange) m_log.InfoFormat("[VehicleDynamics] constructed type={0} name={1} at {2}", _props.Type, _actor.SOPName, _actor.Position);
            //DisplayParameters();
        }

        #region External Vehicle Parameter Setters
        /// <summary>
        /// Clamps a float param
        /// 
        /// WARNING: This code is almost always executed from outside the physics thread.
        /// </summary>
        /// <param name="param"></param>
        /// <param name="newValue"></param>
        /// <returns>clamped_newvalue</returns>
        internal static float ClampFloatParam(FloatParams param, float newValue)
        {
            if (float.IsNaN(newValue))
                newValue = 0;

            switch (param)
            {
                case FloatParams.VehicleAngularDeflectionEfficiency:
                    return Utils.Clamp(newValue, 0.0f, 1.0f);

                case FloatParams.VehicleAngularDeflectionTimescale:
                    return Utils.Clamp(newValue, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);

                case FloatParams.VehicleBankingEfficiency:
                    return Utils.Clamp(newValue, -1.0f, 1.0f);

                case FloatParams.VehicleInvertedBankingModifier:
                    return Utils.Clamp(newValue, -10.0f, 10.0f);

                case FloatParams.VehicleBankingMix:
                    return Utils.Clamp(newValue, 0.0f, 1.0f);

                case FloatParams.VehicleBankingTimescale:
                    return Utils.Clamp(newValue, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);

                case FloatParams.VehicleMouselookAltitude:
                    return Utils.Clamp(newValue, VehicleLimits.ThresholdMouselookAngle, (float)Math.PI);

                case FloatParams.VehicleMouselookAzimuth:
                    return Utils.Clamp(newValue, VehicleLimits.ThresholdMouselookAngle, (float)Math.PI);

                case FloatParams.VehicleBankingAzimuth:
                    return Utils.Clamp(newValue, VehicleLimits.ThresholdBankAngle, (float)Math.PI);

                case FloatParams.VehicleBuoyancy:
                    return Utils.Clamp(newValue, -2.0f, 30.0f); // Wider range because of gravity multiplier

                case FloatParams.VehicleHoverEfficiency:
                    return Utils.Clamp(newValue, 0.0f, 1.0f);

                case FloatParams.VehicleHoverHeight:
                    return Utils.Clamp(newValue, VehicleLimits.MinRegionHeight, VehicleLimits.MaxRegionHeight);

                case FloatParams.VehicleHoverTimescale:
                    return Utils.Clamp(newValue, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxHoverTimescale);

                case FloatParams.VehicleLinearDeflectionEfficiency:
                    return Utils.Clamp(newValue, 0.0f, 1.0f);

                case FloatParams.VehicleLinearDeflectionTimescale:
                    return Utils.Clamp(newValue, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);

                case FloatParams.VehicleVerticalAttractionTimescale:
                    return Utils.Clamp(newValue, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxAttractTimescale);

                default: //no clamping?
                    return newValue;
            }
        }

        /// <summary>
        /// Called when a parameter is changed. Note that these values should all be pre-clamped
        /// </summary>
        /// <param name="param"></param>
        /// <param name="newValue"></param>
        internal void FloatParamChanged(FloatParams param, float newValue)
        {
            // Though the external float param setter permits setting vector params, the LSL wrapper
            // detects that and routes those cases to the internal vector param setter.
            // Reminder: the motor float timescale params are now vector params.

            //m_log.DebugFormat("[FloatParamChanged] {0} <- {1}", param, newValue);

            switch (param)
            {
                case FloatParams.VehicleBuoyancy:
                    
                    // Constrain the buoyancy to the gravity multiplier extent. This maintains
                    // compatibility with GM=1 but permits similar behaviors with other values.
                    float gm = Math.Abs(_actor.Properties.Material.GravityMultiplier);
                    if (gm == 0) gm = 1.0f;
                    newValue = Utils.Clamp(newValue, -gm, gm);
                    _props.ParamsFloat[param] = newValue;
                    SetVehicleBuoyancy();
                    break;

                case FloatParams.VehicleHoverEfficiency:
                    _props.ParamsFloat[param] = newValue;
                    SetVehicleHover();
                    break;

                case FloatParams.VehicleHoverHeight:
                    _props.ParamsFloat[param] = newValue;
                    SetVehicleHover();
                    break;

                case FloatParams.VehicleHoverTimescale:
                    _props.ParamsFloat[param] = newValue;
                    SetVehicleHover();
                    break;
            }

            // Update the active shadow copy.
            _props.ParamsFloat[param] = newValue;
            _actor.WakeUp();
        }

        /// <summary>
        /// Clamps a rotation param
        /// 
        /// WARNING: This code is almost always executed from outside the physics thread.
        /// </summary>
        /// <param name="param"></param>
        /// <param name="newValue"></param>
        /// <returns>clamped_newvalue</returns>
        internal static OpenMetaverse.Quaternion ClampRotationParam(RotationParams param, OpenMetaverse.Quaternion newValue)
        {
            if (float.IsNaN(newValue.X))    newValue.X = 0;
            if (float.IsNaN(newValue.Y))    newValue.Y = 0;
            if (float.IsNaN(newValue.Z))    newValue.Z = 0;
            if (float.IsNaN(newValue.W))    newValue.W = 0;

            switch (param)
            {
                case RotationParams.VehicleReferenceFrame:
                    newValue = OpenMetaverse.Quaternion.Normalize(newValue);
                    break;
            }

            return newValue;
        }

        internal void RotationParamChanged(RotationParams param, OpenMetaverse.Quaternion newValue)
        {
            // Update the active shadow copy.
            _props.ParamsRot[param] = newValue;
            _actor.WakeUp();
        }

        /// <summary>
        /// Clamps a vector param
        /// 
        /// WARNING: This code is almost always executed from outside the physics thread.
        /// </summary>
        /// <param name="param"></param>
        /// <param name="newValue"></param>
        /// <returns>clamped_newvalue</returns>
        internal static OpenMetaverse.Vector3 ClampVectorParam(VectorParams param, OpenMetaverse.Vector3 newValue)
        {
            if (float.IsNaN(newValue.X))    newValue.X = 0;
            if (float.IsNaN(newValue.Y))    newValue.Y = 0;
            if (float.IsNaN(newValue.Z))    newValue.Z = 0;

            switch (param)
            {
                case VectorParams.VehicleAngularMotorDirection:
                    newValue.X = Utils.Clamp(newValue.X, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                    newValue.Y = Utils.Clamp(newValue.Y, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                    newValue.Z = Utils.Clamp(newValue.Z, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                    break;

                case VectorParams.VehicleLinearMotorDirection:
                    newValue.X = Utils.Clamp(newValue.X, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);
                    newValue.Y = Utils.Clamp(newValue.Y, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);
                    newValue.Z = Utils.Clamp(newValue.Z, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);
                    break;

                case VectorParams.VehicleAngularMotorDecayTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleAngularMotorTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleLinearMotorDecayTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleLinearMotorTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleLinearMotorOffset:
                    newValue.X = Utils.Clamp(newValue.X, -VehicleLimits.MaxLinearOffset, VehicleLimits.MaxLinearOffset);
                    newValue.Y = Utils.Clamp(newValue.Y, -VehicleLimits.MaxLinearOffset, VehicleLimits.MaxLinearOffset);
                    newValue.Z = Utils.Clamp(newValue.Z, -VehicleLimits.MaxLinearOffset, VehicleLimits.MaxLinearOffset);
                    break;

                case VectorParams.VehicleAngularFrictionTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleLinearFrictionTimescale:
                    newValue.X = Utils.Clamp(newValue.X, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Y = Utils.Clamp(newValue.Y, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    newValue.Z = Utils.Clamp(newValue.Z, VehicleLimits.MinPhysicsTimestep, VehicleLimits.MaxTimescale);
                    break;

                case VectorParams.VehicleLinearWindEfficiency:
                    newValue.X = Utils.Clamp(newValue.X, -10.0f, 10.0f);
                    newValue.Y = Utils.Clamp(newValue.Y, -10.0f, 10.0f);
                    newValue.Z = Utils.Clamp(newValue.Z, -10.0f, 10.0f);
                    break;

                case VectorParams.VehicleAngularWindEfficiency:
                    newValue.X = Utils.Clamp(newValue.X, -10.0f, 10.0f);
                    newValue.Y = Utils.Clamp(newValue.Y, -10.0f, 10.0f);
                    newValue.Z = Utils.Clamp(newValue.Z, -10.0f, 10.0f);
                    break;
            }

            return newValue;
        }

        /// <summary>
        /// Called from physics when the user has changed a vector param. Note these values should all be pre-clamped
        /// </summary>
        /// <param name="param"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        internal void VectorParamChanged(VectorParams param, OpenMetaverse.Vector3 newValue)
        {
            // Update the active shadow copy.
            _props.ParamsVec[param] = newValue;

            // Some of the parameters cause motor actions, and others need dynamic minimums
            switch (param)
            {
                case VectorParams.VehicleAngularMotorDirection:
                    // Skip angular motor action if in camera mouselook mode and mouselook steer or bank are enabled.
                    if (!(_camData.MouseLook && (_props.Flags & (VehicleFlags.MouselookBank | VehicleFlags.MouselookSteer)) != 0))
                        _motor.MoveAngular(newValue);
                    break;

                case VectorParams.VehicleLinearMotorDirection:
                    _motor.MoveLinear(newValue);
                    break;

                case VectorParams.VehicleLinearMotorOffset:
                    _motor.SetLinearOffset(newValue);
                    break;
            }

            _actor.WakeUp();
        }

        internal void VehicleTypeChanged(VehicleType newType)
        {
            // Whenever the type is changed, update all the vehicle parameters to the defaults for that type.
            _props.Type = newType;
            SetVehicleDefaults(_props);
            SetVehicleDefaultActions();
            _motor.ResetDynamics();

            // Update hover, buoyancy and camera based mouselook since the new type changes these parameters.
            SetVehicleHover();
            SetVehicleBuoyancy();
            PrepareForCameraAndWindMove();

            // Compatibility hack - This should be done as part of object rez, but
            // unfortunately, it isn't, so it is done here.
            _actor.StopRotLookAt();
            _actor.SetMoveToTarget(OpenMetaverse.Vector3.Zero, 0);

            _actor.WakeUp();
        }

        internal void VehicleFlagsChanged(VehicleFlags newFlags)
        {
            // Update the active shadow copy.
            _props.Flags = newFlags;

            //m_log.DebugFormat("[VehicleFlagsChanged] {0} in {1}", newFlags, _actor.SOPName);

            // Update hover, buoyancy and mouselook since the new flag changes these parameters.
            SetVehicleHover();
            SetVehicleBuoyancy();
            PrepareForCameraAndWindMove();

            _actor.WakeUp();
        }
        #endregion

        private OpenSim.Region.Physics.Manager.CameraData _camData = new OpenSim.Region.Physics.Manager.CameraData { Valid = false };

        //
        // Main simulation entry point, called every physics frame (nominally 64x/sec).
        // This is a pipelined simulation, several physical events happen at the same time, and
        // the results of the previous physics frame feed forward into the next frame.
        //
        internal void Simulate(float timeStep, uint frameNum)
        {
            if (_props.Type == VehicleType.None) return;

            // If the vehicle's oriented bounding box is null, request it now.
            // This could end up null ayway, but the subsequent code checks for that possibility.
            if (_actor.OBBobject == null)
            {
                _actor.OBBobject = _actor.DoRequestOBB();
            }

            // Tick smoothing. Under even light loads, the timestep bobbles between 1 and 2 physics frames,
            // sufficiently often that it noticibly affects the movement timings. This smoothing cleans up the glitches.
            // This performs a weighted average of this frame's timestep and the one in the previous frame.
            timeStep = _props.Dynamics.Timestep * 0.8f +  timeStep * 0.2f;

            //handle a race condition where a physics state save happened after the vehicle type has been
            //set, but before physics got a chance to set the default properties
            if (_props.ParamsRot.Count == 0)
            {
                m_log.InfoFormat("[VehicleSimulate] race condition");
                SetVehicleDefaults(_props);
                SetVehicleDefaultActions();
            }

            // Debug params every 10ish seconds.
            if (VehicleLimits.DebugPrintParams && (frameNum % 600) == 0)
            {
                DisplayParameters();
            }

            // Grab overrides vehicle movement.
            // If the vehicle is being grabbed skip the simulation, otherwise
            // severe squirming and tossing will happen.
            if (_actor.Properties.GrabTargetTau != 0) return;

            // Metrics: both world and local angular deltaV, linear deltaV
            vframe           = _props.ParamsRot[RotationParams.VehicleReferenceFrame];
            rotation         = _actor.RotationUnsafe * vframe; // Safe from physics thread (as in this code)

            // A PhysX velocity behavior sends strong and opposite velocities when an object collides: e.g., -55,+55,=55,...
            // Per axis, when velocities are opposing, make a two-frame average to eliminate that physX jitter.

            // Angular
            if ((worldAngularVel.X * _actor.DynActorImpl.AngularVelocity.X) >= 0)
                worldAngularVel.X = _actor.DynActorImpl.AngularVelocity.X;
            else
                worldAngularVel.X  = worldAngularVel.X * 0.5f + _actor.DynActorImpl.AngularVelocity.X * 0.5f;

            if ((worldAngularVel.Y * _actor.DynActorImpl.AngularVelocity.Y) >= 0)
                worldAngularVel.Y = _actor.DynActorImpl.AngularVelocity.Y;
            else
                worldAngularVel.Y  = worldAngularVel.Y * 0.5f + _actor.DynActorImpl.AngularVelocity.Y * 0.5f;

            if ((worldAngularVel.Z * _actor.DynActorImpl.AngularVelocity.Z) >= 0)
                worldAngularVel.Z = _actor.DynActorImpl.AngularVelocity.Z;
            else
                worldAngularVel.Z  = worldAngularVel.Z * 0.5f + _actor.DynActorImpl.AngularVelocity.Z * 0.5f;

            //Linear
            if ((worldLinearVel.X * _actor.DynActorImpl.LinearVelocity.X) >= 0)
                worldLinearVel.X = _actor.DynActorImpl.LinearVelocity.X;
            else
                worldLinearVel.X  = worldLinearVel.X * 0.5f + _actor.DynActorImpl.LinearVelocity.X * 0.5f;

            if ((worldLinearVel.Y * _actor.DynActorImpl.LinearVelocity.Y) >= 0)
                worldLinearVel.Y = _actor.DynActorImpl.LinearVelocity.Y;
            else
                worldLinearVel.Y  = worldLinearVel.Y * 0.5f + _actor.DynActorImpl.LinearVelocity.Y * 0.5f;

            if ((worldLinearVel.Z * _actor.DynActorImpl.LinearVelocity.Z) >= 0)
                worldLinearVel.Z = _actor.DynActorImpl.LinearVelocity.Z;
            else
                worldLinearVel.Z  = worldLinearVel.Z * 0.5f + _actor.DynActorImpl.LinearVelocity.Z * 0.5f;

            localAngularVel  = worldAngularVel * OpenMetaverse.Quaternion.Inverse(rotation);
            localLinearVel   = worldLinearVel * OpenMetaverse.Quaternion.Inverse(rotation);

            //m_log.DebugFormat("[Vehicle Simulate] lvel={0} lspd={1} step={2} frame={3}", localLinearVel, localLinearVel, timeStep, frameNum);

            // Region change check. If the region has changed, the stall detection logic has to be reset one
            // frame, and various vehicle actions (hover, buoyancy, etc.) have to be reestablished
            // Note that the local region field gets cleared on crossings since the object is actually getting recreated.
            //
            if (_scene.RegionID != _regionId)
            {
                if (VehicleLimits.DebugRegionChange)
                    m_log.InfoFormat("[Vehicle Simulate] changed region: new={0}", _scene.RegionID);

                _regionId = _scene.RegionID;

                // Do this to prevent a phantom stall-detection at the crossing
                // object rez or object made physical.
                _props.Dynamics.LastAccessTOD = DateTime.Now;
                SetVehicleDefaultActions();
            }

            // Reset motors if the time between calls is greater than one second. 
            // This handles cases when the vehicle has been edited or rezzed.
            // It prevents the vehicle from taking off using stale forces.
            float actualstep = _motor.CheckResetMotors(timeStep);
            _motor.TorqueInit();
            _motor.ClearLinearMotorStalled();
            _props.Dynamics.Timestep = timeStep;

            // -------------------------------------------------------------------------------------------
            // Mitigate a PhysX velocity spiking bug.
            // This has the side effect of changing the local velocities.
            //
            if (VehicleLimits.DoSpikeDetection)
            {
                MitigatePhysxSpiking(actualstep);
            }

            // Initialize the motor with the entry metrics.
            _motor.MetricsInit(timeStep, vframe, rotation, worldAngularVel, worldLinearVel, localAngularVel, localLinearVel);

            // -------------------------------------------------------------------------------------------
            // Camera based movement:
            //
            SimulateCameraBasedMotorInput(timeStep, frameNum);

            // -------------------------------------------------------------------------------------------
            // Build the wind direction data for this moment.
            //
            BuildWindData(timeStep, frameNum);          

            // -------------------------------------------------------------------------------------------
            // Ground penetration fix. A bug in PhysX permits a vehicle to punch through
            // the ground plane if sufficient force is present. When that happens, apply a very
            // strong upward force. This also fixes the case where someone edits/moves a physical vehicle below
            // ground, which can grief the region.
            //
            float gnd = _motor.GetGroundHeightBeneathVehicle();

            if (_actor.Position.Z - gnd < VehicleLimits.MaxGroundPenetration)
            {
                OpenMetaverse.Vector3 zforce = OpenMetaverse.Vector3.Zero;
                zforce.Z = 1.0f + Math.Abs(localLinearVel.Z * 3.0f);
                _actor.DynActorImpl.AddForce(PhysUtil.OmvVectorToPhysx(zforce), PhysX.ForceMode.VelocityChange, true);
            }

            // -------------------------------------------------------------------------------------------
            // Ground drag for boats - boats have differential friction.
            //
            SimulateBoatGroundDrag(timeStep);

            // -------------------------------------------------------------------------------------------
            // Angular deflection - turning toward the direction of movement.
            //
            if (VehicleLimits.DoAngularDeflection)
            {
                SimulateAngularDeflection(timeStep);
            }

            // -------------------------------------------------------------------------------------------
            // Linear deflection - changing direction of movement toward forward axis.
            //
            if (VehicleLimits.DoLinearDeflection)
            {
                SimulateLinearDeflection(timeStep);
            }

            // -------------------------------------------------------------------------------------------
            // Sled movement - Motors are usually inoperative but a sled needs force assist on downslopes.
            //
            if (_props.Type == VehicleType.Sled)
            {
                SimulateSledMovement(timeStep);
            }

            // -------------------------------------------------------------------------------------------
            // Vertical attractor - Pointing  the local Z axis to the sky in the designated timescale.
            // Banking - induce yaw rotation (about the world z-axis) proportional to angle of roll (about the local x-axis).
            //
            OpenMetaverse.Vector3 attractionForces = OpenMetaverse.Vector3.Zero;
            if (VehicleLimits.DoVerticalAttractor)
            {
                float angle;
                bool  inverted;

                SimulateVerticalAttractor(timeStep, frameNum, out attractionForces, out angle, out inverted);
                SimulateBankingToYaw(timeStep, angle, inverted);
            }

            // -------------------------------------------------------------------------------------------
            // Wind forces
            //
            if ((_props.Flags & VehicleFlags.ReactToWind) != 0)
            {
                SimulateWindForces(timeStep);
            }

            // -------------------------------------------------------------------------------------------
            // Motors
            //
            if (VehicleLimits.DoMotors)
            {
                _motor.Simulate(timeStep, frameNum, attractionForces);
            }

            // -------------------------------------------------------------------------------------------
            // Angular Friction - apply inverse rotational velocity along each axis based on the friction timescales.
            //
            if (VehicleLimits.DoAngularFriction)
            {
                SimulateAngularFriction(timeStep);
            }

            // -------------------------------------------------------------------------------------------
            // Linear Friction - apply inverse velocity along each axis based on the friction timescales.
            //
            if (VehicleLimits.DoLinearFriction)
            {
                SimulateLinearFriction(timeStep);
            }

            // Apply the accumulated torque
            _motor.TorqueFini();

            // Save parameters to be fed forward and used in the next frame.
            _props.Dynamics.LastPosition         = _actor.Position;
            _props.Dynamics.Timestep             = timeStep;
            _props.Dynamics.LocalLinearVelocity  = localLinearVel;
            _props.Dynamics.LocalAngularVelocity = localAngularVel;
        }

        #region Basic Camera and Wind Behaviors
        internal void PrepareForCameraAndWindMove()
        {
            // Clear the camera mouselook setting. It gets set/cleared
            // afterward if the camera data is being acquired.
            _camData.MouseLook = false;

            // Clear the wind speed. It gets updated afterward if wind is required.
            _props.Dynamics.WindDirection  = Vector3.Zero;
            _props.Dynamics.WaterDirection = Vector3.Zero;

            // This can be called before the dynamic actor has been set up.
            if (_actor.DynActorImpl == null) return;

            // Another minor hack. There is no nicer way to do this since the proper way would
            // be to plumb mouselook, point and wind shift events to communicate with the object
            // at all times. This approach yields far less compute overhead.
            if ((_props.Flags & (VehicleFlags.MouselookBank | VehicleFlags.MouselookSteer | 
                                 VehicleFlags.MousePointBank | VehicleFlags.MousePointSteer |
                                 VehicleFlags.ReactToCurrents | VehicleFlags.ReactToWind)) != 0)
                _actor.DynActorImpl.SleepThreshold = 0.0f;
            else
                _actor.DynActorImpl.SleepThreshold = PhysxPhysics.PhysxActorFactory.GetDefaultSleepThreshold();
        }

        internal void SimulateCameraBasedMotorInput(float timeStep, uint frameNum)
        {
            // Read camera data from the avatar controlling the actor. Do so only when the vehicle has 
            // mouse steer/bank flags set. Pace the requests to roughly 8x/sec.
            if ((frameNum % 8 == 0) && (_props.Flags & (VehicleFlags.MouselookBank | VehicleFlags.MouselookSteer |
                                                        VehicleFlags.MousePointBank | VehicleFlags.MousePointSteer)) != 0)
            {
                // Get the camera data
                _camData = _actor.TryGetCameraData();

                // Only use the camera data when it is valid.
                if (_camData.Valid)
                {
                    // Orient the camera rotation to local vectors then get the left/right and up/down vectors.
                    OpenMetaverse.Quaternion localcam  = _camData.CameraRotation * OpenMetaverse.Quaternion.Inverse(rotation);
                    OpenMetaverse.Vector3    localLeft = new OpenMetaverse.Vector3(1, 0, 0) * localcam;
                    OpenMetaverse.Vector3    localUp   = new OpenMetaverse.Vector3(0, 1, 0) * localcam;
                    OpenMetaverse.Vector3    motordir  = _props.ParamsVec[VectorParams.VehicleAngularMotorDirection];

                    //m_log.DebugFormat("[VehicleMouse] hrot={0} brot={1}", _camData.HeadRotation, _camData.BodyRotation);

                    Boolean steering = ((_props.Flags & VehicleFlags.MouselookSteer) != 0 && _camData.MouseLook) ||
                                        (_props.Flags & VehicleFlags.MousePointSteer) != 0;
                    Boolean banking  = ((_props.Flags & VehicleFlags.MouselookBank) != 0 && _camData.MouseLook) ||
                                        (_props.Flags & VehicleFlags.MousePointBank) != 0;

                    // Compute the percentage of off-center axes:
                    float xcam = localLeft.Y;     // -1 (right) to +1 (left)
                    float ycam = localUp.Z;       // -1 (up) to +1 (down)

                    // Clamp to current azimuth/altitude range
                    float xmax = _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth];
                    float ymax = _props.ParamsFloat[FloatParams.VehicleMouselookAltitude];
                    xcam = Utils.Clamp(xcam * (float)Math.PI * 0.5f / xmax, -1, 1);
                    ycam = Utils.Clamp(ycam * (float)Math.PI * 0.5f / ymax, -1, 1);

                    //m_log.DebugFormat("[VehicleCamData]: Cam: localrot={0}, xcam={1} ycam={2} dir={3}", localcam, xcam, ycam, motordir);

                    // Apply a dead zone threshold
                    if (Math.Abs(xcam) > VehicleLimits.ThresholdMouselookAngle)
                    {
                        // Shape the angle into a soft exponential: (e^x-1) / (e-1), which curve fits between 0,0 and 1,1.
                        xcam = (float)(Mathz.PosNeg(xcam) * (Math.Exp(Math.Abs(xcam)) - 1.0) / (Math.E - 1.0));
                    }
                    else xcam = 0;

                    // Apply a dead zone threshold
                    if (Math.Abs(ycam) > VehicleLimits.ThresholdMouselookAngle && (_props.Flags & VehicleFlags.LimitRollOnly) == 0)
                    {
                        // Shape the angle into a soft exponential: (e^x-1) / (e-1), which curve fits between 0,0 and 1,1.
                        ycam = (float)(Mathz.PosNeg(ycam) * (Math.Exp(Math.Abs(ycam)) - 1.0) / (Math.E - 1.0));
                    }
                    else ycam = 0;

                    // Steering is proportional to the angular Y & Z motor direction.
                    if (steering && (xcam != 0 || ycam != 0))
                    {
                        _motor.MoveAngular(new OpenMetaverse.Vector3(0, ycam * motordir.Y, xcam * motordir.Z));
                    }

                    // Banking is proportional to the angular X motor direction
                    if (banking && xcam != 0)
                    {
                        _motor.MoveAngular(new OpenMetaverse.Vector3(-xcam * motordir.X, 0, 0));
                    }

                    //m_log.DebugFormat("[VehicleCamData]: Cam: Rotation {0}, Position {1}", _camData.CameraRotation, _camData.CameraPosition);
                }
            }
        }

        internal void BuildWindData(float timeStep, uint frameNum)
        {
            // Get Wind data frequently, about 8x/sec. This is efficient because the entire wind matrix
            // is directly available to the physics engine.
            Vector3 wind =  _props.Dynamics.WindDirection;
            if (_scene.RegionWindGround != null && _scene.RegionWindAloft != null && _scene.RegionWaterCurrents != null &&
                (frameNum % VehicleLimits.NumTimeStepsPerWindChk == 0) &&
                (_props.Flags & (VehicleFlags.ReactToCurrents | VehicleFlags.ReactToWind)) != 0)
            {
                // TODO: Make the 4-point bilinear interpolation a utility function
                Vector3 pos = _actor.Position;
                int x0 = (int)pos.X / 16;
                int y0 = (int)pos.Y / 16;
                x0 = Utils.Clamp(x0, 0, 15);
                y0 = Utils.Clamp(y0, 0, 15);

                int x1 = Math.Min(x0 + 1, 15);
                int y1 = Math.Min(y0 + 1, 15);

                //Set the wind maxtrix based on the z-position
                float h2o       = _motor.GetWaterHeightBeneathVehicle();
                float maxheight = _scene.RegionTerrainMaxHeights[y0 * 16 + x0];
                float range     = Math.Max(_scene.RegionTerrainRanges[y0 * 16 + x0],  15.0f);
                float boundary  = h2o + maxheight + range;
                Vector2[] windmatrix;

                // Zephyr wind has three zones with very different wind characteristics.
                if (pos.Z < h2o)
                    windmatrix = _scene.RegionWaterCurrents;
                else if (pos.Z <= boundary)
                    windmatrix = _scene.RegionWindGround;
                else
                    windmatrix = _scene.RegionWindAloft;

                // Perform a bilinear interpolation of wind speeds
                // f(x,y) = f(0,0) * (1-x)(1-y) + f(1,0) * x(1-y) + f(0,1) * (1-x)y + f(1,1) * xy. 
                float dx = (pos.X - x0 * 16) / 16.0f;
                float dy = (pos.Y - y0 * 16) / 16.0f;
                //m_log.DebugFormat("pos={0} x0={1} x1={2} y0={3} y1={4} dx={5} dy={6}", pos, x0, x0, y0, y1, dx, dy);

                wind.X =  windmatrix[y0 * 16 + x0].X * (1.0f - dx) * (1.0f - dy);
                wind.X += windmatrix[y0 * 16 + x1].X * dx * (1.0f - dy);
                wind.X += windmatrix[y1 * 16 + x0].X * dy * (1.0f - dx);
                wind.X += windmatrix[y1 * 16 + x1].X * dx * dy;

                wind.Y =  windmatrix[y0 * 16 + x0].Y * (1.0f - dx) * (1.0f - dy);
                wind.Y += windmatrix[y0 * 16 + x1].Y * dx * (1.0f - dy);
                wind.Y += windmatrix[y1 * 16 + x0].Y * dy * (1.0f - dx);
                wind.Y += windmatrix[y1 * 16 + x1].Y * dx * dy;

                if (VehicleLimits.DebugWind) m_log.DebugFormat("[VehicleWindData] pos={0} wind={1}", _actor.Position, wind);
            }

            // Set the winds aloft and water currents
            _props.Dynamics.WindDirection = wind;
            _props.Dynamics.WaterDirection = wind;
        }

        internal void SimulateWindForces(float timeStep)
        {
            OpenMetaverse.Vector3 windscale = _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency];
            OpenMetaverse.Vector3 windvel   = _props.Dynamics.WindDirection * OpenMetaverse.Quaternion.Inverse(rotation);
            OpenMetaverse.Vector3 wtensor   = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.MassSpaceInertiaTensor);

            // The wind force tapers off once the vehicle achieves the current wind speed.
            if (localLinearVel.X * windvel.X >= 0)
                if (Mathz.PosNeg(localLinearVel.X) * (localLinearVel.X - windvel.X) > 0) windvel.X = 0;

            if (localLinearVel.Y * windvel.Y >= 0)
                if (Mathz.PosNeg(localLinearVel.Y) * (localLinearVel.Y - windvel.Y) > 0) windvel.Y = 0;

            if (localLinearVel.Z * windvel.Z >= 0)
                if (Mathz.PosNeg(localLinearVel.Z) * (localLinearVel.Z - windvel.Z) > 0) windvel.Z = 0;

            // Apply efficiency (-10 -to +10).
            windvel.X *= windscale.X;
            windvel.Y *= windscale.Y;
            windvel.Z *= windscale.Z;

            // Linear wind induction
            if (OpenMetaverse.Vector3.Mag(windvel) >= VehicleLimits.MinPhysicsForce)
            {
                if (VehicleLimits.DebugWind) m_log.DebugFormat("[Vehicle Wind] linear force={0} lvel={1}", windvel, localLinearVel);

                // Convert to world forces
                windvel *= timeStep;
                windvel *= _actor.Mass;
                windvel *= rotation;

                _actor.DynActorImpl.AddForce(PhysUtil.OmvVectorToPhysx(windvel), PhysX.ForceMode.Impulse, true);
            }

            // Wind roll and pitch
            windscale = _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency];
            OpenMetaverse.Vector3 windrot   = _props.Dynamics.WindDirection * OpenMetaverse.Quaternion.Inverse(rotation);

            windvel.X = -windrot.Y;
            windvel.Y = windrot.X;
            windvel.Z = 0;

            // The wind force tapers off once the vehicle achieves the current wind speed.
            if (localAngularVel.X * windvel.X >= 0)
                if (Mathz.PosNeg(localAngularVel.X) * (localAngularVel.X - windvel.X) > 0) windvel.X = 0;

            if (localAngularVel.Y * windvel.Y >= 0)
                if (Mathz.PosNeg(localAngularVel.Y) * (localAngularVel.Y - windvel.Y) > 0) windvel.Y = 0;

            if (localAngularVel.Z * windvel.Z >= 0)
                if (Mathz.PosNeg(localAngularVel.Z) * (localAngularVel.Z - windvel.Z) > 0) windvel.Z = 0;

            // Apply timescale.
            windvel.X *= windscale.X;
            windvel.Y *= windscale.Y;
            windvel.Z = 0;

            // Wind roll induction
            if (OpenMetaverse.Vector3.Mag(windvel) >= VehicleLimits.MinPhysicsForce)
            {
                //if (VehicleLimits.DebugWind) m_log.DebugFormat("[Vehicle Wind] angular force={0}", windforce);

                // Convert to world forces
                windvel *= timeStep;

                // The tensor has to be remapped to the object's x/y/z coordinates.
                windvel *= (wtensor * vframe);
                windvel *= rotation;

                _motor.AddTorque(windvel, PhysX.ForceMode.Impulse, "wind");
            }
        }
        #endregion

        #region Idiomatic Vehicle Behaviors
        internal void MitigatePhysxSpiking(float actualstep)
        {
            // Angular spike detection and remediation. PhysX can induce severe angular Y-axis forces
            // and severe Z-axis up forces on rough terrain or across prim edges.
            // When these strong impulses are detected they will be compressed.
            // The timeStep passed to the simulate method does not always reflect actual elapsed time,
            // so a timestamp is used instead for the spike detection.
            OpenMetaverse.Vector3 xyrot = PhysUtil.Rot2Euler(rotation);
            xyrot.Z = 0;
            float angle = PhysUtil.Rot2Angle(Quaternion.CreateFromEulers(xyrot));

            // Diminish the spike detection as the vehicle goes off X or Y axis.
            // This compares the current angular velocity with the one in the previous frame.
            float accel = (localAngularVel.Y - _props.Dynamics.LocalAngularVelocity.Y) / actualstep;
            accel = accel * (float)((Math.PI - angle) / Math.PI);

            if (Math.Abs(accel) > 100.0f)   //empirically determined magic number
            {
                OpenMetaverse.Vector3 remvel = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.AngularVelocity) * Quaternion.Inverse(rotation);
                // Tame the X and Y rotations.
                remvel.Z = 0;
                remvel *= rotation;
                _motor.AddTorque(-remvel, PhysX.ForceMode.VelocityChange, "spike");
                if (VehicleLimits.DebugSpikeDetection) m_log.InfoFormat("[VehicleSpike] angaccel={0} vel={1} name={2}", accel, remvel, _actor.SOPName);
            }

            // This compares the current linear velocity against the one in the previous frame.
            // Only interested in positive (up) accelerations.
            accel = (localLinearVel.Z - _props.Dynamics.LocalLinearVelocity.Z) / actualstep;
            if (accel > 50.0f) //empirically determined magic number
            {
                OpenMetaverse.Vector3 remvel = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.LinearVelocity) * Quaternion.Inverse(rotation);
                // Tame vertical and some sideways velocity kicks.
                remvel.X = 0;
                remvel.Y *= 0.5f;
                remvel *= rotation;
                _actor.DynActorImpl.AddForce(PhysUtil.OmvVectorToPhysx(-remvel), PhysX.ForceMode.VelocityChange, true);
                if (VehicleLimits.DebugSpikeDetection) m_log.InfoFormat("[VehicleSpike] linaccel={0} vel={1} name={2}", accel, remvel, _actor.SOPName);
            }
        }

        internal void SimulateBoatGroundDrag(float timeStep)
        {
            // If this is a boat and it hits ground along its heading, decay the motor.
            // The strong ground drag applies only when certain flags are present.
            if (_actor.OBBobject != null &&
                (_props.Type == VehicleType.Boat || _props.Type == VehicleType.Sailboat) &&
                (_props.Flags & VehicleFlags.HoverWaterOnly) != 0 &&
                (_props.Flags & VehicleFlags.HoverTerrainOnly) == 0 &&
                OpenMetaverse.Vector3.Mag(localLinearVel) > VehicleLimits.ThresholdLinearMotorDeltaV)
            {
                float h2o       = _motor.GetWaterHeightBeneathVehicle();
                Vector3 dfwd    = new Vector3(_actor.OBBobject.Extent.X * 0.9f,  0, -_actor.OBBobject.Extent.Z * 0.98f) * rotation;
                Vector3 dback   = new Vector3(-_actor.OBBobject.Extent.X * 0.9f,  0, -_actor.OBBobject.Extent.Z * 0.98f) * rotation;
                Vector3 dleft   = new Vector3(0, _actor.OBBobject.Extent.Y * 0.9f, -_actor.OBBobject.Extent.Z * 0.98f) * rotation;
                Vector3 dright  = new Vector3(0, -_actor.OBBobject.Extent.Y * 0.9f, -_actor.OBBobject.Extent.Z * 0.98f) * rotation;
                float gndfwd    = _actor.GetTerrainHeightAt(_actor.Position.X + dfwd.X, _actor.Position.Y + dfwd.Y);
                float gndback   = _actor.GetTerrainHeightAt(_actor.Position.X + dback.X, _actor.Position.Y + dback.Y);
                float gndleft   = _actor.GetTerrainHeightAt(_actor.Position.X + dleft.X, _actor.Position.Y + dleft.Y);
                float gndright  = _actor.GetTerrainHeightAt(_actor.Position.X + dright.X, _actor.Position.Y + dright.Y);
                float botfwd    = _actor.Position.Z + _actor.OBBobject.Center.Z + dfwd.Z;
                float botback   = _actor.Position.Z + _actor.OBBobject.Center.Z + dback.Z;
                float botleft   = _actor.Position.Z + _actor.OBBobject.Center.Z + dleft.Z;
                float botright  = _actor.Position.Z + _actor.OBBobject.Center.Z + dright.Z;
                
                if ((localLinearVel.X > VehicleLimits.ThresholdLinearMotorDeltaV && botfwd <= gndfwd) ||
                    (localLinearVel.X < -VehicleLimits.ThresholdLinearMotorDeltaV && botback <= gndback) ||
                    (localLinearVel.Y > VehicleLimits.ThresholdLinearMotorDeltaV && botleft <= gndleft) ||
                    (localLinearVel.Y < -VehicleLimits.ThresholdLinearMotorDeltaV && botright <= gndright)
                    )
                {
                    //m_log.DebugFormat("[Boat Drag] vel={0} extent={9} center={10} fwd={1},{2} back={3},{4} left={5},{6} right={7},{8}", localLinearVel, botfwd,gndfwd, botback,gndback, botleft,gndleft, botright,gndright, _actor.OBBobject.Extent, _actor.OBBobject.Center);
                    // Kill the linear motors.
                    _props.Dynamics.LinearDecayIndex = VehicleLimits.MaxDecayTimescale * 10.0f;
                    _props.Dynamics.LinearTargetVelocity  = OpenMetaverse.Vector3.Zero;
                }
            }
        }

        internal void SimulateSledMovement(float timeStep)
        {
            OpenMetaverse.Vector3 force = OpenMetaverse.Vector3.Zero;

            // Compute the percentage of declination -1 (down) to +1 (up)
            OpenMetaverse.Vector3 probe = new OpenMetaverse.Vector3(1f, 0f, 0f);
            probe *= rotation;

            // If the nose (z-axis) points downward, add some force along the X-axis.
            if (Math.Abs(probe.Z) > VehicleLimits.ThresholdDeflectionAngle)
            {
                force = new OpenMetaverse.Vector3(-Settings.Instance.Gravity * 3.0f, 0f, 0f);

                // The sled has a lower force assist going backwards. This helps
                // gravity assist coasters to work with minimal or no external motors.
                if (probe.Z > 0)
                    force *= -0.1f;
                
                if (!_motor.IsLinearMotorStalled())
                {
                    // Modulate the force based on the amount of declination.
                    force = force * timeStep * (float)Math.Sqrt(Math.Abs(probe.Z));

                    // m_log.DebugFormat("[Vehicle Sled] force={0}probe={1}", force, probe);
                        
                    if (Math.Abs(force.X) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                        Math.Abs(force.Y) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                        Math.Abs(force.Z) >= VehicleLimits.ThresholdLinearMotorDeltaV)
                    {
                        force *= rotation;
                        force *= _actor.Mass;
                        _motor.AddForce(force, PhysX.ForceMode.Force, true);
                    }
                }
            }
        }
        #endregion

        #region Angular & Linear Deflection
        internal void SimulateAngularDeflection(float timeStep)
        {
            if (Math.Abs(worldLinearVel.X) >= VehicleLimits.ThresholdDeflectionSpeed ||
                Math.Abs(worldLinearVel.Y) >= VehicleLimits.ThresholdDeflectionSpeed ||
                Math.Abs(worldLinearVel.Z) >= VehicleLimits.ThresholdDeflectionSpeed)
            {
                // Since there is some movement, create a force that rotates the vehicle to point to the
                // direction of movement in the designated timescale.
                float timescale = Math.Max(_props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale], timeStep);

                if (timescale < VehicleLimits.MaxTimescale)
                {
                    float timepct = timeStep / timescale;
                    float efficiency = _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency];
                    float speed = Utils.Clamp(OpenMetaverse.Vector3.Mag(localLinearVel), 0, VehicleLimits.MaxLegacyLinearVelocity);
                    float speedpct = speed / VehicleLimits.MaxLegacyLinearVelocity;

                    // Compute the rotation between the x axis pointing vector and the linear direction.
                    Vector3 ahead = new Vector3(1, 0, 0) * rotation;
                    Quaternion tween = PhysUtil.RotBetween(ahead, Vector3.Normalize(worldLinearVel));
                    float angle = PhysUtil.AngleBetween(tween, Quaternion.CreateFromEulers(0, 0, 0));
                    Vector3 vtwix = PhysUtil.Rot2Euler(tween);

                    // Cheat: if the local X movement is negative, flip the angle.
                    if (localLinearVel.X < 0)
                    {
                        angle = (float)Math.PI - angle;
                    }

                    // Scale the force.
                    vtwix = OpenMetaverse.Vector3.Normalize(vtwix) * speedpct * (float)Math.PI * timepct * efficiency * (float)Math.Log(1.0 + angle);

                    // Compute damping. Most damping occurs closest to the  target angle but releases at the sweet spot.
                    OpenMetaverse.Vector3 remvel = OpenMetaverse.Vector3.Zero;
                    if (angle < VehicleLimits.ThresholdDeflectionAngle)
                    {
                        remvel = worldAngularVel * timepct * efficiency * (float)(Math.Log(1.0 + Math.PI - angle) / Math.Log(1.0 + Math.PI));
                    }

                    vtwix -= remvel;

                    if (_motor.IsLinearMotorStalled())
                    {
                        vtwix = Vector3.Zero;
                    }

                    if (Math.Abs(vtwix.X) >= VehicleLimits.ThresholdAngularMotorDeltaV ||
                        Math.Abs(vtwix.Y) >= VehicleLimits.ThresholdAngularMotorDeltaV ||
                        Math.Abs(vtwix.Z) >= VehicleLimits.ThresholdAngularMotorDeltaV)
                    {
                        _motor.AddTorque(vtwix, PhysX.ForceMode.VelocityChange, "deflection");
                        if (VehicleLimits.DebugDeflection) m_log.DebugFormat("[Vehicle Deflection] Angular: force={0} pct={1} angle={2} lvel={3} sdelta={4}", vtwix, timepct, angle, localLinearVel, _props.Dynamics.ShortTermPositionDelta);
                    }
                }
            }
        }

        internal void SimulateLinearDeflection(float timeStep)
        {
            if  (Math.Abs(worldLinearVel.X) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                 Math.Abs(worldLinearVel.Y) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                 Math.Abs(worldLinearVel.Z) >= VehicleLimits.ThresholdLinearMotorDeltaV)
            {
                float timescale  = Math.Max(_props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale], timeStep);
                OpenMetaverse.Vector3 newvel;
                OpenMetaverse.Vector3 worldvel;

                if (timescale < VehicleLimits.MaxTimescale)
                {
                    // Since there is some movement, create a force that changes the vehicle's velocity to
                    // travel along the direction of its forward axis in the designated timescale.
                    float timePct = timeStep / timescale;
                    float efficiency = _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency];

                    // Determine the amount of velocity to shift
                    OpenMetaverse.Vector3 remvel = -worldLinearVel;
                    float speed = OpenMetaverse.Vector3.Mag(remvel);
                    remvel = OpenMetaverse.Vector3.Normalize(remvel) * speed * timePct * efficiency;

                    // add some velocity along the forward axis.
                    newvel = new OpenMetaverse.Vector3(speed * timePct * efficiency,0,0) * rotation;

                    // Remove some velocity along the old axis
                    worldvel = newvel + remvel;

                    // Stop any upward deflection
                    if ((_props.Flags & VehicleFlags.NoDeflectionUp) != 0)
                    {
                        if (worldvel.Z > 0) worldvel.Z = 0;
                    }

                    if (Math.Abs(worldvel.X) > VehicleLimits.ThresholdLinearMotorDeltaV ||
                        Math.Abs(worldvel.Y) > VehicleLimits.ThresholdLinearMotorDeltaV ||
                        Math.Abs(worldvel.Z) > VehicleLimits.ThresholdLinearMotorDeltaV)
                    {
                        _motor.AddForce(worldvel, PhysX.ForceMode.VelocityChange, true);
                        if (VehicleLimits.DebugDeflection) m_log.DebugFormat("[Vehicle Deflection] Linear: force={0} nvel={1} remvel={2}", worldvel, newvel, remvel);
                    }
                }
            }
        }
        #endregion

        #region Vertical Attractor & Banking
        internal void SimulateVerticalAttractor(float timeStep, uint frameNum, out OpenMetaverse.Vector3 attractionForces, out float angle, out bool inverted)
        {
            float timescale  = Math.Max(_props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale], timeStep);
            float efficiency = _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency];
            inverted = false;
            angle = 0.0f;

            attractionForces = OpenMetaverse.Vector3.Zero;

            if (timescale < VehicleLimits.MaxAttractTimescale)
            {
                // Compute the X and Y axis deflection from vertical of the Z axis.
                // The resuts are in the Z terms.
                OpenMetaverse.Vector3 xrot = new Vector3(1, 0, 0) * rotation;
                OpenMetaverse.Vector3 yrot = new Vector3(0, 1, 0) * rotation;
                OpenMetaverse.Vector3 xyrot = PhysUtil.Rot2Euler(rotation);

                xyrot.Z = 0;
                angle = Math.Abs(PhysUtil.Rot2Angle(Quaternion.CreateFromEulers(xyrot)));

                // Airplanes have two attractors since they can fly inverted, but
                // because of dihedral angles, the inverted angle is not stable.
                // TODO: Add dihedral extension here (FLoatParam.VEHICLE_DIHEDRAL_ANGLE).
                if (_props.Type == VehicleType.Airplane)
                {
                    if (angle > Math.PI / 2)
                    {
                        angle = (float)Math.PI - angle;
                        inverted = true;    // this is for the banking logic
                        // yrot.Z *= -1.0f; // this creates a second attractor.
                    }
                }

                float apct = angle / (float)Math.PI;

                //m_log.DebugFormat("[VerticalAttractor] xrot={0} yrot={1} angle={2}", xrot, yrot, angle);

                // Go dormant if in the sweet spot or if no angular changes are happening.
                // Do not go dormant if the vehicle is overturned.
                if (Math.Abs(_props.Dynamics.LastVerticalAngle - angle) >= VehicleLimits.ThresholdAttractorAngle)
                {
                    _props.Dynamics.LastVerticalFrameNumber = frameNum;
                }

                if (angle >= VehicleLimits.ThresholdOverturnAngle ||
                    (frameNum - _props.Dynamics.LastVerticalFrameNumber) < (VehicleLimits.MaxAttractDormancy / timeStep))
                {
                    // Compute restoration force in local coordinates
                    OpenMetaverse.Vector3 vtwix = new OpenMetaverse.Vector3(-yrot.Z, xrot.Z, 0);
                    //m_log.DebugFormat("[VerticalAttractor] ang={0} lang={1} force={2} xr={3} yr={4}", angle, _props.Dynamics.LastVerticalAngle, vtwix, xrot, yrot);

                    // The cheat section: different vehicles have different undocumeted characerstics.
                    vtwix = vtwix * (float)Math.PI * (float)Math.Pow(Math.E, efficiency * 3.0);

                    // Vehicles other than airplanes have very strong restorative forces.
                    if (_props.Type != VehicleType.Airplane)
                        vtwix *= (1.0f + (float)Math.Pow(1.0 + apct, 4.0));

                    // Zero out y-axis rotation if the limit roll only flag is set and it's a plane or balloon.
                    // Other vehicles need a tad more stablization because of stronger PhysX collisions.
                    if ((_props.Flags & VehicleFlags.LimitRollOnly) != 0)
                    {
                        if (_props.Type == VehicleType.Airplane || _props.Type == VehicleType.Balloon)
                            vtwix.Y = 0.0f;
                        else
                            vtwix.Y *= 0.1f;
                    }

                    // If overturned and there is no progress in moving toward vertical, keep increasing the force.
                    // The strong vert angle threshold decreases with timescale.
                    if (_props.Type != VehicleType.Airplane)
                    {
                        if (angle >= VehicleLimits.ThresholdOverturnAngle && angle >= _props.Dynamics.LastVerticalAngle)
                        {
                            _props.Dynamics.VerticalForceAdjust *= 1.3f;
                            vtwix *= _props.Dynamics.VerticalForceAdjust;
                        }
                        else
                        {
                            _props.Dynamics.VerticalForceAdjust /= 1.1f;
                            if (_props.Dynamics.VerticalForceAdjust < 1.0f)
                                _props.Dynamics.VerticalForceAdjust = 1.0f;
                            vtwix *= _props.Dynamics.VerticalForceAdjust;
                        }
                    }

                    // Apply efficiency which is really the damping factor.
                    OpenMetaverse.Vector3 remvel = OpenMetaverse.Vector3.Zero;
                    OpenMetaverse.Vector3 wtensor = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.MassSpaceInertiaTensor);
                    remvel.X = localAngularVel.X * _motor.MovementExpGrowth(efficiency, 1.0f);
                    remvel.Y = localAngularVel.Y * _motor.MovementExpGrowth(efficiency, 1.0f);

                    // The tensor has to be remapped to the object's x/y/z coordinates.
                    vtwix *= (wtensor * vframe);

                    // Convert local forces to world forces
                    vtwix = vtwix * rotation;
                    //vtwix.Z = 0;


                    if (vtwix != OpenMetaverse.Vector3.Zero)
                    {
                        // Apply angular velocity
                        attractionForces = vtwix * timeStep / timescale;
                        _motor.AddTorque(attractionForces, PhysX.ForceMode.Impulse, "vattract add");
                        if (VehicleLimits.DebugAttractor) m_log.DebugFormat("[Vehicle Simulate] Vertical Attract: force={0} remvel={1} angle={2} avel={3}", vtwix, remvel, angle, localAngularVel);
                    }

                    // Always apply the damping factor while not in the sweet spot.
                    if (Vector3.Mag(remvel) > 0)
                    {
                        if (VehicleLimits.DebugAttractor) m_log.DebugFormat("[Vehicle Simulate] Vertical Damping: remvel={0} angle={1} avel={2} laxis={3}", remvel, angle, localAngularVel, _actor.Properties.LockedAxes);
                        remvel = remvel * rotation;
                        _motor.AddTorque(-remvel, PhysX.ForceMode.VelocityChange, "vdamping");
                    }
                    _props.Dynamics.LastVerticalAngle = angle;
                }
            }
        }

        internal void SimulateBankingToYaw(float timeStep, float angle, bool inverted)
        {
            // -------------------------------------------------------------------------------------------
            // Banking - induce yaw rotation (about the world z-axis) proportional to angle of roll (about the local x-axis).
            // Banking requires a vertical attractor present and recent motor activity
            //
            float timescale = Math.Max(_props.ParamsFloat[FloatParams.VehicleBankingTimescale], timeStep);

            if (timescale < VehicleLimits.MaxAttractTimescale)
            {
                // Efficiency runs -1 to 1, positive roll induces negative z-torque.
                float efficiency = _props.ParamsFloat[FloatParams.VehicleBankingEfficiency];
                float bmodifier  = _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier];

                if (VehicleLimits.DoBanking && timescale < VehicleLimits.MaxTimescale)
                {
                    float bankingmix = _props.ParamsFloat[FloatParams.VehicleBankingMix];
                    float xspeed = 0.0f;
                    OpenMetaverse.Vector3 motordir = _props.ParamsVec[VectorParams.VehicleAngularMotorDirection];

                    // Cheat: Legacy vehicle support use velocity as an on/off switch. This support will
                    // make it proportional to the velocity and capped at the same speed as the static banking max.
                    if (Math.Abs(localLinearVel.X) > VehicleLimits.ThresholdAngularMotorDeltaV)
                        xspeed = Utils.Clamp(Math.Abs(localLinearVel.X), 0, VehicleLimits.MaxLegacyLinearVelocity); // clamp speed to legacy grid
                    float xspeedpct = xspeed / VehicleLimits.MaxLegacyLinearVelocity;

                    // Compute the percentage of roll: -1 (down) to +1 (up)
                    OpenMetaverse.Vector3 erot = new OpenMetaverse.Vector3(0f, 1f, 0f);
                    erot *= rotation;
                    float xangle = erot.Z;
                    float attitude = (angle > Math.PI / 2.0) ? -1 : 1;
                        

                    // Clamp to current banking range
                    float xmax = _props.ParamsFloat[FloatParams.VehicleBankingAzimuth];
                    xangle = Utils.Clamp(xangle * (float)Math.PI * 0.5f / xmax, -1, 1);

                    // When the vehicle is inverted, apply the inverted banking modifier.
                    if (inverted)
                        efficiency = efficiency * bmodifier;

                    // Apply torque only when above the threshold, stops squirming.
                    //
                    if (Math.Abs(xangle) > VehicleLimits.ThresholdBankAngle)
                    {
                        // Shape the angle into a soft exponential: (e^x-1) / (e-1), which curve fits between 0,0 and 1,1.
                        //xangle = (float)(Mathz.PosNeg(xangle) * (Math.Exp(Math.Abs(xangle)) - 1.0) / (Math.E - 1));

                        // Convert that to a z-torque in world coordinates
                        // The movement takes place in the angular motor.
                        _props.Dynamics.BankingDirection  = -xangle * attitude * efficiency * (1.0f - bankingmix) * (float)Math.PI; // static banking
                        _props.Dynamics.BankingDirection += -xangle * attitude * efficiency * bankingmix * xspeedpct * (float)Math.PI; // dynamic banking
                        if (VehicleLimits.DebugBanking) m_log.DebugFormat("[Vehicle Simulate] banking dir={0} xang={1} att={2} mix={3} eff={4} ts={5}", _props.Dynamics.BankingDirection, xangle, attitude, bankingmix, efficiency, timescale);
                    }
                    else
                    {
                        _props.Dynamics.BankingDirection = 0;
                    }
                }
            }
        }
        #endregion

        #region Angular & Linear Friction
        internal void SimulateAngularFriction(float timeStep)
        {
            OpenMetaverse.Vector3 newvel;
            OpenMetaverse.Vector3 worldvel;
            OpenMetaverse.Vector3 frictionTS = _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale];

            if (frictionTS.X < VehicleLimits.MaxTimescale ||
                frictionTS.Y < VehicleLimits.MaxTimescale ||
                frictionTS.Z < VehicleLimits.MaxTimescale)
            {
                // Clamp the timescale to prevent exceptions.
                frictionTS.X = Math.Max(frictionTS.X, timeStep);
                frictionTS.Y = Math.Max(frictionTS.Y, timeStep);
                frictionTS.Z = Math.Max(frictionTS.Z, timeStep);

                // Normalize to one physics frame time.
                frictionTS.X = _motor.MovementLimitedGrowth(timeStep, frictionTS.X);
                frictionTS.Y = _motor.MovementLimitedGrowth(timeStep, frictionTS.Y);
                frictionTS.Z = _motor.MovementLimitedGrowth(timeStep, frictionTS.Z);

                // Compute new target local deltaV
                newvel.X = -localAngularVel.X * frictionTS.X;
                newvel.Y = -localAngularVel.Y * frictionTS.Y;
                newvel.Z = -localAngularVel.Z * frictionTS.Z;

                newvel.X = Utils.Clamp(newvel.X, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                newvel.Y = Utils.Clamp(newvel.Y, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                newvel.Z = Utils.Clamp(newvel.Z, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);

                // Clean up when below physX minimum threshold
                if (Math.Abs(newvel.X) < VehicleLimits.MinPhysicsForce) newvel.X = Mathz.PosNeg(newvel.X) * VehicleLimits.MinPhysicsForce;
                if (Math.Abs(newvel.Y) < VehicleLimits.MinPhysicsForce) newvel.Y = Mathz.PosNeg(newvel.Y) * VehicleLimits.MinPhysicsForce;
                if (Math.Abs(newvel.Z) < VehicleLimits.MinPhysicsForce) newvel.Z = Mathz.PosNeg(newvel.Z) * VehicleLimits.MinPhysicsForce;

                // Switch back to world coords
                worldvel = newvel * rotation;

                // Apply a force required to make this change in one physics frame.
                // If the velocity drops below the threshold, do nothing to let the vehicle eventually sleep.
                if (Math.Abs(localAngularVel.X) >= VehicleLimits.MinPhysicsForce*2 ||
                    Math.Abs(localAngularVel.Y) >= VehicleLimits.MinPhysicsForce*2 ||
                    Math.Abs(localAngularVel.Z) >= VehicleLimits.MinPhysicsForce*2)
                {
                    if (OpenMetaverse.Vector3.Mag(worldAngularVel) < VehicleLimits.ThresholdAngularFrictionDeltaV)
                    {
                        worldvel = -worldAngularVel;
                        if (VehicleLimits.DebugAngularFriction) m_log.DebugFormat("[Vehicle friction] Angular all stop vel={0}", worldvel);
                    }
                    
                    if (OpenMetaverse.Vector3.Mag(worldvel) > 0)
                    {
                        _motor.AddTorque(worldvel, PhysX.ForceMode.VelocityChange, "friction");
                        if (VehicleLimits.DebugAngularFriction) m_log.DebugFormat("[Vehicle friction] Angular {0} local={1} avel={2}", worldvel, localAngularVel, worldAngularVel);
                    }
                }
            }
        }

        internal void SimulateLinearFriction(float timeStep)
        {
            OpenMetaverse.Vector3 newvel;
            OpenMetaverse.Vector3 worldvel;
            OpenMetaverse.Vector3 frictionTS = _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale];

            if (frictionTS.X < VehicleLimits.MaxTimescale ||
                frictionTS.Y < VehicleLimits.MaxTimescale ||
                frictionTS.Z < VehicleLimits.MaxTimescale)
            {
                // Clamp the timescale to prevent exceptions.
                frictionTS.X = Math.Max(frictionTS.X, timeStep);
                frictionTS.Y = Math.Max(frictionTS.Y, timeStep);
                frictionTS.Z = Math.Max(frictionTS.Z, timeStep);

                // Normalize to one physics frame time.
                frictionTS.X = _motor.MovementLimitedGrowth(timeStep, frictionTS.X);
                frictionTS.Y = _motor.MovementLimitedGrowth(timeStep, frictionTS.Y);
                frictionTS.Z = _motor.MovementLimitedGrowth(timeStep, frictionTS.Z);

                // Compute new target local deltaV
                newvel.X = -localLinearVel.X * frictionTS.X;
                newvel.Y = -localLinearVel.Y * frictionTS.Y;
                newvel.Z = -localLinearVel.Z * frictionTS.Z;


                // Clean up when below physX minimum threshold
                if (Math.Abs(newvel.X) < VehicleLimits.MinPhysicsForce) newvel.X = Mathz.PosNeg(newvel.X) * VehicleLimits.MinPhysicsForce;
                if (Math.Abs(newvel.Y) < VehicleLimits.MinPhysicsForce) newvel.Y = Mathz.PosNeg(newvel.Y) * VehicleLimits.MinPhysicsForce;
                if (Math.Abs(newvel.Z) < VehicleLimits.MinPhysicsForce) newvel.Z = Mathz.PosNeg(newvel.Z) * VehicleLimits.MinPhysicsForce;

                // Switch back to world coords
                worldvel = newvel * rotation;

                // Cheat: Do not fight gravity.
                if (worldvel.Z > 0) worldvel.Z = 0;

                //m_log.DebugFormat("[Vehicle friction] Potential linear friction {0}", worldvel);
                // Apply a force required to make this change in one physics frame
                // If the velocity drops below the threshold, do nothing to let the vehicle eventually sleep.
                if (Math.Abs(localLinearVel.X) >= VehicleLimits.MinPhysicsForce*2 ||
                    Math.Abs(localLinearVel.Y) >= VehicleLimits.MinPhysicsForce*2 ||
                    Math.Abs(localLinearVel.Z) >= VehicleLimits.MinPhysicsForce*2)
                {
                    if (OpenMetaverse.Vector3.Mag(worldLinearVel) < VehicleLimits.ThresholdLinearFrictionDeltaV)
                    {
                        worldvel = -worldLinearVel;
                        if (VehicleLimits.DebugLinearFriction) m_log.DebugFormat("[Vehicle friction] Linear all stop vel={0}", worldvel);
                    }

                    if (OpenMetaverse.Vector3.Mag(worldvel) > 0)
                    {
                        _motor.AddForce(worldvel, PhysX.ForceMode.VelocityChange, true);
                        if (VehicleLimits.DebugLinearFriction) m_log.DebugFormat("[Vehicle friction] Linear {0}", worldvel);
                    }
                }
            }
        }
        #endregion

        #region Utilities
        internal void DisplayParameters()
        {
            m_log.InfoFormat("[Vehicle] {0} info:", _actor.SOPName);
            foreach (var item in _props.ParamsVec)
            {
                m_log.InfoFormat("   ParamsVec.{0}={1}", item.Key, item.Value);
            }

            foreach (var item in _props.ParamsFloat)
            {
                m_log.InfoFormat("   ParamsFloat.{0}={1}", item.Key, item.Value);
            }
        }

        internal Quaternion RotBetweenLinear(Quaternion a, Quaternion b, float t)
        {
            float ang = PhysUtil.AngleBetween(a, b);
            if (ang > Math.PI) ang -= (float)Math.PI * 2.0f;
            return a * PhysUtil.AxisAngle2Rot(PhysUtil.Rot2Axis(b/a) * a, ang * t);
        }

        // Set the vehicle buoyancy. This implementation overrides the LSL basics forces llSetBuoyancy
        // setting to prevent conflicting dynamics.
        internal void SetVehicleBuoyancy()
        {
            float buoyancy  = _props.ParamsFloat[FloatParams.VehicleBuoyancy];
            
            _actor.SetBuoyancy(buoyancy);
            //m_log.DebugFormat("[SetVehicleBuoyancy] {0}", buoyancy);
        }

        // Set, adjust, clear hover depending on the vehicle hover parameters. This implementation
        // overrides the LSL basic forces llSetHover* settings, which is much better than creating two
        // indepdendent and mutally conflicting hover dynamics.
        internal void SetVehicleHover()
        {
            float height        = _props.ParamsFloat[FloatParams.VehicleHoverHeight];
            float tau           = _props.ParamsFloat[FloatParams.VehicleHoverTimescale];
            float efficiency    = _props.ParamsFloat[FloatParams.VehicleHoverEfficiency];
            OpenSim.Region.Physics.Manager.PIDHoverFlag hovertype = OpenSim.Region.Physics.Manager.PIDHoverFlag.None;

            // Remove hover if the tau exceeds the max timescale.
            // Special case for legacy scripts: height<=0 nd eff=0 means no hover.
            if (tau >= VehicleLimits.MaxHoverTimescale || (height<=0 && efficiency==0))
            {
                if ((_actor.GetHoverType() & OpenSim.Region.Physics.Manager.PIDHoverFlag.Vehicle)  != 0)
                {
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale] = VehicleLimits.MaxHoverTimescale;
                    _actor.ClearHover();
                }
            }

            else
            {
                // Global hover overrides water and ground
                if ((_props.Flags & VehicleFlags.HoverGlobalHeight) != 0)
                {
                    hovertype = OpenSim.Region.Physics.Manager.PIDHoverFlag.Global;
                }
                else
                {
                    // Vehicles can hover over ground, water, or both.
                    // These flags are inverted -- WaterOnly means not terrain, TerrainOnly means not water.
                    // Crazily, both flags set or both clear means hover both.
                    hovertype = OpenSim.Region.Physics.Manager.PIDHoverFlag.Water | OpenSim.Region.Physics.Manager.PIDHoverFlag.Ground;
                    if ((_props.Flags & (VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly)) != (VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly))
                    {
                        if ((_props.Flags & VehicleFlags.HoverWaterOnly) != 0)
                            hovertype &= ~OpenSim.Region.Physics.Manager.PIDHoverFlag.Ground;
                        if ((_props.Flags & VehicleFlags.HoverTerrainOnly) != 0)
                            hovertype &= ~OpenSim.Region.Physics.Manager.PIDHoverFlag.Water;
                    }
                }

                // Hovering up-only applies to all modes.
                if ((_props.Flags & VehicleFlags.HoverUpOnly) != 0)
                    hovertype |= OpenSim.Region.Physics.Manager.PIDHoverFlag.UpOnly;

                hovertype |= OpenSim.Region.Physics.Manager.PIDHoverFlag.Vehicle;
                _actor.SetHover(hovertype, height, tau, efficiency);
            }
            //m_log.DebugFormat("[SetVehicleHover] {0} height={1} tau={2} eff={3} flags={4}", hovertype, height, tau, efficiency,_props.Flags);
        }

        internal void SetVehicleDefaultActions()
        {
            if (VehicleLimits.DebugVehicleChange) m_log.InfoFormat("[SetVehicleType] type={0} name={1} at {2}", _props.Type, _actor.SOPName, _actor.Position);

            switch (_props.Type)
            {
                case VehicleType.None:
                    break;

                case VehicleType.Sled:
                    // Turn off friction.
                    Material mstuff = new Material(_physics, 0.0f, 0.0f, _actor.PrimMaterial.Restitution, _actor.PrimMaterial.Density, _actor.PrimMaterial.GravityMultiplier);
                    _actor.SetMaterialSync(mstuff, true);
                    break;

                case VehicleType.Car:
                    break;

                case VehicleType.Motorcycle:    // An InWorldz vehicle type
                    break;

                case VehicleType.Boat:
                    break;

                case VehicleType.Sailboat:  // An InWorldz vehicle type
                    break;

                case VehicleType.Airplane:
                    break;

                case VehicleType.Balloon:
                    break;
            }

            // Update hover, buoyancy and mouselook.
            SetVehicleHover();
            SetVehicleBuoyancy();
            PrepareForCameraAndWindMove();
        }

        internal void OnPhysicsSuspended()
        {
        }

        // On a region crossing set the last access time to now since timestamps will
        // always be slightly off between regions/servers.
        internal void OnPhysicsResumed()
        {
            if (VehicleLimits.DebugRegionChange)
            {
                m_log.InfoFormat("[Vehicle Simulate] OnPhysicsResumed: new={0}", _scene.RegionID);
                //m_log.InfoFormat("[Vehicle Crossing] floats={0} dynamics={1}", _props.ParamsFloat.ToString(), _props.Dynamics.ToString());
            }

            _regionId = _scene.RegionID;

            // Do this to prevent a phantom stall detection at the crossing.
            _props.Dynamics.LastAccessTOD = DateTime.Now;
            SetVehicleDefaultActions();

            // Ensure the physical object remains awake.
            _actor.WakeUp();
        }
        #endregion

        #region Vehicle Types & Parameters
        // Set the default values associated with the new vehicle type. This
        // may require adjusting the current ramping/damping values if they are
        // outside the new limits (done in simulate).
        // NOTE: These constants are from the wiki documentation and needs verification empirically.
        /// <summary>
        /// The standard LSL default parameters for each type of vehicle
        /// WARNING: This code may be executed from outside the physics thread.
        /// </summary>
        internal static void SetVehicleDefaults(VehicleProperties _props)
        {
            VehicleType newType = _props.Type;

            switch (newType)
            {
                // This is here to prepopulate the parameters to avoid an exception.
                // The parameters are also cleared to remove any previous keys that have been removed or renamed.
                case VehicleType.None:
                    _props.ParamsVec.Clear();
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(1000.0f, 1000.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(1000.0f, 1000.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = OpenMetaverse.Vector3.Zero;

                    _props.ParamsFloat.Clear();
                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot.Clear();
                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags = 0;
                    break;

                case VehicleType.Sled:
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(1000.0f, 1.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(1000.0f, 1000.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = new OpenMetaverse.Vector3(0.0f, 0.0f, -0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(1000.0f, 1000.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(1000.0f, 1000.0f, 1000.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(120.0f, 120.0f, 120.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(120.0f, 120.0f, 120.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = OpenMetaverse.Vector3.Zero;

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 0.3f; // 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 1.0f; // 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 1.0f; // 10.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.1f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 10.0f; // 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 10.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags = (VehicleFlags.NoDeflectionUp | VehicleFlags.LimitRollOnly | VehicleFlags.LimitMotorUp);
                    // ~(VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly | VehicleFlags.HoverGlobalHeight | VehicleFlags.HoverUpOnly);
                    break;

                case VehicleType.Car:
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(100.0f, 0.1f, 10.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(100.0f, 100.0f, 0.3f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(0.5f, 1.0f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(0.2f, 0.2f, 0.05f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(10.0f, 2.0f, 2.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(0.3f, 0.3f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = OpenMetaverse.Vector3.Zero;

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.5f; // 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 2.0f; // 10.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.6f; // 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 2.0f; // 10.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = -0.2f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.75f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 2.5f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags = (VehicleFlags.NoDeflectionUp | VehicleFlags.LimitRollOnly | VehicleFlags.HoverUpOnly | VehicleFlags.LimitMotorUp);
                    // ~(VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly | VehicleFlags.HoverGlobalHeight);
                    break;

                case VehicleType.Motorcycle:    // An InWorldz vehicle type
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(100.0f, 0.1f, 10.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(3.0f, 0.2f, 10.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = new OpenMetaverse.Vector3(0.0f, 0.0f, -0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(0.5f, 1.0f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(0.1f, 0.1f, 0.05f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(10.0f, 1.0f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(0.2f, 0.8f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = OpenMetaverse.Vector3.Zero;

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.8f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 0.95f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = -0.5f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 0.1f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 1.5f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 2.5f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags = (VehicleFlags.NoDeflectionUp | VehicleFlags.HoverUpOnly | VehicleFlags.LimitMotorUp | VehicleFlags.LimitMotorDown |
                                    VehicleFlags.LimitRollOnly | VehicleFlags.TorqueWorldZ);
                    // ~(VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly | VehicleFlags.HoverGlobalHeight);
                    break;

                case VehicleType.Boat:
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(200.0f, 0.5f, 3.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(10.0f, 1.0f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(1.0f, 5.0f, 5.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(0.2f, 2.0f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(1.0f, 10.0f, 10.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(0.3f, 0.3f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = OpenMetaverse.Vector3.Zero;

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.5f; // 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.8f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 0.2f; // 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f; // 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 3.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 0.2f; // 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 0.2f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags =  (VehicleFlags.NoDeflectionUp | VehicleFlags.HoverWaterOnly | VehicleFlags.LimitMotorUp | VehicleFlags.LimitMotorDown |
                                     VehicleFlags.TorqueWorldZ);
                    // ~(VehicleFlags.HoverTerrainOnly | VehicleFlags.LimitRollOnly | VehicleFlags.HoverGlobalHeight | VehicleFlags.HoverUpOnly );
                    break;

                case VehicleType.Sailboat:  // An InWorldz vehicle type
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(200.0f, 0.5f, 3.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(10.0f, 1.0f, 0.2f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(1.0f, 5.0f, 5.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(2.0f, 2.0f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(1.0f, 10.0f, 10.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(0.3f, 0.3f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = new OpenMetaverse.Vector3(0.02f, 0.001f, 0.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = new OpenMetaverse.Vector3(0.1f, 0.01f, 0.0f);

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0001f; // 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.8f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 0.5f; // 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 3.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 0.3f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 0.8f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = -0.2f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags =  (VehicleFlags.NoDeflectionUp | VehicleFlags.HoverWaterOnly | VehicleFlags.LimitMotorUp | VehicleFlags.LimitMotorDown | 
                                     VehicleFlags.ReactToWind | VehicleFlags.ReactToCurrents | VehicleFlags.TorqueWorldZ);
                    // ~(VehicleFlags.HoverTerrainOnly | VehicleFlags.LimitRollOnly | VehicleFlags.HoverGlobalHeight | VehicleFlags.HoverUpOnly );
                    break;

                case VehicleType.Airplane:
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(200.0f, 10.0f, 5.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(1.0f, 0.1f, 0.5f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(2.0f, 2.0f, 2.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(1.0f, 2.0f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(60.0f, 60.0f, 60.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(8.0f, 8.0f, 8.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = new OpenMetaverse.Vector3(0.1f, 0.0f, 0.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = new OpenMetaverse.Vector3(0.05f, 0.0f, 0.0f);

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 0.0f; // 0.7f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.9f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.7f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags =  (VehicleFlags.TorqueWorldZ | VehicleFlags.LimitRollOnly);
                    // ~(VehicleFlags.NoDeflectionUp | VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly | 
                    //   VehicleFlags.HoverUpOnly | VehicleFlags.HoverGlobalHeight | VehicleFlags.LimitMotorUp);
                    break;

                case VehicleType.Balloon:
                    _props.ParamsVec[VectorParams.VehicleLinearFrictionTimescale]       = new OpenMetaverse.Vector3(1.0f, 1.0f, 5.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularFrictionTimescale]      = new OpenMetaverse.Vector3(2.0f, 0.5f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDirection]          = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDirection]         = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorOffset]             = OpenMetaverse.Vector3.Zero;
                    _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale]          = new OpenMetaverse.Vector3(1.0f, 5.0f, 5.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale]         = new OpenMetaverse.Vector3(2.0f, 2.0f, 0.3f);
                    _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale]     = new OpenMetaverse.Vector3(60.0f, 60.0f, 60.0f);
                    _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale]    = new OpenMetaverse.Vector3(0.3f, 0.3f, 1.0f);
                    _props.ParamsVec[VectorParams.VehicleLinearWindEfficiency]          = new OpenMetaverse.Vector3(0.1f, 0.1f, 0.1f);
                    _props.ParamsVec[VectorParams.VehicleAngularWindEfficiency]         = new OpenMetaverse.Vector3(0.01f, 0.01f, 0.0f);

                    _props.ParamsFloat[FloatParams.VehicleHoverHeight]                  = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleHoverEfficiency]              = 0.8f;
                    _props.ParamsFloat[FloatParams.VehicleHoverTimescale]               = 10.0f;
                    _props.ParamsFloat[FloatParams.VehicleBuoyancy]                     = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionEfficiency]   = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleLinearDeflectionTimescale]    = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency]  = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale]   = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency] = 0.5f;  // 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale]  = 4.0f; // 1000.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingEfficiency]            = 0.05f;
                    _props.ParamsFloat[FloatParams.VehicleInvertedBankingModifier]      = 1.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingMix]                   = 0.5f;
                    _props.ParamsFloat[FloatParams.VehicleBankingTimescale]             = 5.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAltitude]            = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleMouselookAzimuth]             = (float)Math.PI / 4.0f;
                    _props.ParamsFloat[FloatParams.VehicleBankingAzimuth]               = (float)Math.PI / 2.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove]           = 0.0f;
                    _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter]           = 0.0f;

                    _props.ParamsRot[RotationParams.VehicleReferenceFrame] = new OpenMetaverse.Quaternion(0f, 0f, 0f, 1f);

                    _props.Flags =  (VehicleFlags.ReactToWind);
                    // ~(VehicleFlags.NoDeflectionUp | VehicleFlags.HoverWaterOnly | VehicleFlags.HoverTerrainOnly | 
                    //   VehicleFlags.HoverUpOnly | VehicleFlags.HoverGlobalHeight | VehicleFlags.LimitMotorUp) |
                    //   VehicleFlags.LimitRollOnly;
                    break;
            }
        }
        #endregion
    }

    /// <summary>
    /// The vehicle motors (angular, banking, and linear)
    /// </summary>
    internal class VehicleMotor
    {
        private PhysxPrim _actor;
        private VehicleProperties _props;
        private PhysX.Physics _physics;
        private PhysxScene _scene;

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// These are common computed values used for the current simulation step.
        /// They are ephemeral and need not be stored in the persistent vehicle properties.
        /// </summary>
        private float timeStep;
        private OpenMetaverse.Quaternion vframe;
        private OpenMetaverse.Quaternion rotation;
        private OpenMetaverse.Vector3 worldAngularVel;
        private OpenMetaverse.Vector3 worldLinearVel;
        private OpenMetaverse.Vector3 localAngularVel;
        private OpenMetaverse.Vector3 localLinearVel;

        public VehicleMotor(ref PhysxPrim _actor, ref VehicleProperties _props, ref PhysX.Physics _physics, ref PhysxScene _scene)
        {
            this._actor = _actor;
            this._props = _props;
            this._physics = _physics;
            this._scene = _scene;
        }

        internal void MetricsInit(float timeStep, 
                           OpenMetaverse.Quaternion vframe,       OpenMetaverse.Quaternion rotation, 
                           OpenMetaverse.Vector3 worldAngularVel, OpenMetaverse.Vector3 worldLinearVel,
                           OpenMetaverse.Vector3 localAngularVel, OpenMetaverse.Vector3 localLinearVel)
        {
            this.timeStep = timeStep;
            this.vframe = vframe;
            this.rotation = rotation;
            this.worldAngularVel = worldAngularVel;
            this.worldLinearVel = worldLinearVel;
            this.localAngularVel = localAngularVel;
            this.localLinearVel = localLinearVel;
        }

        #region Motor Utilities
        // Check that the motor simulation is timely. If a long delay has occured,
        // it is most likely because the vehicle has been rezzed or edited. In both
        // cases, the motors are reset to prevent unexpected movement.
        //
        // Actual time passage is used to determine the delay. This value is returned
        // to assist the PhysX spike detector.
        //
        internal float CheckResetMotors(float timeStep)
        {
            DateTime tod = DateTime.Now;
            TimeSpan td = tod - _props.Dynamics.LastAccessTOD;
            float    actualstep = (float)td.TotalSeconds;
            if (actualstep == 0) actualstep = VehicleLimits.MinPhysicsTimestep;

            if (VehicleLimits.DebugTimestep)
            {
                // Verify time step is reasonable
                float tsdelta = actualstep - timeStep;
                if (Math.Abs(tsdelta) > 0.01)
                    m_log.InfoFormat("[VehicleTimestep] slippage actual={0} param={1}", td.TotalSeconds, timeStep);
            }

            _props.Dynamics.LastAccessTOD = tod;
            if (td.TotalMilliseconds > 1000.0)
            {
                //m_log.DebugFormat("[CheckResetMotors] VehicleMotor motors delayed");
                ResetDynamics();
                _actor.WakeUp();
            }

            return actualstep;
        }

        //
        // Determine if the motors are stalled to detect if the vehicle is jammed.
        // This is different than an edit or rez. The code determines if the vehicle
        // has slammed up against another object or the void.
        //
        private Boolean LinearMotorStalled = false;
        private Boolean LinearMotorStallChecked = false;

        internal void ClearLinearMotorStalled()
        {
            LinearMotorStalled = false;
            LinearMotorStallChecked = false;
        }

        internal Boolean IsLinearMotorStalled()
        {
            OpenMetaverse.Vector3 currpos   = _actor.Position;
            OpenMetaverse.Vector3 lastpos   = _props.Dynamics.LastPosition;
            OpenMetaverse.Vector3 posdelta  = currpos - lastpos;
            float currspeed  = OpenMetaverse.Vector3.Mag(worldLinearVel);
            float stposdelta = Vector3.Mag(_props.Dynamics.ShortTermPositionDelta);
            float stspeed    = stposdelta / timeStep;

            if (!LinearMotorStallChecked)
            {
                // At void borders, vehicles are positionally kicked back giving them very high apparent velocity (this is a physx bug).
                // Compute a smoothed short term position delta using a weighted average which is used to smooth out
                // the effects of hitting void borders. 
                _props.Dynamics.ShortTermPositionDelta = _props.Dynamics.ShortTermPositionDelta * 0.8f + posdelta * 0.2f;
                LinearMotorStallChecked = true;

                // If either motor is starting fresh, assume not stalled to give it a chance to develop some velocity.
                if (_props.Dynamics.LinearDecayIndex  > VehicleLimits.ThresholdLinearMotorEngaged &&
                    _props.Dynamics.AngularDecayIndex > VehicleLimits.ThresholdAngularMotorEngaged)
                {
                    // If there is some positional movement, it is not stalled.
                    stposdelta = Vector3.Mag(_props.Dynamics.ShortTermPositionDelta);
                    stspeed    = stposdelta / timeStep;
                    //m_log.DebugFormat("[IsLinearMotorStalled] maybe currspeed={0} stdelta={1}", currspeed, stposdelta);
                    if (stspeed < currspeed * 0.5f)
                    {
                        // If there is no velocity present (with no positional delta) it is not stalled, just not moving.
                        if (currspeed >= VehicleLimits.ThresholdLinearMotorUnstuck)
                        {
                            // it is jittering, so stalled.
                            LinearMotorStalled = true;
                        }
                    }
                }
            }

            if (VehicleLimits.DebugLinearMotor && LinearMotorStalled) m_log.DebugFormat("[IsLinearMotorStalled] {0} spd={1} dspd={2}", LinearMotorStalled, currspeed, stspeed);
            return LinearMotorStalled;
        }

        //
        // Reset all the motion dynamics variables to stop the motors.
        // CAUTION: This method can be called outside of the simulate scope, member variables might not be initialized.
        //
        internal void ResetDynamics()
        {
            //m_log.DebugFormat("[ResetDynamics] Resetting vehicle dynamics");
            _props.Dynamics.LastAccessTOD       = DateTime.Now;
            _props.Dynamics.LastPosition        = _actor.Position;  

            // Can be called before the dynamic actor has been created.
            // Can be called outside of the simulation loop.
            if (_actor.DynActorImpl != null)
            {
                OpenMetaverse.Quaternion vframe     = _props.ParamsRot[RotationParams.VehicleReferenceFrame];
                OpenMetaverse.Quaternion rotation   = _actor.Rotation * vframe;

                _props.Dynamics.LocalLinearVelocity = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.LinearVelocity) * OpenMetaverse.Quaternion.Inverse(rotation);
                _props.Dynamics.LocalAngularVelocity = PhysUtil.PhysxVectorToOmv(_actor.DynActorImpl.AngularVelocity) * OpenMetaverse.Quaternion.Inverse(rotation);
            }
            _props.Dynamics.LastVerticalAngle   = 0.0f;
            _props.Dynamics.VerticalForceAdjust = 1.0f;

            // Motors off initially
            _props.Dynamics.LinearDecayIndex      = VehicleLimits.MaxDecayTimescale * 10.0f;
            _props.Dynamics.AngularDecayIndex     = VehicleLimits.MaxDecayTimescale * 10.0f;
            _props.Dynamics.LinearTargetVelocity  = OpenMetaverse.Vector3.Zero;
            _props.Dynamics.AngularTargetVelocity = OpenMetaverse.Vector3.Zero;

            // Bank/turn motor off
            _props.Dynamics.BankingDirection      = 0;
            _props.Dynamics.BankingTargetVelocity = 0;

            // Delta is zero
            _props.Dynamics.TargetLinearDelta   = OpenMetaverse.Vector3.Zero;
            _props.Dynamics.TargetAngularDelta  = OpenMetaverse.Vector3.Zero;
        }
        
        // Get the ground height beneath the vehicle.
        internal float GetGroundHeightBeneathVehicle()
        {
            return _actor.Position.Z - _actor.GetHeightAbove(OpenSim.Region.Physics.Manager.PIDHoverFlag.Ground);
        }

        // Get the water height beneath the vehicle.
        internal float GetWaterHeightBeneathVehicle()
        {
            return _actor.Position.Z - _actor.GetHeightAbove(OpenSim.Region.Physics.Manager.PIDHoverFlag.Water);
        }

        //  .
        //    .
        //        .
        //               .
        public float MovementExpDecay(float timeindex, float timescale)
        {   
            // Compute Nt = N0 * e^(-t/T). In this case N0 is normalized to 1.0
            // T is the timescale aka Tau.
            // Returns an exponential value between 1.0 and nearly zero.
            if (timeindex <= 0) return 1.0f;
            float factor =  (float)Math.Pow(Math.E, (double)(-timeindex/timescale));

            // In theory, decay is infinitely long. In practice, there is stiction.
            if (factor < VehicleLimits.ThresholdStictionFactor) factor = 0.0f;
            return factor;
        }

        //               .
        //        .
        //    .
        //  .
        public float MovementLimitedGrowth(float timeindex, float timescale)
        {
            // Returns an exponential value between zero and nearly 1.0.
            return 1.0f - MovementExpDecay(timeindex, timescale);
        }

        //               . . . .. 
        //              .
        //            .
        //         .
        //  .
        public float MovementExpGrowth(float timeindex, float timescale)
        {
            // Returns a soft exponential value between zero and 1.0.
            return Utils.Clamp((float)Math.Pow(Math.E, (timeindex / timescale) / Math.E) - 1.0f, 0.0f, 1.0f);
        }

        // Compute a growth/decay rate based on an exponential fit between two velocity points in TAU time.
        public float GetGrowthRate(float svel, float evel, float timescale)
        {
            float elog;

            // Normalize velocities pointing in the same direction
            if (svel * evel > 0)
            {
                evel = Math.Abs(evel);
                svel = Math.Abs(svel);
                elog = (float) Math.Log(evel/svel);
            }

            // When velocities point in opposite directions it gets complicated. 
            // If the ending velocity is zero, then decelerate to zero.
            // Otherwise, the goal is to decelerate the starting velocity toward zero,
            //then cross over and begin acceleration toward the ending velocity.
            else
            {
                // Both velocities zero means no growth.
                if (evel == 0 && svel == 0) return 0;

                // When the ending velocity is zero, then decay to zero.
                if (evel == 0)
                {
                    elog = -(float)Math.Log(1.0 + Math.Abs(svel));
                }

                // When starting from zero, return a growth factor
                else if (svel == 0)
                {
                    elog = (float)Math.Log(1.0 + Math.Abs(evel)); 
                }

                // Otherwise, this is a crossover case.
                // return a decay towards zero, but when close to zero,
                // return a factor that forces a crossover to an inverse velocity.
                else
                {
                    if (Math.Abs(svel) > 0.3f)
                    {
                        elog = -(float)Math.Log(1.0f + Math.Abs(svel-evel));
                    }
                    else return VehicleLimits.ThresholdInverseCrossover;
                }
            }

            return elog / timescale;
        }

        private float HitDist = 0.0f;
        private float HeightExceededTime = 0.0f;

        //
        // Raycastng in physx 3.2.x has bugs. Raycast single is broken, Raycast multiple returns an unordered list, determining the
        // hit array size has to be iterative. If a shape lies within another shape, that shape will be hit twice.
        //
        internal PhysX.RaycastHit RaycastNearest(OpenMetaverse.Vector3  position, OpenMetaverse.Vector3 raydirection, float searchdist)
        {
            int   buffercount = 16;
            int   maxbuffercount = 256;
            float lowestdist  = searchdist + 1.0f;
            PhysX.RaycastHit      hit  = new PhysX.RaycastHit();
            PhysX.RaycastHit[]    hits = null;
            PhysX.SceneQueryFlags oflags = PhysX.SceneQueryFlags.BlockingHit | PhysX.SceneQueryFlags.TouchingHit;

            //Increase the buffer count if the call indicates overflow. Prevent infinite loops.
            while (hits == null && buffercount <= maxbuffercount)
            {
                hits = _scene.SceneImpl.RaycastMultiple(PhysUtil.OmvVectorToPhysx(position),
                                                        PhysUtil.OmvVectorToPhysx(raydirection),
                                                        searchdist, oflags,
                                                        buffercount,
                                                        null);
                buffercount *= 2;
            }

            // Give up,something went awry.
            if (hits == null && buffercount > maxbuffercount) return null;

            int itemnum = 0;
            int itemmax = hits.Length;

            // If nothing hit, return a zero hit.
            if (itemmax == 0)
                return null;

            // Rummage through the hit list, excluding self-hits, to find the nearest shape hit.
            PhysxPrim relactor;
            foreach (var item in hits)
            {
                if (VehicleLimits.DebugRayCast)
                {
                    string sopname;

                    if (item.Shape.Actor.UserData is PhysxPrim)
                    {
                        relactor = (PhysxPrim)(item.Shape.Actor.UserData);
                        sopname = relactor.SOPName;
                    }
                    else if (item.Shape.Actor.UserData is InWorldz.PhysxPhysics.TerrainManager)
                    {
                        relactor = _actor;
                        sopname = "Terrain";
                    }
                    else
                    {
                        sopname = "??";
                    }

                    itemnum++;
                    m_log.DebugFormat("[Raycast] hits={0} of {1} dist={2} type={3} flags={4:X} name={5}", itemnum, itemmax, item.Distance, item.GetType(), item.Flags, sopname);
                }

                if (item.Shape.Actor.UserData is PhysxPrim)
                {
                    relactor = (PhysxPrim)(item.Shape.Actor.UserData);

                    // Eliminate self hits
                    if (relactor == _actor)
                        continue;
                }

                if (item.Distance < lowestdist)
                {
                    lowestdist = item.Distance;
                    hit = item;
                }
            }

            if (lowestdist <= searchdist)
                return hit;
            else
                return null;
        }
#endregion

        #region Linear Motor Force Acumulators
        //
        // Motor add force that applies the vehicle linear offset.
        //
        internal void AddForce(OpenMetaverse.Vector3 force, PhysX.ForceMode mode, bool wakeup)
        {
            OpenMetaverse.Vector3 loffset = _props.ParamsVec[VectorParams.VehicleLinearMotorOffset];

            // Only wake up the object if the forces are above the threshold. Smaller forces, though
            // affecting the object sould permit it to sleep. This stops perpetual drifting issues.
            wakeup = (Vector3.Mag(force) > VehicleLimits.ThresholdLinearMotorDeltaV) ? true : false;

            if (loffset == OpenMetaverse.Vector3.Zero)
                _actor.DynActorImpl.AddForce(PhysUtil.OmvVectorToPhysx(force), mode, wakeup);
            else
            {
                // Velocity change is not supported for this PhysX function, so simulate it.
                if (mode == PhysX.ForceMode.VelocityChange)
                {
                    force *= _actor.Mass;
                }
                _actor.DynActorImpl.AddForceAtLocalPosition(PhysUtil.OmvVectorToPhysx(force), PhysUtil.OmvVectorToPhysx(loffset), PhysX.ForceMode.Impulse, wakeup);
            }

            //m_log.DebugFormat("[VehicleMotor AddForce] force {0}", force);
        }
        #endregion

        #region Angular Motor Force Accumulators
        private bool AccumTorqueFirst;
        private OpenMetaverse.Vector3 AccumTorqueVel;
        private OpenMetaverse.Vector3 AccumTorqueImpulse;

        internal void TorqueInit()
        {
            AccumTorqueFirst   = true;
            AccumTorqueVel     = OpenMetaverse.Vector3.Zero;
            AccumTorqueImpulse = OpenMetaverse.Vector3.Zero;
        }

        internal Vector3 GetTorque()
        {
            return AccumTorqueVel;
        }

        //
        // Motor add torque. It acumulates velocity and impulse
        // torques separately so that they can be applied finally at the end of the physics frame.
        //
        internal void AddTorque(OpenMetaverse.Vector3 force, PhysX.ForceMode mode, string code)
        {
            OpenMetaverse.Vector3 torque = force;
            if (mode == PhysX.ForceMode.VelocityChange)
                AccumTorqueVel += torque;
            if (mode == PhysX.ForceMode.Impulse)
                AccumTorqueImpulse += torque;
            if (VehicleLimits.DebugAngular)
            {
                if (AccumTorqueFirst)
                {
                    m_log.DebugFormat("[----------------------]");
                    m_log.DebugFormat("[VehicleMotor BgnTorque] InitialVelocity {0}", worldAngularVel * Quaternion.Inverse(rotation));
                }
                m_log.DebugFormat("[VehicleMotor AddTorque] {2} {1} {0}", torque * Quaternion.Inverse(rotation), code, mode);
            }
            AccumTorqueFirst = false;
        }

        //
        // This is the torque finalizer, called at the end of the vehicle's physucs frame to apply velocity based or 
        // impulse based torque, if present.
        //
        internal void TorqueFini()
        {
            // Avoid sending in too small values, causes physx to do strange things.
            if (Math.Abs(AccumTorqueVel.X) < VehicleLimits.MinPhysicsForce) AccumTorqueVel.X = 0.0f;
            if (Math.Abs(AccumTorqueVel.Y) < VehicleLimits.MinPhysicsForce) AccumTorqueVel.Y = 0.0f;
            if (Math.Abs(AccumTorqueVel.Z) < VehicleLimits.MinPhysicsForce) AccumTorqueVel.Z = 0.0f;

            if (Math.Abs(AccumTorqueImpulse.X) < VehicleLimits.MinPhysicsForce) AccumTorqueImpulse.X = 0.0f;
            if (Math.Abs(AccumTorqueImpulse.Y) < VehicleLimits.MinPhysicsForce) AccumTorqueImpulse.Y = 0.0f;
            if (Math.Abs(AccumTorqueImpulse.Z) < VehicleLimits.MinPhysicsForce) AccumTorqueImpulse.Z = 0.0f;
            
            Boolean vwake = (Vector3.Mag(AccumTorqueVel) > VehicleLimits.ThresholdAngularMotorDeltaV) ? true : false;
            if (AccumTorqueVel != OpenMetaverse.Vector3.Zero)
                _actor.DynActorImpl.AddTorque(PhysUtil.OmvVectorToPhysx(AccumTorqueVel), PhysX.ForceMode.VelocityChange, vwake);
            
            Boolean iwake = (Vector3.Mag(AccumTorqueImpulse) > VehicleLimits.ThresholdAngularMotorDeltaV) ? true : false;
            if (AccumTorqueImpulse != OpenMetaverse.Vector3.Zero)
                _actor.DynActorImpl.AddTorque(PhysUtil.OmvVectorToPhysx(AccumTorqueImpulse), PhysX.ForceMode.Impulse, iwake);

            if (VehicleLimits.DebugAngular && ((AccumTorqueImpulse != OpenMetaverse.Vector3.Zero) || (AccumTorqueVel != OpenMetaverse.Vector3.Zero)))
                m_log.DebugFormat("[VehicleMotor FinTorque] vel={0} impulse={1}", AccumTorqueVel * Quaternion.Inverse(rotation), AccumTorqueImpulse * Quaternion.Inverse(rotation));
            
            AccumTorqueVel     = OpenMetaverse.Vector3.Zero;
            AccumTorqueImpulse = OpenMetaverse.Vector3.Zero;
            AccumTorqueFirst   = true;
        }
        #endregion

        //
        // Motor simulation entry point.
        //
        internal void Simulate(float timeStep, uint frameNum, OpenMetaverse.Vector3 aforces)
        {
            // Called every physics frame while the object is awake to apply linear and angular motor forces.
            // The two motors have two exponential shifts that occur in parallel. Once is the ramping
            // driven by the motor timescale, and another is the decay which determines the
            // effectiveness across the decay time scale.
            //
            // The ramping ranges from the current velocity to the goal velocity.
            // The decay ranges from the goal velocity to zero.
            OpenMetaverse.Vector3 rfactor;
            OpenMetaverse.Vector3 dfactor;
            OpenMetaverse.Vector3 lastvel;
            OpenMetaverse.Vector3 adjvel;
            OpenMetaverse.Vector3 newvel;
            OpenMetaverse.Vector3 worldvel;
            OpenMetaverse.Vector3 timescale;
            OpenMetaverse.Vector3 dirsign;

            this.timeStep = timeStep;
            float buoy = _props.ParamsFloat[FloatParams.VehicleBuoyancy];
            float hoverts = _props.ParamsFloat[FloatParams.VehicleHoverTimescale];

            // -------------------------------------------------------------------------------------------
            // Raycast beneath the vehicle
            //
            // Perform a downlooking ray cast eight times a second to see if the vehicle is touching the terrain or a surface.
            // This is used further in the pipelne to determine whether certain motors should be disabled.
            // Example: cars should not be able to steer if they are hurtling through the air.
            // Skip these tests if the vehicle has hover or positive buoyacy set or if it is not moving appreciably.
            //
            Boolean DisableMotors = false;
            float motorheightlimit = _props.ParamsFloat[FloatParams.VehicleDisableMotorsAbove];

            // Disable in-air checking if the vehicle has a hover or positive buoyancy going or is not moving much.
            if (hoverts < VehicleLimits.MaxHoverTimescale || buoy > 0.0f || Vector3.Mag(localLinearVel) < 0.5f)
            {
                HitDist = 0;
                motorheightlimit = 0;
            }

            // Check every 1/8 second.
            if (_actor.OBBobject != null && motorheightlimit > 0 && (frameNum % VehicleLimits.NumTimeStepsPerRayCast) == 0)
            {
                PhysX.RaycastHit hit;
                Vector3 apos    = _actor.Position + _actor.OBBobject.Center * rotation;
                Vector3 rdown   = new Vector3(0, 0, -1) * rotation;
                Vector3 zadj    = new Vector3(0, 0, 1.0f) * rotation;
                Vector3 dleft   = new Vector3(0,  _actor.OBBobject.Extent.Y, -_actor.OBBobject.Extent.Z) * rotation;
                Vector3 dright  = new Vector3(0, -_actor.OBBobject.Extent.Y, -_actor.OBBobject.Extent.Z) * rotation;
                Vector3 dup     = new Vector3(0, 0, _actor.OBBobject.Extent.Z) * rotation;
                Vector3 ddown   = new Vector3(0, 0, -_actor.OBBobject.Extent.Z) * rotation;

                if (VehicleLimits.DebugRayCast) m_log.InfoFormat("[Vehicle Raycast] bounds={0} center={5} left={1} right={2} up={3} down={4}", _actor.OBBobject.Extent, dleft, dright, dup, ddown, _actor.OBBobject.Center);

                // Perform a ray cast along the left edge
                hit = RaycastNearest(apos + dleft + zadj, rdown, motorheightlimit * 3);
                if (hit != null)
                {
                    HitDist = hit.Distance - zadj.Z;
                    if (VehicleLimits.DebugRayCast) m_log.DebugFormat("[Vehicle Raycast] left hit={0}", HitDist);
                }
                else
                {
                    if (VehicleLimits.DebugRayCast) m_log.DebugFormat("[Vehicle Raycast] left miss");
                    HitDist = motorheightlimit + 1.0f;
                }

                // If there is no hit on the left, try the right side
                if (HitDist > motorheightlimit)
                {
                    hit = RaycastNearest(apos + dright + zadj, rdown, motorheightlimit * 3);
                    if (hit != null)
                    {
                        HitDist = hit.Distance - zadj.Z;
                        if (VehicleLimits.DebugRayCast) m_log.DebugFormat("[Vehicle Raycast] right hit={0}", HitDist);
                    }
                    else
                    {
                        if (VehicleLimits.DebugRayCast) m_log.DebugFormat("[Vehicle Raycast] right miss");
                        HitDist = motorheightlimit + 1.0f;
                    }
                }
            }

            // Decay the motors if the vehicle is above the limit and it is moving.
            if (HitDist > motorheightlimit)
            {
                //m_log.DebugFormat("[Vehicle Raycast] dist={0} vel={1}", HitDist, Vector3.Mag(_props.Dynamics.LocalLinearVelocity));

                HeightExceededTime += timeStep * VehicleLimits.NumTimeStepsPerRayCast;
                float disableAfter = _props.ParamsFloat[FloatParams.VehicleDisableMotorsAfter];
                if (HeightExceededTime >= disableAfter)
                    DisableMotors = true;
            }
            else
            {
                HeightExceededTime = 0.0f;
            }


            //--------------------------------------------------------------------------------------------------
            //  Linear Motor Simulation
            //
            lastvel = localLinearVel;
            adjvel  = lastvel;

            dirsign.X = Mathz.PosNeg(_props.Dynamics.LinearDirection.X);
            dirsign.Y = Mathz.PosNeg(_props.Dynamics.LinearDirection.Y);
            dirsign.Z = Mathz.PosNeg(_props.Dynamics.LinearDirection.Z);

            // A target velocity is used in cases when the velocity is lower because high friction 
            // during low speeds causes velocity changes to be lost. The target ensures forward progress.
            if (dirsign.X * (_props.Dynamics.LinearTargetVelocity.X - adjvel.X) >= 0) adjvel.X = _props.Dynamics.LinearTargetVelocity.X;
            if (dirsign.Y * (_props.Dynamics.LinearTargetVelocity.Y - adjvel.Y) >= 0) adjvel.Y = _props.Dynamics.LinearTargetVelocity.Y;
            if (dirsign.Z * (_props.Dynamics.LinearTargetVelocity.Z - adjvel.Z) >= 0) adjvel.Z = _props.Dynamics.LinearTargetVelocity.Z;
            
            
            // Compute rampup factors for the linear motor.
            timescale = _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale];
            rfactor.X = GetGrowthRate(adjvel.X, _props.Dynamics.LinearDirection.X, timescale.X/timeStep);
            rfactor.Y = GetGrowthRate(adjvel.Y, _props.Dynamics.LinearDirection.Y, timescale.Y/timeStep);
            rfactor.Z = GetGrowthRate(adjvel.Z, _props.Dynamics.LinearDirection.Z, timescale.Z/timeStep);

            // Compute the decay factor then step to the next index.
            timescale = _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale];
            dfactor.X = MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.X);
            dfactor.Y = MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.Y);
            dfactor.Z = MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.Z);
            _props.Dynamics.LinearDecayIndex += timeStep;

            // If the decay hits maximum entropy no futher computations are required.
            if (OpenMetaverse.Vector3.Mag(dfactor) == 0)
            {
                // Reset the target velocities.
                _props.Dynamics.LinearTargetVelocity = localLinearVel;
            }
            else
            {
                // Compute a new velocity based on the current velocity, the target velocity and timescale
                // This is a stepwise iteration of the continuous a*b^(t*Tau) function.
                newvel.X = (adjvel.X + adjvel.X * rfactor.X);
                newvel.Y = (adjvel.Y + adjvel.Y * rfactor.Y);
                newvel.Z = (adjvel.Z + adjvel.Z * rfactor.Z);

                // If at crossover point,flip to indicate velocity is growing.
                if (rfactor.X == VehicleLimits.ThresholdInverseCrossover) rfactor.X = -rfactor.X;
                if (rfactor.Y == VehicleLimits.ThresholdInverseCrossover) rfactor.Y = -rfactor.Y;
                if (rfactor.Z == VehicleLimits.ThresholdInverseCrossover) rfactor.Z = -rfactor.Z;

                // Avoid zero velocity stiction when the new velocity is increasing.
                if (rfactor.X > 0 && _props.Dynamics.LinearDirection.X != 0 && Math.Abs(newvel.X) < VehicleLimits.ThresholdLinearMotorDeltaV)
                    newvel.X = dirsign.X * VehicleLimits.ThresholdLinearMotorDeltaV * 8;
                if (rfactor.Y > 0 && _props.Dynamics.LinearDirection.Y != 0 && Math.Abs(newvel.Y) < VehicleLimits.ThresholdLinearMotorDeltaV)
                    newvel.Y = dirsign.Y * VehicleLimits.ThresholdLinearMotorDeltaV * 8;
                if (rfactor.Z > 0 && _props.Dynamics.LinearDirection.Z != 0 && Math.Abs(newvel.Z) < VehicleLimits.ThresholdLinearMotorDeltaV)
                    newvel.Z = dirsign.Z * VehicleLimits.ThresholdLinearMotorDeltaV * 8;

                // Compute new target velocities.
                // When the motor is ramping towards zero, the target is set to the lower of the computed new velocity or previous target velocity.
                // When ramping away from zero, the target is set to the lower of the linear direction or the new velocity.
                if (_props.Dynamics.LinearDirection.X * newvel.X < 0 || rfactor.X < 0)
                    _props.Dynamics.LinearTargetVelocity.X = newvel.X;
                else
                    _props.Dynamics.LinearTargetVelocity.X = Utils.Clamp(newvel.X, -Math.Abs(_props.Dynamics.LinearDirection.X), Math.Abs(_props.Dynamics.LinearDirection.X));

                if (_props.Dynamics.LinearDirection.Y * newvel.Y < 0 || rfactor.Y < 0)
                    _props.Dynamics.LinearTargetVelocity.Y = newvel.Y;
                else
                    _props.Dynamics.LinearTargetVelocity.Y = Utils.Clamp(newvel.Y, -Math.Abs(_props.Dynamics.LinearDirection.Y), Math.Abs(_props.Dynamics.LinearDirection.Y));

                if (_props.Dynamics.LinearDirection.Z * newvel.Z < 0 || rfactor.Z < 0)
                    _props.Dynamics.LinearTargetVelocity.Z = newvel.Z;
                else
                    _props.Dynamics.LinearTargetVelocity.Z = Utils.Clamp(newvel.Z, -Math.Abs(_props.Dynamics.LinearDirection.Z), Math.Abs(_props.Dynamics.LinearDirection.Z));

                // Limit max velocities
                newvel.X = Utils.Clamp(newvel.X, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);
                newvel.Y = Utils.Clamp(newvel.Y, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);
                newvel.Z = Utils.Clamp(newvel.Z, -VehicleLimits.MaxLinearVelocity, VehicleLimits.MaxLinearVelocity);

                // Release motors when the delta is zero and the direction is zero.
                if (_props.Dynamics.LinearTargetVelocity.X == 0 && _props.Dynamics.LinearDirection.X == 0) newvel.X = lastvel.X;
                if (_props.Dynamics.LinearTargetVelocity.Y == 0 && _props.Dynamics.LinearDirection.Y == 0) newvel.Y = lastvel.Y;
                if (_props.Dynamics.LinearTargetVelocity.Z == 0 && _props.Dynamics.LinearDirection.Z == 0) newvel.Z = lastvel.Z;

                //m_log.DebugFormat("[VehicleMotor] Linear Simulate: ramp={0} decay={1} nvel={2} tvel={5} ts={3} dindex={4}", rfactor, dfactor, newvel, timescale,  _props.Dynamics.LinearDecayIndex, _props.Dynamics.LinearTargetVelocity);

                // If the local velocity along any axis is greater than the motor speed for that axis, use
                // the velocity (meaning the motor is not adding any power).
                if (rfactor.X >= 0 && (dirsign.X * (lastvel.X - newvel.X) > 0)) dfactor.X = 0;
                if (rfactor.Y >= 0 && (dirsign.Y * (lastvel.Y - newvel.Y) > 0)) dfactor.Y = 0;
                if (rfactor.Z >= 0 && (dirsign.Z * (lastvel.Z - newvel.Z) > 0)) dfactor.Z = 0;

                // Accelerate the decay when stalled.
                if (OpenMetaverse.Vector3.Mag(dfactor) != 0 && IsLinearMotorStalled())
                {
                    dfactor = OpenMetaverse.Vector3.Zero;
                    _props.Dynamics.LinearDecayIndex = VehicleLimits.MaxDecayTimescale * 100.0f;
                    _props.Dynamics.LinearTargetVelocity = OpenMetaverse.Vector3.Zero;
                    //m_log.DebugFormat("[VehicleMotor] LinearMotorSimulate: stalled");
                }

                // Apply decays to each axis.
                newvel.X = (newvel.X * dfactor.X) + lastvel.X * (1.0f - dfactor.X);
                newvel.Y = (newvel.Y * dfactor.Y) + lastvel.Y * (1.0f - dfactor.Y);
                newvel.Z = (newvel.Z * dfactor.Z) + lastvel.Z * (1.0f - dfactor.Z);

                //m_log.DebugFormat("[VehicleMotor] Linear: ramp={0}", rfactor);
                //m_log.DebugFormat("[VehicleMotor] Linear: ramp={0} decay={1} nvel={2} tvel={5} dir={6} ts={3} dindex={4}", rfactor, dfactor, newvel, timescale,  _props.Dynamics.LinearDecayIndex, _props.Dynamics.LinearTargetVelocity, _props.Dynamics.LinearDirection);
                // Switch back to world coords
                worldvel = newvel * rotation;

                // If the force drops below the threshold, do nothing to let the vehicle eventually sleep.
                if (Math.Abs(worldvel.X) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                    Math.Abs(worldvel.Y) >= VehicleLimits.ThresholdLinearMotorDeltaV ||
                    Math.Abs(worldvel.Z) >= VehicleLimits.ThresholdLinearMotorDeltaV)
                {
                    // Convert to deltaV.
                    worldvel -= worldLinearVel;

                    // Limit motor up
                    if ((_props.Flags & VehicleFlags.LimitMotorUp) != 0)
                    {
                        if (worldvel.Z > 0) worldvel.Z = 0;
                    }

                    // Limit motor down
                    if ((_props.Flags & VehicleFlags.LimitMotorDown) != 0)
                    {
                        if (worldvel.Z < 0) worldvel.Z = 0;
                    }

                    // Gravity check.Any up Z-forces less than the effective gravity are nulled.
                    if (worldvel.Z > 0)
                    {
                        worldvel.Z += Settings.Instance.Gravity * (1.0f - buoy);
                        if (worldvel.Z < 0) worldvel.Z = 0;
                    }

                    // Hover moderation. If hover is present, accelerate the motor decay.
                    if (Math.Abs(worldvel.Z) > VehicleLimits.ThresholdLinearMotorDeltaV && 
                        (_props.Flags & (VehicleFlags.HoverGlobalHeight | VehicleFlags.HoverTerrainOnly | VehicleFlags.HoverWaterOnly)) != 0 &&
                        hoverts < VehicleLimits.MaxHoverTimescale)
                    {
                        _props.Dynamics.LinearDecayIndex += 10.0f;
                    }

                    if (!DisableMotors)
                    {
                        // Apply a force required to make this change in one physics frame
                        AddForce(worldvel, PhysX.ForceMode.VelocityChange, true);
                        if (VehicleLimits.DebugLinear) m_log.DebugFormat("[VehicleMotor] Linear Simulate: ramp={0} decay={1} nvel={2} lvel={4} dindex={3}", rfactor, dfactor, worldvel * Quaternion.Inverse(rotation), _props.Dynamics.LinearDecayIndex, lastvel);
                    }
                }
            }


            //--------------------------------------------------------------------------------------------------
            // Angular Motor Simulation
            //
            worldvel = OpenMetaverse.Vector3.Zero;
            float angularz = 0;

            // Compute the decay factor then step to the next index.
            timescale = _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale];
            dfactor.X = MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.X);
            dfactor.Y = MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.Y);
            dfactor.Z = MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.Z);
            _props.Dynamics.AngularDecayIndex += timeStep;

            // If the decay hits maximum entropy no futher computations are required.
            if (OpenMetaverse.Vector3.Mag(dfactor) == 0 || DisableMotors)
            {
                _props.Dynamics.AngularTargetVelocity = OpenMetaverse.Vector3.Zero;
            }
            else
            {
                lastvel   = localAngularVel;
                OpenMetaverse.Vector3 ztorque = OpenMetaverse.Vector3.Zero;

                // Preflight world z-rotation mode.
                if ((_props.Flags & VehicleFlags.TorqueWorldZ) != 0)
                {
                    // Remove the world Z-rotation before converting the vehicle's rotation to local.
                    lastvel   = worldAngularVel;
                    ztorque.Z = lastvel.Z;
                    lastvel.Z = 0;
                    lastvel = lastvel * OpenMetaverse.Quaternion.Inverse(rotation);

                    // Save any velocity rotated into the local Z-axis.
                    // Replace the Z rotation with the world one for the rest of the calcs.
                    ztorque.X = lastvel.Z;
                    lastvel.Z = ztorque.Z;
                }

                adjvel = lastvel;

                dirsign.X = Mathz.PosNeg(_props.Dynamics.AngularDirection.X);
                dirsign.Y = Mathz.PosNeg(_props.Dynamics.AngularDirection.Y);
                dirsign.Z = Mathz.PosNeg(_props.Dynamics.AngularDirection.Z);

                // A target velocity is used in cases when the velocity is lower because high friction 
                // during low speeds causes velocity changes to be lost. The target ensures forward progress.
                if (dirsign.X * (_props.Dynamics.AngularTargetVelocity.X - adjvel.X) > 0) adjvel.X = _props.Dynamics.AngularTargetVelocity.X;
                if (dirsign.Y * (_props.Dynamics.AngularTargetVelocity.Y - adjvel.Y) > 0) adjvel.Y = _props.Dynamics.AngularTargetVelocity.Y;
                if (dirsign.Z * (_props.Dynamics.AngularTargetVelocity.Z - adjvel.Z) > 0) adjvel.Z = _props.Dynamics.AngularTargetVelocity.Z;

                // Compute rampup factors for the angular motor.
                timescale = _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale];
                rfactor.X = GetGrowthRate(adjvel.X, _props.Dynamics.AngularDirection.X, timescale.X/timeStep);
                rfactor.Y = GetGrowthRate(adjvel.Y, _props.Dynamics.AngularDirection.Y, timescale.Y/timeStep);
                rfactor.Z = GetGrowthRate(adjvel.Z, _props.Dynamics.AngularDirection.Z, timescale.Z/timeStep);
           
                // Compute a new velocity based on the current velocity and timescale
                // This is a stepwise iteration of the continuous a*b^(t*Tau) function.
                newvel.X  = (adjvel.X + adjvel.X * rfactor.X);
                newvel.Y  = (adjvel.Y + adjvel.Y * rfactor.Y);
                newvel.Z  = (adjvel.Z + adjvel.Z * rfactor.Z);
                
                // If at crossover point,flip to indicate velocity is growing.
                if (rfactor.X == VehicleLimits.ThresholdInverseCrossover) rfactor.X = -rfactor.X;
                if (rfactor.Y == VehicleLimits.ThresholdInverseCrossover) rfactor.Y = -rfactor.Y;
                if (rfactor.Z == VehicleLimits.ThresholdInverseCrossover) rfactor.Z = -rfactor.Z;

                // Avoid zero velocity stiction when the new velocity is increasing.
                if (rfactor.X > 0 && _props.Dynamics.AngularDirection.X != 0 && Math.Abs(newvel.X) < VehicleLimits.ThresholdAngularMotorDeltaV)
                    newvel.X  = dirsign.X * VehicleLimits.ThresholdAngularMotorDeltaV * 8;
                if (rfactor.Y > 0 && _props.Dynamics.AngularDirection.Y != 0 && Math.Abs(newvel.Y) < VehicleLimits.ThresholdAngularMotorDeltaV)
                    newvel.Y  = dirsign.Y * VehicleLimits.ThresholdAngularMotorDeltaV * 8;
                if (rfactor.Z > 0 && _props.Dynamics.AngularDirection.Z != 0 && Math.Abs(newvel.Z) < VehicleLimits.ThresholdAngularMotorDeltaV)
                    newvel.Z  = dirsign.Z * VehicleLimits.ThresholdAngularMotorDeltaV * 8;
                //m_log.DebugFormat("[VehicleMotor] Angular1: ramp={0} decay={1}/{6} nvel={2} lvel={3} tvel={4} dir={5}", rfactor, dfactor, newvel, lastvel, _props.Dynamics.AngularTargetVelocity, _props.Dynamics.AngularDirection,_props.Dynamics.AngularDecayIndex);


                // Interactions with vertical attractor - limit the angular velocity to not over power
                // the vertical attractor. This fixes bad scripts with extreme banking velocities
                // but it tries to leave well behaved scripts alone.
                float vtimescale  = Math.Max(_props.ParamsFloat[FloatParams.VehicleVerticalAttractionTimescale], timeStep);
                float vefficiency = _props.ParamsFloat[FloatParams.VehicleVerticalAttractionEfficiency];
                if (vtimescale < VehicleLimits.MaxAttractTimescale)
                {
                    float vvel;

                    // These are empirically determined factors.
                    float velclamp = (float)Math.PI;

                    // These are the primary velocity clamp points for the various vehicle types.
                    // Angular velocities below the clamp point are left untouched.
                    // The clamp point represents the maximum velocity permissible given a vertical
                    // attraction timescale of 1.0s. The clamp point drops linearly when the timescale
                    // is below 1.0s.
                    if (_props.Type == VehicleType.Car || _props.Type == VehicleType.Motorcycle)
                        velclamp = (float)Math.PI * 1.1f; // 1.24f
                    else if (_props.Type == VehicleType.Boat || _props.Type == VehicleType.Sailboat)
                        velclamp = (float)Math.PI * 0.95f;
                    else
                        velclamp = (float)Math.PI * 0.8f; //  0.35f;

                    // Lower the clamp point with time scales less than 1.0.
                    if (vtimescale < 1.0f)
                    {
                        velclamp = velclamp * vtimescale;
                    }

                    // The clamp is not absolute. Velocities above the clamp point are reduced by a large logarithmic factor
                    // adjusted by the vertical attactor timescale.
                    // Compute the above clamp factor velocity factor.
                    if (vtimescale < 1.0f)
                        vtimescale = 1.0f;
                    float voverthrust = (float)(Math.Log(vtimescale + 0.06) / Math.Log(VehicleLimits.MaxAttractTimescale));


                    //m_log.DebugFormat("[VehicleAdjust] velx={0} vclamp={1} vover={2} vts={3} veff={4}", newvel.X, velclamp, voverthrust, vtimescale, vefficiency);

                    vvel = Utils.Clamp(newvel.X, -velclamp, velclamp);
                    if (newvel.X != vvel) vvel = vvel + (newvel.X - vvel) * voverthrust;
                    newvel.X = vvel;

                    vvel = Utils.Clamp(newvel.Y, -velclamp, velclamp);
                    if (newvel.Y != vvel) vvel = vvel + (newvel.Y - vvel) * voverthrust;
                    newvel.Y = vvel;
                }

                // Compute new target velocities. The target is set to the computed new velocity when the motor is ramping towards zero.
                // When ramping away from zero, the target is set to the lower of the Angular direction or the new velocity.
                if (_props.Dynamics.AngularDirection.X * newvel.X < 0 || rfactor.X < 0)
                    _props.Dynamics.AngularTargetVelocity.X = newvel.X;
                else
                    _props.Dynamics.AngularTargetVelocity.X = Utils.Clamp(newvel.X, -Math.Abs(_props.Dynamics.AngularDirection.X), Math.Abs(_props.Dynamics.AngularDirection.X));

                if (_props.Dynamics.AngularDirection.Y * newvel.Y < 0 || rfactor.Y < 0)
                    _props.Dynamics.AngularTargetVelocity.Y = newvel.Y;
                else
                    _props.Dynamics.AngularTargetVelocity.Y = Utils.Clamp(newvel.Y, -Math.Abs(_props.Dynamics.AngularDirection.Y), Math.Abs(_props.Dynamics.AngularDirection.Y));

                if (_props.Dynamics.AngularDirection.Z * newvel.Z < 0 || rfactor.Z < 0)
                    _props.Dynamics.AngularTargetVelocity.Z = newvel.Z;
                else
                    _props.Dynamics.AngularTargetVelocity.Z = Utils.Clamp(newvel.Z, -Math.Abs(_props.Dynamics.AngularDirection.Z), Math.Abs(_props.Dynamics.AngularDirection.Z));

                // Limit max velocities
                newvel.X = Utils.Clamp(newvel.X, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                newvel.Y = Utils.Clamp(newvel.Y, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);
                newvel.Z = Utils.Clamp(newvel.Z, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);

                // Release motors when the delta is zero and the direction is zero.
                if (dfactor.X == 0 && _props.Dynamics.AngularDirection.X == 0) newvel.X = lastvel.X;
                if (dfactor.Y == 0 && _props.Dynamics.AngularDirection.Y == 0) newvel.Y = lastvel.Y;
                if (dfactor.Z == 0 && _props.Dynamics.AngularDirection.Z == 0) newvel.Z = lastvel.Z;

                //m_log.DebugFormat("[VehicleMotor] Angular: ramp={0}", rfactor); 
                //m_log.DebugFormat("[VehicleMotor] Angular: ramp={0} decay={1}/{5} nvel={2} lvel={3} dir={4}", rfactor, dfactor, newvel, _props.Dynamics.LocalAngularVelocity, _props.Dynamics.AngularDirection,_props.Dynamics.AngularDecayIndex);

                // If the local velocity along any axis is greater than the motor speed for that axis, use
                // the velocity (meaning the motor is not adding any power).
                if ((aforces.X != 0 && _props.Dynamics.AngularDirection.X == 0) || (rfactor.X >= 0 && (dirsign.X * (lastvel.X - newvel.X) > 0))) dfactor.X = 0;
                if ((aforces.Y != 0 && _props.Dynamics.AngularDirection.Y == 0) || (rfactor.Y >= 0 && (dirsign.Y * (lastvel.Y - newvel.Y) > 0))) dfactor.Y = 0;
                if ((aforces.Z != 0 && _props.Dynamics.AngularDirection.Z == 0) || (rfactor.Z >= 0 && (dirsign.Z * (lastvel.Z - newvel.Z) > 0))) dfactor.Z = 0;

                // Accelerate the angular decay when the linear motor stalled.
                if (OpenMetaverse.Vector3.Mag(dfactor) != 0 && IsLinearMotorStalled())
                {
               
                    dfactor = OpenMetaverse.Vector3.Zero;
                    _props.Dynamics.AngularDecayIndex = VehicleLimits.MaxDecayTimescale * 100.0f;
                    _props.Dynamics.AngularTargetVelocity = OpenMetaverse.Vector3.Zero;
                }

                // Apply decays to each axis.
                newvel.X = (newvel.X * dfactor.X) + lastvel.X * (1.0f - dfactor.X);
                newvel.Y = (newvel.Y * dfactor.Y) + lastvel.Y * (1.0f - dfactor.Y);
                newvel.Z = (newvel.Z * dfactor.Z) + lastvel.Z * (1.0f - dfactor.Z);

                //m_log.DebugFormat("[VehicleMotor] local Angular: force={0} decay={1} lastvel={2}", newvel, dfactor, lastvel);
                // If working with split z forces...
                if ((_props.Flags & VehicleFlags.TorqueWorldZ) != 0)
                {
                    // Switch back to world coords
                    ztorque.Z = newvel.Z;   // This is really world Z-forces
                    newvel.Z  = ztorque.X;  // Restore the original local Z-forces
                    newvel.Z = 0;
                    ztorque.X = 0;

                    worldvel = newvel * rotation;
                    worldvel += ztorque;
                    //worldvel.Z = ztorque.Z;
                    //m_log.DebugFormat("[VehicleMotor] angular split worldvel={0} newvel={1} ztorque={2}", worldvel, newvel, ztorque);
                }
                else
                {
                    // Switch back to world coords
                    worldvel = newvel * rotation;
                }

                // Convert to deltaV.
                worldvel -= worldAngularVel;

                // Save Z forces until after banking has been determined, since they have to be blended.
                angularz = worldvel.Z;
                worldvel.Z = 0;

                // If the force drops below the threshold, do nothing to let the vehicle eventually sleep.
                if (Math.Abs(worldvel.X) >= VehicleLimits.ThresholdAngularMotorDeltaV ||
                    Math.Abs(worldvel.Y) >= VehicleLimits.ThresholdAngularMotorDeltaV ||
                    Math.Abs(worldvel.Z) >= VehicleLimits.ThresholdAngularMotorDeltaV)
                {
                    // Apply a force required to make this change in one physics frame
                    AddTorque(worldvel, PhysX.ForceMode.VelocityChange, "motor");
                    if (VehicleLimits.DebugAngular) m_log.DebugFormat("[VehicleMotor] Angular: ramp={0} decay={1} force={2} lvel={3} tvel={4} dir={5} dindex={6}", rfactor, dfactor, worldvel, _props.Dynamics.LocalAngularVelocity, _props.Dynamics.AngularTargetVelocity, _props.Dynamics.AngularDirection, _props.Dynamics.AngularDecayIndex);
                }
            }


            //--------------------------------------------------------------------------------------------------
            // Banking turn Motor Simulation
            // The banking turn has up-ramp timescale but no decay timescale. The banking turn specifies
            // a minimum turning rate. It is up to friction and the vertical attractor to decay the induced velocity.
            //
            float btimescale = _props.ParamsFloat[FloatParams.VehicleBankingTimescale];
            float bnewvel = 0;

            if (_props.Dynamics.BankingDirection != 0 && btimescale < VehicleLimits.MaxTimescale)
            {
                // Start with the current velocity.
                float blastvel = worldAngularVel.Z;
                float badjvel;
                float bfactor;
                float dirbsign;

                badjvel = blastvel;
                dirbsign = Mathz.PosNeg(_props.Dynamics.BankingDirection);

                // A target velocity is used in cases when the velocity is lower because high friction 
                // during low speeds causes velocity changes to be lost. The target ensures forward progress.
                if (dirbsign * (_props.Dynamics.BankingTargetVelocity - badjvel) > 0) badjvel = _props.Dynamics.BankingTargetVelocity;

                // Compute rampup factor for the bank-to-turn motor.
                bfactor = GetGrowthRate(badjvel, _props.Dynamics.BankingDirection, btimescale / timeStep);
                bnewvel = (badjvel + badjvel * bfactor);

                // If the angular motor is engaged, it reduces the max banking velocity.
                if (_props.Dynamics.AngularDecayIndex < VehicleLimits.ThresholdAngularMotorEngaged)
                {
                    if (Math.Abs(bnewvel) > VehicleLimits.MaxLegacyAngularVelocity)
                        bnewvel = VehicleLimits.MaxLegacyAngularVelocity * Mathz.PosNeg(bnewvel);
                }

                // If at crossover point,flip to indicate velocity is growing.
                if (bfactor == VehicleLimits.ThresholdInverseCrossover) bfactor = -bfactor;

                // Avoid zero velocity stiction when the new velocity is increasing.
                if (bfactor > 0 && Math.Abs(bnewvel) < VehicleLimits.ThresholdAngularMotorDeltaV)
                    bnewvel = dirbsign * VehicleLimits.ThresholdAngularMotorDeltaV * 8;

                // Compute new target banking velocity. The target is set to the computed new velocity when the motor is ramping towards zero.
                // When ramping away from zero, the target is set to the lower of the Angular direction or the new velocity.
                if (_props.Dynamics.BankingDirection * bnewvel < 0 || bfactor < 0)
                    _props.Dynamics.BankingTargetVelocity = bnewvel;
                else
                    _props.Dynamics.BankingTargetVelocity = Utils.Clamp(bnewvel,  -Math.Abs(_props.Dynamics.BankingDirection), Math.Abs(_props.Dynamics.BankingDirection));

                // Limit max velocities
                bnewvel = Utils.Clamp(bnewvel, -VehicleLimits.MaxAngularVelocity, VehicleLimits.MaxAngularVelocity);

                //m_log.DebugFormat("[VehicleMotor] Angular: ramp={0} decay={1}/{5} nvel={2} tvel={3} dir={4}", rfactor, dfactor, newvel, _props.Dynamics.AngularTargetVelocity, _props.Dynamics.AngularDirection,_props.Dynamics.AngularDecayIndex);
                //m_log.DebugFormat("[VehicleMotor] Angular: ramp={0} decay={1}/{5} nvel={2} lvel={3} dir={4}", rfactor, dfactor, newvel, _props.Dynamics.LocalAngularVelocity, _props.Dynamics.AngularDirection,_props.Dynamics.AngularDecayIndex);

                // If the local velocity along any axis is greater than the motor speed for that axis, use
                // the velocity (meaning the motor is not adding any power).
                if (bfactor >= 0 && (dirbsign * (blastvel - bnewvel) > 0)) bnewvel = blastvel;

                // Kill the banking motor when the linear motor stalled.
                if (IsLinearMotorStalled())
                {
                    _props.Dynamics.BankingTargetVelocity = 0.0f;
                    bnewvel = blastvel;
                }

                // Convert to deltaV.
                bnewvel -= blastvel;
            }
            
            // Banking direction is zero.
            else
            {
                _props.Dynamics.BankingTargetVelocity = 0.0f;
                bnewvel = 0;
            }


            // Blend angular Z and banking forces.
            worldvel = OpenMetaverse.Vector3.Zero;
            worldvel.Z = bnewvel + angularz;

            // If the force drops below the threshold, do nothing to let the vehicle eventually sleep.
            if (Math.Abs(worldvel.Z) >= VehicleLimits.ThresholdAngularMotorDeltaV)
            {
                if (VehicleLimits.DebugBlendedZ|| VehicleLimits.DebugAngular) 
                    m_log.DebugFormat("[VehicleMotor] BlendedZ bankz={0} angularz={1} lastz={2} bankT={3} angularT={4}", 
                    bnewvel, angularz, worldAngularVel.Z, _props.Dynamics.BankingTargetVelocity, _props.Dynamics.AngularTargetVelocity.Z);

                // Apply a force required to make this change in one physics frame
                AddTorque(worldvel, PhysX.ForceMode.VelocityChange, "angz+banking");
            }
        }

        internal void SetLinearOffset(OpenMetaverse.Vector3 direction)
        {
            // Change center of linear force.
            // The offset is implemented in the motors.
        }

        internal void MoveLinear(OpenMetaverse.Vector3 direction)
        {
            if (_props.Type == VehicleType.None) return;
            if (_actor.DynActorImpl == null) return;

            _props.Dynamics.LastAccessTOD     = DateTime.Now;

            // Scale the target if the motor has been decaying.
            if (_props.Dynamics.LinearDecayIndex > VehicleLimits.ThresholdLinearMotorEngaged)
            {
                OpenMetaverse.Vector3 timescale = _props.ParamsVec[VectorParams.VehicleLinearMotorDecayTimescale];
                _props.Dynamics.LinearTargetVelocity.X *= MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.X);
                _props.Dynamics.LinearTargetVelocity.Y *= MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.Y);
                _props.Dynamics.LinearTargetVelocity.Z *= MovementExpDecay(_props.Dynamics.LinearDecayIndex, timescale.Z);
                if (VehicleLimits.DebugLinearMotor) m_log.DebugFormat("[MoveLinear] scale target dindex={0} tvel={1}", _props.Dynamics.LinearDecayIndex, _props.Dynamics.LinearTargetVelocity);
            }
            
            _props.Dynamics.LinearDecayIndex  = 0.0f;

            // SL cheat, ramp and decay should proceed simultaneously, but short decays often will prevent
            // motor ramp-up altogether. The cheat is to delay the decay by a tiny amount.
            float moahfubar = 0.0f;
            Vector3 mts = _props.ParamsVec[VectorParams.VehicleLinearMotorTimescale];
            if (Vector3.Mag(mts) < 0.9)
                moahfubar =  1.0f - Vector3.Mag(mts);
            _props.Dynamics.LinearDecayIndex = VehicleLimits.MinPhysicsTimestep * (1.0f-moahfubar) - VehicleLimits.ThresholdDelayFubar * moahfubar;

            _props.Dynamics.TargetLinearDelta = direction - _props.Dynamics.LinearDirection;
            _props.Dynamics.LinearDirection   = direction;
            _actor.WakeUp();
            if (VehicleLimits.DebugLinearMotor) m_log.DebugFormat("[MoveLinear] {0} mf={1} tvel={2} name={3}", direction, moahfubar, _props.Dynamics.LinearTargetVelocity, _actor.SOPName);
        }

        internal void MoveAngular(OpenMetaverse.Vector3 direction)
        {
            if (_props.Type == VehicleType.None) return;
            if (_actor.DynActorImpl == null) return;

            _props.Dynamics.LastAccessTOD     = DateTime.Now;

            // Scale the target if the motor has been decaying.
            if (_props.Dynamics.AngularDecayIndex > VehicleLimits.ThresholdAngularMotorEngaged)
            {
                OpenMetaverse.Vector3 timescale = _props.ParamsVec[VectorParams.VehicleAngularMotorDecayTimescale];
                _props.Dynamics.AngularTargetVelocity.X *= MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.X);
                _props.Dynamics.AngularTargetVelocity.Y *= MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.Y);
                _props.Dynamics.AngularTargetVelocity.Z *= MovementExpDecay(_props.Dynamics.AngularDecayIndex, timescale.Z);
                if (VehicleLimits.DebugAngularMotor) m_log.DebugFormat("[MoveAngular] scale target dindex={0} tvel={1}", _props.Dynamics.AngularDecayIndex, _props.Dynamics.AngularTargetVelocity.Z);
            }

            _props.Dynamics.AngularDecayIndex  = 0.0f;

            // SL cheat, ramp and decay should proceed simultaneously, but short decays often will prevent
            // motor ramp-up altogether. The cheat is to delay the decay by a tiny amount.
            float moahfubar = 0.0f;
            Vector3 mts = _props.ParamsVec[VectorParams.VehicleAngularMotorTimescale];
            if (Vector3.Mag(mts) < 0.9)
                moahfubar =  1.0f - Vector3.Mag(mts);
            _props.Dynamics.AngularDecayIndex = VehicleLimits.MinPhysicsTimestep * (1.0f-moahfubar) - VehicleLimits.ThresholdDelayFubar * moahfubar;
            
            // SL induces an angular rate limiter based on the angular deflection timescale.
            // Basically angular deflection is enabled even when the vehicle is not moving. This is 
            // an old havok1 bug that has become institutionalized.
            float ts = _props.ParamsFloat[FloatParams.VehicleAngularDeflectionTimescale];
            float de = _props.ParamsFloat[FloatParams.VehicleAngularDeflectionEfficiency];
            float sl = (ts + 0.001f) / (de + 0.00001f);
            if (sl < 1.0f)
            {
                direction *= Math.Max(sl, 0.1f);
            }

            _props.Dynamics.TargetAngularDelta = direction - _props.Dynamics.AngularDirection;
            _props.Dynamics.AngularDirection   = direction;
            _actor.WakeUp();
            if (VehicleLimits.DebugAngularMotor) m_log.DebugFormat("[MoveAngular] {0} mf={1} sl={2} tvel={3} name={4}", direction, moahfubar, sl, _props.Dynamics.AngularTargetVelocity, _actor.SOPName);
        }
    }

    /// <summary>
    ///  A running variance and standard deviation class
    ///  used to gather real time metrics.
    /// </summary>
    // Borrowed from: http://www.johndcook.com/standard_deviation.html
    internal class RunningStat
    {
        private int     m_n;
        private double  m_oldM, m_newM, m_oldS, m_newS;

        public RunningStat()
        {
            m_n = 0;
        }

        public void Clear()
        {
            m_n = 0;
        }

        public void Push(double x)
        {
            m_n++;

            // See Knuth TAOCP vol 2, 3rd edition, page 232
            if (m_n == 1)
            {
                m_oldM = m_newM = x;
                m_oldS = 0.0;
            }
            else
            {
                m_newM = m_oldM + (x - m_oldM)/m_n;
                m_newS = m_oldS + (x - m_oldM)*(x - m_newM);
    
                // set up for next iteration
                m_oldM = m_newM; 
                m_oldS = m_newS;
            }
        }

        public int NumDataValues()
        {
            return m_n;
        }

        public double Mean()
        {
            return (m_n > 0) ? m_newM : 0.0;
        }

        public double Variance()
        {
            return ( (m_n > 1) ? m_newS/(m_n - 1) : 0.0 );
        }

        public double StandardDeviation()
        {
            return Math.Sqrt( Variance() );
        }
    }
}
