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

/* This version has been heavily modified from it's original version by InWorldz,LLC
 * Beth Reischl - 3/25/2010
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using OpenMetaverse;
using TransactionInfoBlock = OpenMetaverse.Packets.MoneyBalanceReplyPacket.TransactionInfoBlock;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.Avatar.Dialog;
using OpenSim.Data.SimpleDB;


namespace OpenSim.Region.CoreModules.Avatar.Currency
{
    public class AvatarCurrency : IMoneyModule, IRegionModule
    {
        //
        // Log module
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();


        private ConnectionFactory _connFactory;

        public static UUID DEFAULT_CURRENCY_ACCOUNT_ID = new UUID("efbfe4e6-95c2-4af3-9d27-25a35c2fd575");

        /// <summary>
        /// Setup of the base vars that will be pulled from the ini file
        /// </summary>
        ///

        public UUID CurrencyAccountID = DEFAULT_CURRENCY_ACCOUNT_ID;

        private float EnergyEfficiency = 0f;
        //private ObjectPaid handlerOnObjectPaid;

        private int ObjectCapacity = OpenSim.Framework.RegionInfo.DEFAULT_REGION_PRIM_LIMIT;
        private int ObjectCount = 0;
        private int PriceEnergyUnit = 0;
        private int PriceGroupCreate = 0;
        private int PriceObjectClaim = 0;
        private float PriceObjectRent = 0f;
        private float PriceObjectScaleFactor = 0f;
        private int PriceParcelClaim = 0;
        private float PriceParcelClaimFactor = 0f;
        private int PriceParcelRent = 0;
        private int PricePublicObjectDecay = 0;
        private int PricePublicObjectDelete = 0;
        private int PriceRentLight = 0;
        private int PriceUpload = 0;
        private int TeleportMinPrice = 0;
        private int MinDebugMoney = Int32.MinValue;
        private string CurrencySymbol = "I'z$";

        private float TeleportPriceExponent = 0f;

        #region AvatarCurrency Members

        public event ObjectPaid OnObjectPaid;

        public void Initialize(Scene scene, IConfigSource config)
        {
            IConfig economyConfig = config.Configs["Economy"];
            string connstr = economyConfig.GetString("EconomyConnString", String.Empty);
            if (connstr.Length <= 0) // EconomyConnString not found or blank, try the connection string this code previously used.
            {
                m_log.Info("[CURRENCY] EconomyConnString not found in INI file section [Economy]. Falling back to legacy usage of ProfileConnString in [Profile].");
            IConfig profileConfig = config.Configs["Profile"];
                connstr = profileConfig.GetString("ProfileConnString", String.Empty);
            }

            _connFactory = new ConnectionFactory("MySQL", connstr);

            m_log.Info("[CURRENCY] InWorldz Currency Module is activated");

            const int DEFAULT_PRICE_ENERGY_UNIT = 100;
            const int DEFAULT_PRICE_OBJECT_CLAIM = 10;
            const int DEFAULT_PRICE_PUBLIC_OBJECT_DECAY = 4;
            const int DEFAULT_PRICE_PUBLIC_OBJECT_DELETE = 4;
            const int DEFAULT_PRICE_PARCEL_CLAIM = 1;
            const float DEFAULT_PRICE_PARCEL_CLAIM_FACTOR = 1f;
            const int DEFAULT_PRICE_UPLOAD = 0;
            const int DEFAULT_PRICE_RENT_LIGHT = 5;
            const int DEFAULT_TELEPORT_MIN_PRICE = 2;
            const float DEFAULT_TELEPORT_PRICE_EXPONENT = 2f;
            const int DEFAULT_ENERGY_EFFICIENCY = 1;
            const int DEFAULT_PRICE_OBJECT_RENT = 1;
            const int DEFAULT_PRICE_OBJECT_SCALE_FACTOR = 10;
            const int DEFAULT_PRICE_PARCEL_RENT = 1;
            const int DEFAULT_PRICE_GROUP_CREATE = -1;

            if (economyConfig != null)
            {
                ObjectCapacity = economyConfig.GetInt("ObjectCapacity", OpenSim.Framework.RegionInfo.DEFAULT_REGION_PRIM_LIMIT);
                PriceEnergyUnit = economyConfig.GetInt("PriceEnergyUnit", DEFAULT_PRICE_ENERGY_UNIT);
                PriceObjectClaim = economyConfig.GetInt("PriceObjectClaim", DEFAULT_PRICE_OBJECT_CLAIM);
                PricePublicObjectDecay = economyConfig.GetInt("PricePublicObjectDecay", DEFAULT_PRICE_PUBLIC_OBJECT_DECAY);
                PricePublicObjectDelete = economyConfig.GetInt("PricePublicObjectDelete", DEFAULT_PRICE_PUBLIC_OBJECT_DELETE);
                PriceParcelClaim = economyConfig.GetInt("PriceParcelClaim", DEFAULT_PRICE_PARCEL_CLAIM);
                PriceParcelClaimFactor = economyConfig.GetFloat("PriceParcelClaimFactor", DEFAULT_PRICE_PARCEL_CLAIM_FACTOR);
                PriceUpload = economyConfig.GetInt("PriceUpload", DEFAULT_PRICE_UPLOAD);
                PriceRentLight = economyConfig.GetInt("PriceRentLight", DEFAULT_PRICE_RENT_LIGHT);
                TeleportMinPrice = economyConfig.GetInt("TeleportMinPrice", DEFAULT_TELEPORT_MIN_PRICE);
                TeleportPriceExponent = economyConfig.GetFloat("TeleportPriceExponent", DEFAULT_TELEPORT_PRICE_EXPONENT);
                EnergyEfficiency = economyConfig.GetFloat("EnergyEfficiency", DEFAULT_ENERGY_EFFICIENCY);
                PriceObjectRent = economyConfig.GetFloat("PriceObjectRent", DEFAULT_PRICE_OBJECT_RENT);
                PriceObjectScaleFactor = economyConfig.GetFloat("PriceObjectScaleFactor", DEFAULT_PRICE_OBJECT_SCALE_FACTOR);
                PriceParcelRent = economyConfig.GetInt("PriceParcelRent", DEFAULT_PRICE_PARCEL_RENT);
                PriceGroupCreate = economyConfig.GetInt("PriceGroupCreate", DEFAULT_PRICE_GROUP_CREATE);

                CurrencySymbol = economyConfig.GetString("CurrencySymbol", CurrencySymbol);

                string option = economyConfig.GetString("CurrencyAccount", DEFAULT_CURRENCY_ACCOUNT_ID.ToString()).Trim();
                if (!UUID.TryParse(option, out CurrencyAccountID)) {
                    CurrencyAccountID = DEFAULT_CURRENCY_ACCOUNT_ID;
                }

                // easy way for all accounts on debug servers to have some cash to test Buy operations and transfers
                MinDebugMoney = economyConfig.GetInt("MinDebugMoney", Int32.MinValue);
                if (MinDebugMoney != Int32.MinValue)
                    m_log.InfoFormat("[CURRENCY] MinDebugMoney activated at: {0}", MinDebugMoney);
            }
            else
            {
                ObjectCapacity = OpenSim.Framework.RegionInfo.DEFAULT_REGION_PRIM_LIMIT;
                PriceEnergyUnit = DEFAULT_PRICE_ENERGY_UNIT;
                PriceObjectClaim = DEFAULT_PRICE_OBJECT_CLAIM;
                PricePublicObjectDecay = DEFAULT_PRICE_PUBLIC_OBJECT_DECAY;
                PricePublicObjectDelete = DEFAULT_PRICE_PUBLIC_OBJECT_DELETE;
                PriceParcelClaim = DEFAULT_PRICE_PARCEL_CLAIM;
                PriceParcelClaimFactor = DEFAULT_PRICE_PARCEL_CLAIM_FACTOR;
                PriceUpload = DEFAULT_PRICE_UPLOAD;
                PriceRentLight = DEFAULT_PRICE_RENT_LIGHT;
                TeleportMinPrice = DEFAULT_TELEPORT_MIN_PRICE;
                TeleportPriceExponent = DEFAULT_TELEPORT_PRICE_EXPONENT;
                EnergyEfficiency = DEFAULT_ENERGY_EFFICIENCY;
                PriceObjectRent = DEFAULT_PRICE_OBJECT_RENT;
                PriceObjectScaleFactor = DEFAULT_PRICE_OBJECT_SCALE_FACTOR;
                PriceParcelRent = DEFAULT_PRICE_PARCEL_RENT;
                PriceGroupCreate = DEFAULT_PRICE_GROUP_CREATE;

                MinDebugMoney = Int32.MinValue;
            }

            scene.RegisterModuleInterface<IMoneyModule>(this);
            IHttpServer httpServer = scene.CommsManager.HttpServer;

            if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
            {
                m_scenel[scene.RegionInfo.RegionHandle] = scene;
            }
            else
            {
                m_scenel.Add(scene.RegionInfo.RegionHandle, scene);
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMoneyTransfer += MoneyTransferAction;
            scene.EventManager.OnClassifiedPayment += ClassifiedPayment;
            scene.EventManager.OnValidateLandBuy += ValidateLandBuy;
            scene.EventManager.OnLandBuy += processLandBuy;

            /*
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnClientClosed += ClientLoggedOut;
            */

        }

        public string GetCurrencySymbol()
        {
            return CurrencySymbol;
        }

        public bool UploadChargeApplies(AssetType type)
        {
            if (PriceUpload <= 0)
                return false;

            return (type == AssetType.Texture) || (type == AssetType.Sound) || (type == AssetType.ImageTGA) || (type == AssetType.TextureTGA) || (type == AssetType.Animation);
        }

        public bool UploadCovered(UUID agentID)
        {
            return AmountCovered(agentID, PriceUpload);
        }

        public void ApplyUploadCharge(UUID agentID)
        {
            if (PriceUpload > 0)
                ApplyCharge(agentID, (int)MoneyTransactionType.UploadCharge, PriceUpload, "upload");
        }

        public int MeshUploadCharge(int meshCount, int textureCount)
        {
            return (meshCount * PriceUpload) + (textureCount * PriceUpload);
        }

        public bool MeshUploadCovered(UUID agentID, int meshCount, int textureCount)
        {
            int amount = MeshUploadCharge(meshCount, textureCount);
            return AmountCovered(agentID, amount);
        }

        public void ApplyMeshUploadCharge(UUID agentID, int meshCount, int textureCount)
        {
            int transAmount = MeshUploadCharge(meshCount, textureCount);
            if (transAmount <= 0)
                return;

            string transDesc = "mesh upload";
            int transCode = (int)MoneyTransactionType.UploadCharge;
            UUID transID = ApplyCharge(agentID, transCode, transAmount, transDesc);

            // The viewer notifies the user for most upload transactions, except for mesh uploads.
            // So if there's a client, notify them now.
            IClientAPI client = LocateClientObject(agentID);
            if (client != null)
            {
                TransactionInfoBlock transInfo = new TransactionInfoBlock();
                transInfo.Amount = transAmount;
                transInfo.TransactionType = transCode;
                transInfo.SourceID = agentID;
                transInfo.DestID = UUID.Zero;
                transInfo.IsSourceGroup = false;
                transInfo.IsDestGroup = false;
                transInfo.ItemDescription = Util.StringToBytes256(transDesc);

                string message;
                if (String.IsNullOrEmpty(transDesc))
                    message = "You paid " + CurrencySymbol + transAmount.ToString() + ".";
                else
                    message = "You paid " + CurrencySymbol + transAmount.ToString() + " for " + transDesc + ".";
                SendMoneyBalanceTransaction(client, transID, true, message, transInfo);
            }
        }

        public bool GroupCreationCovered(UUID agentID)
        {
            return AmountCovered(agentID, PriceGroupCreate);
        }

        public void ApplyGroupCreationCharge(UUID agentID)
        {
            if (PriceGroupCreate > 0)
                ApplyCharge(agentID, (int)MoneyTransactionType.GroupCreate, PriceGroupCreate, "group creation");
        }

        // transCode is the transaction code, e.g. 1101 for uploads
        public UUID ApplyCharge(UUID agentID, int transCode, int transAmount, string transDesc)
        {
            // for transCodes, see comments at EOF
            UUID transID = doMoneyTransfer(agentID, CurrencyAccountID, transAmount, transCode, transDesc);

            return transID;
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InWorldzCurrencyModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public EconomyData GetEconomyData()
        {
            EconomyData edata = new EconomyData();
            edata.ObjectCapacity = ObjectCapacity;
            edata.ObjectCount = ObjectCount;
            edata.PriceEnergyUnit = PriceEnergyUnit;
            edata.PriceGroupCreate = PriceGroupCreate;
            edata.PriceObjectClaim = PriceObjectClaim;
            edata.PriceObjectRent = PriceObjectRent;
            edata.PriceObjectScaleFactor = PriceObjectScaleFactor;
            edata.PriceParcelClaim = PriceParcelClaim;
            edata.PriceParcelClaimFactor = PriceParcelClaimFactor;
            edata.PriceParcelRent = PriceParcelRent;
            edata.PricePublicObjectDecay = PricePublicObjectDecay;
            edata.PricePublicObjectDelete = PricePublicObjectDelete;
            edata.PriceRentLight = PriceRentLight;
            edata.PriceUpload = PriceUpload;
            edata.TeleportMinPrice = TeleportMinPrice;
            return edata;
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            //client.OnMoneyBalanceRequest += GetClientFunds(client);
            client.OnRequestPayPrice += requestPayPrice;
            client.OnObjectBuy += ObjectBuy;
            client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
        }


        private void EconomyDataRequestHandler(IClientAPI client, UUID agentID)
        {
            client.SendEconomyData(EnergyEfficiency, ObjectCapacity, ObjectCount, PriceEnergyUnit,
                    PriceGroupCreate, PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor,
                    PriceParcelClaim, PriceParcelClaimFactor, PriceParcelRent, PricePublicObjectDecay,
                    PricePublicObjectDelete, PriceRentLight, PriceUpload, TeleportMinPrice, TeleportPriceExponent);
        }


        private void OnMoneyBalanceRequest(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID TransactionID)
        {
            Util.FireAndForget(delegate(object obj)
            {
                SendMoneyBalance(remoteClient);
            });
        }


        /// <summary>
        /// Get the Current Balance
        /// </summary>
        /// <param name="avatarID">UUID of the avatar we're getting balance for</param>
        /// <returns></returns>
        private int getCurrentBalance(UUID avatarID)
        {
            int avatarFunds = 0;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                const string TOTALS_SEARCH = "SELECT total FROM economy_totals WHERE user_id=?avatarID";
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?avatarID", avatarID);

                List<Dictionary<string, string>> fundsResult = db.QueryWithResults(TOTALS_SEARCH, parms);

                if (fundsResult.Count == 1)
                {
                    avatarFunds = Convert.ToInt32(fundsResult[0]["total"]);
                }
                else if (fundsResult.Count == 0)
                {
                    //this user does not yet have a totals entry, we need to insert one
                    const string TOTALS_POPULATOR =
                        "INSERT IGNORE INTO economy_totals(user_id, total) " +
                        "SELECT ?avatarID, SUM(transactionAmount) FROM economy_transaction WHERE destAvatarID = ?avatarID;";
                    db.QueryNoResults(TOTALS_POPULATOR, parms);

                    //search again for the new total we just inserted
                    fundsResult = db.QueryWithResults(TOTALS_SEARCH, parms);

                    if (fundsResult.Count == 0)
                    {
                        //something is horribly wrong
                        m_log.ErrorFormat("[CURRENCY]: Could not obtain currency total for avatar {0} after initial population", avatarID);
                        avatarFunds = 0;
                    }
                    else
                    {
                        avatarFunds = Convert.ToInt32(fundsResult[0]["total"]);
                    }
                }
                else
                {
                    //something is horribly wrong
                    m_log.ErrorFormat("[CURRENCY]: Multiple currency totals found for {0}. Table corrupt?", avatarID);
                    avatarFunds = 0;
                }


                if (avatarFunds < MinDebugMoney)    // only if configfile has MinDebugMoney=nnn
                    avatarFunds = MinDebugMoney;    // allow testing with fake money that restocks on login

                return avatarFunds;
            }
        }

        // See comments at the end of this file for the meaning of 'type' here (transaction type).
        private UUID doMoneyTransfer(UUID sourceAvatarID, UUID destAvatarID, int amount, int type, string description)
        {
            if (amount < 0) return UUID.Zero;

            // This is where we do all transfers of monies. This is a two step process, one to update the giver, and one
            // to update the recipient.

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                //verify the existance of the source and destination avatars
                Dictionary<string, object> testParms = new Dictionary<string, object>();
                testParms.Add("?sourceAvatarID", sourceAvatarID);
                testParms.Add("?destAvatarID", destAvatarID);

                List<Dictionary<string, string>> results
                    = db.QueryWithResults("SELECT COUNT(*) as matchCount FROM users WHERE UUID IN (?sourceAvatarID, ?destAvatarID);", testParms);

                if (results[0]["matchCount"] != "2")
                {
                    if ((sourceAvatarID != destAvatarID) || (results[0]["matchCount"] != "1"))  // don't report user paying themself
                        m_log.Debug("[CURRENCY]: Source or destination avatar(s) do not exist in transaction.  This is most likely a spoofed destination.");
                    return UUID.Zero;
                }

                DateTime saveNow = DateTime.Now;
                int saveTime = Util.ToUnixTime(saveNow);

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?sourceAvatarID", sourceAvatarID);
                parms.Add("?destAvatarID", destAvatarID);
                parms.Add("?amount", amount);
                parms.Add("?debit", -amount);
                parms.Add("?type", type);
                parms.Add("?description", description);
                parms.Add("?time", saveTime);

                string bankQuery1 = "insert into economy_transaction (sourceAvatarID, destAvatarID, transactionAmount, transactionType, transactionDescription, timeOccurred) " +
                    "VALUES (?sourceAvatarID, ?destAvatarID, ?amount, ?type, ?description, ?time)";
                db.QueryNoResults(bankQuery1, parms);

                testParms.Clear();
                results = db.QueryWithResults("SELECT LAST_INSERT_ID() AS id;", testParms);
                ulong transationID = Convert.ToUInt64(results[0]["id"]);

                string bankQuery2 = "insert into economy_transaction (sourceAvatarID, destAvatarID, transactionAmount, transactionType, transactionDescription, timeOccurred) " +
                    "VALUES (?destAvatarID, ?sourceAvatarID, ?debit, ?type, ?description, ?time)";
                db.QueryNoResults(bankQuery2, parms);

                return new UUID(transationID);
            }
        }

        /// <summary>
        /// Send the Balance to the viewer
        /// </summary>
        /// <param name="client">Client requesting information</param>
        private void SendMoneyBalanceTransaction(IClientAPI client, UUID transaction, bool success, string transactionDescription, TransactionInfoBlock transInfo)
        {
            UUID avatarID = client.AgentId;
            int avatarFunds = getCurrentBalance(avatarID);
            client.SendMoneyBalance(transaction, success, transactionDescription, avatarFunds, transInfo);
        }

        // Send a pure balance notification only.
        private void SendMoneyBalance(IClientAPI client)
        {
            SendMoneyBalanceTransaction(client, UUID.Zero, true, String.Empty, null);
        }

        // Returns the transaction ID that shows up in the transaction history.
        public string ObjectGiveMoney(UUID objectID, UUID sourceAvatarID, UUID destAvatarID, int amount, out string reason)
        {
            reason = String.Empty;
            if (amount <= 0)
            {
                reason = "INVALID_AMOUNT";
                return String.Empty;
            }

            SceneObjectPart part = findPrim(objectID);
            if (part == null)
            {
                reason = "MISSING_PERMISSION_DEBIT";    // if you can't find it, no perms either
                return String.Empty;
            }

            string objName = part.ParentGroup.RootPart.Name;
            Vector3 pos = part.AbsolutePosition;
            int posx = (int)(pos.X + 0.5);
            int posy = (int)(pos.Y + 0.5);
            int posz = (int)(pos.Z + 0.5);
            string description = String.Format("Paid {0} via {1} in {2} at <{3},{4},{5}>",
                resolveAgentName(destAvatarID), objName, 
                part.ParentGroup.Scene.RegionInfo.RegionName, posx, posy, posz);
            int transType = (int)MoneyTransactionType.ObjectPays;

            if (amount > 0)
            {   // allow users with negative balances to buy freebies
                int sourceAvatarFunds = getCurrentBalance(sourceAvatarID);
                if (sourceAvatarFunds < amount)
                {
                    reason = "LINDENDOLLAR_INSUFFICIENTFUNDS";
                    return String.Empty;
                }
            }

            UUID transID = doMoneyTransfer(sourceAvatarID, destAvatarID, amount, transType, description);
            // the transID UUID is actually a ulong stored in a UUID.
            string result = transID.GetULong().ToString();
            reason = String.Empty;

            TransactionInfoBlock transInfo = new TransactionInfoBlock();
            transInfo.Amount = amount;
            transInfo.TransactionType = transType;
            transInfo.SourceID = sourceAvatarID;
            transInfo.DestID = destAvatarID;
            transInfo.IsSourceGroup = false;
            transInfo.IsDestGroup = false;
            transInfo.ItemDescription = Util.StringToBytes256(objName);

            IClientAPI sourceAvatarClient = LocateClientObject(sourceAvatarID);
            if (sourceAvatarClient == null)
            {
                // Just a quick catch for a null reference, just cause they can't be found doesn't
                // mean the item can't pay out the money.
            }
            else
            {
                string sourceText = objName + " paid out " + CurrencySymbol + amount + " to " + resolveAgentName(destAvatarID);
                SendMoneyBalanceTransaction(sourceAvatarClient, transID, true, sourceText, transInfo);
            }

            IClientAPI destAvatarClient = LocateClientObject(destAvatarID);
            if(destAvatarClient == null)
            {
                // Quick catch due to scene issues, don't want to it to fall down if
                // the destination avatar is not in the same scene list or online at all.
            } else
            {
                string destText = "You were paid " + CurrencySymbol + amount + " by " + part.ParentGroup.Name;
                SendMoneyBalanceTransaction(destAvatarClient, transID, true, destText, transInfo);
            }

            return result;
        }

        private bool CheckPayObjectAmount(SceneObjectPart part, int amount)
        {
            SceneObjectPart root = part.ParentGroup.RootPart;
            if (amount < 0)
                return false;   // must be positive amount
            if (part.ParentGroup.RootPart.PayPrice[0] != SceneObjectPart.PAY_HIDE)
                return true;    // any other value is legal
            for (int x = 1; x <= 4; x++)
            {
                if (root.PayPrice[x] == SceneObjectPart.PAY_DEFAULT)
                {   // amount is only implied, check the implied value
                    switch (x)
                    {
                        case 1:
                            if (amount == SceneObjectPart.PAY_DEFAULT1)
                                return true;
                            break;
                        case 2:
                            if (amount == SceneObjectPart.PAY_DEFAULT2)
                                return true;
                            break;
                        case 3:
                            if (amount == SceneObjectPart.PAY_DEFAULT3)
                                return true;
                            break;
                        case 4:
                            if (amount == SceneObjectPart.PAY_DEFAULT4)
                                return true;
                            break;
                    }
                }
                else
                {   // not PAY_DEFAULT, a specific amount on the button
                    if (amount == part.ParentGroup.RootPart.PayPrice[x])
                        return true;    // it's one of the legal amounts listed
                }
            }
            return false;   // not one of the listed amounts
        }

        // Returns true if the operation should be blocked.
        private static IMuteListModule m_muteListModule = null;
        private bool IsMutedObject(SceneObjectGroup group, UUID senderID)
        {
            // This may seem backwards but if the payer has this object
            // or its owner muted, the object cannot give anything back
            // for payment. Do not allow payment to objects that are muted.
            if (m_muteListModule == null)
                m_muteListModule = group.Scene.RequestModuleInterface<IMuteListModule>();
            if (m_muteListModule == null)
                return false;

            // payer has object owner muted?
            if (m_muteListModule.IsMuted(group.OwnerID, senderID))
                return true;

            // payer has the object muted?
            if (m_muteListModule.IsMuted(group.UUID, senderID))
                return true;

            // neither the object nor owner are muted
            return false;
        }

        private void MoneyTransferAction(Object osender, EventManager.MoneyTransferArgs e)
        {
            if (e.amount < 0) return;

            //ScenePresence sourceAvatar = m_scene.GetScenePresence(e.sender);
            //IClientAPI sourceAvatarClient = sourceAvatar.ControllingClient;
            UUID sourceAvatarID = e.sender;
            int transType = e.transactiontype;
            string transDesc = e.description;
            UUID destAvatarID = e.receiver;
            int transAmount = e.amount;
            IClientAPI sourceAvatarClient;
            IClientAPI destAvatarClient;
            string sourceText = String.Empty;
            string destText = String.Empty;

            TransactionInfoBlock transInfo = new TransactionInfoBlock();
            transInfo.Amount = transAmount;
            transInfo.TransactionType = transType;
            transInfo.SourceID = sourceAvatarID;
            transInfo.DestID = destAvatarID;
            transInfo.IsSourceGroup = false;
            transInfo.IsDestGroup = false;
            transInfo.ItemDescription = new byte[0];

            sourceAvatarClient = LocateClientObject(sourceAvatarID);
            if (sourceAvatarClient == null)
            {
                m_log.Debug("[CURRENCY]: Source Avatar not found!");
                return;
            }

            if (transType == (int)MoneyTransactionType.PayObject)
            {
                SceneObjectPart part = findPrim(e.receiver);
                if (!CheckPayObjectAmount(part, e.amount))
                {
                    sourceAvatarClient.SendAgentAlertMessage("Invalid amount used for payment to object.", false);
                    return;
                }

                // Check if the object or owner are muted.
                if (IsMutedObject(part.ParentGroup, sourceAvatarID))
                {
                    sourceAvatarClient.SendAgentAlertMessage("Cannot give money to an object or object owner that you have muted. If the viewer has automatically unblocked the owner, you can retry payment.", false);
                    return;
                }

                destAvatarID = part.OwnerID;
                destAvatarClient = LocateClientObject(destAvatarID);

                Vector3 pos = part.AbsolutePosition;
                int posx = (int)(pos.X + 0.5);  // round to nearest
                int posy = (int)(pos.Y + 0.5);  // round to nearest
                int posz = (int)(pos.Z + 0.5);  // round to nearest

                transInfo.DestID = destAvatarID;
                transInfo.ItemDescription = Util.StringToBytes256(part.ParentGroup.RootPart.Name);

                transDesc = String.Format("Paid {0} via object {1} in {2} at <{3},{4},{5}>", resolveAgentName(destAvatarID), part.ParentGroup.Name, part.ParentGroup.Scene.RegionInfo.RegionName, posx, posy, posz);
                sourceText = "You paid " + resolveAgentName(destAvatarID) + " " + CurrencySymbol + transAmount + " via " + part.ParentGroup.Name;
                destText = resolveAgentName(sourceAvatarID) + " paid you " + CurrencySymbol + transAmount;
            }
            else
            {
                destAvatarID = (e.receiver);
                destAvatarClient = LocateClientObject(destAvatarID);
                if (destAvatarClient == null)
                {
                }
                else if (LocateSceneClientIn(destAvatarID).GetScenePresence(destAvatarID).IsBot)
                {
                    sourceAvatarClient.SendAgentAlertMessage("You cannot pay a bot.", false);
                    return;
                }
                transDesc = "Gift";
                sourceText = "You paid " + resolveAgentName(destAvatarID) + " " + CurrencySymbol + transAmount;
                destText = resolveAgentName(sourceAvatarID) + " paid you " + CurrencySymbol + transAmount;
            }

            if (transAmount > 0)
            {   // allow users with negative balances to buy freebies
                int avatarFunds = getCurrentBalance(sourceAvatarID);
                if (avatarFunds < transAmount)
                {
                    sourceAvatarClient.SendAgentAlertMessage("Insufficient funds at this time", false);
                    return;
                }
            }

            UUID transID;
            bool success;
            if (sourceAvatarID == destAvatarID)
            {
                // catching here, they can test but no reason to stuff crap in the db for testing
                transID = UUID.Zero;
                success = true;
            }
            else
            {
                transID = doMoneyTransfer(sourceAvatarID, destAvatarID, transAmount, transType, transDesc);
                success = (transID != UUID.Zero);
            }

            //if this is a scripted transaction let the script know money has changed hands
            if (transType == (int)MoneyTransactionType.PayObject)
            {
                this.OnObjectPaid(e.receiver, sourceAvatarID, transAmount);
            }

            SendMoneyBalanceTransaction(sourceAvatarClient, transID, success, sourceText, transInfo);

            if (destAvatarClient == null)
            {
                // don't want to do anything, as they can not be found, this is ugly but will suffice
                // until we get the scene working correctly to pull every avatar
            }
            else
            {
                SendMoneyBalanceTransaction(destAvatarClient, transID, success, destText, transInfo);
            }

        }

        private void ClassifiedPayment(Object osender, EventManager.ClassifiedPaymentArgs e)
        {
            IClientAPI remoteClient = (IClientAPI)osender;
            e.mIsAuthorized = false;

            int costToApply = e.mUpdatedPrice - e.mOrigPrice;
            if (costToApply > 0)    // no refunds for lower prices
            {
                // Handle the actual money transaction here.
                int avatarFunds = getCurrentBalance(e.mBuyerID);
                if (avatarFunds < costToApply)
                {
                    string costText = String.Empty;
                    if (e.mOrigPrice != 0) // this is an updated classified
                        costText = "increase ";
                    remoteClient.SendAgentAlertMessage("The classified price " + costText + " (" + CurrencySymbol + costToApply.ToString() + ") exceeds your current balance (" + avatarFunds.ToString() + ").", true);
                    return;
                }
            }

            e.mIsAuthorized = true;
            if (e.mIsPreCheck)
                return;

            // perform actual money transaction
            doMoneyTransfer(e.mBuyerID, CurrencyAccountID, costToApply, (int)MoneyTransactionType.ClassifiedCharge, e.mDescription);
            SendMoneyBalance(remoteClient);   // send balance update

            if (e.mOrigPrice == 0) // this is an initial classified charge.
                remoteClient.SendAgentAlertMessage("You have paid " + CurrencySymbol + costToApply.ToString() + " for this classified ad.", false);
            else  // this is an updated classified
                remoteClient.SendAgentAlertMessage("You have paid an additional " + CurrencySymbol + costToApply.ToString() + " for this classified ad.", false);
        }

        public void ObjectBuy(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID groupID, UUID categoryID,
                uint localID, byte saleType, int salePrice)
        {
            if (salePrice < 0) return;

            if (salePrice > 0)
            {   // allow users with negative balances to buy freebies
                int avatarFunds = getCurrentBalance(agentID);
                if (avatarFunds < salePrice)
                {
                    // The viewer runs a check on monies balance, however, let's make sure another viewer
                    // can't exploit this by removing and check their funds ourselves.
                    remoteClient.SendAgentAlertMessage("Insufficient funds to purchase this item!", false);
                    return;
                }
            }

            IClientAPI sourceAvatarClient = LocateClientObject(remoteClient.AgentId);
            if (sourceAvatarClient == null)
            {
                sourceAvatarClient.SendAgentAlertMessage("Purchase failed. No Controlling client found for sourceAvatar!", false);
                return;
            }

            Scene s = LocateSceneClientIn(remoteClient.AgentId);
            //Scene s = GetScenePresence(remoteClient.AgentId);

            SceneObjectPart objectPart = s.GetSceneObjectPart(localID);
            if (objectPart == null)
            {
                sourceAvatarClient.SendAgentAlertMessage("Purchase failed. The object was not found.", false);
                return;
            }

            /////  Prevent purchase spoofing, as well as viewer bugs.  /////
            // Verify that the object is actually for sale
            if (objectPart.ObjectSaleType == (byte)SaleType.Not)
            {
                remoteClient.SendAgentAlertMessage("Purchase failed. The item is not for sale.", false);
                return;
            }
            // Verify that the viewer sale type actually matches the correct sale type of the object
            if (saleType != objectPart.ObjectSaleType)
            {
                remoteClient.SendAgentAlertMessage("Purchase failed.  The sale type does not match.", false);
                return;
            }
            // Verify that the buyer is paying the correct amount
            if (salePrice != objectPart.SalePrice)
            {
                remoteClient.SendAgentAlertMessage("Purchase failed.  The payment price does not match the sale price.", false);
                return;
            }

            string objName = objectPart.ParentGroup.RootPart.Name;
            Vector3 pos = objectPart.AbsolutePosition;
            int posx = (int)(pos.X + 0.5);
            int posy = (int)(pos.Y + 0.5);
            int posz = (int)(pos.Z + 0.5);
            string transDesc = String.Format("{0} in {1} at <{2},{3},{4}>", objName, objectPart.ParentGroup.Scene.RegionInfo.RegionName, posx, posy, posz);

            string sourceAlertText = "Purchased " + objName + " for " + CurrencySymbol + salePrice;
            string destAlertText = resolveAgentName(agentID) + " paid you " + CurrencySymbol + salePrice + " via " + objName;

            int transType = (int)MoneyTransactionType.ObjectSale;
            UUID transID = UUID.Zero;

            TransactionInfoBlock transInfo = new TransactionInfoBlock();
            transInfo.Amount = salePrice;
            transInfo.TransactionType = transType;
            transInfo.SourceID = remoteClient.AgentId;
            transInfo.DestID = objectPart.OwnerID;
            transInfo.IsSourceGroup = false;
            transInfo.IsDestGroup = false;
            transInfo.ItemDescription = Util.StringToBytes256(objName);

            if (agentID == objectPart.OwnerID)
            {
                // we'll let them test the buy, but nothing happens money wise.
                if (!s.PerformObjectBuy(remoteClient, categoryID, localID, saleType))
                    return;
                sourceAvatarClient.SendBlueBoxMessage(agentID, String.Empty, sourceAlertText);
            }
            else
            {
                if (salePrice == 0)
                {
                    // We need to add a counter here for Freebies thus bypassing the DB for transactions cause
                    // Freebies are a pain to have to track in the transaction history.
                    if (!s.PerformObjectBuy(remoteClient, categoryID, localID, saleType))
                        return;
                }
                else
                {
                    UUID originalOwnerID = objectPart.OwnerID;  // capture the original seller's UUID for the money transfer
                    if (!s.PerformObjectBuy(remoteClient, categoryID, localID, saleType))    // changes objectPart.OwnerID
                        return;
                    transID = doMoneyTransfer(remoteClient.AgentId, originalOwnerID, salePrice, transType, transDesc);
                }
                SendMoneyBalanceTransaction(sourceAvatarClient, transID, true, sourceAlertText, transInfo);
            }

            IClientAPI destAvatarClient = LocateClientObject(objectPart.OwnerID);
            if (destAvatarClient == null)
            {
                return;
            }
            else
            {
                SendMoneyBalanceTransaction(destAvatarClient, transID, true, destAlertText, transInfo);
            }
        }

        // This method is called after LandManagementModule's handleLandValidationRequest() method has performed that.
        // All land-related validation belongs there.
        private void ValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            // internal system parameters (validated)
//            Scene scene = (Scene)osender;
            if (e.landValidated && e.parcel != null)
            {
                UUID sourceClientID = e.agentId;    // this viewer parameter has been validated by LLCV
                int avatarFunds = getCurrentBalance(sourceClientID);
                LandData parcel = e.parcel.landData;

                UUID destClientID = parcel.OwnerID;
                int transAmount = parcel.SalePrice;
                if (avatarFunds >= transAmount)
                    e.economyValidated = true;
            }
        }

        private void processLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            if (e.transactionID != 0)
            {
                // Not sure what this error is, duplicate purchase request for if the packet comes in a second time?
                return;
            }
            e.transactionID = Util.UnixTimeSinceEpoch();

            // This method should not do any additional validation.
            // Any required validation should have been performed either
            // in LandManagementModule handleLandValidationRequest() or
            // in ValidateLandBuy above.

            int transType = (int)MoneyTransactionType.LandSale;
            string transDesc = "Land purchase";
            UUID sourceClientID = e.agentId;

            IClientAPI sourceClient = LocateClientObject(sourceClientID);

            if (!e.economyValidated)
            {
                if (sourceClient != null)
                    sourceClient.SendAgentAlertMessage("Could not validate user account balance for purchase.", false);
                return;
            }
            if ((e.parcel == null) || (!e.landValidated))
                return;

            LandData parcel = e.parcel.landData;
            int transAmount = e.originalParcelPrice;
            UUID transID = UUID.Zero;
            UUID destClientID = e.originalParcelOwner;
            IClientAPI destClient = LocateClientObject(destClientID);

            // Pick a spot inside the parcel. Since blocks are 4x4, pick a spot 2m inside the bottom corner block.
            Vector3 pos = parcel.AABBMin;
            pos.X += 2;
            pos.Y += 2;
            transDesc += " at " + e.parcel.regionName + " (" + pos.X + "," + pos.Y + "): " + parcel.Name;
            // limit the result to 255 for the db storage
            if (transDesc.Length > 255)
                transDesc = transDesc.Substring(0, 255);

            // requires 2 users: source and dest clients must both be users (not groups, not same)
            transID = doMoneyTransfer(sourceClientID, destClientID, transAmount, transType, transDesc);

            TransactionInfoBlock transInfo = new TransactionInfoBlock();
            transInfo.Amount = transAmount;
            transInfo.TransactionType = transType;
            transInfo.ItemDescription = Util.StringToBytes256(transDesc);
            transInfo.SourceID = sourceClientID;
            transInfo.IsSourceGroup = false;
            transInfo.DestID = e.originalParcelOwner;
            if (e.originalIsGroup)
            {
                // not currently supported (blocked in LandManagementModule handleLandValidationRequest())
                // unless price is 0, because we cannot pass a group ID to doMoneyTransfer above.
                transInfo.IsDestGroup = true;
            }
            else
            {
                transInfo.IsDestGroup = false;
            }

            if (sourceClient != null)
            {
                string destName = resolveAgentName(destClientID);
                if (String.IsNullOrEmpty(destName)) destName = "a group (or unknown user)";
                string sourceText = "You paid " + CurrencySymbol + transAmount + " to " + destName + " for a parcel of land.";
                SendMoneyBalanceTransaction(sourceClient, transID, true, sourceText, transInfo);
            }
            if (destClient != null)
            {
                string destName = resolveAgentName(sourceClientID);
                if (String.IsNullOrEmpty(destName)) destName = "a group (or unknown user)";
                string destText = "You were paid " + CurrencySymbol + transAmount + " by " + destName + " for a parcel of land.";
                SendMoneyBalanceTransaction(destClient, transID, true, destText, transInfo);
            }
        }

        public bool AmountCovered(UUID agentID, int amount)
        {
            if (amount <= 0)
                return true;    // allow users with 0 or negative balance to buy freebies

            int avatarFunds = getCurrentBalance(agentID);
            return avatarFunds >= amount;
        }

        public void requestPayPrice(IClientAPI client, UUID objectID)
        {
            Scene scene = LocateSceneClientIn(client.AgentId);
            if (scene == null)
                return;

            SceneObjectPart task = scene.GetSceneObjectPart(objectID);
            if (task == null)
                return;
            SceneObjectGroup group = task.ParentGroup;
            SceneObjectPart root = group.RootPart;

            client.SendPayPrice(objectID, root.PayPrice);
        }

        #region AvatarCurrency Helper Functions

        private SceneObjectPart findPrim(UUID objectID)
        {
            lock (m_scenel)
            {
                foreach (Scene s in m_scenel.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        private SceneObjectPart findPrimID(uint localID)
        {
            lock (m_scenel)
            {
                foreach (Scene objectScene in m_scenel.Values)
                {
                    SceneObjectPart part = objectScene.GetSceneObjectPart(localID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        private string resolveAgentName(UUID agentID)
        {
            // try avatar username surname
            Scene scene = GetRandomScene();
            string name = scene.CommsManager.UserService.Key2Name(agentID,false);
            if (String.IsNullOrEmpty(name))
                m_log.ErrorFormat("[MONEY]: Could not resolve user {0}", agentID);

            return name;
        }

        public Scene GetRandomScene()
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                    return rs;
            }
            return null;
        }

        private Scene LocateSceneClientIn(UUID AgentId)
        {
            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {

                    ScenePresence tPresence = _scene.GetScenePresence(AgentId);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            return _scene;
                        }
                    }
                }
            }
            return null;
        }

        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }
            return null;
        }
    }

        #endregion

}

// Transaction types, from https://lists.secondlife.com/pipermail/sldev-commits/2009-September.txt

/*
+class MoneyTransactionType(object):
+    String.Empty" Money transaction type constants String.Empty"
+
+    Null                   = 0
+
+# Codes 1000-1999 reserved for one-time charges
+    ObjectClaim            = 1000
+    LandClaim              = 1001
+    GroupCreate            = 1002
+    ObjectPublicClaim      = 1003
+    GroupJoin              = 1004 # May be moved to group transactions eventually
+    TeleportCharge         = 1100 # FF not sure why this jumps to 1100...
+    UploadCharge           = 1101
+    LandAuction            = 1102
+    ClassifiedCharge       = 1103
+
+# Codes 2000-2999 reserved for recurrent charges
+    ObjectTax              = 2000
+    LandTax                = 2001
+    LightTax               = 2002
+    ParcelDirFee           = 2003
+    GroupTax               = 2004 # Taxes incurred as part of group membership
+    ClassifiedRenew        = 2005
+
+# Codes 3000-3999 reserved for inventory transactions
+    GiveInventory          = 3000
+
+# Codes 5000-5999 reserved for transfers between users
+    ObjectSale             = 5000
+    Gift                   = 5001
+    LandSale               = 5002
+    ReferBonus             = 5003
+    InventorySale          = 5004
+    RefundPurchase         = 5005
+    LandPassSale           = 5006
+    DwellBonus             = 5007
+    PayObject              = 5008
+    ObjectPays             = 5009
+
+# Codes 6000-6999 reserved for group transactions
+#   GroupJoin              = 6000  # reserved for future use
+    GroupLandDeed          = 6001
+    GroupObjectDeed        = 6002
+    GroupLiability         = 6003
+    GroupDividend          = 6004
+    MembershipDues         = 6005
+
+# Codes 8000-8999 reserved for one-type credits
+    ObjectRelease          = 8000
+    LandRelease            = 8001
+    ObjectDelete           = 8002
+    ObjectPublicDecay      = 8003
+    ObjectPublicDelete     = 8004
+
+# Code 9000-9099 reserved for usertool transactions
+    LindenAdjustment       = 9000
+    LindenGrant            = 9001
+    LindenPenalty          = 9002
+    EventFee               = 9003
+    EventPrize             = 9004
+
+# Codes 10000-10999 reserved for stipend credits
+    StipendBasic           = 10000
+    StipendDeveloper       = 10001
+    StipendAlways          = 10002
+    StipendDaily           = 10003
+    StipendRating          = 10004
+    StipendDelta           = 10005
+
+class TransactionFlags(object):
+    Null = 0
+    SourceGroup = 1
+    DestGroup = 2
+    OwnerGroup = 4
+    SimultaneousContribution = 8
+    SimultaneousContributionRemoval = 16

*/
