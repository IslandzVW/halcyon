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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections.Generic;
using System.Reflection;

using log4net;
using OpenMetaverse;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Wind;

namespace OpenSim.Region.CoreModules.World.Wind.Plugins
{
    class ZephyrWind : Mono.Addins.TypeExtensionNode, IWindModelPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private OpenSim.Region.Framework.Scenes.Scene _scene;

        // This cell size cannot change without upending the entire wind framework.
        // To improve realism, Zephyr creates three bands of currents: sea, surface, and aloft.
        // sea 'wind' is an undulating current that simulates the back and forth surge.
        // surface wind is subject to terrain turblence.
        // wind aloft is laminar with gentle shifts.
        private Vector2[] m_waterCurrent = new Vector2[16 * 16];
        private Vector2[] m_windsGround = new Vector2[16 * 16];
        private Vector2[] m_windsAloft  = new Vector2[16 * 16];
        private WindConstants[] m_options = new WindConstants[16 * 16];
        private float[] m_skews         = new float[16 * 16];
        private float[] m_ranges        = new float[16 * 16];
        private float[] m_maxheights    = new float[16 * 16];

        private float m_avgWindStrength     = 7.0f; // Average magnitude of the wind vector
        private float m_avgWindDirection    = 0.0f; // Average direction of the wind in degrees
        private float m_varWindStrength     = 7.0f; // Max Strength  Variance  
        private float m_varWindDirection    = 30.0f;// Max Direction Variance
        private float m_rateChangeAloft     = 1.0f; // Rate of change of the winds aloft
        private float m_rateChangeFlutter   = 64.0f;

        private float m_avgWaterStrength    = 1.0f;
        private float m_avgWaterDirection   = 10.0f;
        private float m_varWaterStrength    = 2.0f;
        private float m_varWaterDirection   = 180.0f;
        private float m_rateChangeSurge     = 128.0f;

        private float SunFudge = 0.0f;
        private float LastSunPos;

        #region IPlugin Members

        public string Version
        {
            get { return "1.0.0.0"; }
        }

        public string Name
        {
            get { return "ZephyrWind"; }
        }

        public void Initialize()
        {
            
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            m_waterCurrent = null;
            m_windsGround = null;
            m_windsAloft  = null;
            m_options     = null;
            m_skews       = null;
            m_ranges      = null;
            m_maxheights  = null;
        }

        #endregion

        #region IWindModelPlugin Members

        public void WindConfig(OpenSim.Region.Framework.Scenes.Scene scene, Nini.Config.IConfig windConfig)
        {
            _scene = scene;

            // Default to ground turbulence
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    m_options[y * 16 + x] = WindConstants.WindSpeedTurbulence;
                }
            }
            
            if (windConfig != null)
            {
                // Uses strength value if avg_strength not specified
                m_avgWindStrength = windConfig.GetFloat("strength", 5.0F);
                m_avgWindStrength = windConfig.GetFloat("avg_strength", 5.0F);

                m_avgWindDirection = windConfig.GetFloat("avg_direction", 0.0F);
                m_varWindStrength  = windConfig.GetFloat("var_strength", 5.0F);
                m_varWindDirection = windConfig.GetFloat("var_direction", 30.0F);
                m_rateChangeAloft   = windConfig.GetFloat("rate_change_aloft", 1.0f);
                m_rateChangeFlutter = windConfig.GetFloat("rate_change_flutter", 64.0f);

                LogSettings();
            }
        }

        public void WindUpdate(uint frame)
        {
            float h2o = (float)_scene.RegionInfo.RegionSettings.WaterHeight;
            float gnd;
            float sunpos;

            // Simulate time passing on fixed sun regions.
            // Otherwise use the region's local sun position.
            if (_scene.RegionInfo.RegionSettings.FixedSun)
                sunpos = (float)((DateTime.Now.TimeOfDay.TotalSeconds / 600.0) % 24.0);
            else
                sunpos = (float)(_scene.RegionInfo.RegionSettings.SunPosition % 24.0);

            // Sun position is quantized by the simulator to about once every three seconds.
            // Add a fudge factor to fill in the steps.
            if (LastSunPos != sunpos)
            {
                LastSunPos = sunpos;
                SunFudge = 0.0f;
            }
            else
            {
                SunFudge += 0.002f;
            }

            sunpos += SunFudge;

            RunningStat rsCell = new RunningStat();

            // Based on the Prevailing wind algorithm
            // Inspired by Kanker Greenacre
            // Modified by Balpien Hammerer to account for terrain turbulence, winds aloft and wind setters

            // Wind Direction
            double ThetaWD = (sunpos / 24.0 * (2.0 * Math.PI) * m_rateChangeFlutter) % (Math.PI * 2.0);
            double offset = Math.Sin(ThetaWD) * Math.Sin(ThetaWD*2) * Math.Sin(ThetaWD*9) * Math.Cos(ThetaWD*4);
            double windDir = m_avgWindDirection * (Math.PI/180.0f) + (m_varWindDirection * (Math.PI/180.0f) * offset);

            // Wind Speed
            double ThetaWS = (sunpos / 24.0 * (2.0 * Math.PI) * m_rateChangeAloft) % (Math.PI * 2.0);
            offset = Math.Sin(ThetaWS) * Math.Sin(ThetaWS*4) + (Math.Sin(ThetaWS*13) / 3.0);
            double windSpeed = (m_avgWindStrength + (m_varWindStrength * offset));

            if (windSpeed < 0)
            {
                windSpeed = -windSpeed;
                windDir += Math.PI;
            }

            // Water Direction
            double ThetaHD = (sunpos / 24.0 * (2.0 * Math.PI) * m_rateChangeFlutter) % (Math.PI * 2.0);
            double woffset = Math.Sin(ThetaHD) * Math.Sin(ThetaHD*2) * Math.Sin(ThetaHD*9) * Math.Cos(ThetaHD*4);
            double waterDir = m_avgWaterDirection * (Math.PI/180.0f) + (m_varWaterDirection * (Math.PI/180.0f) * woffset);

            // Water Speed
            double ThetaHS = (sunpos / 24.0 * (2.0 * Math.PI) * m_rateChangeSurge) % (Math.PI * 2.0);
            woffset = Math.Sin(ThetaHS) * Math.Sin(ThetaHS*3) * Math.Sin(ThetaHS*9) * Math.Cos(ThetaHS*4) * Math.Cos(ThetaHS*13);
            double waterSpeed = (m_avgWaterStrength + (m_varWaterStrength * woffset));

            if (waterSpeed < 0)
            {
                waterSpeed = -waterSpeed;
                waterDir += Math.PI;
            }
            //m_log.DebugFormat("[{0}] sunpos={1} water={2} dir={3} ThetaHD={4} ThetaHS={5} wo={6}", Name, sunpos, waterSpeed, waterDir, ThetaHD, ThetaHS, woffset);

            //m_log.DebugFormat("[{0}] sunpos={1} wind={2} dir={3} theta1={4} theta2={5}", Name, sunpos, windSpeed, windDir, theta1, theta2);

            // Set the predominate wind in each cell, but examine the terrain heights
            // to adjust the winds. Elevation and the pattern of heights in each 16m
            // cell infuence the ground winds.
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    rsCell.Clear();

                    // Compute terrain statistics. They are needed later.
                    for (int iy = 0; iy < 16; iy++)
                    {
                        for (int ix = 0; ix < 16; ix++)
                        {
                            // For the purpose of these computations, it is the above water height that matters.
                            // Any ground below water is treated as zero.
                            gnd = Math.Max(_scene.PhysicsScene.TerrainChannel.GetRawHeightAt(x * 16 + ix, y * 16 + iy) - h2o, 0);
                            rsCell.Push(gnd);
                        }
                    }

                    // Look for the range of heights in the cell and the overall skewness. Use this
                    // to determine ground induced deflection. It is a rough approximation of
                    // ground wind turbulance. The max height is used later to determine the boundary layer.
                    float cellrange = (float)Math.Max((rsCell.Max() - rsCell.Min()) / 5.0f, 1.0);
                    float cellskew = (float)rsCell.Skewness();
                    m_ranges[y * 16 + x] = (float)(rsCell.Max() - rsCell.Min());
                    m_skews[y * 16 + x] = cellskew;
                    m_maxheights[y * 16 + x] = (float)rsCell.Max();

                    // Begin with winds aloft,starting with the fixed wind value set by wind setters.
                    Vector2 wind = m_windsAloft[y * 16 + x];

                    // Update the cell with default zephyr winds aloft (no turbulence) if the wind speed is not fixed.
                    if ((m_options[y * 16 + x] & WindConstants.WindSpeedFixed) == 0)
                    {
                        // Compute the winds aloft (no turbulence)
                        wind.X = (float)Math.Cos(windDir);
                        wind.Y = (float)Math.Sin(windDir);
                        //wind.Normalize();
                        wind.X *= (float)windSpeed;
                        wind.Y *= (float)windSpeed;
                        m_windsAloft[y * 16 + x] = wind;
                    }

                    // Compute ground winds (apply terrain turbulence) from the winds aloft cell.
                    if ((m_options[y * 16 + x] & WindConstants.WindSpeedTurbulence) != 0)
                    {
                        double speed = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
                        wind = wind / (float)speed;
                        double dir = Math.Atan2(wind.Y, wind.X);

                        wind.X = (float)Math.Cos((Math.PI * cellskew * 0.333 * cellrange) + dir);
                        wind.Y = (float)Math.Sin((Math.PI * cellskew * 0.333 * cellrange) + dir);
                        //wind.Normalize();
                        wind.X *= (float)speed;
                        wind.Y *= (float)speed;
                    }

                    m_windsGround[y * 16 + x] = wind;

                    // Update the cell with default zephyr water currents if the speed is not fixed.
                    if ((m_options[y * 16 + x] & WindConstants.WindSpeedFixed) == 0)
                    {
                        // Compute the winds aloft (no turbulence)
                        wind.X = (float)Math.Cos(waterDir);
                        wind.Y = (float)Math.Sin(waterDir);
                        //wind.Normalize();
                        wind.X *= (float)waterSpeed;
                        wind.Y *= (float)waterSpeed;
                        m_waterCurrent[y * 16 + x] = wind;
                    }

                    //m_log.DebugFormat("[ZephyrWind] speed={0} dir={1} skew={2} range={3} wind={4}", windSpeed, windDir, cellskew, cellrange, wind);
                    
                }
            }
           
            // Send the updated wind data to the physics engine.
            _scene.PhysicsScene.SendPhysicsWindData(m_waterCurrent, m_windsGround, m_windsAloft, m_ranges, m_maxheights);
        }

        /// <summary>
        /// Determine the wind speed at the specified local region coordinate.
        /// Wind speed returned is influenced by the nearest neighbors.
        /// </summary>
        /// <param name="fX">region local coordinate</param>
        /// <param name="fY">region local coordinate</param>
        /// <param name="fZ">Presently ignored</param>
        /// <returns></returns>
        public Vector3 WindSpeed(float fX, float fY, float fZ)
        {
            int x0 = (int)fX / 16;
            int y0 = (int)fY / 16;
            if (x0 < 0 || x0 >= 16) return Vector3.Zero;
            if (y0 < 0 || y0 >= 16) return Vector3.Zero;

            int x1 = Math.Min(x0+1, 15);
            int y1 = Math.Min(y0+1, 15);

            //Set the wind maxtrix based on the z-position
            float h2o = (float)_scene.RegionInfo.RegionSettings.WaterHeight;
            float maxheight = m_maxheights[y0 * 16 + x0];
            float range = Math.Max(m_ranges[y0 * 16 + x0], 15.0f);
            float boundary = maxheight + range;
            Vector2[] windmatrix;

            if (fZ < h2o)
                windmatrix = m_waterCurrent;
            else if (fZ <= boundary)
                windmatrix = m_windsGround;
            else
                windmatrix = m_windsAloft;

            // Perform a bilinear interpolation of wind speeds
            // f(x,y) = f(0,0) * (1-x)(1-y) + f(1,0) * x(1-y) + f(0,1) * (1-x)y + f(1,1) * xy. 
            Vector3 wind = Vector3.Zero;
            float     dx = (fX - x0*16) / 16.0f;
            float     dy = (fY - y0*16) / 16.0f;
            //m_log.DebugFormat("pos={0} x0={1} x1={2} y0={3} y1={4} dx={5} dy={6}", new  Vector3(fX,fY,fZ), x0, x0, y0, y1, dx, dy);

            wind.X  = windmatrix[y0 * 16 + x0].X * (1.0f-dx) * (1.0f-dy);
            wind.X += windmatrix[y0 * 16 + x1].X * dx * (1.0f-dy);
            wind.X += windmatrix[y1 * 16 + x0].X * dy * (1.0f-dx);
            wind.X += windmatrix[y1 * 16 + x1].X * dx *dy;

            wind.Y  = windmatrix[y0 * 16 + x0].Y * (1.0f-dx) * (1.0f-dy);
            wind.Y += windmatrix[y0 * 16 + x1].Y * dx * (1.0f-dy);
            wind.Y += windmatrix[y1 * 16 + x0].Y * dy * (1.0f-dx);
            wind.Y += windmatrix[y1 * 16 + x1].Y * dx *dy;
 
            return wind;
        }

        /// <summary>
        /// Set the wind speed characteristics for a cell. A cell is
        /// presently 16x16m SW corner origined.
        /// </summary>
        /// <param name="type">WindConstant - fixed or default</param>
        /// <param name="loc">Region local coordinate</param>
        /// <param name="speed">Wind Speed</param>
        public void WindSet(int type, Vector3 loc, Vector3 speed)
        {
            float h2o = (float)_scene.RegionInfo.RegionSettings.WaterHeight;
            WindConstants wtype = (WindConstants)type;
            int x = (int)loc.X / 16;
            int y = (int)loc.Y / 16;
            if (x < 0 || x >= 16) return;
            if (y < 0 || y >= 16) return;

            // Handle default case, which really means not fixed + terrain turbulence
            if (wtype == WindConstants.WindSpeedDefault)
            {
                wtype = WindConstants.WindSpeedTurbulence;
            }

            //  Save the overrides
            m_options[y * 16 + x] = wtype;

            // If fixed wind set, copy the set windspeed into the winds aloft cell.
            // If the z location is underwater, then set the water current speed.
            if ((wtype & WindConstants.WindSpeedFixed) != 0)
            {
                if (loc.Z >= h2o)
                    m_windsAloft[y * 16 + x] = new Vector2(speed.X, speed.Y);
                else
                    m_waterCurrent[y * 16 + x] = new Vector2(speed.X, speed.Y);
            }

            //m_log.DebugFormat("[ZephyrWind] {0} {1} {2}", type, loc, speed);
        }

        /// <summary>
        /// Return the wind speed matrix. This must comply with the
        /// viewer's expectations: a 256 cell array.
        /// </summary>
        /// <returns>wind speed matrix</returns>
        public Vector2[] WindLLClientArray(Vector3 pos)
        {
            float h2o = (float)_scene.RegionInfo.RegionSettings.WaterHeight;
            int x = Utils.Clamp((int)pos.X/16, 0, 15);
            int y = Utils.Clamp((int)pos.Y/16, 0, 15);
            pos.Z = Utils.Clamp(pos.Z,  0, OpenSim.Framework.Constants.REGION_MAXIMUM_Z);
            float maxheight = m_maxheights[y * 16 + x];
            float range = Math.Max(m_ranges[y * 16 + x], 15.0f);
            float boundary;

            // When x-pos is the sentinel value, set the boundary to water level.
            // This is used to communicate the three matrices to the physics engine.
            if (pos.X == -1)
                boundary = h2o;
            // Otherwise, determine ground vs aloft based on the twice the height above the cell at
            // the avatar's position. This is used to give the viewer the avatar's proximate winds.
            else
                boundary = h2o + maxheight + range;

            //m_log.DebugFormat("[ZephyrWind] ClientArray pos={0} range={1} maxh={2} boundary={3} h2o={4}", pos, range, maxheight, boundary, h2o);

            if (pos.Z < h2o)
                return m_waterCurrent;
            else if (pos.Z <= boundary)
                return m_windsGround;
            else
                return m_windsAloft;
        }

        public string Description
        {
            get 
            {
                return "Provides a sun and terrain influenced predominate wind direction that can be adjusted by wind setters."; 
            }
        }

        public System.Collections.Generic.Dictionary<string, string> WindParams()
        {
            Dictionary<string, string> Params = new Dictionary<string, string>();

            Params.Add("avgStrength", "average wind strength");
            Params.Add("avgDirection", "average wind direction in degrees");
            Params.Add("varStrength", "allowable variance in wind strength");
            Params.Add("varDirection", "allowable variance in wind direction in +/- degrees");
            Params.Add("rateChangeAloft", "rate of change of winds aloft");
            Params.Add("rateChangeFlutter", "rate of change of wind flutter");

            return Params;
        }

        public void WindParamSet(string param, float value)
        {
            switch (param)
            {
                case "avgStrength":
                     m_avgWindStrength = value;
                     break;
                case "avgDirection":
                     m_avgWindDirection = value;
                     break;
                 case "varStrength":
                     m_varWindStrength = value;
                     break;
                 case "varDirection":
                     m_varWindDirection = value;
                     break;
                 case "rateChangeAloft":
                     m_rateChangeAloft = value;
                     break;
                case "rateChangeFlutter":
                     m_rateChangeFlutter = value;
                     break;
            }

            LogSettings();
        }

        public float WindParamGet(string param)
        {
            switch (param)
            {
                case "avgStrength":
                    return m_avgWindStrength;
                case "avgDirection":
                    return m_avgWindDirection;
                case "varStrength":
                    return m_varWindStrength;
                case "varDirection":
                    return m_varWindDirection;
                case "rateChangeAloft":
                    return m_rateChangeAloft;
                case "rateChangeFlutter":
                    return m_rateChangeFlutter;
                default:
                    //throw new Exception(String.Format("Unknown {0} parameter {1}", this.Name, param));
                    m_log.InfoFormat("[{0}] Unknown parameter {1}", this.Name, param);
                    return 0.0f;
            }
        }



        #endregion


        private void LogSettings()
        {
            m_log.InfoFormat("[{0}] Average Strength   : {1}", Name, m_avgWindStrength);
            m_log.InfoFormat("[{0}] Average Direction  : {1}", Name, m_avgWindDirection);
            m_log.InfoFormat("[{0}] Varience Strength  : {1}", Name, m_varWindStrength);
            m_log.InfoFormat("[{0}] Varience Direction : {1}", Name, m_varWindDirection);
            m_log.InfoFormat("[{0}] Rate Change        : {1}", Name, m_rateChangeAloft);
        }

        #region IWindModelPlugin Members


        #endregion
    }
    
    /// <summary>
    ///  A running variance and standard deviation class
    ///  used to gather real time metrics.
    /// </summary>
    // Borrowed from: http://www.johndcook.com/standard_deviation.html and http://www.johndcook.com/skewness_kurtosis.html
    internal class RunningStat
    {
        private long    n;
        private double  M1, M2, M3, /*M4,*/ M5, M6;

        public RunningStat()
        {
            Clear();
        }

        public void Clear()
        {
            n = 0;
            M1 = M2 = M3 = /*M4 =*/ M5 = M6 = 0.0;
        }

        public void Push(double x)
        {
            double delta, delta_n, term1;
            long n1 = n;

            // See Knuth TAOCP vol 2, 3rd edition, page 232
            n++;
            delta = x - M1;
            delta_n = delta / n;
            term1 = delta * delta_n * n1;
            M1 += delta_n;

            //profiling runs show M4 calculation as hot and we're not using it. so comment out for now
            //M4 += term1 * delta_n2 * (n*n - 3*n + 3) + 6 * delta_n2 * M2 - 4 * delta_n * M3;
            M3 += term1 * delta_n * (n - 2) - 3 * delta_n * M2;
            M2 += term1;

            if (n == 1)
            {
                M5 = M6 = x;
            }
            else
            {
                if (x < M5) M5 = x;
                if (x > M6) M6 = x;
            }

        }

        public long NumDataValues()
        {
            return n;
        }

        public double Min()
        {
            return M5;
        }

        public double Max()
        {
            return M6;
        }

        public double Mean()
        {
            return M1;
        }

        public double Variance()
        {
            if (n == 0) return 0.0;
            return M2/(n-1.0);
        }

        public double StandardDeviation()
        {
            return Math.Sqrt( Variance() );
        }

        public double Skewness()
        {
            double result = Math.Sqrt((double)n) * M3/ Math.Pow(M2, 1.5);
            if (double.IsNaN(result)) result = 0.0;
            return result;
        }

        /*public double Kurtosis()
        {
            return (double)n*M4 / (M2*M2) - 3.0;
        }*/
    }
}
