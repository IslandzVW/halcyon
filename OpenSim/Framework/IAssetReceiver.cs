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

namespace OpenSim.Framework
{
    /// <summary>
    /// Implemented by classes which with to asynchronously receive asset data from the asset service
    /// </summary>
    /// <remarks>could change to delegate?</remarks>
    public interface IAssetReceiver
    {
        /// <summary>
        /// Call back made when a requested asset has been retrieved by an asset server
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="IsTexture"></param>
        void AssetReceived(AssetBase asset, AssetRequestInfo data);

        /// <summary>
        /// Call back made when an asset server could not find a requested asset
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="IsTexture"></param>
        void AssetNotFound(OpenMetaverse.UUID assetID, AssetRequestInfo data);

        /// <summary>
        /// Call back made when there was an error trying to fetch or store an asset
        /// </summary>
        /// <param name="assetID">The UUID of the asset that was requested</param>
        /// <param name="e">The exception that was thrown</param>
        /// <param name="data">Request data</param>
        void AssetError(OpenMetaverse.UUID assetID, Exception e, AssetRequestInfo data);
    }
}
