/* Originally started back last year, and completely rewritten from the ground up to handle the
 * Profiles for InWorldz, LLC.
 * Modified again on 1/22/2011 by Beth Reischl to:
 * Pull a couple of DB queries from the Classifieds section that were not needed
 * Pulled a DB query from Profiles and modified another one.
 * Pulled out the queryParcelUUID from PickInfoUpdate method, this was resulting in null parcel IDs
 * being passed all the time.
 * Fixed the PickInfoUpdate and PickInfoRequest to show Username, Parcel Information, Region Name, (xyz) 
 * coords for proper teleportation
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Data.SimpleDB;

namespace OpenSimProfile.Modules.OpenProfile
{
    public class OpenProfileModule : IRegionModule
    {
        //
        // Log module
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private bool m_Enabled = true;

        private ConnectionFactory _connFactory;
        private ConnectionFactory _regionConnFactory;

        private string _currencyString = "I'z$";

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (!m_Enabled)
                return;

            //TODO: At some point, strip this out of the ini file, in Search Module, 
            // we're going to recycle the Profile connection string however to make 
            // life a bit easier.
            IConfig profileConfig = config.Configs["Startup"];
            string connstr = profileConfig.GetString("core_connection_string", String.Empty);


            m_log.Info("[PROFILE] Profile module is activated");

            //TODO: Bad assumption on my part that we're enabling it and the connstr is actually
            // valid, but I'm sick of dinking with this thing  :)
            m_Enabled = true;

            _connFactory = new ConnectionFactory("MySQL", connstr);

            string storageConnStr = profileConfig.GetString("storage_connection_string", String.Empty);

            _regionConnFactory = new ConnectionFactory("MySQL", storageConnStr);

            IMoneyModule mm = scene.RequestModuleInterface<IMoneyModule>();
            if (mm != null)
            {
                _currencyString = mm.GetCurrencySymbol();
            }

            // Hook up events
            scene.EventManager.OnNewClient += OnNewClient;
        }

        public void PostInitialize()
        {
            if (!m_Enabled)
                return;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ProfileModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        /// New Client Event Handler
        private void OnNewClient(IClientAPI client)
        {
            // Classifieds
            client.AddGenericPacketHandler("avatarclassifiedsrequest", HandleAvatarClassifiedsRequest);
            client.OnClassifiedInfoUpdate += ClassifiedInfoUpdate;
            client.OnClassifiedDelete += ClassifiedDelete;

            // Picks
            //TODO: there is an error generated here in the Grid as we've removed the
            // need for this and wrapped it down below. This needs to be fixed.
            // This applies to any reqeusts made for general info.

            //client.AddGenericPacketHandler("avatarpicksrequest", HandlePickInfoRequest);

            client.AddGenericPacketHandler("pickinforequest", HandlePickInfoRequest);

            client.OnPickInfoUpdate += PickInfoUpdate;
            client.OnPickDelete += PickDelete;

            // Notes
            client.AddGenericPacketHandler("avatarnotesrequest", HandleAvatarNotesRequest);

            client.OnAvatarNotesUpdate += AvatarNotesUpdate;

            // Interests
            client.OnRequestAvatarProperties += new RequestAvatarProperties(client_OnRequestAvatarProperties);

            client.OnAvatarInterestsUpdate += AvatarInterestsUpdate;
        }

        void client_OnRequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            List<String> alist = new List<String>();
            alist.Add(avatarID.ToString());
            HandleAvatarInterestsRequest(remoteClient, alist);
            HandleAvatarPicksRequest(remoteClient, alist);
        }


        // Interests Handler

        public void HandleAvatarInterestsRequest(Object sender, List<String> args)
        {
            IClientAPI remoteClient = (IClientAPI)sender;

            UUID avatarID = new UUID(args[0]);

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                uint skillsMask = new uint();
                string skillsText = String.Empty;
                uint wantToMask = new uint();
                string wantToText = String.Empty;
                string languagesText = String.Empty;

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);

                string query = "SELECT skillsMask, skillsText, wantToMask, wantToText, languagesText FROM users " +
                                        "WHERE UUID=?avatarID";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                foreach (Dictionary<string, string> row in results)
                {

                    skillsMask = Convert.ToUInt16(row["skillsMask"]); 
                    skillsText = row["skillsText"];
                    wantToMask = Convert.ToUInt16(row["wantToMask"]);
                    wantToText = row["wantToText"];
                    languagesText = row["languagesText"];
                }

                remoteClient.SendAvatarInterestsReply(avatarID,
                    skillsMask,
                    skillsText,
                    wantToMask,
                    wantToText,
                    languagesText);
            }

        }


        // Interests Update

        public void AvatarInterestsUpdate(IClientAPI remoteClient, uint querySkillsMask, string querySkillsText, uint queryWantToMask, string queryWantToText, string
                                            queryLanguagesText)
        {
            UUID avatarID = remoteClient.AgentId;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?skillsMask", querySkillsMask);
                parms.Add("?skillsText", querySkillsText);
                parms.Add("?wantToMask", queryWantToMask);
                parms.Add("?wantToText", queryWantToText);
                parms.Add("?languagesText", queryLanguagesText);

                string query = "UPDATE users set skillsMask=?wantToMask, skillsText=?wantToText, wantToMask=?skillsMask, " +
                            "wantToText=?skillsText, languagesText=?languagesText where UUID=?avatarID";

                db.QueryNoResults(query, parms);
            }

        }

        // Classifieds Handler

        public void HandleAvatarClassifiedsRequest(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;

            UUID avatarID = new UUID(args[0]);

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<UUID, string> classifieds = new Dictionary<UUID, string>();

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);

                string query = "SELECT classifieduuid, name from classifieds " +
                                        "WHERE creatoruuid=?avatarID";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                
                foreach (Dictionary<string, string> row in results)
                {
                    classifieds[new UUID(row["classifieduuid"].ToString())] = row["name"].ToString();
                }

                remoteClient.SendAvatarClassifiedReply(avatarID,
                    classifieds);
            }

        }

        // Classifieds Update
        public const int MIN_CLASSIFIED_PRICE = 50;
        public void ClassifiedInfoUpdate(UUID queryclassifiedID, uint queryCategory, string queryName, string queryDescription, UUID queryParcelID,
                                        uint queryParentEstate, UUID querySnapshotID, Vector3 queryGlobalPos, byte queryclassifiedFlags,
                                        int queryclassifiedPrice, IClientAPI remoteClient)
        {
            // getting information lined up for the query
            UUID avatarID = remoteClient.AgentId;
            UUID regionUUID = remoteClient.Scene.RegionInfo.RegionID;
            UUID ParcelID = UUID.Zero;

            uint regionX = remoteClient.Scene.RegionInfo.RegionLocX;
            uint regionY = remoteClient.Scene.RegionInfo.RegionLocY;

//            m_log.DebugFormat("[CLASSIFIED]: Got the RegionX Location as: {0}, and RegionY as: {1}", regionX.ToString(), regionY.ToString());
            
            string regionName = remoteClient.Scene.RegionInfo.RegionName;
            int creationDate = Util.UnixTimeSinceEpoch();
            int expirationDate = creationDate + 604800;

            if (queryclassifiedPrice < MIN_CLASSIFIED_PRICE)
            {
                m_log.ErrorFormat("[CLASSIFIED]: Got a request for invalid price I'z${0} on a classified from {1}.", queryclassifiedPrice.ToString(), remoteClient.AgentId.ToString());
                remoteClient.SendAgentAlertMessage("Error: The minimum price for a classified advertisement is I'z$" + MIN_CLASSIFIED_PRICE.ToString()+".", true);
                return;
            }

            // Check for hacked names that start with special characters
            if (!Char.IsLetterOrDigit(queryName, 0))
            {
                m_log.ErrorFormat("[CLASSIFIED]: Got a hacked request from {0} for invalid name classified name: {1}", remoteClient.AgentId.ToString(), queryName);
                remoteClient.SendAgentAlertMessage("Error: The name of your classified must start with a letter or a number. No punctuation is allowed.", true);
                return;
            }


            // In case of insert, original values are the new values (by default)
            int origPrice = 0;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                //if this is an existing classified make sure the client is the owner or don't touch it
                string existingCheck = "SELECT creatoruuid FROM classifieds WHERE classifieduuid = ?classifiedID";
                Dictionary<string, object> checkParms = new Dictionary<string, object>();
                checkParms.Add("?classifiedID", queryclassifiedID);

                List<Dictionary<string, string>> existingResults = db.QueryWithResults(existingCheck, checkParms);
                if (existingResults.Count > 0)
                {
                    string existingAuthor = existingResults[0]["creatoruuid"];
                    if (existingAuthor != avatarID.ToString())
                    {
                        m_log.ErrorFormat("[CLASSIFIED]: Got a request for from {0} to modify a classified from {1}: {2}",
                            remoteClient.AgentId.ToString(), existingAuthor, queryclassifiedID.ToString());
                        remoteClient.SendAgentAlertMessage("Error: You do not have permission to modify that classified ad.", true);
                        return;
                    }
                }

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?classifiedID", queryclassifiedID);
                parms.Add("?category", queryCategory);
                parms.Add("?name", queryName);
                parms.Add("?description", queryDescription);
                //parms.Add("?parcelID", queryParcelID);
                parms.Add("?parentEstate", queryParentEstate);
                parms.Add("?snapshotID", querySnapshotID);
                parms.Add("?globalPos", queryGlobalPos);
                parms.Add("?classifiedFlags", queryclassifiedFlags);
                parms.Add("?classifiedPrice", queryclassifiedPrice);
                parms.Add("?creationDate", creationDate);
                parms.Add("?expirationDate", expirationDate);
                parms.Add("?regionUUID", regionUUID);
                parms.Add("?regionName", regionName);

                // We need parcelUUID from land to place in the query properly
                // However, there can be multiple Parcel UUIDS per region,
                // so we need to do some math from the classified entry
                // to the positioning of the avatar 
                // The point we have is a global position value, which means
                // we need to to get the location with: (GlobalX / 256) - RegionX and
                // (GlobalY / 256) - RegionY for the values of the avatar standign position
                // then compare that to the parcels for the closest match

                // explode the GlobalPos value off the bat

                string origGlobPos = queryGlobalPos.ToString();
                string tempAGlobPos = origGlobPos.Replace("<", String.Empty);
                string tempBGlobPos = tempAGlobPos.Replace(">", String.Empty);

                char[] delimiterChars = { ',', ' ' };
                string[] globalPosBits = tempBGlobPos.Split(delimiterChars);

                uint tempAvaXLoc = Convert.ToUInt32(Convert.ToDouble(globalPosBits[0]));
                uint tempAvaYLoc = Convert.ToUInt32(Convert.ToDouble(globalPosBits[2]));

                uint avaXLoc = tempAvaXLoc - (256 * regionX);
                uint avaYLoc = tempAvaYLoc - (256 * regionY);


                //uint avatarPosX = (posGlobalX / 256) - regionX;
                parms.Add("?avaXLoc", avaXLoc.ToString());
                parms.Add("?avaYLoc", avaYLoc.ToString());
                string parcelLocate = "select  uuid, MIN(ABS(UserLocationX - ?avaXLoc)) as minXValue, MIN(ABS(UserLocationY - ?avaYLoc)) as minYValue from land where RegionUUID=?regionUUID GROUP BY UserLocationX ORDER BY minXValue, minYValue LIMIT 1;";

                using (ISimpleDB landDb = _regionConnFactory.GetConnection())
                {
                    List<Dictionary<string, string>> parcelLocated = landDb.QueryWithResults(parcelLocate, parms);
                    foreach (Dictionary<string, string> row in parcelLocated)
                    {
                        ParcelID = new UUID(row["uuid"].ToString());
                    }
                }

                parms.Add("?parcelID", ParcelID);
                string queryClassifieds = "select * from classifieds where classifieduuid=?classifiedID AND creatoruuid=?avatarID";
                List<Dictionary <string, string>> results = db.QueryWithResults(queryClassifieds, parms);
                bool isUpdate = false;
                int costToApply;
                string transactionDesc;

                if (results.Count != 0)
                {
                    if (results.Count != 1)
                    {
                        remoteClient.SendAgentAlertMessage("Classified record is not consistent. Contact Support for assistance.", false);
                        m_log.ErrorFormat("[CLASSIFIED]: Error, query for user {0} classified ad {1} returned {2} results.",
                            avatarID.ToString(), queryclassifiedID.ToString(), results.Count.ToString());
                        return;
                    }

                    // This is an upgrade of a classified ad.
                    Dictionary<string, string> row = results[0];
                    isUpdate = true;
                    transactionDesc = "Classified price change";
                    origPrice = Convert.ToInt32(row["priceforlisting"]);

                    // Also preserve original creation date and expiry.
                    creationDate = Convert.ToInt32(row["creationdate"]);
                    expirationDate = Convert.ToInt32(row["expirationdate"]);

                    costToApply = queryclassifiedPrice - origPrice;
                    if (costToApply < 0) costToApply = 0;
                }
                else
                {
                    // This is the initial placement of the classified.
                    transactionDesc = "Classified charge";

                    creationDate = Util.UnixTimeSinceEpoch();
                    expirationDate = creationDate + 604800;

                    costToApply = queryclassifiedPrice;
                }
                EventManager.ClassifiedPaymentArgs paymentArgs = new EventManager.ClassifiedPaymentArgs(remoteClient.AgentId, queryclassifiedID, origPrice, queryclassifiedPrice, transactionDesc, true);

                if (costToApply > 0)
                {
                    // Now check whether the payment is authorized by the currency system.
                    ((Scene)remoteClient.Scene).EventManager.TriggerClassifiedPayment(remoteClient, paymentArgs);
                    if (!paymentArgs.mIsAuthorized)
                        return; // already reported to user by the check above.
                }

                string query;
                if (isUpdate)
                {
                    query = "UPDATE classifieds set creationdate=?creationDate, " + 
                            "category=?category, name=?name, description=?description, parceluuid=?parcelID, " +
                            "parentestate=?parentEstate, snapshotuuid=?snapshotID, simname=?regionName, posglobal=?globalPos, parcelname=?name, " +
                            " classifiedflags=?classifiedFlags, priceforlisting=?classifiedPrice where classifieduuid=?classifiedID";
                }
                else
                {
                    query = "INSERT into classifieds (classifieduuid, creatoruuid, creationdate, expirationdate, category, name, " +
                            "description, parceluuid, parentestate, snapshotuuid, simname, posglobal, parcelname, classifiedflags, priceforlisting) " +
                            "VALUES (?classifiedID, ?avatarID, ?creationDate, ?expirationDate, ?category, ?name, ?description, ?parcelID, " +
                            "?parentEstate, ?snapshotID, ?regionName, ?globalPos, ?name, ?classifiedFlags, ?classifiedPrice)";
                }
                db.QueryNoResults(query, parms);

                if (costToApply > 0)    // no refunds for lower prices
                {
                    // Handle the actual money transaction here.
                    paymentArgs.mIsPreCheck = false;    // now call it again but for real this time
                    ((Scene)remoteClient.Scene).EventManager.TriggerClassifiedPayment(remoteClient, paymentArgs);
                    // Errors reported by the payment request above.
                }
            }

        }

        // Classifieds Delete

        public void ClassifiedDelete(UUID queryClassifiedID, IClientAPI remoteClient)
        {

            UUID avatarID = remoteClient.AgentId;
            UUID classifiedID = queryClassifiedID;

            using (ISimpleDB db = _connFactory.GetConnection())
            {

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?classifiedID", classifiedID);
                parms.Add("?avatarID", avatarID);

                string query = "delete from classifieds where classifieduuid=?classifiedID AND creatorUUID=?avatarID";

                db.QueryNoResults(query, parms);
            }

        }

        // Picks Handler

        public void HandleAvatarPicksRequest(Object sender, List<String> args)
        {


            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;

            UUID avatarID = new UUID(args[0]);

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<UUID, string> picksRequest = new Dictionary<UUID, string>();

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);

                string query = "SELECT pickuuid, name from userpicks " +
                                        "WHERE creatoruuid=?avatarID";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                foreach (Dictionary<string, string> row in results)
                {
                    picksRequest[new UUID(row["pickuuid"].ToString())] = row["name"].ToString();
                }

                remoteClient.SendAvatarPicksReply(avatarID,
                            picksRequest);

            }

        }

        // Picks Request

        public void HandlePickInfoRequest(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;

            UUID avatarID = new UUID(args[0]);
            UUID pickID = new UUID(args[1]);

            using (ISimpleDB db = _connFactory.GetConnection())
            {

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?pickID", pickID);

                string query = "SELECT * from userpicks WHERE creatoruuid=?avatarID AND pickuuid=?pickID";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                bool topPick = new bool();
                UUID parcelUUID = new UUID();
                string name = String.Empty;
                string description = String.Empty;
                UUID snapshotID = new UUID();
                string userName = String.Empty;
                string originalName = String.Empty;
                string simName = String.Empty;
                Vector3 globalPos = new Vector3();
                int sortOrder = new int();
                bool enabled = new bool();


                foreach (Dictionary<string, string> row in results)
                {
                    topPick = Boolean.Parse(row["toppick"]);
                    parcelUUID = UUID.Parse(row["parceluuid"]);
                    name = row["name"];
                    description = row["description"];
                    snapshotID = UUID.Parse(row["snapshotuuid"]);
                    userName = row["user"];
                    //userName = row["simname"];
                    originalName = row["originalname"];
                    simName = row["simname"];
                    globalPos = Vector3.Parse(row["posglobal"]);
                    sortOrder = Convert.ToInt32(row["sortorder"]);
                    enabled = Boolean.Parse(row["enabled"]);

                }

                remoteClient.SendPickInfoReply( pickID, avatarID,
                        topPick, parcelUUID, name, description,
                        snapshotID, userName, originalName,
                        simName, globalPos, sortOrder,
                        enabled);
                
            }

        }

        // Picks Update

        // pulled the original method due to UUID queryParcelID always being returned as null. If this is ever fixed to where
        // the viewer does in fact return the parcelID, then we can put this back in.
        //public void PickInfoUpdate(IClientAPI remoteClient, UUID pickID, UUID creatorID, bool topPick, string name, string desc, 
        //                    UUID queryParcelID, Vector3 queryGlobalPos, UUID snapshotID, int sortOrder, bool enabled)

        public void PickInfoUpdate(IClientAPI remoteClient, UUID pickID, UUID creatorID, bool topPick, string name, string desc,
                            Vector3 queryGlobalPos, UUID snapshotID, int sortOrder, bool enabled)
        {
            string userRegion = remoteClient.Scene.RegionInfo.RegionName;
            UUID userRegionID = remoteClient.Scene.RegionInfo.RegionID;
            string userFName = remoteClient.FirstName;
            string userLName = remoteClient.LastName;
            string avatarName = userFName + " " + userLName;
            UUID tempParcelUUID = UUID.Zero;
            UUID avatarID = remoteClient.AgentId;


            using (ISimpleDB db = _connFactory.GetConnection())
            {
                //if this is an existing pick make sure the client is the owner or don't touch it
                string existingCheck = "SELECT creatoruuid FROM userpicks WHERE pickuuid = ?pickID";
                Dictionary<string, object> checkParms = new Dictionary<string, object>();
                checkParms.Add("?pickID", pickID);

                List<Dictionary<string, string>> existingResults = db.QueryWithResults(existingCheck, checkParms);
                if (existingResults.Count > 0)
                {
                    if (existingResults[0]["creatoruuid"] != avatarID.ToString())
                    {
                        return;
                    }
                }

                //reassign creator id, it has to be this avatar
                creatorID = avatarID;

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?pickID", pickID);
                parms.Add("?creatorID", creatorID);
                parms.Add("?topPick", topPick);
                parms.Add("?name", name);
                parms.Add("?desc", desc);
                parms.Add("?globalPos", queryGlobalPos);
                parms.Add("?snapshotID", snapshotID);
                parms.Add("?sortOrder", sortOrder);
                parms.Add("?enabled", enabled);
                parms.Add("?regionID", userRegionID);
                parms.Add("?regionName", userRegion);
                
                // we need to know if we're on a parcel or not, and if so, put it's UUID in there 
                // viewer isn't giving it to us from what I can determine
                // TODO: David will need to clean this up cause more arrays are not my thing  :)

                string queryParcelUUID = "select UUID from land where regionUUID=?regionID AND name=?name limit 1";
                using (ISimpleDB landDb = _regionConnFactory.GetConnection())
                {
                    List<Dictionary<string, string>> simID = landDb.QueryWithResults(queryParcelUUID, parms);

                    foreach (Dictionary<string, string> row in simID)
                    {
                        tempParcelUUID = UUID.Parse(row["UUID"]);
                    }
                }

                UUID parcelUUID = tempParcelUUID;
                parms.Add("?parcelID", parcelUUID);
                m_log.Debug("Got parcel of: " + parcelUUID.ToString());
                parms.Add("?parcelName", name);

                string queryPicksCount = "select COUNT(pickuuid) from userpicks where pickuuid=?pickID AND " +
                            "creatoruuid=?creatorID";

                List<Dictionary <string, string>> countList = db.QueryWithResults(queryPicksCount, parms);
                
                string query;
                string picksCount = String.Empty;
                
                foreach (Dictionary<string, string> row in countList)
                {
                    picksCount = row["COUNT(pickuuid)"];
                }

                parms.Add("?avatarName", avatarName);

                //TODO: We're defaulting topPick to false for the moment along with enabled default to True
                // We'll need to look over the conversion vs MySQL cause data truncating should not happen
                if(picksCount == "0")
                {
                    query = "INSERT into userpicks (pickuuid, creatoruuid, toppick, parceluuid, name, description, snapshotuuid, user, " +
                            "originalname, simname, posglobal, sortorder, enabled) " +
                            "VALUES (?pickID, ?creatorID, 'false', ?parcelID, ?name, ?desc, ?snapshotID, " +
                            "?avatarName, ?parcelName, ?regionName, ?globalPos, ?sortOrder, 'true')";
                } else
                {
                    query = "UPDATE userpicks set toppick='false', " + 
                            " parceluuid=?parcelID, name=?name, description=?desc, snapshotuuid=?snapshotID, " +
                            "user=?avatarName, originalname=?parcelName, simname=?regionName, posglobal=?globalPos, sortorder=?sortOrder, " +
                            " enabled='true' where pickuuid=?pickID";
                }
                db.QueryNoResults(query, parms);
            }

        }

        // Picks Delete

        public void PickDelete(IClientAPI remoteClient, UUID queryPickID)
        {

            UUID avatarID = remoteClient.AgentId;

            using (ISimpleDB db = _connFactory.GetConnection())
            {

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?pickID", queryPickID);
                parms.Add("?avatarID", avatarID);
                
                string query = "delete from userpicks where pickuuid=?pickID AND creatoruuid=?avatarID";

                db.QueryNoResults(query, parms);
            }

        }

        private const string LEGACY_EMPTY = "No notes currently for this avatar!";
        public void HandleAvatarNotesRequest(Object sender, string method, List<String> args)
        {

            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;
            UUID avatarID = remoteClient.AgentId;
            UUID targetAvatarID = new UUID(args[0]);

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?targetID", targetAvatarID);

                string query = "SELECT notes from usernotes where useruuid=?avatarID AND targetuuid=?targetID";
                List<Dictionary<string, string>> notesResult = db.QueryWithResults(query, parms);

                string notes = String.Empty;
                if (notesResult.Count > 0)
                    notes = notesResult[0]["notes"];
                if (notes == LEGACY_EMPTY)  // filter out the old text that said there was no text. ;)
                    notes = String.Empty;

                remoteClient.SendAvatarNotesReply(targetAvatarID, notes);
            }

        }

        public void AvatarNotesUpdate(IClientAPI remoteClient, UUID queryTargetID, string queryNotes)
        {
            UUID avatarID = remoteClient.AgentId;

            // allow leading spaces for formatting, but TrimEnd will help us detect an empty field other than spaces
            string notes = queryNotes.TrimEnd();

            // filter out the old text that said there was no text. ;)
            if (notes == LEGACY_EMPTY)
                notes = String.Empty;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);
                parms.Add("?targetID", queryTargetID);
                parms.Add("?notes", notes);

                string query;
                if (String.IsNullOrEmpty(notes))
                    query = "DELETE FROM usernotes WHERE useruuid=?avatarID AND targetuuid=?targetID";
                else
                    query = "INSERT INTO usernotes(useruuid, targetuuid, notes) VALUES(?avatarID,?targetID,?notes) ON DUPLICATE KEY UPDATE notes=?notes";
                db.QueryNoResults(query, parms);
            }
        }

    }
}
