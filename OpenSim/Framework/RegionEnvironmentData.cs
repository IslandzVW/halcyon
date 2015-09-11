/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;

namespace OpenSim.Framework
{
#if USE_REGION_ENVIRONMENT_DATA
    public class RegionEnvironmentData : ICloneable
    {
        public bool valid = false;
        public UUID regionID = UUID.Zero;

        // Current settings?
        public float currentFloat;    // not sure what this is but parrot it back.
        public string currentSetting;

        // Sky
        public Vector4 ambient = new Vector4(1.05f, 1.05f, 1.05f, 0.35f);
        public Vector4 blue_density = new Vector4(0.24475f, 0.44872f, 0.76f, 0.38f);
        public Vector4 blue_horizon = new Vector4(0.49548f, 0.49548f, 0.64f, 0.32f);
        public Vector4 cloud_color = new Vector4(0.41f, 0.41f, 0.41f, 0.41f);
        public Vector4 cloud_pos_density1 = new Vector4(1.68841f, 0.526097f, 1.0f, 1.0f);
        public Vector4 cloud_pos_density2 = new Vector4(1.68841f, 0.526097f, 0.125f, 1.0f);
        public Vector4 cloud_scale = new Vector4(0.42f, 0.0f, 0.0f, 1.0f);
        public Vector2 cloud_scroll_rate = new Vector2(10.200f, 10.011f);
        public Vector4 cloud_shadow = new Vector4(0.27f, 0.0f, 0.0f, 1.0f);
        public Vector4 density_multiplier = new Vector4(0.00018f, 0.0f, 0.0f, 1.0f);
        public Vector4 distance_multiplier = new Vector4(0.8f, 0.0f, 0.0f, 1.0f);
        public float east_angle = 0.0f;
        public bool enable_cloud_scroll_x = false;  // LLSD is an array of 2 bool elements
        public bool enable_cloud_scroll_y = false;  // without the _x and _y in the IDs
        public Vector4 gamma = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        public Vector4 glow = new Vector4(5.0f, 0.001f, -0.48f, 1.0f);
        public Vector4 haze_density = new Vector4(0.7f, 0.0f, 0.0f, 1.0f);
        public Vector4 haze_horizon = new Vector4(0.19f, 0.199156f, 0.199156f, 1.0f);
        public Vector4 lightnorm = new Vector4(0.0f, 0.91269159317016602f, -0.40864911675453186f, 0.0f);
        public Vector4 max_y = new Vector4(1605.0f, 0.0f, 0.0f, 1.0f);
        public int preset_num = 22; // windlight/skies/default.xml is preset #22
        public float star_brightness = 0.0f;
        public float sun_angle = 1.99177f;
        public Vector4 sunlight_color = new Vector4(0.73421f, 0.78158f, 0.9f, 0.3f);

        // water
        public float blurMultiplier = 0.040f;
        public float fresnelOffset = 0.50f;
        public float fresnelScale = 0.40f;
        public Vector3 normScale = new Vector3(2.0f, 2.0f, 2.0f);   // LLSD is array of three integers
        public UUID normalMap = new UUID("822ded49-9a6c-f61c-cb89-6df54f42cdf4");
        public float scaleAbove = 0.03f;
        public float scaleBelow = 0.20f;
        public float underWaterFogMod = 0.25f;
        public Vector4 waterFogColor = new Vector4(0.0157f, 0.14902f, 0.25098f, 1.0f);
        public float waterFogDensity = 16.0f;
        public Vector2 wave1Dir = new Vector2(1.05f, -0.42f);   // bigWaveDirection
        public Vector2 wave2Dir = new Vector2(1.11f, -1.160f);  // littleWaveDirection

        public delegate void SaveDelegate(RegionEnvironmentData wl);
        public event SaveDelegate OnSave;
        public void Save()
        {
            if (OnSave != null)
                OnSave(this);
        }
        public object Clone()
        {
            return this.MemberwiseClone();      // call clone method
        }

        /// <summary>
        /// Create an OSDMap from the appearance data
        /// </summary>
        public OSDMap Pack()
        {
            OSDMap block1 = new OSDMap();
            block1["regionID"] = OSD.FromUUID(regionID);

            OSDArray block2 = new OSDArray(2);
            block2.Add(currentFloat);
            block2.Add(currentSetting);
            OSDMap block3 = new OSDMap();
            block3["regionID"] = OSD.FromUUID(regionID);
            block3["fresnel_scale"] = OSD.FromReal(fresnelScale);
            block3["fresnel_offset"] = OSD.FromReal(fresnelOffset);
            

#if false
            data["water_color_r"] = OSD.FromReal(waterColor.X);
            data["water_color_g"] = OSD.FromReal(waterColor.Y);
            data["water_color_b"] = OSD.FromReal(waterColor.Z);
            data["water_fog_density_exponent"] = OSD.FromReal(waterFogDensityExponent);
            data["reflection_wavelet_scale_1"] = OSD.FromReal(reflectionWaveletScale.X);
            data["reflection_wavelet_scale_2"] = OSD.FromReal(reflectionWaveletScale.Y);
            data["reflection_wavelet_scale_3"] = OSD.FromReal(reflectionWaveletScale.Z);
            data["refract_scale_above"] = OSD.FromReal(refractScaleAbove);
            data["refract_scale_below"] = OSD.FromReal(refractScaleBelow);
            data["blur_multiplier"] = OSD.FromReal(blurMultiplier);
            data["big_wave_direction_x"] = OSD.FromReal(bigWaveDirection.X);
            data["big_wave_direction_y"] = OSD.FromReal(bigWaveDirection.Y);
            data["little_wave_direction_x"] = OSD.FromReal(littleWaveDirection.X);
            data["little_wave_direction_y"] = OSD.FromReal(littleWaveDirection.Y);
            data["normal_map_texture"] = OSD.FromUUID(normalMapTexture);

            data["x"] = OSD.FromReal(x);
            data["x"] = OSD.FromReal(x);

            // Wearables
            List<AvatarWearable> wearables = GetWearables();
            OSDArray wears = new OSDArray(wearables.Count);
            foreach (AvatarWearable wearable in wearables)
                wears.Add(wearable.Pack());
            data["wearables"] = wears;

            // Avatar Textures
            OSDArray textures = new OSDArray(AvatarAppearance.TEXTURE_COUNT);
            for (uint i = 0; i < AvatarAppearance.TEXTURE_COUNT; i++)
            {
                if (m_texture.FaceTextures[i] != null)
                    textures.Add(OSD.FromUUID(m_texture.FaceTextures[i].TextureID));
                else
                    textures.Add(OSD.FromUUID(AppearanceManager.DEFAULT_AVATAR_TEXTURE));
            }
            data["textures"] = textures;

            // Visual Parameters
            OSDBinary visualparams = new OSDBinary(m_visualparams);
            data["visualparams"] = visualparams;

            // Attachments
            List<AvatarAttachment> attachments = GetAttachments();
            OSDArray attachs = new OSDArray(attachments.Count);
            foreach (AvatarAttachment attach in attachments)
                attachs.Add(attach.Pack());
            data["attachments"] = attachs;
#endif

            return block1;
        }

    }
#endif
}
