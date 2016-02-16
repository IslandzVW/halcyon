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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.UserServer.Modules
{
    public class UserServerAvatarAppearanceModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UserDataBaseService m_userDataBaseService;
        private BaseHttpServer m_httpServer;

        public UserServerAvatarAppearanceModule(UserDataBaseService userDataBaseService)
        {
            m_userDataBaseService = userDataBaseService;
        }

        public void Initialize(IGridServiceCore core)
        {

        }

        public void PostInitialize()
        {

        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;
            
            m_httpServer.AddXmlRPCHandler("get_avatar_appearance", XmlRPCGetAvatarAppearance);
            m_httpServer.AddXmlRPCHandler("get_bot_outfit", XmlRPCGetBotOutfit);
            m_httpServer.AddXmlRPCHandler("update_avatar_appearance", XmlRPCUpdateAvatarAppearance);
            m_httpServer.AddXmlRPCHandler("add_bot_outfit", XmlRPCAddBotOutfit);
            m_httpServer.AddXmlRPCHandler("remove_bot_outfit", XmlRPCRemoveBotOutfit);
            m_httpServer.AddXmlRPCHandler("get_bot_outfits_by_owner", XmlRPCGetBotOutfitsByOwner);
            m_httpServer.AddXmlRPCHandler("get_cached_baked_textures", XmlRPCGetCachedBakedTextures);
            m_httpServer.AddXmlRPCHandler("set_cached_baked_textures", XmlRPCSetCachedBakedTextures);

            // New Style
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_avatar_appearance"), XmlRPCGetAvatarAppearance));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_bot_outfit"), XmlRPCGetBotOutfit));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("update_avatar_appearance"), XmlRPCUpdateAvatarAppearance));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("add_bot_outfit"), XmlRPCAddBotOutfit));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("remove_bot_outfit"), XmlRPCRemoveBotOutfit));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_bot_outfits_by_owner"), XmlRPCGetBotOutfitsByOwner));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_cached_baked_textures"), XmlRPCGetCachedBakedTextures));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("set_cached_baked_textures"), XmlRPCSetCachedBakedTextures));
        }

        public XmlRpcResponse XmlRPCGetAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            AvatarAppearance appearance;
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                appearance = m_userDataBaseService.GetUserAppearance(new UUID((string)requestData["owner"]));
                if (appearance == null)
                {
                    responseData = new Hashtable();
                    responseData["error_type"] = "no appearance";
                    responseData["error_desc"] = "There was no appearance found for this avatar";
                }
                else
                {
                    responseData = appearance.ToHashTable();
                }
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }

            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCGetBotOutfit(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            AvatarAppearance appearance;
            Hashtable responseData;
            if (requestData.Contains("owner") && requestData.Contains("outfitName"))
            {
                appearance = m_userDataBaseService.GetBotOutfit(new UUID((string)requestData["owner"]), (string)requestData["outfitName"]);
                if (appearance == null)
                {
                    responseData = new Hashtable();
                    responseData["error_type"] = "no appearance";
                    responseData["error_desc"] = "There was no appearance found for this bot outfit";
                }
                else
                {
                    responseData = appearance.ToHashTable();
                }
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The outfit requested is not in the database";
            }

            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCUpdateAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                AvatarAppearance appearance = new AvatarAppearance(requestData);
                m_userDataBaseService.UpdateUserAppearance(new UUID((string)requestData["owner"]), appearance);
                responseData = new Hashtable();
                responseData["returnString"] = "TRUE";
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCAddBotOutfit(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData;
            if (requestData.Contains("owner") && requestData.Contains("outfitName"))
            {
                AvatarAppearance appearance = new AvatarAppearance(requestData);
                m_userDataBaseService.AddOrUpdateBotOutfit(new UUID((string)requestData["owner"]), (string)requestData["outfitName"], appearance);
                responseData = new Hashtable();
                responseData["returnString"] = "TRUE";
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCRemoveBotOutfit(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData;
            if (requestData.Contains("userID") && requestData.Contains("outfitName"))
            {
                m_userDataBaseService.RemoveBotOutfit(new UUID((string)requestData["userID"]), (string)requestData["outfitName"]);
                responseData = new Hashtable();
                responseData["returnString"] = "TRUE";
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCGetBotOutfitsByOwner(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            Hashtable responseData = new Hashtable();
            if (requestData.Contains("userID"))
            {
                List<string> responseArgs = m_userDataBaseService.GetBotOutfitsByOwner(new UUID((string)requestData["userID"]));
                
                if (responseArgs != null)
                {
                    int i = 0;
                    foreach (string resp in responseArgs)
                        responseData.Add((i++).ToString(), resp);
                    responseData["returnString"] = "TRUE";
                }
                else
                    responseData["returnString"] = "FALSE";
            }
            else
                responseData["returnString"] = "FALSE";
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCGetCachedBakedTextures(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            List<CachedAgentArgs> requestArgs = new List<CachedAgentArgs>();
            foreach (object key in requestData.Keys)
                requestArgs.Add(new CachedAgentArgs() { TextureIndex = byte.Parse(key.ToString()), ID = UUID.Parse(requestData[key].ToString()) });

            List<CachedAgentArgs> responseArgs = m_userDataBaseService.GetCachedBakedTextures(requestArgs);
            Hashtable responseData = new Hashtable();

            if (responseArgs != null)
            {
                foreach (CachedAgentArgs resp in responseArgs)
                    responseData.Add(resp.TextureIndex.ToString(), resp.ID.ToString());
                responseData["returnString"] = "TRUE";
            }
            else
                responseData["returnString"] = "FALSE";
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCSetCachedBakedTextures(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            Dictionary<UUID, UUID> textures = new Dictionary<UUID, UUID>();
            foreach (object key in requestData.Keys)
                textures.Add(UUID.Parse(key.ToString()), UUID.Parse(requestData[key].ToString()));

            m_userDataBaseService.SetCachedBakedTextures(textures);

            Hashtable responseData = new Hashtable();
            responseData["returnString"] = "TRUE";
            response.Value = responseData;
            return response;
        }
    }
}
