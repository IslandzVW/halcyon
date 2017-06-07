using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FlatBuffers;
using InWorldz.Arbiter.Serialization;
using InWorldz.Arbiter.Transform;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using Quaternion = OpenMetaverse.Quaternion;
using Vector3 = OpenMetaverse.Vector3;

namespace InWorldz.Arbiter
{
    internal enum HalcyonObjectType
    {
        HAL_PRIM,
        HAL_OBJECT
    }
    internal class SceneObjectDTO
    {
        public ByteBuffer Buffer { get; set; }
        public ulong Hash { get; set; }
        public HalcyonObjectType HalType { get; set; } = HalcyonObjectType.HAL_PRIM;
    }

    public class VirtualClientView : IClientAPI
    {
        private HashSet<uint> _groupsSeen = new HashSet<uint>();
        private Gateway _transformGateway;

        public Vector3 StartPos { get; set; }
        public OpenMetaverse.UUID AgentId { get; }
        public OpenMetaverse.UUID SessionId { get; }
        public OpenMetaverse.UUID SecureSessionId { get; }
        public OpenMetaverse.UUID ActiveGroupId { get; }
        public string ActiveGroupName { get; }
        public ulong ActiveGroupPowers { get; }

        public ulong GetGroupPowers(UUID groupID)
        {
            throw new NotImplementedException();
        }

        public ulong? GetGroupPowersOrNull(UUID groupID)
        {
            throw new NotImplementedException();
        }

        public bool IsGroupMember(UUID GroupID)
        {
            throw new NotImplementedException();
        }

        public List<AgentGroupData> GetAllGroupPowers()
        {
            throw new NotImplementedException();
        }

        public void SetGroupPowers(IEnumerable<AgentGroupData> groupPowers)
        {
            throw new NotImplementedException();
        }

        public void SetActiveGroupInfo(AgentGroupData activeGroup)
        {
            throw new NotImplementedException();
        }

        public string FirstName { get; }
        public string LastName { get; }
        public IScene Scene { get; }
        public int NextAnimationSequenceNumber { get; }
        public string Name { get; }
        public bool IsActive { get; }
        public bool SendLogoutPacketWhenClosing { get; set; }
        public bool DebugCrossings { get; set; }
        public uint CircuitCode { get; }
        public uint LocalId { get; set; }

        public event Action<int> OnSetThrottles;
        public event GenericMessage OnGenericMessage;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event RestoreObject OnRestoreObject;
        public event ModifyTerrain OnModifyTerrain;
        public event BakeTerrain OnBakeTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event SimWideDeletesDelegate OnSimWideDeletes;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event ObjectDrop OnObjectDrop;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event RequestAvatarInterests OnRequestAvatarInterests;
        public event SetAlwaysRun OnSetAlwaysRun;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event DeRezObjects OnDeRezObjects;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event AddNewPrim OnAddPrim;
        public event FetchInventory OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event SpinStart OnSpinStart;
        public event SpinObject OnSpinUpdate;
        public event SpinStop OnSpinStop;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event ObjectRequest OnObjectRequest;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVectorWithUndoSupport OnUpdatePrimGroupPosition;
        public event UpdateVectorWithUndoSupport OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event ObjectPermissions OnObjectPermissions;
        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event LinkInventoryItem OnLinkInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event UpdateInventoryFolder OnUpdateInventoryFolder;
        public event MoveInventoryFolder OnMoveInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItem OnUpdateInventoryItem;
        public event CopyInventoryItem OnCopyInventoryItem;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event RemoveInventoryItem OnPreRemoveInventoryItem;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;
        public event UUIDNameRequest OnNameFromUUIDRequest;
        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelGodForceOwner OnParcelGodForceOwner;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelDeedToGroup OnParcelDeedToGroup;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;
        public event AvatarInterestsUpdate OnAvatarInterestsUpdate;
        public event ParcelBuy OnParcelBuy;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event RequestTerrain OnRequestTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event AgentSit OnUndo;
        public event AgentSit OnRedo;
        public event LandUndo OnLandUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;
        public event TerrainUnacked OnUnackedTerrain;
        public event ActivateGestures OnActivateGestures;
        public event DeactivateGestures OnDeactivateGestures;
        public event ObjectOwner OnObjectOwner;
        public event DirPlacesQuery OnDirPlacesQuery;
        public event DirFindQuery OnDirFindQuery;
        public event DirLandQuery OnDirLandQuery;
        public event DirPopularQuery OnDirPopularQuery;
        public event DirClassifiedQuery OnDirClassifiedQuery;
        public event EventInfoRequest OnEventInfoRequest;
        public event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime;
        public event MapItemRequest OnMapItemRequest;
        public event OfferCallingCard OnOfferCallingCard;
        public event AcceptCallingCard OnAcceptCallingCard;
        public event DeclineCallingCard OnDeclineCallingCard;
        public event SoundTrigger OnSoundTrigger;
        public event StartLure OnStartLure;
        public event TeleportLureRequest OnTeleportLureRequest;
        public event NetworkStats OnNetworkStatsUpdate;
        public event ClassifiedInfoRequest OnClassifiedInfoRequest;
        public event ClassifiedInfoUpdate OnClassifiedInfoUpdate;
        public event ClassifiedDelete OnClassifiedDelete;
        public event ClassifiedDelete OnClassifiedGodDelete;
        public event EventNotificationAddRequest OnEventNotificationAddRequest;
        public event EventNotificationRemoveRequest OnEventNotificationRemoveRequest;
        public event EventGodDelete OnEventGodDelete;
        public event ParcelDwellRequest OnParcelDwellRequest;
        public event UserInfoRequest OnUserInfoRequest;
        public event UpdateUserInfo OnUpdateUserInfo;
        public event RetrieveInstantMessages OnRetrieveInstantMessages;
        public event PickDelete OnPickDelete;
        public event PickGodDelete OnPickGodDelete;
        public event PickInfoUpdate OnPickInfoUpdate;
        public event AvatarNotesUpdate OnAvatarNotesUpdate;
        public event MuteListRequest OnMuteListRequest;
        public event MuteListEntryUpdate OnUpdateMuteListEntry;
        public event MuteListEntryRemove OnRemoveMuteListEntry;
        public event PlacesQuery OnPlacesQuery;
        public event GrantUserRights OnGrantUserRights;
        public event FreezeUserUpdate OnParcelFreezeUser;
        public event EjectUserUpdate OnParcelEjectUser;
        public event GroupVoteHistoryRequest OnGroupVoteHistoryRequest;
        public event GroupAccountDetailsRequest OnGroupAccountDetailsRequest;
        public event GroupAccountSummaryRequest OnGroupAccountSummaryRequest;
        public event GroupAccountTransactionsRequest OnGroupAccountTransactionsRequest;
        public event AgentCachedTextureRequest OnAgentCachedTextureRequest;
        public event ActivateGroup OnActivateGroup;
        public event GodlikeMessage OnGodlikeMessage;
        public event GodlikeMessage OnEstateTelehubRequest;
        public IPEndPoint RemoteEndPoint { get; }
        public bool IsLoggingOut { get; set; }

        public VirtualClientView(Nini.Config.IConfigSource configSource)
        {
            string gatewayUrl = configSource.Configs["Network"].GetString("transform_gateway_url", string.Empty);
            _transformGateway = new Gateway(gatewayUrl);

            SetupDataflowWorker();
        }

        private BufferBlock<SceneObjectDTO> _hashingBuffer;

        private void SetupDataflowWorker()
        {
            if (_hashingBuffer != null) return;

            var dataflowOpts = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                BoundedCapacity = 1024 // TODO: Performance tune
            };
            _hashingBuffer = new BufferBlock<SceneObjectDTO>();
            var hashBlock = new TransformBlock<SceneObjectDTO, SceneObjectDTO>(
                new Func<SceneObjectDTO, SceneObjectDTO>(ComputeHash)
                , dataflowOpts);
            var sendObjectWithHash = new ActionBlock<SceneObjectDTO>(dto =>
                {

                }, dataflowOpts);
            _hashingBuffer.LinkTo(hashBlock);
            hashBlock.LinkTo(sendObjectWithHash);

            hashBlock.Completion.ContinueWith(delegate { sendObjectWithHash.Complete(); }); // CHECKME: Needed?
        }

        private SceneObjectDTO ComputeHash(SceneObjectDTO dto)
        {
            switch (dto.HalType)
            {
                case HalcyonObjectType.HAL_PRIM:
                    dto.Hash = _transformGateway.GetPrimHash(dto.Buffer);
                    break;
                case HalcyonObjectType.HAL_OBJECT:
                    dto.Hash = _transformGateway.GetObjectGroupHash(dto.Buffer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return dto;
        }

        private static IPropagatorBlock<TInput, TOutput>
            CreateConcurrentOrderedTransformBlock<TInput, TOutput>(
                Func<TInput, TOutput> transform)
        {
            var queue = new TransformBlock<Task<TOutput>, TOutput>(t => t);
            var processor = new ActionBlock<Tuple<TInput, Action<TOutput>>>(
                tuple => tuple.Item2(transform(tuple.Item1)),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded // TODO: Performance tune
                });
            var enqueuer = new ActionBlock<TInput>(
                async item =>
                {
                    var tcs = new TaskCompletionSource<TOutput>();
                    await processor.SendAsync(
                        new Tuple<TInput, Action<TOutput>>(item, tcs.SetResult));
                    await queue.SendAsync(tcs.Task);
                });
            enqueuer.Completion.ContinueWith(
                _ =>
                {
                    //if (_.IsFaulted)
                    //{
                    //    TODO: ???
                    //}
                    queue.Complete();
                    processor.Complete();
                });
            return DataflowBlock.Encapsulate(enqueuer, queue);
        }

        public void SetDebugPacketLevel(int newDebug)
        {
            throw new NotImplementedException();
        }

        public void ProcessInPacket(Packet NewPack)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void WaitForClose()
        {
            throw new NotImplementedException();
        }

        public void Kick(string message)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            throw new NotImplementedException();
        }

        public void SendAppearance(AvatarAppearance app, Vector3 hover)
        {
            throw new NotImplementedException();
        }

        public void SendStartPingCheck(byte seq)
        {
            throw new NotImplementedException();
        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            throw new NotImplementedException();
        }

        public void SendKillObjects(ulong regionHandle, uint[] localIDs)
        {
            throw new NotImplementedException();
        }

        public void SendNonPermanentKillObject(ulong regionHandle, uint localID)
        {
            throw new NotImplementedException();
        }

        public void SendNonPermanentKillObjects(ulong regionHandle, uint[] localIDs)
        {
            throw new NotImplementedException();
        }

        public void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
            throw new NotImplementedException();
        }

        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            throw new NotImplementedException();
        }

        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, UUID ownerID,
            byte source, byte audible)
        {
            throw new NotImplementedException();
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            throw new NotImplementedException();
        }

        public void SendGenericMessage(string method, List<string> message)
        {
            throw new NotImplementedException();
        }

        public void SendLayerData(float[] map)
        {
            throw new NotImplementedException();
        }

        public void SendLayerData(int px, int py, float[] map)
        {
            throw new NotImplementedException();
        }

        public void SendWindData(Vector2[] windSpeeds)
        {
            throw new NotImplementedException();
        }

        public void SendCloudData(float[] cloudCover)
        {
            throw new NotImplementedException();
        }

        public void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            throw new NotImplementedException();
        }

        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
            throw new NotImplementedException();
        }

        public AgentCircuitData RequestClientInfo()
        {
            throw new NotImplementedException();
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
            throw new NotImplementedException();
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
            throw new NotImplementedException();
        }

        public void SendTeleportFailed(string reason)
        {
            throw new NotImplementedException();
        }

        public void SendTeleportLocationStart()
        {
            throw new NotImplementedException();
        }

        public void SendMoneyBalance(UUID transaction, bool success, string description, int balance, MoneyBalanceReplyPacket.TransactionInfoBlock transInfo)
        {
            throw new NotImplementedException();
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, string grouptitle, UUID avatarID,
            uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation, Vector4 collisionPlane,
            Vector3 velocity, bool immediate)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Vector3 velocity,
            Vector3 acceleration, Quaternion rotation, UUID agentid, Vector4 collisionPlane)
        {
            throw new NotImplementedException();
        }

        public void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
        {
            throw new NotImplementedException();
        }

        public void AttachObject(uint localID, Quaternion rotation, byte attachPoint, UUID ownerID)
        {
            throw new NotImplementedException();
        }

        public void SetChildAgentThrottle(byte[] throttle)
        {
            throw new NotImplementedException();
        }

        public void SendPrimitiveToClient(object sop, uint clientFlags, Vector3 lpos, PrimUpdateFlags updateFlags)
        {
            SceneObjectPart part = (SceneObjectPart) sop;
            if (SeeingGroupFirstTime(part.ParentGroup))
            {
                //send the group update
                SendObjectGroupUpdate(part.ParentGroup, clientFlags, lpos);
            }
            else
            {
                //just send the part update
                SendPartUpdate(part, clientFlags, lpos);
            }
        }
        
        private void SendPartUpdate(SceneObjectPart sop, uint clientFlags, Vector3 lpos)
        {
            var builder = new FlatBufferBuilder(16); // *TODO: Find a good initial FB size
            var halPrimOff = Mapper.MapPartToFlatbuffer(builder, sop, true);
            builder.Finish(halPrimOff.Value);
            ByteBuffer buffer = builder.DataBuffer;

            _hashingBuffer.SendAsync(new SceneObjectDTO()
            {
                Buffer = buffer,
                HalType = HalcyonObjectType.HAL_PRIM
            });
        }

        private void SendObjectGroupUpdate(SceneObjectGroup sog, uint clientFlags, Vector3 lpos)
        {
            var builder = new FlatBufferBuilder(32); // *TODO: Find a good initial FB size
            var halGroupOff = Mapper.MapGroupToFlatbuffer(builder, sog, true);
            builder.Finish(halGroupOff.Value);
            ByteBuffer buffer = builder.DataBuffer;

            _hashingBuffer.SendAsync(new SceneObjectDTO()
            {
                Buffer = buffer,
                HalType = HalcyonObjectType.HAL_OBJECT
            });
        }

        private bool SeeingGroupFirstTime(SceneObjectGroup parentGroup)
        {
            lock (_groupsSeen)
            {
                return _groupsSeen.Add(parentGroup.LocalId);
            }
        }

        public void SendPrimitiveToClientImmediate(object sop, uint clientFlags, Vector3 lpos)
        {
            throw new NotImplementedException();
        }

        public void SendPrimTerseUpdate(object sop)
        {
            throw new NotImplementedException();
        }

        public void SendInventoryFolderDetails(UUID ownerID, InventoryFolderBase folder, List<InventoryItemBase> items, List<InventoryFolderBase> folders, bool fetchFolders,
            bool fetchItems)
        {
            throw new NotImplementedException();
        }

        public void FlushPrimUpdates()
        {
            throw new NotImplementedException();
        }

        public void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
            throw new NotImplementedException();
        }

        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId)
        {
            throw new NotImplementedException();
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            throw new NotImplementedException();
        }

        public void SendTakeControls(int controls, bool TakeControls, bool passToAgent)
        {
            throw new NotImplementedException();
        }

        public void SendTakeControls2(int controls1, bool takeControls1, bool passToAgent1, int controls2, bool takeControls2,
            bool passToAgent2)
        {
            throw new NotImplementedException();
        }

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
            throw new NotImplementedException();
        }

        public void SendBulkUpdateInventory(InventoryNodeBase node)
        {
            throw new NotImplementedException();
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            throw new NotImplementedException();
        }

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
            int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor,
            int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay,
            int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            throw new NotImplementedException();
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers,
            string groupname, string grouptitle)
        {
            throw new NotImplementedException();
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            throw new NotImplementedException();
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            throw new NotImplementedException();
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position,
            float gain)
        {
            throw new NotImplementedException();
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            throw new NotImplementedException();
        }

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            throw new NotImplementedException();
        }

        public void SendAlertMessage(string message)
        {
            throw new NotImplementedException();
        }

        public void SendAlertMessage(string message, string infoMessage, OSD extraParams)
        {
            throw new NotImplementedException();
        }

        public void SendAgentAlertMessage(string message, bool modal)
        {
            throw new NotImplementedException();
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            throw new NotImplementedException();
        }

        public void SendDialog(string objectname, UUID objectID, UUID ownerID, string ownerFirstname, string ownerLastname, string msg,
            UUID textureID, int ch, string[] buttonlabels)
        {
            throw new NotImplementedException();
        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear,
            float OrbitalPosition)
        {
            throw new NotImplementedException();
        }

        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
            throw new NotImplementedException();
        }

        public void SendViewerTime(int phase)
        {
            throw new NotImplementedException();
        }

        public UUID GetDefaultAnimation(string name)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, byte[] charterMember, string flAbout,
            uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarInterests(UUID avatarID, uint skillsMask, string skillsText, uint wantToMask, string wantToText,
            string languagesText)
        {
            throw new NotImplementedException();
        }

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            throw new NotImplementedException();
        }

        public void SendHealth(float health)
        {
            throw new NotImplementedException();
        }

        public void SendEstateUUIDList(UUID invoice, int whichList, UUID[] UUIDList, uint estateID)
        {
            throw new NotImplementedException();
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID)
        {
            throw new NotImplementedException();
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            throw new NotImplementedException();
        }

        public void SendEstateCovenantInformation(UUID covenant, uint lastUpdated)
        {
            throw new NotImplementedException();
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags,
            uint sunPosition, UUID covenant, uint covenantLastUpdated, string abuseEmail, UUID estateOwner)
        {
            throw new NotImplementedException();
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData,
            float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            throw new NotImplementedException();
        }

        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
            throw new NotImplementedException();
        }

        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
            throw new NotImplementedException();
        }

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
            throw new NotImplementedException();
        }

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
            throw new NotImplementedException();
        }

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            throw new NotImplementedException();
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID, byte autoScale, string mediaType, string mediaDesc,
            int mediaWidth, int mediaHeight, byte mediaLoop)
        {
            throw new NotImplementedException();
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            throw new NotImplementedException();
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            throw new NotImplementedException();
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            throw new NotImplementedException();
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
            throw new NotImplementedException();
        }

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            throw new NotImplementedException();
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            throw new NotImplementedException();
        }

        public void SendImageNotFound(UUID imageid)
        {
            throw new NotImplementedException();
        }

        public void SendDisableSimulator()
        {
            throw new NotImplementedException();
        }

        public void SendSimStats(SimStats stats)
        {
            throw new NotImplementedException();
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID, uint BaseMask,
            uint OwnerMask, uint GroupMask, uint EveryoneMask, uint NextOwnerMask, int OwnershipCost, byte SaleType,
            int SalePrice, uint Category, UUID LastOwnerID, string ObjectName, string Description)
        {
            throw new NotImplementedException();
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID,
            UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID, UUID OwnerUUID, string TouchTitle,
            byte[] TextureID, string SitTitle, string ItemName, string ItemDescription, uint OwnerMask, uint NextOwnerMask,
            uint GroupMask, uint EveryoneMask, uint BaseMask, uint FoldedOwnerMask, uint FoldedNextOwnerMask, byte saleType,
            int salePrice)
        {
            throw new NotImplementedException();
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            throw new NotImplementedException();
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            throw new NotImplementedException();
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot, Vector3 CameraAtOffset,
            Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            throw new NotImplementedException();
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            throw new NotImplementedException();
        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {
            throw new NotImplementedException();
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            throw new NotImplementedException();
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
            throw new NotImplementedException();
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success)
        {
            throw new NotImplementedException();
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
            throw new NotImplementedException();
        }

        public void SendCreateGroupReply(UUID groupID, bool success, string message)
        {
            throw new NotImplementedException();
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, List<LandStatReportItem> lsrpl)
        {
            throw new NotImplementedException();
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            throw new NotImplementedException();
        }

        public void SendAsset(AssetBase asset, AssetRequestInfo req)
        {
            throw new NotImplementedException();
        }

        public void SendTexture(AssetBase TextureAsset)
        {
            throw new NotImplementedException();
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            throw new NotImplementedException();
        }

        public event ViewerEffectEventHandler OnViewerEffect;
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;
        public void SendBlueBoxMessage(UUID FromAvatarID, string FromAvatarName, string Message)
        {
            throw new NotImplementedException();
        }

        public void SendLogoutPacket()
        {
            throw new NotImplementedException();
        }

        public void SetClientInfo(ClientInfo info)
        {
            throw new NotImplementedException();
        }

        public void SetClientOption(string option, string value)
        {
            throw new NotImplementedException();
        }

        public string GetClientOption(string option)
        {
            throw new NotImplementedException();
        }

        public void SendSetFollowCamProperties(UUID objectID, Dictionary<int, float> parameters)
        {
            throw new NotImplementedException();
        }

        public void SendClearFollowCamProperties(UUID objectID)
        {
            throw new NotImplementedException();
        }

        public void SendRegionHandle(UUID regoinID, ulong handle)
        {
            throw new NotImplementedException();
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            throw new NotImplementedException();
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            throw new NotImplementedException();
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendEventInfoReply(EventData info)
        {
            throw new NotImplementedException();
        }

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
            throw new NotImplementedException();
        }

        public void SendOfferCallingCard(UUID srcID, UUID transactionID)
        {
            throw new NotImplementedException();
        }

        public void SendAcceptCallingCard(UUID transactionID)
        {
            throw new NotImplementedException();
        }

        public void SendDeclineCallingCard(UUID transactionID)
        {
            throw new NotImplementedException();
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarInterestsReply(UUID avatarID, uint skillsMask, string skillsText, uint wantToMask, string wantToTask,
            string languagesText)
        {
            throw new NotImplementedException();
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category,
            string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName,
            Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
            throw new NotImplementedException();
        }

        public void SendAgentDropGroup(UUID groupID)
        {
            throw new NotImplementedException();
        }

        public void RefreshGroupMembership()
        {
            throw new NotImplementedException();
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
            throw new NotImplementedException();
        }

        public void SendPickInfoReply(UUID pickID, UUID creatorID, bool topPick, UUID parcelID, string name, string desc,
            UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled)
        {
            throw new NotImplementedException();
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
            throw new NotImplementedException();
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
            throw new NotImplementedException();
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
            throw new NotImplementedException();
        }

        public void SendUseCachedMuteList()
        {
            throw new NotImplementedException();
        }

        public void SendMuteListUpdate(string filename)
        {
            throw new NotImplementedException();
        }

        public void KillEndDone()
        {
            throw new NotImplementedException();
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            throw new NotImplementedException();
        }

        public void SendChangeUserRights(UUID agent, UUID agentRelated, int relatedRights)
        {
            throw new NotImplementedException();
        }

        public void SendTextBoxRequest(string message, int chatChannel, string objectname, UUID ownerID, string firstName,
            string lastName, UUID objectId)
        {
            throw new NotImplementedException();
        }

        public void FreezeMe(uint flags, UUID whoKey, string who)
        {
            throw new NotImplementedException();
        }

        public void SendAbortXfer(ulong id, int result)
        {
            throw new NotImplementedException();
        }

        public void RunAttachmentOperation(Action action)
        {
            throw new NotImplementedException();
        }

        public void SendAgentCachedTexture(List<CachedAgentArgs> args)
        {
            throw new NotImplementedException();
        }

        public void SendTelehubInfo(Vector3 TelehubPos, Quaternion TelehubRot, List<Vector3> SpawnPoint, UUID ObjectID, string nameT)
        {
            throw new NotImplementedException();
        }

        public void HandleWithInventoryWriteThread(Action toHandle)
        {
            throw new NotImplementedException();
        }

        public Task PauseUpdatesAndFlush()
        {
            throw new NotImplementedException();
        }

        public void ResumeUpdates(IEnumerable<uint> excludeObjectIds)
        {
            throw new NotImplementedException();
        }

        public void AfterAttachedToConnection(AgentCircuitData circuitData)
        {
            throw new NotImplementedException();
        }

        public int GetThrottleTotal()
        {
            throw new NotImplementedException();
        }
    }
}