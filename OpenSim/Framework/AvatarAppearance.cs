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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// Contains the Avatar's Appearance and methods to manipulate the appearance.
    /// </summary>
    public class AvatarAppearance
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly static int VISUALPARAM_COUNT_1X = 218;   // 1.x viewers, i.e. before avatar physics layers
        public readonly static int VISUALPARAM_COUNT = 251;

        public readonly static byte VISUALPARAM_DEFAULT = 100;   // what to use for a default value? 0=alien, 150=fat scientist

        public readonly static int TEXTURE_COUNT = 21;
        public readonly static byte[] BAKE_INDICES = new byte[] { 8, 9, 10, 11, 19, 20 };

        protected UUID m_owner; 
        protected int m_serial = 0;
        protected byte[] m_visualparams;
        protected Primitive.TextureEntry m_texture;
        protected Dictionary<int, AvatarWearable> m_wearables;
        protected Dictionary<int, List<AvatarAttachment>> m_attachments;
        protected float m_avatarHeight = 0;
        protected float m_hipOffset = 0;

        public bool IsBotAppearance { get; set; }

        public virtual UUID Owner
        {
            get { return m_owner; }
            set { m_owner = value; }
        }

        public virtual int Serial
        {
            get { return m_serial; }
            set { m_serial = value; }
        }

        public virtual byte[] VisualParams
        {
            get { return m_visualparams; }
            set { m_visualparams = value; }
        }

        public virtual Primitive.TextureEntry Texture
        {
            get { return m_texture; }
            set { m_texture = value; }
        }

        public virtual float AvatarHeight
        {
            get { return m_avatarHeight; }
            set { m_avatarHeight = value; }
        }

        public virtual float HipOffset
        {
            get { return m_hipOffset; }
        }

        public static byte[] GetDefaultVisualParams()
        {
            byte[] visualParams = new byte[VISUALPARAM_COUNT];

            //            m_visualparams = new byte[] { 
            //                33,61,85,23,58,127,63,85,63,42,0,85,63,36,85,95,153,63,34,0,63,109,88,132,63,136,81,85,103,136,127,0,150,150,150,127,0,0,0,0,0,127,0,0,255,127,114,127,99,63,127,140,127,127,0,0,0,191,0,104,0,0,0,0,0,0,0,0,0,145,216,133,0,127,0,127,170,0,0,127,127,109,85,127,127,63,85,42,150,150,150,150,150,150,150,25,150,150,150,0,127,0,0,144,85,127,132,127,85,0,127,127,127,127,127,127,59,127,85,127,127,106,47,79,127,127,204,2,141,66,0,0,127,127,0,0,0,0,127,0,159,0,0,178,127,36,85,131,127,127,127,153,95,0,140,75,27,127,127,0,150,150,198,0,0,63,30,127,165,209,198,127,127,153,204,51,51,255,255,255,204,0,255,150,150,150,150,150,150,150,150,150,150,0,150,150,150,150,150,0,127,127,150,150,150,150,150,150,150,150,0,0,150,51,132,150,150,150 };

            // This sets Visual Params with *less* weirder values then default.
            for (int i = 0; i < visualParams.Length; i++)
                visualParams[i] = VISUALPARAM_DEFAULT;

            return visualParams;
        }

        public static Primitive.TextureEntry GetDefaultTexture()
        {
            Primitive.TextureEntry textu = new Primitive.TextureEntry(new UUID("C228D1CF-4B5D-4BA8-84F4-899A0796AA97"));
            textu.CreateFace(0).TextureID = new UUID("00000000-0000-1111-9999-000000000012");
            textu.CreateFace(1).TextureID = Util.BLANK_TEXTURE_UUID;
            textu.CreateFace(2).TextureID = Util.BLANK_TEXTURE_UUID;
            textu.CreateFace(3).TextureID = new UUID("6522E74D-1660-4E7F-B601-6F48C1659A77");
            textu.CreateFace(4).TextureID = new UUID("7CA39B4C-BD19-4699-AFF7-F93FD03D3E7B");
            textu.CreateFace(5).TextureID = new UUID("00000000-0000-1111-9999-000000000010");
            textu.CreateFace(6).TextureID = new UUID("00000000-0000-1111-9999-000000000011");
            return textu;
        }

        public AvatarAppearance() : this(UUID.Zero)
        {
        }

        public AvatarAppearance(UUID owner)
        {
            m_owner = owner;
            m_serial = 0;
            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
            m_wearables = new Dictionary<int, AvatarWearable>();
            
            SetDefaultWearables();
            SetDefaultTexture();
            SetDefaultParams();
            SetHeight();
        }

        public AvatarAppearance(OSDMap map)
        {
            Unpack(map);
            SetHeight();
        }

        public AvatarAppearance(UUID owner, AvatarWearable[] wearables, Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
//            m_log.WarnFormat("[AVATAR APPEARANCE] create initialized appearance");

            m_owner = owner;
            m_serial = 0;
            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
            m_wearables = new Dictionary<int, AvatarWearable>();

            if (wearables != null)
            {
                ClearWearables();
                SetWearables(new List<AvatarWearable>(wearables));
            }
            else
                SetDefaultWearables();

            if (textureEntry != null)
                m_texture = textureEntry;
            else
                SetDefaultTexture();

            if (visualParams != null)
                m_visualparams = visualParams;
            else
                SetDefaultParams();

            SetHeight();
        }

        public AvatarAppearance(AvatarAppearance appearance) : this(appearance, true)
        {
        }

        public AvatarAppearance(AvatarAppearance appearance, bool copyWearables)
        {
//            m_log.WarnFormat("[AVATAR APPEARANCE] create from an existing appearance");

            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
            m_wearables = new Dictionary<int, AvatarWearable>(); 

            if (appearance == null)
            {
                m_serial = 0;
                m_owner = UUID.Zero;
                SetDefaultWearables();
                SetDefaultTexture();
                SetDefaultParams();
                SetHeight();
                return;
            }

            m_owner = appearance.Owner;
            m_serial = appearance.Serial;

            if (copyWearables == true)
            {
                ClearWearables();
                SetWearables(appearance.GetWearables());
            }
            else
                SetDefaultWearables();

            m_texture = null;
            if (appearance.Texture != null)
            {
                byte[] tbytes = appearance.Texture.GetBytes();
                m_texture = new Primitive.TextureEntry(tbytes, 0, tbytes.Length);
            }
            else
            {
                SetDefaultTexture();
            }

            m_visualparams = null;
            if (appearance.VisualParams != null)
                m_visualparams = (byte[])appearance.VisualParams.Clone();
            else
                SetDefaultParams();

            IsBotAppearance = appearance.IsBotAppearance;

            SetHeight();

            SetAttachments(appearance.GetAttachments());
        }

        protected virtual void SetDefaultWearables()
        {
            SetWearables(AvatarWearable.GetDefaultWearables());
        }

        /// <summary>
        /// Invalidate all of the baked textures in the appearance, useful
        /// if you know that none are valid
        /// </summary>
        public virtual void ResetAppearance()
        {
//            m_log.WarnFormat("[AVATAR APPEARANCE]: Reset appearance");
            m_serial = 0;
            SetDefaultTexture();
        }
        
        protected virtual void SetDefaultParams()
        {
            m_visualparams = GetDefaultVisualParams();
            SetHeight();
        }

        protected virtual void SetDefaultTexture()
        {
            m_texture = GetDefaultTexture();
        }

        /// <summary>
        /// Set up appearance texture ids.
        /// </summary>
        /// <returns>
        /// True if any existing texture id was changed by the new data.
        /// False if there were no changes or no existing texture ids.
        /// </returns>
        public virtual bool SetTextureEntries(Primitive.TextureEntry textureEntry)
        {
            if (textureEntry == null)
                return false;

            // There are much simpler versions of this copy that could be
            // made. We determine if any of the textures actually
            // changed to know if the appearance should be saved later
            bool changed = false;
            for (uint i = 0; i < AvatarAppearance.TEXTURE_COUNT; i++)
            {
                Primitive.TextureEntryFace newface = textureEntry.FaceTextures[i];
                Primitive.TextureEntryFace oldface = m_texture.FaceTextures[i];

                if (newface == null)
                {
                    if (oldface == null)
                        continue;
                }
                else
                {
                    if (oldface != null && oldface.TextureID == newface.TextureID)
                        continue;
                }

                changed = true;
            }

            m_texture = textureEntry;
            return changed;
        }

        /// <summary>
        /// Set up visual parameters for the avatar and refresh the avatar height
        /// </summary>
        /// <returns>
        /// True if any existing visual parameter was changed by the new data.
        /// False if there were no changes or no existing visual parameters.
        /// </returns>
        public virtual bool SetVisualParams(byte[] visualParams)
        {
            bool changed = false;

            if (visualParams == null)
                return changed;

            // If the arrays are different sizes replace the whole thing.
            // its likely from different viewers
            if (visualParams.Length != m_visualparams.Length)
            {
                m_visualparams = (byte[])visualParams.Clone();
                changed = true;
            }
            else
            {
                for (int i = 0; i < visualParams.Length; i++)
                {
                    if (visualParams[i] != m_visualparams[i])
                    {
                        m_visualparams[i] = visualParams[i];
                        changed = true;
                    }
                }
            }

            // Reset the height if the visual parameters actually changed
            if (changed)
                SetHeight();

            return changed;
        }

        public virtual void SetAppearance(Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
            SetTextureEntries(textureEntry);
            SetVisualParams(visualParams);
            SetHeight();
        }

		public virtual void SetHeight()
		{
            m_avatarHeight = 1.23077f  // Shortest possible avatar height
                           + 0.516945f * (float)m_visualparams[25] / 255.0f   // Body height
                           + 0.072514f * (float)m_visualparams[120] / 255.0f  // Head size
                           + 0.3836f * (float)m_visualparams[125] / 255.0f    // Leg length
                           + 0.08f * (float)m_visualparams[77] / 255.0f    // Shoe heel height
                           + 0.07f * (float)m_visualparams[78] / 255.0f    // Shoe platform height
                           + 0.076f * (float)m_visualparams[148] / 255.0f;    // Neck length
            m_hipOffset = (0.615385f // Half of avatar
                           + 0.08f * (float)m_visualparams[77] / 255.0f    // Shoe heel height
                           + 0.07f * (float)m_visualparams[78] / 255.0f    // Shoe platform height
                           + 0.3836f * (float)m_visualparams[125] / 255.0f    // Leg length
                           - m_avatarHeight / 2) * 0.3f - 0.04f;

            //System.Console.WriteLine(">>>>>>> [APPEARANCE]: Height {0} Hip offset {1}" + m_avatarHeight + " " + m_hipOffset);
            //m_log.Debug("------------- Set Appearance Texture ---------------");
            //Primitive.TextureEntryFace[] faces = Texture.FaceTextures;
            //foreach (Primitive.TextureEntryFace face in faces)
            //{
            //    if (face != null)
            //        m_log.Debug("  ++ " + face.TextureID);
            //    else
            //        m_log.Debug("  ++ NULL ");
            //}
            //m_log.Debug("----------------------------");
        }

        // this is used for OGS1
        // It should go away soon in favor of the pack/unpack sections below
        public virtual Hashtable ToHashTable()
        {
            Hashtable h = new Hashtable();
            AvatarWearable wearable;

            h["owner"] = Owner.ToString();
            h["serial"] = Serial.ToString();
            h["visual_params"] = VisualParams;
            h["texture"] = Texture.GetBytes();
            h["avatar_height"] = AvatarHeight.ToString();

            wearable = GetWearableOfType(AvatarWearable.BODY);
            h["body_item"] = wearable.ItemID.ToString();
            h["body_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.SKIN);
            h["skin_item"] = wearable.ItemID.ToString();
            h["skin_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.HAIR);
            h["hair_item"] = wearable.ItemID.ToString();
            h["hair_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.EYES);
            h["eyes_item"] = wearable.ItemID.ToString();
            h["eyes_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.SHIRT);
            h["shirt_item"] = wearable.ItemID.ToString();
            h["shirt_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.PANTS);
            h["pants_item"] = wearable.ItemID.ToString();
            h["pants_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.SHOES);
            h["shoes_item"] = wearable.ItemID.ToString();
            h["shoes_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.SOCKS);
            h["socks_item"] = wearable.ItemID.ToString();
            h["socks_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.JACKET);
            h["jacket_item"] = wearable.ItemID.ToString();
            h["jacket_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.GLOVES);
            h["gloves_item"] = wearable.ItemID.ToString();
            h["gloves_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.UNDERSHIRT);
            h["undershirt_item"] = wearable.ItemID.ToString();
            h["undershirt_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.UNDERPANTS);
            h["underpants_item"] = wearable.ItemID.ToString();
            h["underpants_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.SKIRT);
            h["skirt_item"] = wearable.ItemID.ToString();
            h["skirt_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.ALPHA);
            h["alpha_item"] = wearable.ItemID.ToString();
            h["alpha_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.TATTOO);
            h["tattoo_item"] = wearable.ItemID.ToString();
            h["tattoo_asset"] = wearable.AssetID.ToString();
            wearable = GetWearableOfType(AvatarWearable.PHYSICS);
            h["physics_item"] = wearable.ItemID.ToString();
            h["physics_asset"] = wearable.AssetID.ToString();

            string attachments = GetAttachmentsString();
            if (attachments != String.Empty)
                h["attachments"] = attachments;

            return h;
        }

        public AvatarAppearance(Hashtable h)
        {
            if (h == null)
                return;

            if (h.ContainsKey("owner"))
                Owner = new UUID((string)h["owner"]);
            else
                Owner = UUID.Zero;

            if (h.ContainsKey("serial"))
                Serial = Convert.ToInt32((string)h["serial"]);

            if (h.ContainsKey("visual_params"))
                VisualParams = (byte[])h["visual_params"];
            else
                VisualParams = GetDefaultVisualParams();

            if (h.ContainsKey("texture") && ((byte[])h["texture"] != null))
            {
                byte[] textureData = (byte[])h["texture"];
                Texture = new Primitive.TextureEntry(textureData, 0, textureData.Length);
            }
            else
            {
                Texture = GetDefaultTexture();
            }

            if (h.ContainsKey("avatar_height"))
                AvatarHeight = (float)Convert.ToDouble((string)h["avatar_height"]);

            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
            m_wearables = new Dictionary<int, AvatarWearable>();

            ClearWearables();

            SetWearable(new AvatarWearable(AvatarWearable.BODY, new UUID((string)h["body_item"]), new UUID((string)h["body_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.SKIN, new UUID((string)h["skin_item"]), new UUID((string)h["skin_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.HAIR, new UUID((string)h["hair_item"]), new UUID((string)h["hair_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.EYES, new UUID((string)h["eyes_item"]), new UUID((string)h["eyes_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.SHIRT, new UUID((string)h["shirt_item"]), new UUID((string)h["shirt_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.PANTS, new UUID((string)h["pants_item"]), new UUID((string)h["pants_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.SHOES, new UUID((string)h["shoes_item"]), new UUID((string)h["shoes_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.SOCKS, new UUID((string)h["socks_item"]), new UUID((string)h["socks_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.JACKET, new UUID((string)h["jacket_item"]), new UUID((string)h["jacket_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.GLOVES, new UUID((string)h["gloves_item"]), new UUID((string)h["gloves_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.UNDERSHIRT, new UUID((string)h["undershirt_item"]), new UUID((string)h["undershirt_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.UNDERPANTS, new UUID((string)h["underpants_item"]), new UUID((string)h["underpants_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.SKIRT, new UUID((string)h["skirt_item"]), new UUID((string)h["skirt_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.ALPHA, new UUID((string)h["alpha_item"]), new UUID((string)h["alpha_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.TATTOO, new UUID((string)h["tattoo_item"]), new UUID((string)h["tattoo_asset"])));
            SetWearable(new AvatarWearable(AvatarWearable.PHYSICS, new UUID((string)h["physics_item"]), new UUID((string)h["physics_asset"])));

            if (h.ContainsKey("attachments"))
            {
                SetAttachmentsString(h["attachments"].ToString());
            }
        } 
        
        #region Wearables

        public void ClearWearables()
        {
            lock (m_wearables)
            {
                m_wearables.Clear();
                SetWearable(AvatarWearable.DEFAULT_BODY);
                SetWearable(AvatarWearable.DEFAULT_HAIR);
                SetWearable(AvatarWearable.DEFAULT_SKIN);
                SetWearable(AvatarWearable.DEFAULT_EYES);
            }
        }

        /// <summary>
        /// Get the wearable of type "i".
        /// </summary>
        /// <remarks>
        /// </remarks>
        public AvatarWearable GetWearableOfType(int i)
        {
            lock (m_wearables)
            {
                if (m_wearables.ContainsKey(i))
                    return (m_wearables[i]);
            }

            return (new AvatarWearable(i, UUID.Zero, UUID.Zero));
        }

        public void SetWearable(AvatarWearable wearable)
        {
            if ((wearable.WearableType < 0) || (wearable.WearableType >= AvatarWearable.MAX_WEARABLES))
            {
                m_log.WarnFormat("[AVATAR APPEARANCE]: AvatarWearable type {0} is out of range", wearable.WearableType);
                return;
            }

            if (AvatarWearable.IsRequiredWearable(wearable.WearableType) && (wearable.ItemID == UUID.Zero))
            {
                m_log.WarnFormat("[AVATAR APPEARANCE]: Refusing to set a ZERO wearable for a required item of type {0}", wearable.WearableType);
                return;
            }

            lock (m_wearables)
            {
                m_wearables[wearable.WearableType] = wearable;
            }
        }

        public List<AvatarWearable> GetWearables()
        {
            List<AvatarWearable> alist = new List<AvatarWearable>();

            lock (m_wearables)
            {
                return (new List<AvatarWearable>(m_wearables.Values));
            }
        }

        public List<int> GetWearableTypes()
        {
            lock (m_wearables)
            {
                return new List<int>(m_wearables.Keys);
            }
        }

        /// <summary>
        /// Rebuilds the entire list with locks held.  Use this.
        /// </summary>
        /// <param name="attachments"></param>
        public void SetWearables(List<AvatarWearable> wearables)
        {
            lock (m_wearables)
            {
                // Will also make sure reasonable defaults are applied
                ClearWearables();

                foreach (AvatarWearable wearable in wearables)
                {
                    SetWearable(wearable);
                }
            }
        }

        public AvatarWearable GetWearableForItem(UUID itemID)
        {
            lock (m_wearables)
            {
                foreach (KeyValuePair<int, AvatarWearable> kvp in m_wearables)
                {
                    if (kvp.Value.ItemID == itemID)
                        return (kvp.Value);
                }
            }
            return null;
        }

        public int GetWearableType(UUID itemID)
        {
            AvatarWearable wearable = GetWearableForItem(itemID);
            if (wearable == null)
                return AvatarWearable.NONE;
            else
                return (wearable.WearableType);
        }

        #endregion

        #region Attachments

        /// <summary>
        /// Get a list of the attachments.
        /// </summary>
        /// <remarks>
        /// There may be duplicate attachpoints
        /// </remarks>
        public List<AvatarAttachment> GetAttachments()
        {
            List<AvatarAttachment> alist = new List<AvatarAttachment>();

            lock (m_attachments)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    foreach (AvatarAttachment attach in kvp.Value)
                        alist.Add(attach);
                }
            }

            return alist;
        }

        public List<int> GetAttachedPoints()
        {
            lock (m_attachments)
            {
                return new List<int>(m_attachments.Keys);
            }
        }

        public List<AvatarAttachment> GetAttachmentsAtPoint(int attachPoint)
        {
            lock (m_attachments)
            {
                return (new List<AvatarAttachment>(m_attachments[attachPoint]));
            }
        }

        internal void AppendAttachment(AvatarAttachment attach)
        {
//            m_log.DebugFormat(
//                "[AVATAR APPEARNCE]: Appending itemID={0}, assetID={1} at {2}",
//                attach.ItemID, attach.AssetID, attach.AttachPoint);

            lock (m_attachments)
            {
                if (!m_attachments.ContainsKey(attach.AttachPoint))
                    m_attachments[attach.AttachPoint] = new List<AvatarAttachment>();
    
                m_attachments[attach.AttachPoint].Add(attach);
            }
        }

        internal void ReplaceAttachment(AvatarAttachment attach)
        {
//            m_log.DebugFormat(
//                "[AVATAR APPEARANCE]: Replacing itemID={0}, assetID={1} at {2}",
//                attach.ItemID, attach.AssetID, attach.AttachPoint);

            lock (m_attachments)
            {
                m_attachments[attach.AttachPoint] = new List<AvatarAttachment>();
                m_attachments[attach.AttachPoint].Add(attach);
            }
        }

        /// <summary>
        /// Set an attachment
        /// </summary>
        /// <remarks>
        /// Append or Replace based on the append flag
        /// If item is passed in as UUID.Zero, then an any attachment at the 
        /// attachpoint is removed.
        /// </remarks>
        /// <param name="attachpoint"></param>
        /// <param name="item"></param>
        /// <param name="asset"></param>
        /// <returns>
        /// return true if something actually changed
        /// </returns>
        public bool SetAttachment(int attachpoint, bool append, UUID item, UUID asset)
        {
            //            m_log.DebugFormat(
            //                "[AVATAR APPEARANCE]: Setting attachment at {0} with item ID {1}, asset ID {2}",
            //                 attachpoint, item, asset);

            if (attachpoint == 0)
                return false;

            if (item == UUID.Zero)
            {
                lock (m_attachments)
                {
                    if (m_attachments.ContainsKey(attachpoint))
                    {
                        m_attachments.Remove(attachpoint);
                        return true;
                    }
                }

                return false;
            }

            if (append)
                AppendAttachment(new AvatarAttachment(attachpoint, item, asset));
            else
                ReplaceAttachment(new AvatarAttachment(attachpoint, item, asset));

            return true;
        }   

        /// <summary>
        /// Rebuilds the entire list with locks held.  Use this.
        /// </summary>
        /// <param name="attachments"></param>
        public void SetAttachments(List<AvatarAttachment> attachments)
        {
            lock (m_attachments)
            {
                m_attachments.Clear();

                foreach (AvatarAttachment attachment in attachments)
                {
                    if (!m_attachments.ContainsKey(attachment.AttachPoint))
                        m_attachments.Add(attachment.AttachPoint, new List<AvatarAttachment>());

                    m_attachments[attachment.AttachPoint].Add(attachment);
                }
            }
        }

        /// <summary>
        /// Rebuilds the entire list with locks held.  Use this.
        /// </summary>
        /// <param name="attachments"></param>
        public void SetAttachmentsForPoint(int attachPoint, List<AvatarAttachment> attachments)
        {
            lock (m_attachments)
            {
                m_attachments.Remove(attachPoint);
                foreach (AvatarAttachment attachment in attachments)
                {
                    if (!m_attachments.ContainsKey(attachment.AttachPoint))
                        m_attachments.Add(attachment.AttachPoint, new List<AvatarAttachment>());
                    m_attachments[attachment.AttachPoint].Add(attachment);
                }
            }
        }

        /// <summary>
        /// If the item is already attached, return it.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Returns null if this item is not attached.</returns>
        public AvatarAttachment GetAttachmentForItem(UUID itemID)
        {
            lock (m_attachments)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    int index = kvp.Value.FindIndex(delegate(AvatarAttachment a) { return a.ItemID == itemID; });
                    if (index >= 0)
                        return kvp.Value[index];
                }
            }

            return null;
        }

        public int GetAttachpoint(UUID itemID)
        {
            lock (m_attachments)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    int index = kvp.Value.FindIndex(delegate(AvatarAttachment a) { return a.ItemID == itemID; });
                    if (index >= 0)
                        return kvp.Key;
                }
            }

            return 0;
        }

        /// <summary>
        /// Remove an attachment if it exists
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>The AssetID for detached asset or UUID.Zero</returns>
        public UUID DetachAttachment(UUID itemID)
        {
            UUID assetID;

            lock (m_attachments)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    int index = kvp.Value.FindIndex(delegate(AvatarAttachment a) { return a.ItemID == itemID; });
                    if (index >= 0)
                    {
                        AvatarAttachment attachment = m_attachments[kvp.Key][index];
                        assetID = attachment.AssetID;

                        // Remove it from the list of attachments at that attach point
                        m_attachments[kvp.Key].RemoveAt(index);
    
                        // And remove the list if there are no more attachments here
                        if (m_attachments[kvp.Key].Count == 0)
                            m_attachments.Remove(kvp.Key);

                        return assetID;
                    }
                }
            }

            return UUID.Zero;
        }

        string GetAttachmentsString()
        {
            List<string> strings = new List<string>();

            lock (m_attachments)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    foreach (AvatarAttachment attachment in kvp.Value)
                    {
                        strings.Add(attachment.AttachPoint.ToString());
                        strings.Add(attachment.ItemID.ToString());
                        strings.Add(attachment.AssetID.ToString());
                    }
                }
            }

            return String.Join(",", strings.ToArray());
        }

        void SetAttachmentsString(string data)
        {
            string[] strings = data.Split(new char[] { ',' });
            int i = 0;

            List<AvatarAttachment> attachments = new List<AvatarAttachment>();

            while (strings.Length - i > 2)
            {
                int attachpoint = Int32.Parse(strings[i]);
                UUID item = new UUID(strings[i + 1]);
                UUID sogId = new UUID(strings[i + 2]);
                i += 3;

                AvatarAttachment attachment = new AvatarAttachment(attachpoint, item, sogId);
                attachments.Add(attachment);
            }

            SetAttachments(attachments);
        }

        public void ClearAttachments()
        {
            lock (m_attachments)
                m_attachments.Clear();
        }

        #endregion

        #region Packing Functions

        /// <summary>
        /// Create an OSDMap from the appearance data
        /// </summary>
        public OSDMap Pack()
        {
            OSDMap data = new OSDMap();

            data["owner"] = OSD.FromUUID(Owner);
            data["serial"] = OSD.FromInteger(m_serial);
            data["height"] = OSD.FromReal(m_avatarHeight);

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

            return data;
        } 

        /// <summary>
        /// Unpack and OSDMap and initialize the appearance
        /// from it
        /// </summary>
        public void Unpack(OSDMap data)
        {
            if (data == null)
            {
                m_log.Warn("[AVATAR APPEARANCE]: failed to unpack avatar appearance");
                return;
            }

            if (data.ContainsKey("owner"))
                m_owner = data["owner"].AsUUID();
            else
                m_owner = UUID.Zero;

            if (data.ContainsKey("serial"))
                m_serial = data["serial"].AsInteger();

            if (data.ContainsKey("height"))
                m_avatarHeight = (float)data["height"].AsReal();

            try
            {
                // Wearablles
                m_wearables = new Dictionary<int, AvatarWearable>();
                ClearWearables();

                if (data.ContainsKey("wearables") && ((data["wearables"]).Type == OSDType.Array))
                {
                    OSDArray wears = (OSDArray)data["wearables"];
                    for (int i = 0; i < wears.Count; i++)
                    {
                        AvatarWearable wearable = new AvatarWearable((OSDMap)wears[i]);
                        SetWearable(wearable);
                    }
                }
                else
                {
                    m_log.Warn("[AVATAR APPEARANCE]: failed to unpack wearables");
                }

                // Avatar Textures
                SetDefaultTexture();
                if (data.ContainsKey("textures") && ((data["textures"]).Type == OSDType.Array))
                {
                    OSDArray textures = (OSDArray)(data["textures"]);
                    for (int i = 0; i < AvatarAppearance.TEXTURE_COUNT && i < textures.Count; i++)
                    {
                        UUID textureID = AppearanceManager.DEFAULT_AVATAR_TEXTURE;
                        if (textures[i] != null)
                            textureID = textures[i].AsUUID();
                        m_texture.CreateFace((uint)i).TextureID = new UUID(textureID);
                    }
                }
                else
                {
                    m_log.Warn("[AVATAR APPEARANCE]: failed to unpack textures");
                }

                // Visual Parameters
                SetDefaultParams();
                if (data.ContainsKey("visualparams"))
                {
                    if ((data["visualparams"].Type == OSDType.Binary) || (data["visualparams"].Type == OSDType.Array))
                        m_visualparams = data["visualparams"].AsBinary();
                }
                else
                {
                    m_log.Warn("[AVATAR APPEARANCE]: failed to unpack visual parameters");
                }

                // Attachments
                m_attachments = new Dictionary<int, List<AvatarAttachment>>();
                if (data.ContainsKey("attachments") && ((data["attachments"]).Type == OSDType.Array))
                {
                    OSDArray attachs = (OSDArray)(data["attachments"]);
                    for (int i = 0; i < attachs.Count; i++)
                    {
                        AvatarAttachment att = new AvatarAttachment((OSDMap)attachs[i]);
                        AppendAttachment(att);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[AVATAR APPEARANCE]: unpack failed badly: {0}{1}", e.Message, e.StackTrace);
            }
        }

        #endregion
    }
}
