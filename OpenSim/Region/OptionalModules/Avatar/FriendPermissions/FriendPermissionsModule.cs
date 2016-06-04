/*
 * Copyright(C) 2009 Inworldz LLC
 * Initial Revision:  2009-12-14 David C. Daeschler
 */
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using log4net;
using System.Reflection;
using OpenSim.Data.SimpleDB;
using Nini.Config;

namespace OpenSim.Region.OptionalModules.Avatar.FriendPermissions
{
    class FriendPermissionsModule : INonSharedRegionModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OpenSim.Region.Framework.Scenes.Scene m_scene = null;

        //connection factory 
        private ConnectionFactory _connFactory;

        #region IRegionModuleBase Members

        public string Name
        {
            get { return "FriendPermissionsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialize(Nini.Config.IConfigSource source)
        {
            //read our connection string 
            IConfig userConfig = source.Configs["UserService"];

            string connString = String.Empty;

            if (userConfig != null)
            {
                connString = userConfig.GetString("ConnectionString", String.Empty);
            }

            if (String.IsNullOrEmpty(connString))
            {
                userConfig = source.Configs["StandAlone"];
                connString = userConfig.GetString("user_source", String.Empty);
            }

            _connFactory = new ConnectionFactory("MySQL", connString);
        }

        public void Close()
        {
            
        }

        public void AddRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            scene.EventManager.OnNewClient += OnNewClient;
            m_scene = scene;
        }

        public void RemoveRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            
        }

        public void RegionLoaded(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            //m_log.DebugFormat("[FRIENDPERMS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            client.OnGrantUserRights += new GrantUserRights(client_OnGrantUserRights);
        }

        void client_OnGrantUserRights(IClientAPI sender, OpenMetaverse.UUID grantor, OpenMetaverse.UUID grantee, int rights)
        {
            if (sender.AgentId != grantor)
            {
                return;
            }

            //set the user rights on the DB
            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("rights", rights);
                parms.Add("ownerID", grantor);
                parms.Add("friendID", grantee);

                db.QueryNoResults(  "UPDATE userfriends " +
                                        "SET friendPerms = ?rights " +
                                    "WHERE ownerID = ?ownerID AND friendID = ?friendID;",
                                    parms );
            }

            m_scene.CommsManager.UserService.UpdateUserFriendPerms(grantor, grantee, (uint)rights);
            sender.SendChangeUserRights(grantor, grantee, rights);
            Framework.Scenes.ScenePresence receiver = m_scene.GetScenePresence(grantee);
            if ((receiver != null) && (receiver.ControllingClient != null))
                receiver.ControllingClient.SendChangeUserRights(grantor, grantee, rights);
        }
    }
}
