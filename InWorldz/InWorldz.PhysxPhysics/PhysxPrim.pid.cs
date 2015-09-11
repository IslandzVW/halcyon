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

//
// This file contains functions that perform some of the more advanced physics 
// functionality that is not related to vehicles, but that makes frame by frame 
// changes to the simulation
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Physics.Manager;

namespace InWorldz.PhysxPhysics
{
    internal partial class PhysxPrim
    {
        private const float MIN_TAU = PhysxScene.TIMESTEP_IN_SECONDS * 2.0f;

        private const float MIN_ROTLOOK_DAMPING = 0.01f;
        private const float MIN_ROTLOOK_STRENGTH = 0.01f;
        private const float MIN_PHYSX_FORCE = 0.001f;

        /// <summary>
        /// Tolerance for our rotation to be considered at target (radians)
        /// </summary>
        private const float AT_ROT_TOLERANCE = (float)Math.PI / 180.0f;

        /// <summary>
        /// Used for scaling forces in the RotLook simulation
        /// </summary>
        private const float ROT_LOOKAT_SCALING_CONSTANT = 100.0f;

        /// <summary>
        /// Radial tolerance for a prim to be considered at its movement target
        /// </summary>
        private const float AT_TARGET_TOLERANCE = 0.05f;
        private const float AT_TARGET_TOLERANCE_SQUARED = AT_TARGET_TOLERANCE * AT_TARGET_TOLERANCE;

        /// <summary>
        /// Determined through experimentation on the SL grid
        /// </summary>
        private const float MOVE_TO_TARGET_DAMPING_MULTIPLIER = 0.095f;

        /// <summary>
        /// Tolerance/fudge allowed for hover height before forces will be applied to correct the object's altitude
        /// </summary>
        private const float AT_HOVER_HEIGHT_TOLERANCE = 0.05f;

        /// <summary>
        /// The minimum velocity of an object in the hover butter zone that we will damp before we zero 
        /// the Z velocity entirely
        /// </summary>
        private const float MIN_HOVER_DAMPING_VEL_BEFORE_ZEROING = 0.01f;

        /// <summary>
        /// Determined through experimentation on the SL grid
        /// </summary>
        private const float HOVER_DAMPING_MULTIPLIER = 2.0f;

        /// <summary>
        /// Whether or not sethoverheight/groundrepel wants gravity disabled
        /// </summary>
        bool _hoverDisableGravity;

        /// <summary>
        /// Whether or not grabtarget wants gravity disabled
        /// </summary>
        bool _grabDisableGravity;

        public override PIDHoverFlag GetHoverType()
        {
            lock (_properties)
            {
                return _properties.HoverType;
            }
        }

        public override void SetHover(PIDHoverFlag hoverType, float height, float tau, float damping)
        {
            lock (_properties)
            {
                _properties.HoverType = hoverType;
                _properties.HoverHeight = height;
                _properties.HoverTau = tau;
                _properties.HoverDamping = damping;
            }

            _scene.QueueCommand(new Commands.WakeUpCmd(this));
        }

        public override void ClearHover()
        {
            lock (_properties)
            {
                _properties.HoverType = PIDHoverFlag.None;
                _properties.HoverHeight = 0.0f;
                _properties.HoverTau = 0.0f;
                _properties.HoverDamping = 1.0f;
            }

            _scene.QueueCommand(new Commands.WakeUpCmd(this));
        }

        public override void SetGrabSpinVelocity(OpenMetaverse.Vector3 target)
        {
            lock (_properties)
            {
                if (!_properties.BlockGrab)
                {
                    _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(target), PhysX.ForceMode.VelocityChange, true);
                }
            }
        }

        public override void SetGrabTarget(OpenMetaverse.Vector3 target, float tau)
        {
            lock (_properties)
            {
                if (!_properties.BlockGrab)
                {
                    _properties.GrabTarget = target;
                    _properties.GrabTargetTau = tau;
                }
            }

            _scene.QueueCommand(new Commands.WakeUpCmd(this));
        }

        public override void SetMoveToTarget(OpenMetaverse.Vector3 target, float tau)
        {
            lock (_properties)
            {
                _properties.MoveTarget = target;
                _properties.MoveTargetTau = tau;
            }

            _scene.QueueCommand(new Commands.WakeUpCmd(this));
        }

        public override void SetRotLookAtTarget(OpenMetaverse.Quaternion target, float strength, float damping)
        {
            lock (_properties)
            {
                _properties.RotLookTarget = target;
                _properties.RotLookStrength = OpenMetaverse.Utils.Clamp(strength, 0.1f, 1.0f);
                _properties.RotLookDamping = OpenMetaverse.Utils.Clamp(damping, 0.1f, 100.0f);
            }

            _scene.QueueCommand(new Commands.WakeUpCmd(this));
        }

        public override void StopRotLookAt()
        {
            lock (_properties)
            {
                _properties.RotLookTarget = OpenMetaverse.Quaternion.Identity;
                _properties.RotLookStrength = 0.0f;
                _properties.RotLookDamping = 0.0f;
            }
        }

        /// <summary>
        /// Called each simulation step to move an object towards a move or rotation target 
        /// </summary>
        /// <param name="timeStep"></param>
        private void UpdateDynamicForPIDTargets(float timeStep, uint frameNum)
        {
            if (IsFreeDynamic)
            {
                DoGrabTarget(timeStep, frameNum);
                DoPIDMoveTarget(timeStep, frameNum);
                DoRotLookTarget(timeStep, frameNum);
                DoHoverTarget(timeStep, frameNum);
                DoAxisLock(timeStep, frameNum);
            }
        }

        private void DoHoverTarget(float timeStep, uint frameNum)
        {
            PIDHoverFlag hoverType;
            float hoverHeight;
            float hoverTau;
            float hoverDamping;
            float gtau;
            
            lock (_properties)
            {
                hoverType = _properties.HoverType;
                hoverHeight = _properties.HoverHeight;
                hoverTau = _properties.HoverTau;
                hoverDamping = _properties.HoverDamping;
                gtau = _properties.GrabTargetTau;
            }

            // Grab overrides hover/repel.
            if (gtau != 0)
                return;

            if (hoverType == OpenSim.Region.Physics.Manager.PIDHoverFlag.None ||
                hoverType == OpenSim.Region.Physics.Manager.PIDHoverFlag.Vehicle || 
                hoverTau < MIN_TAU)
            {
                if (_hoverDisableGravity == true)
                {
                    _hoverDisableGravity = false;
                    ChangeGravityIfNeeded();
                }

                return;
            }

            float groundHeight = GetHeightAbove(hoverType);
            float targetDiff = Math.Abs(groundHeight - hoverHeight);

            //if we're repelling against water or land, and we're around the proper height, make no
            //forces against the object (butter zone case)
            if (targetDiff < (AT_HOVER_HEIGHT_TOLERANCE * hoverDamping))
            {
                if (_hoverDisableGravity == false)
                {
                    _hoverDisableGravity = true;
                    ChangeGravityIfNeeded();
                }

                DoHoverDamping(hoverDamping, timeStep);

                return;
            }

            if ((hoverType & OpenSim.Region.Physics.Manager.PIDHoverFlag.Repel) != 0)
            {
                //if we're not within tolerance, we may be too high. if this is the case, we want to enable gravity and do nothing
                if (groundHeight > hoverHeight + AT_HOVER_HEIGHT_TOLERANCE)
                {
                    _hoverDisableGravity = false;
                    ChangeGravityIfNeeded();
                    return;
                }

                DoGroundRepel(groundHeight, hoverHeight, hoverTau, timeStep);
            }
            else
            {
                DoHover(groundHeight, hoverHeight, hoverTau, hoverDamping, timeStep);
            }

        }

        private void DoHoverDamping( float hoverDamping, float timeStep)
        {
            if (_velocity.Z != 0)
            {
                PhysX.Math.Vector3 velWithZOnly = new PhysX.Math.Vector3(0f, 0f, _velocity.Z);

                if (Math.Abs(velWithZOnly.Z) <= (MIN_HOVER_DAMPING_VEL_BEFORE_ZEROING * hoverDamping))
                {
                    //_dynActor.LinearVelocity = new PhysX.Math.Vector3(_dynActor.LinearVelocity.X, _dynActor.LinearVelocity.Y, 0.0f) * hoverDamping;
                    _dynActor.AddForce(-velWithZOnly * hoverDamping, PhysX.ForceMode.VelocityChange, true);
                }
                else
                {
                    _dynActor.AddForce(-velWithZOnly * _mass * timeStep * HOVER_DAMPING_MULTIPLIER * hoverDamping, PhysX.ForceMode.Impulse, true);
                }
            }
        }

        private void DoHover(float currentHeightAboveGround, float desiredHoverHeight, float tau, float damping, float timeStep)
        {
            if (_hoverDisableGravity == false)
            {
                _hoverDisableGravity = true;
                ChangeGravityIfNeeded();
            }

            //if we got here, we must be outside of the target zone. if there are z forces
            //headed in the wrong direction we want to cancel them right away
            float diff = desiredHoverHeight - currentHeightAboveGround;
            float zforce = _velocity.Z;

            // Stay above PhysX NOP limits.
            if (Math.Abs(zforce) < MIN_PHYSX_FORCE) zforce = Math.Sign(zforce) * MIN_PHYSX_FORCE;

            // Apply strong correction if the movement is departing from the target.
            PhysX.Math.Vector3 velWithZOnly = new PhysX.Math.Vector3(0f, 0f, zforce);
            if (Math.Sign(_velocity.Z) != Math.Sign(diff))
            {
                if (damping > 0.75f)
                    _dynActor.AddForce(-velWithZOnly, PhysX.ForceMode.VelocityChange, true);
                else
                    _dynActor.AddForce(-velWithZOnly * _mass * timeStep, PhysX.ForceMode.Impulse, true);
            }

            // Note: the buoyancy check is here to be bug for bug compatible with standard vehicles;
            // that is, unless there is some positive buoyancy, hover does not work.
            if ((_properties.HoverType & PIDHoverFlag.UpOnly) == 0 || diff > 0 || _properties.Buoyancy > 0)
            {
                //apply enough force to get to the target in TAU, as well as applying damping
                zforce = diff * (1.0f / tau) * _mass * timeStep;

                // Stay above PhysX NOP limits.
                if (Math.Abs(zforce) < MIN_PHYSX_FORCE) zforce = Math.Sign(zforce) * MIN_PHYSX_FORCE;

                _dynActor.AddForce(new PhysX.Math.Vector3(0f, 0f, zforce), PhysX.ForceMode.Impulse, true);
                DoHoverDamping(damping, timeStep);
            }
        }

        private void DoGroundRepel(float currentHeightAboveGround, float desiredHoverHeight, float tau, float timeStep)
        {
            if (_hoverDisableGravity == false)
            {
                _hoverDisableGravity = true;
                ChangeGravityIfNeeded();
            }

            //we only want to apply positive z forces for ground repel. popping up above the
            //target zone is ok
            float diff = desiredHoverHeight - currentHeightAboveGround;
            float zforce = _velocity.Z;

            // Stay above PhysX NOP limits.
            if (Math.Abs(zforce) < MIN_PHYSX_FORCE) zforce = Math.Sign(zforce) * MIN_PHYSX_FORCE;

            PhysX.Math.Vector3 velWithZOnly = new PhysX.Math.Vector3(0f, 0f, zforce);
            if (velWithZOnly.Z < 0.0f)
            {
                //cancel -Z velocity
                _dynActor.AddForce(-velWithZOnly, PhysX.ForceMode.VelocityChange, true);
            }

            //apply enough force to get to the target in TAU, as well as applying damping
            zforce = diff * (1.0f / tau) * _mass * timeStep;

            // Stay above PhysX NOP limits.
            if (Math.Abs(zforce) < MIN_PHYSX_FORCE) zforce = Math.Sign(zforce) * MIN_PHYSX_FORCE;

            //apply enough force to get to the target in TAU, as well as applying damping
            _dynActor.AddForce(new PhysX.Math.Vector3(0f, 0f, zforce), PhysX.ForceMode.Impulse, true);
            DoHoverDamping(1.0f, timeStep);
        }

        internal float GetHeightAbove(OpenSim.Region.Physics.Manager.PIDHoverFlag hovertype)
        {
            if ((hovertype & OpenSim.Region.Physics.Manager.PIDHoverFlag.Ground) != 0 && 
                (hovertype & OpenSim.Region.Physics.Manager.PIDHoverFlag.Water) != 0)
            {
                return _position.Z - Math.Max(_scene.TerrainChannel.CalculateHeightAt(_position.X, _position.Y), (float)_scene.RegionSettings.WaterHeight);
            }

            else if ((hovertype & OpenSim.Region.Physics.Manager.PIDHoverFlag.Water) != 0)
            {
                return _position.Z - (float)_scene.RegionSettings.WaterHeight;
            }

            else if ((hovertype & OpenSim.Region.Physics.Manager.PIDHoverFlag.Ground) != 0)
            {
                return _position.Z - _scene.TerrainChannel.CalculateHeightAt(_position.X, _position.Y);
            }

            else // Global - except when it wants to be lower than the ground.
            {
                float gnd = _scene.TerrainChannel.CalculateHeightAt(_position.X, _position.Y);
                if (_position.Z < gnd) return _position.Z - gnd;
                return _position.Z;
            }
        }

        private void DoRotLookTarget(float timeStep, uint frameNum)
        {
            float strength;
            float damping;
            OpenMetaverse.Quaternion target;
            float gtau;

            lock (_properties)
            {
                strength = _properties.RotLookStrength;
                damping = _properties.RotLookDamping;
                target = _properties.RotLookTarget;
                gtau = _properties.GrabTargetTau;
            }

            // Grab overrides rot target
            if (gtau != 0)
                return;

            if (damping < MIN_ROTLOOK_DAMPING || strength < MIN_ROTLOOK_STRENGTH)
            {
                return;
            }

            OpenMetaverse.Quaternion normRot = OpenMetaverse.Quaternion.Normalize(_rotation);

            //this is the first frame we're beginning a new rotlook force
            //if we're not already at the target rotation, we need to find 
            //the difference between our current rotation and the desired 
            //rotation, then use the damping number as a tau to set the
            //angular velocity
            if (normRot.ApproxEquals(target, AT_ROT_TOLERANCE))
            {
                //nothing to do
                if (_dynActor.AngularVelocity != PhysX.Math.Vector3.Zero)
                {
                    _dynActor.AngularVelocity = PhysX.Math.Vector3.Zero;
                }
                return;
            }

            /* 
                * rotation velocity is vector that corresponds to axis of rotation and have length equal to rotation speed, 
                * for example in radians per second. It is sort of axis-angle thing.
                * Let's first find quaternion q so q*q0=q1 it is q=q1/q0 
                    
                * For unit length quaternions, you can use q=q1*Conj(q0)

                * To find rotation velocity that turns by q during time Dt you need to convert quaternion to axis angle using something like this:
                    
                * double len=sqrt(q.x*q.x+q.y*q.y+q.z*q.z)
                * double angle=2*atan2(len, q.w);
                * vector3 axis;
                * if(len>0)axis=q.xyz()/len; else axis=vector3(1,0,0);
                    
                * then rotation velocity w=axis*angle/dt
                    
                * For q1,q2 very close to eachother, xyz part of 2*q1*Conj(q0) is roughly equal to rotation velocity. (because for small angles sin(a)~=a) 
                * 
                * http://www.gamedev.net/topic/347752-quaternion-and-angular-velocity/
            */

            OpenMetaverse.Quaternion q = target * OpenMetaverse.Quaternion.Conjugate(normRot);

            double len = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z);
            double angle = 2 * Math.Atan2(len, q.W);
            OpenMetaverse.Vector3 axis;
            if (len > 0)
            {
                axis.X = (float)(q.X / len);
                axis.Y = (float)(q.Y / len);
                axis.Z = (float)(q.Z / len);
            }
            else
            {
                axis = new OpenMetaverse.Vector3(1, 0, 0);
            }

            float fangle = ShortestAngle((float)angle);

            OpenMetaverse.Vector3 targetAngVel = ((axis * fangle) / damping) * ROT_LOOKAT_SCALING_CONSTANT;

            OpenMetaverse.Quaternion comRot = _centerOfMassLocalPose.Rotation;
            OpenMetaverse.Quaternion adjRot = _rotation * comRot;
            OpenMetaverse.Vector3 tensor = _massSpaceInertiaTensor;

            OpenMetaverse.Vector3 t = (tensor * (targetAngVel * OpenMetaverse.Quaternion.Inverse(adjRot))) * adjRot;

            OpenMetaverse.Vector3 currAngVel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);
            OpenMetaverse.Vector3 velDamping = (tensor * (-currAngVel * strength * ROT_LOOKAT_SCALING_CONSTANT * OpenMetaverse.Quaternion.Inverse(adjRot))) * adjRot;

            _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(t * timeStep), PhysX.ForceMode.Impulse, true);
            _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(velDamping * timeStep), PhysX.ForceMode.Impulse, true);

            //m_log.DebugFormat("New Ang Vel: {0}", targetAngVel);
            
        }

        private float ShortestAngle(float p)
        {
            float fPI = (float)Math.PI;

            if (p > fPI)
            {
                p -= fPI * 2.0f;
            }
            else if (p < -fPI)
            {
                p += fPI * 2.0f;
            }

            return p;
        }

        private void DoPIDMoveTarget(float timeStep, uint frameNum)
        {
            float tau;
            OpenMetaverse.Vector3 target;
            float gtau;
            lock (_properties)
            {
                tau = _properties.MoveTargetTau;
                target = _properties.MoveTarget;
                gtau = _properties.GrabTargetTau;
            }

            // Grab overrides move to target
            if (gtau != 0)
                return;

            if (tau > MIN_TAU)
            {
                //i had a whole elaborate setup here to do this.. turns out the linden
                //implementation was much simpler
                OpenMetaverse.Vector3 distance = target - _position;

                if (distance.LengthSquared() <= AT_TARGET_TOLERANCE_SQUARED)
                {
                    if (_velocity != OpenMetaverse.Vector3.Zero)
                    {
                        StopLinearMovement();
                        return;
                    }
                }

                ChangeGravityIfNeeded();
                _dynActor.AddForce(PhysUtil.OmvVectorToPhysx((distance * (1.0f / tau)) - _velocity), PhysX.ForceMode.VelocityChange, true);
            }
            else // Check for stop move to target - need to turn gravity back on if buoyancy isn't enabled
            if ((target == OpenMetaverse.Vector3.Zero) && (tau == 0.0f))
                ChangeGravityIfNeeded();
        }

        private bool DoGrabTarget(float timeStep, uint frameNum)
        {
            float tau;
            OpenMetaverse.Vector3 target;
            lock (_properties)
            {
                tau = _properties.GrabTargetTau;
                target = _properties.GrabTarget;
            }

            if (tau > MIN_TAU)
            {
                _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(-_angularVelocity * 0.9f), PhysX.ForceMode.VelocityChange, false);
                OpenMetaverse.Vector3 distance = target - _position;

                if (distance.LengthSquared() <= AT_TARGET_TOLERANCE_SQUARED)
                {
                    if (_velocity != OpenMetaverse.Vector3.Zero)
                    {
                        StopLinearMovement();
                        return true;
                    }
                }

                _grabDisableGravity = true;
                ChangeGravityIfNeeded();
                _dynActor.AddForce(PhysUtil.OmvVectorToPhysx((distance * (1.0f / tau)) - _velocity), PhysX.ForceMode.VelocityChange, true);
            }
            else
            {
                // Check for stop move to grab - need to turn gravity back on if buoyancy isn't enabled
                if (tau == 0.0f)
                {
                    _grabDisableGravity = false;
                    ChangeGravityIfNeeded();
                    return false;
                }
            }
            return true;
        }

        private void DoAxisLock(float timeStep, uint frameNum)
        {
            OpenMetaverse.Vector3 lockedaxis;
            float gtau;

            lock (_properties)
            {
                lockedaxis = _properties.LockedAxes;
                gtau = _properties.GrabTargetTau;
            }

            // Grab overrides axis lock
            if (gtau != 0)
                return;

            // Fast bypass: If all axes are unlocked, skip the math.
            if (lockedaxis.X == 0 && lockedaxis.Y == 0 && lockedaxis.Z == 0)
                return;
            
            // Convert angular velocity to local.
            OpenMetaverse.Vector3 localangvel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity) * OpenMetaverse.Quaternion.Inverse(_rotation);
            
            // Stop angular velocity on locked local axes (rotaxis.N == 0 means axis is locked)
            if (lockedaxis.X != 0)
                localangvel.X = 0;
            if (lockedaxis.Y != 0)
                localangvel.Y = 0;
            if (lockedaxis.Z != 0)
                localangvel.Z = 0;

            // Convert to global angular velocity.
            PhysX.Math.Vector3 angvel = PhysUtil.OmvVectorToPhysx(localangvel * _rotation);
            
            // This is a harsh way to do this, but a locked axis must have no angular velocity whatsoever.
            if (angvel != _dynActor.AngularVelocity)
            {
                _dynActor.ClearTorque();
                _dynActor.AngularVelocity = angvel;
            }
        }

        private void StopLinearMovement()
        {
            _dynActor.ClearForce();
            _dynActor.AddForce(PhysUtil.OmvVectorToPhysx(-_velocity), PhysX.ForceMode.VelocityChange, false);
        }
    }
}
