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
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    /// <summary>
    /// Carries information about an asset request
    /// </summary>
    public class AssetRequestInfo
    {
        public static AssetRequestInfo InternalRequest()
        {
            return new AssetRequestInfo(RequestOrigin.SRC_INTERNAL);
        }

        public static AssetRequestInfo GenericNetRequest()
        {
            return new AssetRequestInfo(RequestOrigin.SRC_NET);
        }


        private RequestOrigin _origin;
        private NetSourceType _netSrc;
        private AssetRequestCallback _callback;
        private UUID _requestedId;
        private UUID _transferId;
        private byte[] _params;

        /// <summary>
        /// Where is this request coming from
        /// </summary>
        public enum RequestOrigin
        {
            SRC_INTERNAL,
            SRC_NET
        }

        /// <summary>
        /// The client identifying it's source (net only)
        /// </summary>
        public enum NetSourceType
        {
            LLTST_UNKNOWN = 0,
            LLTST_FILE = 1,
            LLTST_ASSET = 2,
            LLTST_SIM_INV_ITEM = 3,
            LLTST_SIM_ESTATE = 4,
            LLTST_NUM_TYPES = 5
        }

        /// <summary>
        /// Channel this is being requested from (net only)
        /// </summary>
        public enum NetChannelType
        {
            LLTCT_UNKNOWN = 0,
            LLTCT_MISC = 1,
            LLTCT_ASSET = 2,
            LLTCT_NUM_TYPES = 3
        }

        public RequestOrigin Origin
        {
            get
            {
                return _origin;
            }
        }

        public NetSourceType NetSource
        {
            get
            {
                return _netSrc;
            }
        }

        public AssetRequestCallback Callback
        {
            get
            {
                return _callback;
            }

            set
            {
                _callback = value;
            }
        }

        public UUID AssetId
        {
            get
            {
                return _requestedId;
            }

            set
            {
                _requestedId = value;
            }
        }

        public UUID TransferId
        {
            get
            {
                return _transferId;
            }
        }

        public byte[] Params
        {
            get
            {
                return _params;
            }
        }

        public ulong RequestTime = Util.GetLongTickCount();

        public ulong RequestDuration
        {
            get { return Util.GetLongTickCount() - RequestTime; }
        }

        /// <summary>
        /// Updated internally by asset services to track what server this request was last tried on
        /// </summary>
        public short ServerNumber { get; set; }

        /// <summary>
        /// Creates info from a net request
        /// </summary>
        /// <param name="fromReq"></param>
        public AssetRequestInfo(TransferRequestPacket fromReq, IClientAPI userInfo)
        {
            _origin = RequestOrigin.SRC_NET;
            _netSrc = (NetSourceType)fromReq.TransferInfo.SourceType;

            if (fromReq.TransferInfo.SourceType == 2)
            {
                //direct asset request
                _requestedId = new UUID(fromReq.TransferInfo.Params, 0);
            }
            else if (fromReq.TransferInfo.SourceType == 3)
            {
                //inventory asset request
                _requestedId = new UUID(fromReq.TransferInfo.Params, 80);
            }
            else if (fromReq.TransferInfo.SourceType == 4)
            {
                //sim estate request
                //We have to just send the covenent in this case,
                // I've looked through the params, and the UUID
                // of the covenent isn't in there (Matt Beardmore)
                _requestedId = userInfo.Scene.RegionInfo.RegionSettings.Covenant;
            }

            _transferId = fromReq.TransferInfo.TransferID;
            _params = fromReq.TransferInfo.Params;
        }

        /// <summary>
        /// Creates request info with the given origin
        /// </summary>
        /// <param name="origin"></param>
        public AssetRequestInfo(RequestOrigin origin)
        {
            _origin = origin;
        }
    }
}
