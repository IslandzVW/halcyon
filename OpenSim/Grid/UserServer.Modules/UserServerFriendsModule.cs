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
using System.IO;
using System.Net;
using log4net;
using Nwc.XmlRpc;
using ProtoBuf;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Messages;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.UserServer.Modules
{
    public class UserServerFriendsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UserDataBaseService m_userDataBaseService;

        private BaseHttpServer m_httpServer;

        public UserServerFriendsModule(UserDataBaseService userDataBaseService)
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

            m_httpServer.AddXmlRPCHandler("add_new_user_friend", XmlRpcResponseXmlRPCAddUserFriend);
            m_httpServer.AddXmlRPCHandler("remove_user_friend", XmlRpcResponseXmlRPCRemoveUserFriend);
            m_httpServer.AddXmlRPCHandler("update_user_friend_perms", XmlRpcResponseXmlRPCUpdateUserFriendPerms);
            m_httpServer.AddXmlRPCHandler("get_user_friend_list", XmlRpcResponseXmlRPCGetUserFriendList);

            // New Style
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("add_new_user_friend"), XmlRpcResponseXmlRPCAddUserFriend));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("remove_user_friend"), XmlRpcResponseXmlRPCRemoveUserFriend));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("update_user_friend_perms"), XmlRpcResponseXmlRPCUpdateUserFriendPerms));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_user_friend_list"), XmlRpcResponseXmlRPCGetUserFriendList));

            // Protobuf Handlers
            m_httpServer.AddStreamHandler(new BufferStreamHandler("POST", "/get_user_friend_list2/", HandleGetUserFriendList2));
        }

        public XmlRpcResponse FriendListItemListtoXmlRPCResponse(List<FriendListItem> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information

            responseData["avcount"] = returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["ownerID" + i] = returnUsers[i].FriendListOwner.ToString();
                responseData["friendID" + i] = returnUsers[i].Friend.ToString();
                responseData["ownerPerms" + i] = returnUsers[i].FriendListOwnerPerms.ToString();
                responseData["friendPerms" + i] = returnUsers[i].FriendPerms.ToString();
            }
            response.Value = responseData;

            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCAddUserFriend(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information

            if (requestData.Contains("ownerID") && requestData.Contains("friendID") &&
                requestData.Contains("friendPerms"))
            {
                // UserManager.AddNewuserFriend
                m_userDataBaseService.AddNewUserFriend(new UUID((string)requestData["ownerID"]),
                                 new UUID((string)requestData["friendID"]),
                                 (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCRemoveUserFriend(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information

            if (requestData.Contains("ownerID") && requestData.Contains("friendID"))
            {
                // UserManager.AddNewuserFriend
                m_userDataBaseService.RemoveUserFriend(new UUID((string)requestData["ownerID"]),
                                 new UUID((string)requestData["friendID"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCUpdateUserFriendPerms(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";

            if (requestData.Contains("ownerID") && requestData.Contains("friendID") &&
                requestData.Contains("friendPerms"))
            {
                // UserManager.UpdateUserFriendPerms
                m_userDataBaseService.UpdateUserFriendPerms(new UUID((string)requestData["ownerID"]),
                                      new UUID((string)requestData["friendID"]),
                                      (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCGetUserFriendList(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());
            
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            // Hashtable responseData = new Hashtable();

            List<FriendListItem> returndata = new List<FriendListItem>();

            if (requestData.Contains("ownerID"))
            {
                UUID friendlistowner = new UUID((string)requestData["ownerID"]);
                m_log.Warn("[FRIEND]: XmlRpcResponseXmlRPCGetUserFriendList was called for " + friendlistowner.ToString());
                returndata = m_userDataBaseService.GetUserFriendList(friendlistowner);
            }

            return FriendListItemListtoXmlRPCResponse(returndata);
        }

        //(Stream request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        public byte[] HandleGetUserFriendList2(Stream requestStream, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            Util.SlowTimeReporter slowCheck = new Util.SlowTimeReporter("[FRIEND]: GetUserFriendList2 took", TimeSpan.FromMilliseconds(1000));
            try
            {
                // Check IP Endpoint Access
                if (!TrustManager.Instance.IsTrustedPeer(httpRequest.RemoteIPEndPoint))
                {
                    httpResponse.StatusCode = 401;
                    return new byte[0];
                }

                // Request/response succeeded.
                FriendsListRequest request = ProtoBuf.Serializer.Deserialize<FriendsListRequest>(requestStream);
                UUID friendlistowner = request.ToUUID();

                // Now perform the actual friends lookup.
                List<FriendListItem> returndata = m_userDataBaseService.GetUserFriendList(friendlistowner);

                // Generate and send the response.
                if (returndata == null)
                    returndata = new List<FriendListItem>();

                httpResponse.StatusCode = 200;
                return FriendsListResponse.ToBytes(returndata);
            }
            finally
            {
                slowCheck.Complete();
            }
        }
    }
}
