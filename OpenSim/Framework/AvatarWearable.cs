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
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public class AvatarWearable
    {
        // these are guessed at by the list here -
        // http://wiki.secondlife.com/wiki/Avatar_Appearance.  We'll
        // correct them over time for when were are wrong.
        // See updated list at: http://lecs.opensource.secondlife.com/doxygen/llwearabletype_8h_source.html
        public readonly static int BODY = 0;
        public readonly static int SKIN = 1;
        public readonly static int HAIR = 2;
        public readonly static int EYES = 3;
        public readonly static int SHIRT = 4;
        public readonly static int PANTS = 5;
        public readonly static int SHOES = 6;
        public readonly static int SOCKS = 7;
        public readonly static int JACKET = 8;
        public readonly static int GLOVES = 9;
        public readonly static int UNDERSHIRT = 10;
        public readonly static int UNDERPANTS = 11;
        public readonly static int SKIRT = 12;
        public readonly static int ALPHA = 13;
        public readonly static int TATTOO = 14;
        public readonly static int PHYSICS = 15;

        public readonly static int INVALID = 255;
        public readonly static int NONE = -1;

        public readonly static int MAX_WEARABLES_1X = 15;
        public readonly static int MAX_WEARABLES = 16;

        public static readonly UUID DEFAULT_BODY_ITEM = new UUID("66c41e39-38f9-f75a-024e-585989bfaba9");
        public static readonly UUID DEFAULT_BODY_ASSET = new UUID("66c41e39-38f9-f75a-024e-585989bfab73");

        public static readonly UUID DEFAULT_HAIR_ITEM = new UUID("d342e6c1-b9d2-11dc-95ff-0800200c9a66");
        public static readonly UUID DEFAULT_HAIR_ASSET = new UUID("d342e6c0-b9d2-11dc-95ff-0800200c9a66");

        public static readonly UUID DEFAULT_SKIN_ITEM = new UUID("77c41e39-38f9-f75a-024e-585989bfabc9");
        public static readonly UUID DEFAULT_SKIN_ASSET = new UUID("77c41e39-38f9-f75a-024e-585989bbabbb");

        public static readonly UUID DEFAULT_EYES_ITEM = new UUID("4d3af499-976b-400a-a04a-f13df0babc0b");
        public static readonly UUID DEFAULT_EYES_ASSET = new UUID("4bb6fa4d-1cd2-498a-a84c-95c1a0e745a7");

        public static readonly UUID DEFAULT_SHIRT_ITEM = new UUID("77c41e39-38f9-f75a-0000-585989bf0000");
        public static readonly UUID DEFAULT_SHIRT_ASSET = new UUID("00000000-38f9-1111-024e-222222111110");

        public static readonly UUID DEFAULT_PANTS_ITEM = new UUID("77c41e39-38f9-f75a-0000-5859892f1111");
        public static readonly UUID DEFAULT_PANTS_ASSET = new UUID("00000000-38f9-1111-024e-222222111120");

        //        public static readonly UUID DEFAULT_ALPHA_ITEM = new UUID("bfb9923c-4838-4d2d-bf07-608c5b1165c8");
        //        public static readonly UUID DEFAULT_ALPHA_ASSET = new UUID("1578a2b1-5179-4b53-b618-fe00ca5a5594");

        //        public static readonly UUID DEFAULT_TATTOO_ITEM = new UUID("c47e22bd-3021-4ba4-82aa-2b5cb34d35e1");
        //        public static readonly UUID DEFAULT_TATTOO_ASSET = new UUID("00000000-0000-2222-3333-100000001007");
        
        public static readonly AvatarWearable DEFAULT_BODY = new AvatarWearable(BODY, DEFAULT_BODY_ITEM, DEFAULT_BODY_ASSET);
        public static readonly AvatarWearable DEFAULT_HAIR = new AvatarWearable(HAIR, DEFAULT_HAIR_ITEM, DEFAULT_HAIR_ASSET);
        public static readonly AvatarWearable DEFAULT_SKIN = new AvatarWearable(SKIN, DEFAULT_SKIN_ITEM, DEFAULT_SKIN_ASSET);
        public static readonly AvatarWearable DEFAULT_EYES = new AvatarWearable(EYES, DEFAULT_EYES_ITEM, DEFAULT_EYES_ASSET);
        public static readonly AvatarWearable DEFAULT_SHIRT = new AvatarWearable(SHIRT, DEFAULT_SHIRT_ITEM, DEFAULT_SHIRT_ASSET);
        public static readonly AvatarWearable DEFAULT_PANTS = new AvatarWearable(PANTS, DEFAULT_PANTS_ITEM, DEFAULT_PANTS_ASSET);

        public int WearableType;
        public UUID ItemID;
        public UUID AssetID;

        public AvatarWearable(int wearableType, UUID itemID, UUID assetID)
        {
            WearableType = wearableType;
            ItemID = itemID;
            AssetID = assetID;
        }

        public AvatarWearable(AvatarWearable wearable)
        {
            WearableType = wearable.WearableType;
            ItemID = wearable.ItemID;
            AssetID = wearable.AssetID;
        }

        public AvatarWearable(OSDMap args)
        {
            Unpack(args);
        }

        public static bool IsRequiredWearable(int wearableType)
        {
            if ((wearableType == BODY) || (wearableType == SKIN) ||
                (wearableType == HAIR) || (wearableType == EYES))
                return true;
            else
                return false;
        }
        
        public static List<AvatarWearable> GetDefaultWearables()
        {
            List<AvatarWearable> defaultWearables = new List<AvatarWearable>();

            defaultWearables.Add(DEFAULT_BODY);
            defaultWearables.Add(DEFAULT_HAIR);
            defaultWearables.Add(DEFAULT_SKIN);
            defaultWearables.Add(DEFAULT_EYES); 
            defaultWearables.Add(DEFAULT_SHIRT);
            defaultWearables.Add(DEFAULT_PANTS);

            return defaultWearables;
        }

        public OSDMap Pack()
        {
            OSDMap itemdata = new OSDMap();
            itemdata["type"] = OSD.FromInteger(WearableType);
            itemdata["item"] = OSD.FromUUID(ItemID);
            itemdata["asset"] = OSD.FromUUID(AssetID);

            return itemdata;
        }

        public void Unpack(OSDMap args)
        {
            WearableType = (args["type"] != null) ? args["type"].AsInteger() : AvatarWearable.INVALID;
            ItemID = (args["item"] != null) ? args["item"].AsUUID() : UUID.Zero;
            AssetID = (args["asset"] != null) ? args["asset"].AsUUID() : UUID.Zero;
        }    
    }

}
