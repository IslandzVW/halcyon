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
using OpenSim.Framework.Communications;
using OpenSim.Framework;
using OpenSim.Data;
using OpenMetaverse;

using Aquiles.Core.Cluster;
using Aquiles.Cassandra10;
using Aquiles.Helpers;
using Aquiles.Helpers.Encoders;
using log4net;
using System.Reflection;
using System.Diagnostics;
using Apache.Cassandra;


namespace InWorldz.Data.Inventory.Cassandra
{
    public class InventoryStorage : IInventoryStorage
    {
        /// <summary>
        /// What kind of mutations we want to apply to a folder
        /// </summary>
        private enum FolderMutationSelector
        {
            /// <summary>
            /// Mutate all the folder properties
            /// </summary>
            All,

            /// <summary>
            /// Mutate all folder properties except for the folder's parent
            /// </summary>
            AllButParent,

            /// <summary>
            /// Mutate only the folder's parent
            /// </summary>
            ParentOnly
        }

        private const string KEYSPACE = "IWInventory";

        private const string FOLDERS_CF = "Folders";
        private const string ITEMPARENTS_CF = "ItemParents";
        private const string USERFOLDERS_CF = "UserFolders";
        private const string USERACTIVEGESTURES_CF = "UserActiveGestures";
        private const string FOLDERVERSIONS_CF = "FolderVersions";

        private const int FOLDER_INDEX_CHUNK_SZ = 1024;
        private const int FOLDER_VERSION_CHUNK_SZ = 1024;
        private const int FOLDER_CONTENTS_CHUNK_SZ = 512;

        private const ConsistencyLevel DEFAULT_CONSISTENCY_LEVEL = ConsistencyLevel.QUORUM;

        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _clusterName;

        private DelayedMutationManager _delayedMutationMgr = null;

        internal DelayedMutationManager DelayedMutationMgr
        {
            get { return _delayedMutationMgr; }
            set { _delayedMutationMgr = value; }
        }

        public InventoryStorage(string clusterName)
        {
            _clusterName = clusterName;
        }

        

        #region IInventoryStorage Members

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            try
            {
                Dictionary<Guid, InventoryFolderBase> index = GetFolderIndex(userId);
                if (index.Count == 0)
                {
                    return new List<InventoryFolderBase>();
                }

                List<byte[]> keys = new List<byte[]>(index.Count);
                foreach (KeyValuePair<Guid, InventoryFolderBase> indexInfo in index)
                {
                    keys.Add(ByteEncoderHelper.GuidEncoder.ToByteArray(indexInfo.Value.ID.Guid));
                }


                //retrieve the versions for all folders
                ColumnParent versionParent = new ColumnParent
                {
                    Column_family = FOLDERVERSIONS_CF,
                };

                SlicePredicate versionPred = new SlicePredicate();
                versionPred.Column_names = new List<byte[]> { ByteEncoderHelper.UTF8Encoder.ToByteArray("count") };

                Dictionary<byte[], List<ColumnOrSuperColumn>> verColumns =
                    this.RetrieveRowsInChunks(FOLDER_VERSION_CHUNK_SZ, keys, versionParent, versionPred, DEFAULT_CONSISTENCY_LEVEL);

                foreach (KeyValuePair<byte[], List<ColumnOrSuperColumn>> kvp in verColumns)
                {
                    Guid fid = ByteEncoderHelper.GuidEncoder.FromByteArray(kvp.Key);

                    InventoryFolderBase f;
                    if (index.TryGetValue(fid, out f))
                    {
                        if (kvp.Value.Count == 1)
                        {
                            f.Version = (ushort)(kvp.Value[0].Counter_column.Value % (long)ushort.MaxValue);
                        }
                    }
                }

                return new List<InventoryFolderBase>(index.Values);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder skeleton: {0}", e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        /// <summary>
        /// Retrieves all the rows for the given list of keys in chunkSize chunks
        /// </summary>
        /// <param name="chunkSize"></param>
        /// <param name="allKeys"></param>
        /// <param name="colParent"></param>
        /// <param name="pred"></param>
        /// <param name="consistencyLevel"></param>
        /// <returns></returns>
        private Dictionary<byte[], List<ColumnOrSuperColumn>> RetrieveRowsInChunks(int chunkSize, List<byte[]> allKeys,
            ColumnParent colParent, SlicePredicate pred, ConsistencyLevel consistencyLevel)
        {
            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

            if (allKeys.Count <= chunkSize)
            {
                object val =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.multiget_slice(allKeys, colParent, pred, consistencyLevel);

                    }), KEYSPACE);

                return (Dictionary<byte[], List<ColumnOrSuperColumn>>)val;
            }
            else
            {
                Dictionary<byte[], List<ColumnOrSuperColumn>> ret = new Dictionary<byte[], List<ColumnOrSuperColumn>>();

                for (int i = 0; i < allKeys.Count; i += chunkSize)
                {
                    int remaining = allKeys.Count - i;
                    List<byte[]> keys = allKeys.GetRange(i, remaining >= chunkSize ? chunkSize : remaining);

                    object val =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.multiget_slice(keys, colParent, pred, consistencyLevel);

                    }), KEYSPACE);

                    Dictionary<byte[], List<ColumnOrSuperColumn>> chunk = (Dictionary<byte[], List<ColumnOrSuperColumn>>)val;
                    foreach(KeyValuePair<byte[], List<ColumnOrSuperColumn>> kvp in chunk)
                    {
                        ret.Add(kvp.Key, kvp.Value);
                    }
                }

                return ret;
            }
            
        }

        private InventoryFolderBase DecodeFolderBaseFromIndexedCols(Guid id, Dictionary<string, Column> indexedCols)
        {
            return new InventoryFolderBase
            {
                ID = new UUID(id),
                Name = ByteEncoderHelper.UTF8Encoder.FromByteArray(indexedCols["name"].Value),
                Type = (short)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(indexedCols["type"].Value),
                Owner = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(indexedCols["user_id"].Value)),
                ParentID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(indexedCols["parent"].Value)),
                Level = (InventoryFolderBase.FolderLevel)indexedCols["level"].Value[0]
            };
        }

        private InventoryFolderBase DecodeFolderBase(Guid id, List<ColumnOrSuperColumn> columns)
        {
            Dictionary<string, Column> indexedCols = this.IndexColumnsByUTF8Name(columns);

            return this.DecodeFolderBaseFromIndexedCols(id, indexedCols);
        }

        private InventoryFolderBase DecodeFolderBase(Guid id, List<Column> columns)
        {
            Dictionary<string, Column> indexedCols = this.IndexColumnsByUTF8Name(columns);

            return this.DecodeFolderBaseFromIndexedCols(id, indexedCols);
        }

        private Dictionary<string, Column> IndexColumnsByUTF8Name(List<ColumnOrSuperColumn> columns)
        {
            Dictionary<string, Column> ret = new Dictionary<string, Column>();

            foreach (ColumnOrSuperColumn col in columns)
            {
                ret.Add(ByteEncoderHelper.UTF8Encoder.FromByteArray(col.Column.Name), col.Column);
            }

            return ret;
        }

        private Dictionary<string, Column> IndexColumnsByUTF8Name(List<Column> columns)
        {
            Dictionary<string, Column> ret = new Dictionary<string, Column>();

            foreach (Column col in columns)
            {
                ret.Add(ByteEncoderHelper.UTF8Encoder.FromByteArray(col.Name), col);
            }

            return ret;
        }

        private List<ColumnOrSuperColumn> RetrieveAllColumnsInChunks(int chunkSize, byte[] key,
            ColumnParent columnParent, SlicePredicate pred, ConsistencyLevel consistencyLevel)
        {
            pred.Slice_range.Count = chunkSize;
            List<ColumnOrSuperColumn> retColumns = new List<ColumnOrSuperColumn>();

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            while (true)
            {
                object val =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.get_slice(key, columnParent, pred, consistencyLevel);

                    }), KEYSPACE);

                List<ColumnOrSuperColumn> cols = (List<ColumnOrSuperColumn>)val;

                if (pred.Slice_range.Start.Length == 0)
                {
                    //no start specified, beginning of range
                    retColumns.AddRange(cols);
                }
                else
                {
                    //omit the first returned item since it will be the last item
                    //in the previous get
                    for (int i = 1; i < cols.Count; i++)
                    {
                        retColumns.Add(cols[i]);
                    }
                }

                //if we didnt retrieve chunkSize rows, we finished
                if (cols.Count < chunkSize)
                {
                    break;
                }

                //else, we need to set the new start and continue
                if (cols[cols.Count - 1].Column != null)
                {
                    pred.Slice_range.Start = cols[cols.Count - 1].Column.Name;
                }
                else
                {
                    pred.Slice_range.Start = cols[cols.Count - 1].Super_column.Name;
                }
            }

            return retColumns;
        }

        /// <summary>
        /// Retrieves the index of all folders owned by this user
        /// </summary>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        private Dictionary<Guid, InventoryFolderBase> GetFolderIndex(UUID ownerId)
        {
            try
            {
                byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(ownerId.Guid);

                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = USERFOLDERS_CF;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;


                List<ColumnOrSuperColumn> cols = this.RetrieveAllColumnsInChunks(FOLDER_INDEX_CHUNK_SZ,
                    ownerIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                Dictionary<Guid, InventoryFolderBase> retIndex = new Dictionary<Guid, InventoryFolderBase>();

                foreach (ColumnOrSuperColumn col in cols)
                {
                    Dictionary<string, Column> columns = this.IndexColumnsByUTF8Name(col.Super_column.Columns);

                    try
                    {
                        InventoryFolderBase folder = new InventoryFolderBase
                        {
                            ID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Super_column.Name)),
                            Level = (InventoryFolderBase.FolderLevel)columns["level"].Value[0],
                            Name = ByteEncoderHelper.UTF8Encoder.FromByteArray(columns["name"].Value),
                            Owner = ownerId,
                            ParentID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(columns["parent_folder"].Value)),
                            Type = (short)ByteEncoderHelper.Int32Encoder.FromByteArray(columns["type"].Value),
                        };

                        retIndex.Add(folder.ID.Guid, folder);
                    }
                    catch (KeyNotFoundException)
                    {
                        //there is a corruption, this folder can not be read
                        _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra]: Unable to read all columns from folder index item: {0}",
                            new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Super_column.Name)).ToString());
                    }
                }

                return retIndex;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder index: {0}", e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        public InventoryFolderBase GetFolder(UUID folderId)
        {
            if (folderId == UUID.Zero) throw new InventorySecurityException("Not returning folder with ID UUID.Zero");

            try
            {
                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = FOLDERS_CF;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;

                byte[] folderIdArray = ByteEncoderHelper.GuidEncoder.ToByteArray(folderId.Guid);

                ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
                List<ColumnOrSuperColumn> cols = this.RetrieveAllColumnsInChunks(FOLDER_CONTENTS_CHUNK_SZ,
                    folderIdArray, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                if (cols.Count == 0)
                {
                    throw new InventoryObjectMissingException(String.Format("Folder with ID {0} could not be found", folderId));
                }

                ParsedFolderData parsedData = new ParsedFolderData(cols);
                InventoryFolderBase folder = DecodeFolderBase(folderId.Guid, parsedData.Properties.Super_column.Columns);

                if (parsedData.SubFolders != null)
                {
                    foreach (Column col in parsedData.SubFolders.Super_column.Columns)
                    {
                        SubFolderData data = SubFolderData.FromByteArray(col.Value);
                        InventorySubFolderBase folderStub = new InventorySubFolderBase
                        {
                            ID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Name)),
                            Name = data.Name,
                            Owner = folder.Owner,
                            Type = data.Type
                        };

                        folder.SubFolders.Add(folderStub);
                    }
                }

                foreach (ColumnOrSuperColumn itemCol in parsedData.Items)
                {
                    Guid itemId = ByteEncoderHelper.GuidEncoder.FromByteArray(itemCol.Super_column.Name);
                    InventoryItemBase item = this.DecodeInventoryItem(itemCol.Super_column.Columns, itemId, folder.ID.Guid);

                    folder.Items.Add(item);
                }

                //grab the folder version
                try
                {
                    ColumnPath path = new ColumnPath
                    {
                        Column = ByteEncoderHelper.UTF8Encoder.ToByteArray("count"),
                        Column_family = FOLDERVERSIONS_CF
                    };

                    object verVal =
                        cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                        {
                            return client.get(folderIdArray, path, DEFAULT_CONSISTENCY_LEVEL);

                        }), KEYSPACE);

                    ColumnOrSuperColumn verColumn = (ColumnOrSuperColumn)verVal;

                    folder.Version = (ushort)(verColumn.Counter_column.Value % (long)ushort.MaxValue);
                }
                catch (Exception e)
                {
                    _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Could not retrieve the version for folder {0} substituting 1: {1}", folderId, e);

                    //version column missing. this is either a partially deleted folder
                    //or the version mutation never happened. Return 1
                    folder.Version = 1;
                }

                return folder;
            }
            catch (InventoryObjectMissingException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder attributes: {0}", e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder attributes: {0}", e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        public InventoryFolderBase GetFolderAttributes(UUID folderId)
        {
            if (folderId == UUID.Zero) throw new InventorySecurityException("Not returning folder with ID UUID.Zero");

            try
            {
                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = FOLDERS_CF;
                columnParent.Super_column = ByteEncoderHelper.UTF8Encoder.ToByteArray("properties");

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;

                byte[] folderIdArray = ByteEncoderHelper.GuidEncoder.ToByteArray(folderId.Guid);

                ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
                object val =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.get_slice(folderIdArray, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                    }), KEYSPACE);

                List<ColumnOrSuperColumn> cols = (List<ColumnOrSuperColumn>)val;
                if (cols.Count == 0)
                {
                    throw new InventoryObjectMissingException(String.Format("Folder with ID {0} could not be found", folderId));
                }

                InventoryFolderBase folder = DecodeFolderBase(folderId.Guid, cols);

                //grab the folder version
                ColumnPath path = new ColumnPath
                {
                    Column = ByteEncoderHelper.UTF8Encoder.ToByteArray("count"),
                    Column_family = FOLDERVERSIONS_CF
                };


                object verVal =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.get(folderIdArray, path, DEFAULT_CONSISTENCY_LEVEL);

                    }), KEYSPACE);

                ColumnOrSuperColumn verColumn = (ColumnOrSuperColumn)verVal;

                folder.Version = (ushort)(verColumn.Counter_column.Value % (long)ushort.MaxValue);

                return folder;
            }
            catch (InventoryObjectMissingException e)
            {
//                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder attributes: {0}", e);
                throw; // produces a duplicate error farther up with more context
            }
            catch (Exception e)
            {
//                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve folder attributes: {0}", e);
                throw new InventoryStorageException(e.Message, e); // produces another error farther up with more context
            }
        }

        private Mutation VersionIncrement()
        {
            Mutation versionMut = new Mutation();
            versionMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            versionMut.Column_or_supercolumn.Counter_column = new CounterColumn();
            versionMut.Column_or_supercolumn.Counter_column.Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("count");
            versionMut.Column_or_supercolumn.Counter_column.Value = 1;

            return versionMut;
        }

        private void GetFolderStorageMutations(InventoryFolderBase folder, byte[] folderIdBytes, byte[] userIdBytes,
            FolderMutationSelector mutationTypes, Dictionary<byte[], Dictionary<string, List<Mutation>>> outMuts, Guid newParent,
            long timeStamp)
        {
            //Folder CF mutations
            List<Mutation> folderMutList = new List<Mutation>();

            Mutation propertiesMut = new Mutation();
            propertiesMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column = new SuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column.Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("properties");

            List<Column> propertiesColumns = new List<Column>();

            if (mutationTypes == FolderMutationSelector.All || mutationTypes == FolderMutationSelector.AllButParent)
            {
                Column nameCol = new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("name"),
                    Value = ByteEncoderHelper.UTF8Encoder.ToByteArray(folder.Name),
                    Timestamp = timeStamp,
                };

                Column typeCol = new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("type"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(folder.Type),
                    Timestamp = timeStamp,
                };

                Column userIdCol = new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("user_id"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.Owner.Guid),
                    Timestamp = timeStamp,
                };

                Column levelCol = new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("level"),
                    Value = new byte[] { (byte)folder.Level },
                    Timestamp = timeStamp,
                };

                propertiesColumns.Add(nameCol);
                propertiesColumns.Add(typeCol);
                propertiesColumns.Add(userIdCol);
                propertiesColumns.Add(levelCol);
            }

            if (mutationTypes == FolderMutationSelector.All || mutationTypes == FolderMutationSelector.ParentOnly)
            {
                Column parentIdCol = new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("parent"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(newParent),
                    Timestamp = timeStamp,
                };

                propertiesColumns.Add(parentIdCol);
            }


            propertiesMut.Column_or_supercolumn.Super_column.Columns = propertiesColumns;
            folderMutList.Add(propertiesMut);

            Dictionary<string, List<Mutation>> folderKeyMuts = new Dictionary<string, List<Mutation>>();
            folderKeyMuts[FOLDERS_CF] = folderMutList;

            //version increment
            Mutation versionMut = VersionIncrement();
            folderKeyMuts[FOLDERVERSIONS_CF] = new List<Mutation> { versionMut };

            outMuts[folderIdBytes] = folderKeyMuts;


            //UserFolder CF mutations
            if (!outMuts.ContainsKey(userIdBytes))
            {
                outMuts[userIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!outMuts[userIdBytes].ContainsKey(USERFOLDERS_CF))
            {
                outMuts[userIdBytes].Add(USERFOLDERS_CF, new List<Mutation>());
            }

            List<Mutation> userKeyMuts = outMuts[userIdBytes][USERFOLDERS_CF];
            List<Mutation> userFolderMutList = new List<Mutation>();

            //userfolders index, the list of all folders per user
            Mutation userFolderPropertiesMut = new Mutation();
            userFolderPropertiesMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            userFolderPropertiesMut.Column_or_supercolumn.Super_column = this.BuildFolderIndexEntry(folder, folderIdBytes, mutationTypes, newParent, timeStamp);
            userKeyMuts.Add(userFolderPropertiesMut);
        }

        public void CreateFolder(InventoryFolderBase folder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                CreateFolderInternal(folder, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while creating folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.CreateFolderInternal(folder, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "CreateFolder(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not create folder " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private SuperColumn BuildFolderIndexEntry(InventoryFolderBase folder, byte[] folderIdBytes, FolderMutationSelector mutationTypes, 
            Guid newParent, long timeStamp)
        {
            SuperColumn indexColumn = new SuperColumn();
            indexColumn.Name = folderIdBytes;
            List<Column> columns = new List<Column>();

            if (mutationTypes == FolderMutationSelector.All || mutationTypes == FolderMutationSelector.AllButParent)
            {
                columns.Add(new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("name"),
                    Value = ByteEncoderHelper.UTF8Encoder.ToByteArray(folder.Name),
                    Timestamp = timeStamp
                });

                columns.Add(new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("type"),
                    Value = ByteEncoderHelper.Int32Encoder.ToByteArray((int)folder.Type),
                    Timestamp = timeStamp
                });

                columns.Add(new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("level"),
                    Value = new byte[] { (byte)folder.Level },
                    Timestamp = timeStamp
                });
            }

            if (mutationTypes == FolderMutationSelector.All || mutationTypes == FolderMutationSelector.ParentOnly)
            {
                columns.Add(new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("parent_folder"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(newParent),
                    Timestamp = timeStamp
                });
            }

            indexColumn.Columns = columns;

            return indexColumn;
        }

        private void CreateFolderInternal(InventoryFolderBase folder, long timeStamp)
        {
            CheckBasicFolderIntegrity(folder);

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ID.Guid);
            byte[] userIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.Owner.Guid);

            GetFolderStorageMutations(folder, folderIdBytes, userIdBytes, FolderMutationSelector.All, muts, folder.ParentID.Guid, timeStamp);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);


            UpdateParentWithNewChild(folder, folder.ParentID.Guid, Guid.Empty, timeStamp);
        }

        private static void CheckBasicFolderIntegrity(InventoryFolderBase folder)
        {
            if (folder.ID == UUID.Zero)
            {
                throw new UnrecoverableInventoryStorageException("Not creating zero UUID folder");
            }

            if (folder.ParentID == folder.ID)
            {
                throw new UnrecoverableInventoryStorageException("Not creating a folder with a parent set to itself");
            }

            if (folder.ParentID == UUID.Zero && folder.Level != InventoryFolderBase.FolderLevel.Root)
            {
                throw new UnrecoverableInventoryStorageException("Not storing a folder with parent set to ZERO that is not FolderLevel.Root");
            }
        }

        private void GenerateParentUpdateForSubfolder(InventoryFolderBase child, byte[] parentIdArray,
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts, long timeStamp)
        {
            //never mutate the zero ID folder
            if (new Guid(parentIdArray) == Guid.Empty) return;

            List<Mutation> folderMutList = new List<Mutation>();

            Mutation propertiesMut = new Mutation();
            propertiesMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column = new SuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column.Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("sub_folders");

            Column subfolderCol = new Column
            {
                Name = ByteEncoderHelper.GuidEncoder.ToByteArray(child.ID.Guid),
                Value = SubFolderData.Encode(child.Name, child.Type),
                Timestamp = timeStamp
            };

            List<Column> propertiesColumns = new List<Column>();
            propertiesColumns.Add(subfolderCol);

            propertiesMut.Column_or_supercolumn.Super_column.Columns = propertiesColumns;

            folderMutList.Add(propertiesMut);

            muts[parentIdArray].Add(FOLDERS_CF, folderMutList);
        }

        internal void UpdateParentWithNewChild(InventoryFolderBase child, Guid parentId, Guid oldParentId, long timeStamp)
        {
            byte[] childIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(child.ID.Guid);

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();


            byte[] parentIdArray = ByteEncoderHelper.GuidEncoder.ToByteArray(parentId);

            muts[parentIdArray] = new Dictionary<string, List<Mutation>>();

            GenerateParentUpdateForSubfolder(child, parentIdArray, muts, timeStamp);
            muts[parentIdArray].Add(FOLDERVERSIONS_CF, new List<Mutation> { VersionIncrement() });


            if (oldParentId != Guid.Empty && oldParentId != parentId)
            {
                GenerateSubfolderIndexDeletion(oldParentId, timeStamp, childIdBytes, muts);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);
                return null;

            }), KEYSPACE);
        }

        private void GenerateSubfolderIndexDeletion(Guid oldParentId, long timeStamp, byte[] childIdBytes, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            //we have a new parent, we have to tell the old one we're not its child anymore
            Mutation propertiesRem = new Mutation();
            propertiesRem.Deletion = new Deletion();
            propertiesRem.Deletion.Super_column = ByteEncoderHelper.UTF8Encoder.ToByteArray("sub_folders");
            propertiesRem.Deletion.Predicate = new SlicePredicate();
            propertiesRem.Deletion.Predicate.Column_names = new List<byte[]> { childIdBytes };
            propertiesRem.Deletion.Timestamp = timeStamp;

            Dictionary<string, List<Mutation>> oldParentMutations = new Dictionary<string, List<Mutation>> { { FOLDERS_CF, new List<Mutation> { propertiesRem } } };
            oldParentMutations[FOLDERVERSIONS_CF] = new List<Mutation> { VersionIncrement() };

            muts[ByteEncoderHelper.GuidEncoder.ToByteArray(oldParentId)] = oldParentMutations;
        }

        public void SaveFolder(InventoryFolderBase folder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                SaveFolderInternal(folder, timeStamp);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unrecoverable error caught while saving folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while saving folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.SaveFolderInternal(folder, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "SaveFolder(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not save folder " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void SaveFolderInternal(InventoryFolderBase folder, long timeStamp)
        {
            CheckBasicFolderIntegrity(folder);

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ID.Guid);
            byte[] parentIdArray = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ParentID.Guid);
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.Owner.Guid);

            GetFolderStorageMutations(folder, folderIdBytes, ownerIdBytes, FolderMutationSelector.AllButParent, muts, Guid.Empty, timeStamp);

            muts[parentIdArray] = new Dictionary<string, List<Mutation>>();
            GenerateParentUpdateForSubfolder(folder, parentIdArray, muts, timeStamp);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        public void MoveFolder(InventoryFolderBase folder, UUID parentId)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                //don't do anything with a folder that wants to set its new parent
                //to the same folder as its current parent, this can cause corruption
                if (folder.ParentID == parentId)
                {
                    _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Refusing to move folder {0} to new parent {1} for {2}. The source and destination are the same",
                        folder.ID, parentId, folder.Owner);
                    return;
                }

                //don't do anything with a folder that wants to set its new parent to UUID.Zero
                if (parentId == UUID.Zero)
                {
                    _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Refusing to move folder {0} to new parent {1} for {2}. New parent has ID UUID.Zero",
                        folder.ID, parentId, folder.Owner);
                    return;
                }

                MoveFolderInternal(folder, parentId, timeStamp);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while moving folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while moving folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.MoveFolderInternal(folder, parentId, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "MoveFolder(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not move folder " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void MoveFolderInternal(InventoryFolderBase folder, UUID parentId, long timeStamp)
        {
            if (parentId == folder.ID)
            {
                throw new UnrecoverableInventoryStorageException(String.Format("The parent for folder {0} can not be set to itself", folder.ID));
            }

            ChangeFolderParent(folder, parentId.Guid, timeStamp);
            UpdateParentWithNewChild(folder, parentId.Guid, folder.ParentID.Guid, timeStamp);
            folder.ParentID = parentId;
        }

        private void ChangeFolderParent(InventoryFolderBase f, Guid newParent, long timeStamp)
        {
            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(f.ID.Guid);
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(f.Owner.Guid);

            GetFolderStorageMutations(f, folderIdBytes, ownerIdBytes, FolderMutationSelector.ParentOnly, muts, newParent, timeStamp);

            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        public UUID SendFolderToTrash(InventoryFolderBase folder, UUID trashFolderHint)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                return SendFolderToTrashInternal(folder, trashFolderHint, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while sending folder {0} to trash for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.SendFolderToTrashInternal(folder, trashFolderHint, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "TrashFolder(" + folder.ID.ToString() + ")");

                    return trashFolderHint;
                }
                else
                {
                    throw new InventoryStorageException("Could not send folder " + folder.ID.ToString() + " to trash: " + e.Message, e);
                }
            }
        }

        public InventoryFolderBase FindFolderForType(UUID owner, AssetType type)
        {
            Dictionary<Guid, InventoryFolderBase> folderIndex = this.GetFolderIndex(owner);

            foreach (KeyValuePair<Guid, InventoryFolderBase> indexInfo in folderIndex)
            {
                if (indexInfo.Value.Level == InventoryFolderBase.FolderLevel.TopLevel ||
                    indexInfo.Value.Level == InventoryFolderBase.FolderLevel.Root)
                {
                    if ((short)type == indexInfo.Value.Type)
                        return indexInfo.Value;
                    if (((short)type == (short)FolderType.Root) && (indexInfo.Value.Type == (short)FolderType.OldRoot)) // old AssetType.RootFolder == 9
                        return indexInfo.Value; // consider 9 to be FolderType.Root too
                }
            }

            throw new InventoryStorageException(String.Format("Unable to find a suitable folder for type {0} and user {1}", type, owner));
        }

        // Searches the parentage tree for an ancestor folder with a matching type (e.g. Trash)
        public InventoryFolderBase FindTopLevelFolderFor(UUID owner, UUID folderID)
        {
            Dictionary<Guid, InventoryFolderBase> folderIndex = this.GetFolderIndex(owner);

            Guid parentFolderID = folderID.Guid;
            InventoryFolderBase parentFolder = null;

            while ((parentFolderID != Guid.Empty) && folderIndex.ContainsKey(parentFolderID))
            {
                parentFolder = folderIndex[parentFolderID];
                if ((parentFolder.Level == InventoryFolderBase.FolderLevel.TopLevel) || (parentFolder.Level == InventoryFolderBase.FolderLevel.Root))
                    return parentFolder;    // found it

                // otherwise we need to walk farther up the parentage chain
                parentFolderID = parentFolder.ParentID.Guid;
            }

            // No top-level/root folder found for this folder.
            return null;
        }

        private UUID SendFolderToTrashInternal(InventoryFolderBase folder, UUID trashFolderHint, long timeStamp)
        {
            if (trashFolderHint != UUID.Zero)
            {
                this.MoveFolderInternal(folder, trashFolderHint, timeStamp);
                return trashFolderHint;
            }
            else
            {
                InventoryFolderBase trashFolder;

                try
                {
                    trashFolder = this.FindFolderForType(folder.Owner, (AssetType)FolderType.Trash);
                }
                catch (Exception e)
                {
                    throw new InventoryStorageException(String.Format("Trash folder could not be found for user {0}: {1}", folder.Owner, e), e);
                }

                this.MoveFolderInternal(folder, trashFolder.ID, timeStamp);
                return trashFolder.ID;
            }
        }

        public void PurgeFolderContents(InventoryFolderBase folder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                PurgeFolderContentsInternal(folder, timeStamp);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unrecoverable error while purging contents in folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while sending folder {0} to trash for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeFolderContentsInternal(folder, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeFolderContents(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge folder contents for " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void PurgeFolderContentsInternal(InventoryFolderBase folder, long timeStamp)
        {
            //block all deletion requests for a folder with a 0 id
            if (folder.ID == UUID.Zero)
            {
                throw new UnrecoverableInventoryStorageException("Refusing to allow the purge of the inventory ZERO root folder");
            }

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ID.Guid);
            
            //to purge a folder, we have to find all subfolders and items inside a folder
            //for each of the sub folders folders they choose, we need to recurse into all
            //sub-sub folders and grab out the items and folders. Once we have all of them
            //to the last leaf level we do simple removes on all the items and folders
            List<UUID> allFolders = new List<UUID>();
            List<UUID> allItems = new List<UUID>();

            C5.HashSet<UUID> rootItems = new C5.HashSet<UUID>();
            C5.HashSet<UUID> rootFolders = new C5.HashSet<UUID>();

            StringBuilder debugFolderList = new StringBuilder();
            this.RecursiveCollectSubfoldersAndItems(folder.ID, folder.Owner, allFolders, allItems, rootItems, rootFolders, true, null, debugFolderList);

            this.DebugFolderPurge("PurgeFolderContentsInternal", folder, debugFolderList);

            List<byte[]> allFolderIdBytes = new List<byte[]>();
            List<byte[]> rootFolderIdBytes = new List<byte[]>();
            foreach (UUID fid in allFolders)
            {
                byte[] thisFolderIdbytes = ByteEncoderHelper.GuidEncoder.ToByteArray(fid.Guid);

                allFolderIdBytes.Add(thisFolderIdbytes);

                if (rootFolders.Contains(fid))
                {
                    rootFolderIdBytes.Add(thisFolderIdbytes);
                }
            }

            List<byte[]> allItemIdBytes = new List<byte[]>();
            List<byte[]> rootItemIdBytes = new List<byte[]>();

            foreach (UUID iid in allItems)
            {
                byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(iid.Guid);

                allItemIdBytes.Add(itemIdBytes);

                if (rootItems.Contains(iid))
                {
                    rootItemIdBytes.Add(itemIdBytes);
                }
            }

            //we have all the contents, so delete the actual folders and their versions...
            //this will wipe out the folders and in turn all items in subfolders
            //but does not take care of the items in the root
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.Owner.Guid);
            this.GetFolderDeletionMutations(ownerIdBytes, allFolderIdBytes, timeStamp, muts);

            //remove the individual items from the root folder
            foreach (byte[] rootItem in rootItemIdBytes)
            {
                this.GetItemDeletionMutations(rootItem, folderIdBytes, timeStamp, muts, false);
            }

            //remove the individual folder references from the root folder
            foreach (byte[] rootFolder in rootFolderIdBytes)
            {
                this.GetSubfolderEntryDeletionMutations(rootFolder, folderIdBytes, timeStamp, muts);
            }

            //delete the ItemParents folder references for the removed items...
            foreach (byte[] itemId in allItemIdBytes)
            {
                this.GetItemParentDeletionMutations(itemId, timeStamp, muts);
            }

            
            //increment the version of the purged folder
            this.GetFolderVersionIncrementMutations(muts, folderIdBytes);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        private void DebugFolderPurge(string method, InventoryFolderBase folder, StringBuilder debugFolderList)
        {
            _log.DebugFormat("[Inworldz.Data.Inventory.Cassandra] About to purge from {0} {1}\n Objects:\n{2}",
                folder.Name, folder.ID, debugFolderList.ToString());
        }

        public void PurgeFolder(InventoryFolderBase folder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                PurgeFolderInternal(folder, timeStamp);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unrecoverable error while purging folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while purging folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeFolderInternal(folder, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeFolder(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge folder " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void PurgeFolderInternal(InventoryFolderBase folder, long timeStamp)
        {
            //block all deletion requests for a folder with a 0 id
            if (folder.ID == UUID.Zero)
            {
                throw new UnrecoverableInventoryStorageException("Refusing to allow the deletion of the inventory ZERO root folder");
            }

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ID.Guid);
            byte[] parentFolderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.ParentID.Guid);

            //to purge a folder, we have to find all subfolders and items inside a folder
            //for each of the sub folders folders they choose, we need to recurse into all
            //sub-sub folders and grab out the items and folders. Once we have all of them
            //to the last leaf level we do simple removes on all the items and folders
            List<UUID> allFolders = new List<UUID>();
            List<UUID> allItems = new List<UUID>();
            C5.HashSet<UUID> rootItems = new C5.HashSet<UUID>();
            C5.HashSet<UUID> rootFolders = new C5.HashSet<UUID>();

            StringBuilder debugFolderList = new StringBuilder();
            this.RecursiveCollectSubfoldersAndItems(folder.ID, folder.Owner, allFolders, allItems, rootItems, rootFolders, true, null, debugFolderList);

            this.DebugFolderPurge("PurgeFolderInternal", folder, debugFolderList);

            List<byte[]> allFolderIdBytes = new List<byte[]>();
            foreach (UUID fid in allFolders)
            {
                allFolderIdBytes.Add(ByteEncoderHelper.GuidEncoder.ToByteArray(fid.Guid));
            }

            List<byte[]> allItemIdBytes = new List<byte[]>();
            List<byte[]> rootItemIdBytes = new List<byte[]>();
            foreach (UUID iid in allItems)
            {
                byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(iid.Guid);

                allItemIdBytes.Add(itemIdBytes);

                if (rootItems.Contains(iid))
                {
                    rootItemIdBytes.Add(itemIdBytes);
                }
            }

            //we have all the contents, so delete the actual folders and their versions...
            //this will wipe out the folders and in turn all items in subfolders
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folder.Owner.Guid);
            this.GetFolderDeletionMutations(ownerIdBytes, allFolderIdBytes, timeStamp, muts);
            //then we delete this actual folder
            this.GetSingleFolderDeletionMutations(ownerIdBytes, folderIdBytes, timeStamp, muts);
            //and remove the subfolder reference from this folders parent
            this.GetSubfolderEntryDeletionMutations(folderIdBytes, parentFolderIdBytes, timeStamp, muts);

            //delete the ItemParents folder references for the removed items...
            foreach (byte[] itemId in allItemIdBytes)
            {
                this.GetItemParentDeletionMutations(itemId, timeStamp, muts);
            }


            //increment the version of the parent of the purged folder
            if (folder.ParentID != UUID.Zero)
            {
                this.GetFolderVersionIncrementMutations(muts, parentFolderIdBytes);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        // This is an optimized PurgeFolderInternal that does not refetch the tree
        // but assumes the caller knows that the ID specified has no items or subfolders.
        private void PurgeEmptyFolderInternal(UUID ownerID, long timeStamp, UUID folderID, UUID parentID)
        {
            //block all deletion requests for a folder with a 0 id
            if (folderID == UUID.Zero)
            {
                throw new UnrecoverableInventoryStorageException("Refusing to allow the deletion of the inventory ZERO root folder");
            }

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folderID.Guid);
            byte[] parentFolderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(parentID.Guid);

            //we have all the contents, so delete the actual folders and their versions...
            //this will wipe out the folders and in turn all items in subfolders
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(ownerID.Guid);
            //delete this actual folder
            this.GetSingleFolderDeletionMutations(ownerIdBytes, folderIdBytes, timeStamp, muts);
            //and remove the subfolder reference from this folders parent
            this.GetSubfolderEntryDeletionMutations(folderIdBytes, parentFolderIdBytes, timeStamp, muts);

            //increment the version of the parent of the purged folder
            if (parentID != UUID.Zero)
            {
                this.GetFolderVersionIncrementMutations(muts, parentFolderIdBytes);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        // This is an optimized PurgeFolderInternal that does not refetch the tree
        // but assumes the caller knows that the ID specified has no items or subfolders.
        public void PurgeEmptyFolder(InventoryFolderBase folder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                if ((folder.Items.Count != 0) || (folder.SubFolders.Count != 0))
                    throw new UnrecoverableInventoryStorageException("Refusing to PurgeEmptyFolder for folder that is not empty");

                PurgeEmptyFolderInternal(folder.Owner, timeStamp, folder.ID, folder.ParentID);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unrecoverable error while purging empty folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while purging empty folder {0} for {1}: {2}",
                    folder.ID, folder.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeFolderInternal(folder, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeEmptyFolder(" + folder.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge empty folder " + folder.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        public void PurgeFolders(IEnumerable<InventoryFolderBase> folders)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                PurgeFoldersInternal(folders, timeStamp);
            }
            catch (UnrecoverableInventoryStorageException e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unrecoverable error while purging folders: {0}", e);
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while purging folders: {0}", e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeFoldersInternal(folders, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeFolders(" + timeStamp.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge folders: " + e.Message, e);
                }
            }
        }

        private void PurgeFoldersInternal(IEnumerable<InventoryFolderBase> folders, long timeStamp)
        {
            foreach (InventoryFolderBase folder in folders)
            {
                this.PurgeFolderInternal(folder, timeStamp);
            }
        }

        private void GetFolderVersionIncrementMutations(Dictionary<byte[], Dictionary<string, List<Mutation>>> muts, byte[] folderIdBytes)
        {
            if (!muts.ContainsKey(folderIdBytes))
            {
                muts[folderIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!muts[folderIdBytes].ContainsKey(FOLDERVERSIONS_CF))
            {
                muts[folderIdBytes][FOLDERVERSIONS_CF] = new List<Mutation>();
            }

            muts[folderIdBytes][FOLDERVERSIONS_CF].Add(this.VersionIncrement());
        }

        private void GetFolderDeletionMutations(byte[] userIdBytes, List<byte[]> allFolderIdBytes, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            for (int i = 0; i < allFolderIdBytes.Count; i++)
            {
                GetSingleFolderDeletionMutations(userIdBytes, allFolderIdBytes[i], timeStamp, muts);
            }
        }

        private void GetSingleFolderDeletionMutations(byte[] userIdBytes, byte[] folderIdBytes, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            Mutation folderMut = new Mutation();
            folderMut.Deletion = new Deletion();
            folderMut.Deletion.Timestamp = timeStamp;

            if (!muts.ContainsKey(folderIdBytes))
            {
                muts[folderIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!muts[folderIdBytes].ContainsKey(FOLDERS_CF))
            {
                muts[folderIdBytes][FOLDERS_CF] = new List<Mutation>();
            }

            muts[folderIdBytes][FOLDERS_CF].Add(folderMut);
            

            //index removal
            if (!muts.ContainsKey(userIdBytes))
            {
                muts[userIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!muts[userIdBytes].ContainsKey(USERFOLDERS_CF))
            {
                muts[userIdBytes][USERFOLDERS_CF] = new List<Mutation>();
            }

            Mutation userFolderMut = new Mutation();
            userFolderMut.Deletion = new Deletion();
            userFolderMut.Deletion.Super_column = folderIdBytes;
            userFolderMut.Deletion.Timestamp = timeStamp;

            muts[userIdBytes][USERFOLDERS_CF].Add(userFolderMut);


            //version removal
            if (!muts[folderIdBytes].ContainsKey(FOLDERVERSIONS_CF))
            {
                muts[folderIdBytes][FOLDERVERSIONS_CF] = new List<Mutation>();
            }

            Mutation versionMut = new Mutation();
            versionMut.Deletion = new Deletion();
            versionMut.Deletion.Timestamp = timeStamp;
            muts[folderIdBytes][FOLDERVERSIONS_CF].Add(versionMut);
        }

        private void GetSubfolderEntryDeletionMutations(byte[] folderIdBytes, byte[] parentFolderIdBytes, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            Mutation folderMut = new Mutation();
            folderMut.Deletion = new Deletion();
            folderMut.Deletion.Super_column = ByteEncoderHelper.UTF8Encoder.ToByteArray("sub_folders");
            folderMut.Deletion.Timestamp = timeStamp;
            folderMut.Deletion.Predicate = new SlicePredicate();
            folderMut.Deletion.Predicate.Column_names = new List<byte[]>();
            folderMut.Deletion.Predicate.Column_names.Add(folderIdBytes);

            if (!muts.ContainsKey(parentFolderIdBytes))
            {
                muts[parentFolderIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!muts[parentFolderIdBytes].ContainsKey(FOLDERS_CF))
            {
                muts[parentFolderIdBytes][FOLDERS_CF] = new List<Mutation>();
            }

            muts[parentFolderIdBytes][FOLDERS_CF].Add(folderMut);
        }

        private void RecursiveCollectSubfoldersAndItems(UUID id, UUID ownerId, List<UUID> allFolders, List<UUID> allItems, C5.HashSet<UUID> rootItems, C5.HashSet<UUID> rootFolders, bool isRoot,
            Dictionary<Guid, InventoryFolderBase> index, StringBuilder debugFolderList)
        {
            if (index == null)
            {
                index = GetFolderIndex(ownerId);
            }

            InventoryFolderBase folder;
            try
            {
                folder = this.GetFolder(id);
            }
            catch (InventoryObjectMissingException)
            {
                //missing a folder is not a fatal exception, it could indicate a corrupted or temporarily
                //inconsistent inventory state. this should not stop the remainder of the collection
                _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Found missing folder with subFolder index remaining in parent. Inventory may need subfolder index maintenance.");
                return;
            }
            catch (InventoryStorageException e)
            {
                if (e.InnerException != null && e.InnerException is KeyNotFoundException)
                {
                    //not a fatal exception, it could indicate a corrupted or temporarily
                    //inconsistent inventory state. this should not stop the remainder of the collection
                    _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Found corrupt folder with subFolder index remaining in parent. User inventory needs subfolder index maintenance.");
                    return;
                }
                else
                {
                    throw;
                }
            }

            foreach (InventoryItemBase item in folder.Items)
            {
                allItems.Add(item.ID);

                if (isRoot)
                {
                    rootItems.Add(item.ID);
                }

                debugFolderList.AppendLine("I " + item.ID.ToString() + " " + item.Name);
            }

            foreach (InventoryNodeBase subFolder in folder.SubFolders)
            {
                if (subFolder.Owner != ownerId)
                {
                    throw new UnrecoverableInventoryStorageException(
                        String.Format("Changed owner found during recursive folder collection. Folder: {0}, Expected Owner: {1}, Found Owner: {2}",
                        subFolder.ID, ownerId, subFolder.Owner)); ;
                }


                if (SubfolderIsConsistent(subFolder.ID, folder.ID, index))
                {
                    debugFolderList.AppendLine("F " + subFolder.ID.ToString() + " " + subFolder.Name);

                    allFolders.Add(subFolder.ID);

                    if (isRoot)
                    {
                        rootFolders.Add(subFolder.ID);
                    }

                    this.RecursiveCollectSubfoldersAndItems(subFolder.ID, ownerId, allFolders, allItems, rootItems, rootFolders, false, index, debugFolderList);
                }
                else
                {
                    _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Not recursing into folder {0} with parent {1}. Index is inconsistent", 
                        subFolder.ID, folder.ID);
                }
            }
        }

        /// <summary>
        /// Makes sure that both the index and subfolder index agree that the given subfolder
        /// id belongs to the given parent
        /// </summary>
        /// <param name="subfolderId"></param>
        /// <param name="subfolderIndexParentId"></param>
        /// <returns></returns>
        private bool SubfolderIsConsistent(UUID subfolderId, UUID subfolderIndexParentId, Dictionary<Guid, InventoryFolderBase> index)
        {
            InventoryFolderBase indexFolder;
            if (index.TryGetValue(subfolderId.Guid, out indexFolder))
            {
                if (indexFolder.ParentID == subfolderIndexParentId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves a set of items in the same folder. This should be efficient compared to
        /// retrieving each item separately regardless of parent. This will be mostly used
        /// for gestures which are usually all in the same folder anyways
        /// </summary>
        /// <param name="folderId"></param>
        /// <param name="itemIds"></param>
        /// <returns></returns>
        private List<InventoryItemBase> GetItemsInSameFolder(UUID folderId, IEnumerable<UUID> itemIds, bool throwOnItemMissing)
        {
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folderId.Guid);

            ColumnParent columnParent = new ColumnParent();
            columnParent.Column_family = FOLDERS_CF;

            SlicePredicate pred = new SlicePredicate();
            pred.Column_names = new List<byte[]>();
            foreach (UUID id in itemIds)
            {
                pred.Column_names.Add(ByteEncoderHelper.GuidEncoder.ToByteArray(id.Guid));
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

            object itemDataObj =
                cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                {
                    return client.get_slice(folderIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                }), KEYSPACE);

            List<ColumnOrSuperColumn> itemCols = (List<ColumnOrSuperColumn>)itemDataObj;

            if (throwOnItemMissing && itemCols.Count != pred.Column_names.Count)
            {
                throw new InventoryObjectMissingException("One or more items requested could not be found");
            }

            List<InventoryItemBase> retItems = new List<InventoryItemBase>();
            foreach (ColumnOrSuperColumn superCol in itemCols)
            {
                Guid itemId = ByteEncoderHelper.GuidEncoder.FromByteArray(superCol.Super_column.Name);
                InventoryItemBase item = this.DecodeInventoryItem(superCol.Super_column.Columns, itemId, folderId.Guid);
                retItems.Add(item);
            }

            return retItems;
        }

        public InventoryItemBase GetItem(UUID itemId, UUID parentFolderHint)
        {
            //Retrieving an item requires a lookup of the parent folder followed by 
            //a retrieval of the item. This was a consious decision made since the 
            //inventory item data currently takes up the most space and a
            //duplication of this data to prevent the index lookup 
            //would be expensive in terms of space required

            try
            {
                Guid parentId;
                
                if (parentFolderHint != UUID.Zero)
                {
                    parentId = parentFolderHint.Guid;
                }
                else
                {
                    parentId = FindItemParentFolderId(itemId);
                }

                if (parentId == Guid.Empty)
                {
                    throw new InventoryObjectMissingException(String.Format("Item with ID {0} could not be found", itemId), "Item was not found in the index");
                }

                //try to retrieve the item. note that even though we have an index there is a chance we will
                //not have the item data due to a race condition between index mutation and item mutation
                byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(itemId.Guid);
                byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(parentId);

                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = FOLDERS_CF;
                columnParent.Super_column = itemIdBytes;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;

                ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

                object itemDataObj =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        return client.get_slice(folderIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                    }), KEYSPACE);

                List<ColumnOrSuperColumn> itemCols = (List<ColumnOrSuperColumn>)itemDataObj;

                if (itemCols.Count == 0)
                {
                    throw new InventoryObjectMissingException(String.Format("Item with ID {0} could not be found", itemId), "Item was not found in its folder");
                }

                InventoryItemBase item = this.DecodeInventoryItem(itemCols, itemId.Guid, parentId);

                return item;
            }
            catch (InventoryStorageException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to retrieve item {0}: {1}", itemId, e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        public List<InventoryItemBase> GetItems(IEnumerable<UUID> itemIds, bool throwOnNotFound)
        {
            Dictionary<UUID, List<UUID>> folderItemMapping = this.FindItemParentFolderIds(itemIds);

            List<InventoryItemBase> foundItems = new List<InventoryItemBase>();
            foreach (var kvp in folderItemMapping)
            {
                foundItems.AddRange(this.GetItemsInSameFolder(kvp.Key, kvp.Value, throwOnNotFound));
            }

            return foundItems;
        }

        private InventoryItemBase DecodeInventoryItem(List<ColumnOrSuperColumn> itemCols, Guid itemId, Guid folderId)
        {
            var itemPropsMap = this.IndexColumnsByUTF8Name(itemCols);

            return DecodeInventoryItemFromIndexedCols(itemId, folderId, itemPropsMap);
        }

        private InventoryItemBase DecodeInventoryItem(List<Column> itemCols, Guid itemId, Guid folderId)
        {
            var itemPropsMap = this.IndexColumnsByUTF8Name(itemCols);

            return DecodeInventoryItemFromIndexedCols(itemId, folderId, itemPropsMap);
        }

        private static InventoryItemBase DecodeInventoryItemFromIndexedCols(Guid itemId, Guid folderId, Dictionary<string, Column> itemPropsMap)
        {
            InventoryItemBase retItem = new InventoryItemBase
            {
                AssetID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(itemPropsMap["asset_id"].Value)),
                AssetType = ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["asset_type"].Value),
                BasePermissions = (uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["base_permissions"].Value),
                CreationDate = ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["creation_date"].Value),
                CreatorId = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(itemPropsMap["creator_id"].Value)).ToString(),
                CurrentPermissions = unchecked((uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["current_permissions"].Value)),
                Description = ByteEncoderHelper.UTF8Encoder.FromByteArray(itemPropsMap["description"].Value),
                EveryOnePermissions = unchecked((uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["everyone_permissions"].Value)),
                Flags = unchecked((uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["flags"].Value)),
                Folder = new UUID(folderId),
                GroupID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(itemPropsMap["group_id"].Value)),
                GroupOwned = itemPropsMap["group_owned"].Value[0] == 0 ? false : true,
                GroupPermissions = unchecked((uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["group_permissions"].Value)),
                ID = new UUID(itemId),
                InvType = ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["inventory_type"].Value),
                Name = ByteEncoderHelper.UTF8Encoder.FromByteArray(itemPropsMap["name"].Value),
                NextPermissions = unchecked((uint)ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["next_permissions"].Value)),
                Owner = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(itemPropsMap["owner_id"].Value)),
                SalePrice = ByteEncoderHelper.LittleEndianInt32Encoder.FromByteArray(itemPropsMap["sale_price"].Value),
                SaleType = itemPropsMap["sale_type"].Value[0]
            };

            return retItem;
        }

        public Guid FindItemParentFolderId(UUID itemId)
        {
            ColumnParent columnParent = new ColumnParent();
            columnParent.Column_family = ITEMPARENTS_CF;

            SlicePredicate pred = new SlicePredicate();
            SliceRange range = new SliceRange();
            range.Start = new byte[0];
            range.Finish = new byte[0];
            range.Reversed = false;
            range.Count = int.MaxValue;
            pred.Slice_range = range;

            byte[] itemIdArray = ByteEncoderHelper.GuidEncoder.ToByteArray(itemId.Guid);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

            object val = 
                cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                {
                    return client.get_slice(itemIdArray, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                }), KEYSPACE);

            List<ColumnOrSuperColumn> indexCols = (List<ColumnOrSuperColumn>)val;

            //no index means the item doesnt exist
            if (indexCols.Count == 0)
            {
                return Guid.Empty;
            }

            var indexedColsByName = this.IndexColumnsByUTF8Name(indexCols);

            return ByteEncoderHelper.GuidEncoder.FromByteArray(indexedColsByName["parent"].Value);
        }

        /// <summary>
        /// Returns a dictionary of parents with a list of items Dictionary[FolderID, List[Items]] 
        /// </summary>
        /// <param name="itemIds"></param>
        /// <returns></returns>
        public Dictionary<UUID, List<UUID>> FindItemParentFolderIds(IEnumerable<UUID> itemIds)
        {
            ColumnParent columnParent = new ColumnParent();
            columnParent.Column_family = ITEMPARENTS_CF;

            SlicePredicate pred = new SlicePredicate();
            pred.Column_names = new List<byte[]>();
            pred.Column_names.Add(ByteEncoderHelper.UTF8Encoder.ToByteArray("parent"));

            List<byte[]> allItemIdBytes = new List<byte[]>();
            foreach (UUID id in itemIds)
            {
                allItemIdBytes.Add(ByteEncoderHelper.GuidEncoder.ToByteArray(id.Guid));
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

            object val =
                cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                {
                    return client.multiget_slice(allItemIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                }), KEYSPACE);

            Dictionary<byte[], List<ColumnOrSuperColumn>> itemParentCols = (Dictionary<byte[], List<ColumnOrSuperColumn>>)val;
            Dictionary<UUID, List<UUID>> retParents = new Dictionary<UUID, List<UUID>>();

            foreach (KeyValuePair<byte[], List<ColumnOrSuperColumn>> kvp in itemParentCols)
            {
                if (kvp.Value.Count == 1)
                {
                    Column col = kvp.Value[0].Column;

                    UUID parentId = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Value));

                    if (!retParents.ContainsKey(parentId))
                    {
                        retParents.Add(parentId, new List<UUID>());
                    }

                    retParents[parentId].Add(new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(kvp.Key)));
                }
            }

            return retParents;
        }

        public void CreateItem(InventoryItemBase item)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            CheckAndFixItemParentFolder(item);

            try
            {
                CreateItemInternal(item, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while creating item {0} for {1}: {2}",
                    item.ID, item.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.CreateItemInternal(item, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "CreateItem(" + item.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not create item " + item.ID.ToString()+ e.Message, e);
                }
            }
        }

        private void CheckAndFixItemParentFolder(InventoryItemBase item)
        {
            if (item.Folder == UUID.Zero)
            {
                _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Repairing parent folder ID for item {0} for {1}: Folder set to UUID.Zero", item.ID, item.Owner);
                item.Folder = this.FindFolderForType(item.Owner, (AssetType)FolderType.Root).ID;
            }
        }

        private void CreateItemInternal(InventoryItemBase item, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();
        
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Folder.Guid);
            byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.ID.Guid);

            muts[itemIdBytes] = new Dictionary<string, List<Mutation>>();
            
            //create the item properly in its parent folder
            this.GetItemStorageMutations(item, folderIdBytes, itemIdBytes, muts, timeStamp);

            //also add the reference to the item in ItemParents
            this.GetItemParentStorageMutations(item, itemIdBytes, folderIdBytes, timeStamp, muts);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        private void GetItemParentStorageMutations(InventoryItemBase item, byte[] itemIdBytes, byte[] folderIdBytes,
            long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> outMuts)
        {
            Mutation itemParentMut = new Mutation();
            itemParentMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            itemParentMut.Column_or_supercolumn.Column = new Column();
            itemParentMut.Column_or_supercolumn.Column.Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("parent");
            itemParentMut.Column_or_supercolumn.Column.Timestamp = timeStamp;
            itemParentMut.Column_or_supercolumn.Column.Value = folderIdBytes;

            outMuts[itemIdBytes][ITEMPARENTS_CF] = new List<Mutation>();
            outMuts[itemIdBytes][ITEMPARENTS_CF].Add(itemParentMut);
        }

        private void GetItemStorageMutations(InventoryItemBase item, byte[] folderIdBytes, byte[] itemIdBytes, 
            Dictionary<byte[], Dictionary<string, List<Mutation>>> outMuts, long timeStamp)
        {
            //Folder CF mutations
            List<Mutation> itemMutList = new List<Mutation>();

            Mutation propertiesMut = new Mutation();
            propertiesMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column = new SuperColumn();
            propertiesMut.Column_or_supercolumn.Super_column.Name = itemIdBytes;

            List<Column> propertiesColumns = new List<Column>();

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("name"),
                    Value = ByteEncoderHelper.UTF8Encoder.ToByteArray(item.Name),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("description"),
                    Value = ByteEncoderHelper.UTF8Encoder.ToByteArray(item.Description),

                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("creation_date"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(item.CreationDate),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("creator_id"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(item.CreatorIdAsUuid.Guid),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("owner_id"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Owner.Guid),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("asset_id"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(item.AssetID.Guid),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("asset_type"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(item.AssetType),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("inventory_type"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(item.InvType),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("flags"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.Flags)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("group_owned"),
                    Value = new byte[1] { item.GroupOwned == true ? (byte)1 : (byte)0 },
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("group_id"),
                    Value = ByteEncoderHelper.GuidEncoder.ToByteArray(item.GroupID.Guid),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("group_permissions"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.GroupPermissions)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("current_permissions"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.CurrentPermissions)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("base_permissions"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.BasePermissions)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("next_permissions"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.NextPermissions)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("everyone_permissions"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(unchecked((int)item.EveryOnePermissions)),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("sale_price"),
                    Value = ByteEncoderHelper.LittleEndianInt32Encoder.ToByteArray(item.SalePrice),
                    Timestamp = timeStamp,
                }
            );

            propertiesColumns.Add(
                new Column
                {
                    Name = ByteEncoderHelper.UTF8Encoder.ToByteArray("sale_type"),
                    Value = new byte[1] { item.SaleType },
                    Timestamp = timeStamp,
                }
            );

            propertiesMut.Column_or_supercolumn.Super_column.Columns = propertiesColumns;
            itemMutList.Add(propertiesMut);

            Dictionary<string, List<Mutation>> folderKeyMuts = new Dictionary<string, List<Mutation>>();
            folderKeyMuts[FOLDERS_CF] = itemMutList;

            //version increment
            Mutation versionMut = VersionIncrement();
            folderKeyMuts[FOLDERVERSIONS_CF] = new List<Mutation> { versionMut };

            outMuts[folderIdBytes] = folderKeyMuts;
        }

        public void SaveItem(InventoryItemBase item)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            CheckAndFixItemParentFolder(item);

            try
            {
                SaveItemInternal(item, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while saving item {0} for {1}: {2}",
                    item.ID, item.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.SaveItemInternal(item, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "SaveItem(" + item.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not save item " + item.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void SaveItemInternal(InventoryItemBase item, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Folder.Guid);
            byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.ID.Guid);

            muts[itemIdBytes] = new Dictionary<string, List<Mutation>>();

            //update the item properly in its parent folder
            this.GetItemStorageMutations(item, folderIdBytes, itemIdBytes, muts, timeStamp);

            //to keep the transaction consistent, if we're updating the item in its parent folder, 
            //we also rewrite the ItemParent link. That way this op can run in parallel with other
            //ops affecting the item and at least one of them will "win"
            //this is to mitigate an effect seen in production where the item loses its ItemParent
            //entry but is still in the folder
            this.GetItemParentStorageMutations(item, itemIdBytes, folderIdBytes, timeStamp, muts);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        public void MoveItem(InventoryItemBase item, InventoryFolderBase parentFolder)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            if (parentFolder.ID == UUID.Zero)
            {
                throw new InventoryStorageException("Not moving item to new folder. Destination folder has ID UUID.Zero");
            }

            try
            {
                //dont do anything with an item that wants to set its new parent 
                //to its current parent. this can cause corruption
                if (item.Folder == parentFolder.ID)
                {
                    _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Refusing to move item {0} to new folder {1} for {2}. The source and destination folder are the same",
                        item.ID, parentFolder.ID, item.Owner);
                    return;
                }

                MoveItemInternal(item, parentFolder.ID, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while moving item {0} to folder {1}: {2}",
                    item.ID, parentFolder.ID, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.MoveItemInternal(item, parentFolder.ID, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "MoveItem(" + item.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not move item " + item.ID.ToString() + " " + e.Message, e);
                }
            }
        }

        private void MoveItemInternal(InventoryItemBase item, UUID parentFolder, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] oldParentFolderBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Folder.Guid);
            byte[] newParentFolderBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(parentFolder.Guid);
            byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.ID.Guid);

            muts[itemIdBytes] = new Dictionary<string, List<Mutation>>();

            //insert the item into its new folder
            this.GetItemStorageMutations(item, newParentFolderBytes, itemIdBytes, muts, timeStamp);

            //update the index to point to the new parent
            this.GetItemParentStorageMutations(item, itemIdBytes, newParentFolderBytes, timeStamp, muts);

            //remove the item from the old folder
            this.GetItemDeletionMutations(itemIdBytes, oldParentFolderBytes, timeStamp, muts, true);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);

            item.Folder = parentFolder;
        }

        /// <summary>
        /// Removes the item from its parent folder container
        /// </summary>
        /// <param name="itemIdBytes"></param>
        /// <param name="oldParentFolderBytes"></param>
        /// <param name="timeStamp"></param>
        /// <param name="muts"></param>
        private void GetItemDeletionMutations(byte[] itemIdBytes, byte[] folderIdBytes, long timeStamp, 
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts, bool includeParentFolderVersionInc)
        {
            Mutation itemParentMut = new Mutation();
            itemParentMut.Deletion = new Deletion();
            itemParentMut.Deletion.Super_column = itemIdBytes;
            itemParentMut.Deletion.Timestamp = timeStamp;

            if (!muts.ContainsKey(folderIdBytes))
            {
                muts[folderIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!muts[folderIdBytes].ContainsKey(FOLDERS_CF))
            {
                muts[folderIdBytes][FOLDERS_CF] = new List<Mutation>();
            }

            muts[folderIdBytes][FOLDERS_CF].Add(itemParentMut);

            if (includeParentFolderVersionInc)
            {
                if (!muts[folderIdBytes].ContainsKey(FOLDERVERSIONS_CF))
                {
                    muts[folderIdBytes][FOLDERVERSIONS_CF] = new List<Mutation>();
                }

                //version increment
                Mutation versionMut = VersionIncrement();
                muts[folderIdBytes][FOLDERVERSIONS_CF].Add(versionMut);
            }
        }

        public UUID SendItemToTrash(InventoryItemBase item, UUID trashFolderHint)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                return SendItemToTrashInternal(item, trashFolderHint, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while sending item {0} to trash for {1}: {2}",
                    item.ID, item.Owner, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.SendItemToTrashInternal(item, trashFolderHint, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "TrashItem(" + item.ID.ToString() + ")");

                    return trashFolderHint;
                }
                else
                {
                    throw new InventoryStorageException("Could not send item " + item.ID.ToString() + " to trash: " + e.Message, e);
                }
            }
        }

        private UUID SendItemToTrashInternal(InventoryItemBase item, UUID trashFolderHint, long timeStamp)
        {
            if (trashFolderHint != UUID.Zero)
            {
                this.MoveItemInternal(item, trashFolderHint, timeStamp);
                return trashFolderHint;
            }
            else
            {
                InventoryFolderBase trashFolder;

                try
                {
                    trashFolder = this.FindFolderForType(item.Owner, (AssetType)FolderType.Trash);
                }
                catch (Exception e)
                {
                    throw new InventoryStorageException(String.Format("Trash folder could not be found for user {0}: {1}", item.Owner, e), e);
                }

                this.MoveItemInternal(item, trashFolder.ID, timeStamp);
                return trashFolder.ID;
            }
        }

        public void PurgeItem(InventoryItemBase item)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                string invType;
                if (item.AssetType == (int)AssetType.Link)
                    invType = "link";
                else
                if (item.AssetType == (int)AssetType.LinkFolder)
                    invType = "folder link";
                else
                    invType = "type "+item.AssetType.ToString();

                _log.WarnFormat("[Inworldz.Data.Inventory.Cassandra] Purge of {0} id={1} asset={2} '{3}' for user={4}", invType, item.ID, item.AssetID, item.Name, item.Owner);
                PurgeItemInternal(item, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while purging item {0}: {1}",
                    item.ID, e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeItemInternal(item, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeItem(" + item.ID.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge item " + item.ID.ToString() + ": " + e.Message, e);
                }
            }
        }

        private void PurgeItemInternal(InventoryItemBase item, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] oldParentFolderBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Folder.Guid);
            byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.ID.Guid);

            muts[itemIdBytes] = new Dictionary<string, List<Mutation>>();

            //remove the item from the index
            this.GetItemParentDeletionMutations(itemIdBytes, timeStamp, muts);

            //remove the item from the old folder
            this.GetItemDeletionMutations(itemIdBytes, oldParentFolderBytes, timeStamp, muts, true);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }
        
        private void GetItemParentDeletionMutations(byte[] itemIdBytes, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> outMuts)
        {
            Mutation itemParentMut = new Mutation();
            itemParentMut.Deletion = new Deletion();
            itemParentMut.Deletion.Timestamp = timeStamp;

            if (! outMuts.ContainsKey(itemIdBytes))
            {
                outMuts[itemIdBytes] = new Dictionary<string, List<Mutation>>();
            }

            if (!outMuts[itemIdBytes].ContainsKey(ITEMPARENTS_CF))
            {
                outMuts[itemIdBytes][ITEMPARENTS_CF] = new List<Mutation>();
            }
            
            outMuts[itemIdBytes][ITEMPARENTS_CF].Add(itemParentMut);
        }

        public void PurgeItems(IEnumerable<InventoryItemBase> items)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            try
            {
                PurgeItemsInternal(items, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Exception caught while purging items: {0}", e);

                if (_delayedMutationMgr != null)
                {
                    DelayedMutation.DelayedMutationDelegate delayedDelegate = delegate() { this.PurgeItemsInternal(items, timeStamp); };
                    _delayedMutationMgr.AddMutationForRetry(delayedDelegate, "PurgeItems(" + timeStamp.ToString() + ")");
                }
                else
                {
                    throw new InventoryStorageException("Could not purge items: " + e.Message, e);
                }
            }
        }

        private void PurgeItemsInternal(IEnumerable<InventoryItemBase> items, long timeStamp)
        {
            C5.HashSet<byte[]> allParentFolderBytes = new C5.HashSet<byte[]>(new ByteArrayValueComparer());
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            foreach (InventoryItemBase item in items)
            {
                byte[] oldParentFolderBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Folder.Guid);
                allParentFolderBytes.FindOrAdd(ref oldParentFolderBytes);

                byte[] itemIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(item.ID.Guid);

                muts[itemIdBytes] = new Dictionary<string, List<Mutation>>();

                //remove the item from the index
                this.GetItemParentDeletionMutations(itemIdBytes, timeStamp, muts);

                //remove the item from the old folder
                this.GetItemDeletionMutations(itemIdBytes, oldParentFolderBytes, timeStamp, muts, true);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        public void ActivateGestures(UUID userId, IEnumerable<UUID> itemIds)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            //failed gesture de/activation is not really fatal nor do we want to retry
            //so we don't bother to run it through the delayed mutation manager
            try
            {
                ActivateGesturesInternal(userId, itemIds, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to activate gestures for {0}: {1}",
                    userId, e);

                throw new InventoryStorageException(String.Format("Unable to activate gestures for {0}: {1}", userId, e.Message), e);
            }
        }

        private void ActivateGesturesInternal(UUID userId, IEnumerable<UUID> itemIds, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] userIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(userId.Guid);
            muts[userIdBytes] = new Dictionary<string, List<Mutation>>();
            muts[userIdBytes][USERACTIVEGESTURES_CF] = new List<Mutation>();

            foreach (UUID item in itemIds)
            {
                this.GetGestureActivationMutations(userIdBytes, item, timeStamp, muts);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        private void GetGestureActivationMutations(byte[] userIdBytes, UUID item, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            Mutation gestureAddMut = new Mutation();
            gestureAddMut.Column_or_supercolumn = new ColumnOrSuperColumn();
            gestureAddMut.Column_or_supercolumn.Column = new Column();
            gestureAddMut.Column_or_supercolumn.Column.Name = ByteEncoderHelper.GuidEncoder.ToByteArray(item.Guid);
            gestureAddMut.Column_or_supercolumn.Column.Timestamp = timeStamp;
            gestureAddMut.Column_or_supercolumn.Column.Value = new byte[0];

            muts[userIdBytes][USERACTIVEGESTURES_CF].Add(gestureAddMut);
        }

        public void DeactivateGestures(UUID userId, IEnumerable<UUID> itemIds)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            //failed gesture de/activation is not really fatal nor do we want to retry
            //so we don't bother to run it through the delayed mutation manager
            try
            {
                DeactivateGesturesInternal(userId, itemIds, timeStamp);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to deactivate gestures for {0}: {1}",
                    userId, e);

                throw new InventoryStorageException(String.Format("Unable to deactivate gestures for {0}: {1}", userId, e));
            }
        }

        private void DeactivateGesturesInternal(UUID userId, IEnumerable<UUID> itemIds, long timeStamp)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] userIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(userId.Guid);
            muts[userIdBytes] = new Dictionary<string, List<Mutation>>();
            muts[userIdBytes][USERACTIVEGESTURES_CF] = new List<Mutation>();

            foreach (UUID item in itemIds)
            {
                this.GetGestureDeactivationMutations(userIdBytes, item, timeStamp, muts);
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        private void GetGestureDeactivationMutations(byte[] userIdBytes, UUID item, long timeStamp, Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            Mutation gestureRemMut = new Mutation();
            gestureRemMut.Deletion = new Deletion();
            gestureRemMut.Deletion.Predicate = new SlicePredicate();
            gestureRemMut.Deletion.Predicate.Column_names = new List<byte[]>();
            gestureRemMut.Deletion.Predicate.Column_names.Add(ByteEncoderHelper.GuidEncoder.ToByteArray(item.Guid));
            gestureRemMut.Deletion.Timestamp = timeStamp;

            muts[userIdBytes][USERACTIVEGESTURES_CF].Add(gestureRemMut);
        }

        public List<InventoryItemBase> GetActiveGestureItems(UUID userId)
        {
            List<UUID> gestureItemIds = this.GetActiveGestureItemIds(userId);

            return this.GetItems(gestureItemIds, false);
        }

        private List<UUID> GetActiveGestureItemIds(UUID userId)
        {
            try
            {
                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = USERACTIVEGESTURES_CF;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;

                ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);

                object retobj =
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        byte[] userIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(userId.Guid);
                        return client.get_slice(userIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);
                    
                    }), KEYSPACE);

                List<ColumnOrSuperColumn> cols = (List<ColumnOrSuperColumn>)retobj;
                List<UUID> ret = new List<UUID>();

                foreach (ColumnOrSuperColumn col in cols)
                {
                    ret.Add(new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Column.Name)));
                }

                return ret;
            }
            catch (Exception e)
            {
                throw new InventoryStorageException(e.Message, e);
            }
        }

        /// <summary>
        /// Retrieves the index of all folders owned by this user and attempts to
        /// find and repair any inconsistencies
        /// </summary>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        public void Maint_RepairFolderIndex(UUID ownerId)
        {
            try
            {
                byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(ownerId.Guid);

                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = USERFOLDERS_CF;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;


                List<ColumnOrSuperColumn> cols = this.RetrieveAllColumnsInChunks(FOLDER_INDEX_CHUNK_SZ,
                    ownerIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                List<Guid> badIndexFolders = new List<Guid>();

                foreach (ColumnOrSuperColumn col in cols)
                {
                    Dictionary<string, Column> columns = this.IndexColumnsByUTF8Name(col.Super_column.Columns);

                    try
                    {
                        InventoryFolderBase folder = new InventoryFolderBase
                        {
                            ID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Super_column.Name)),
                            Level = (InventoryFolderBase.FolderLevel)columns["level"].Value[0],
                            Name = ByteEncoderHelper.UTF8Encoder.FromByteArray(columns["name"].Value),
                            Owner = ownerId,
                            ParentID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(columns["parent_folder"].Value)),
                            Type = (short)ByteEncoderHelper.Int32Encoder.FromByteArray(columns["type"].Value),
                        };
                    }
                    catch (KeyNotFoundException)
                    {
                        //there is a corruption, this folder can not be read
                        badIndexFolders.Add(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Super_column.Name));
                    }
                }

                List<Guid> destroyedFolders = new List<Guid>();
                List<InventoryFolderBase> recoverableFolders = new List<InventoryFolderBase>();

                //for each folder that has a bad index, try to read the folder.
                //if we can read the folder, restore it from the data we have
                //otherwise delete the index, the data is gone
                foreach (Guid id in badIndexFolders)
                {
                    try
                    {
                        InventoryFolderBase folder = this.GetFolderAttributes(new UUID(id));

                        //also verify the parent exists and is readable
                        InventoryFolderBase parentFolder = this.GetFolderAttributes(folder.ParentID);

                        recoverableFolders.Add(folder);
                    }
                    catch (KeyNotFoundException)
                    {
                        destroyedFolders.Add(id);
                    }
                    catch (InventoryObjectMissingException)
                    {
                        destroyedFolders.Add(id);
                    }
                }

                long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

                foreach (InventoryFolderBase folder in recoverableFolders)
                {
                    //recover anything recoverable
                    this.CreateFolderInternal(folder, timeStamp);
                }

                foreach (Guid id in destroyedFolders)
                {
                    this.RemoveFromIndex(ownerId.Guid, id, timeStamp);
                }
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to recover folder index: {0}", e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        public void Maint_RebuildItemIndex(UUID ownerId)
        {
            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            //to rebuild the index, we collect all items and rewrite their item->folder index entries
            List<InventoryFolderBase> skel = this.GetInventorySkeleton(ownerId);

            List<KeyValuePair<InventoryItemBase, UUID>> parentFolders = new List<KeyValuePair<InventoryItemBase, UUID>>();

            foreach (InventoryFolderBase skelFolder in skel)
            {
                InventoryFolderBase fullFolder = this.GetFolder(skelFolder.ID);
                foreach (var item in fullFolder.Items)
                {
                    parentFolders.Add(new KeyValuePair<InventoryItemBase, UUID>(item, fullFolder.ID));
                }
            }

            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            foreach (KeyValuePair<InventoryItemBase, UUID> itemParent in parentFolders)
            {
                byte[] itemId = ByteEncoderHelper.GuidEncoder.ToByteArray(itemParent.Key.ID.Guid);
                byte[] folderId = ByteEncoderHelper.GuidEncoder.ToByteArray(itemParent.Value.Guid);

                muts[itemId] = new Dictionary<string, List<Mutation>>();

                this.GetItemParentStorageMutations(itemParent.Key, itemId, folderId, timeStamp, muts);
            }

            if (muts.Count == 0)
            {
                return;
            }

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        /// <summary>
        /// Pulls down all folders and collects the list of subfolder
        /// UUIDs for each. Then attempts to read each of the sub folders 
        /// listed in the subfolder index, and removes any indexed subfolders
        /// that are no longer readable due to a partial deletion
        /// </summary>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        public void Maint_RepairSubfolderIndexes(UUID ownerId)
        {
            try
            {
                byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(ownerId.Guid);

                ColumnParent columnParent = new ColumnParent();
                columnParent.Column_family = USERFOLDERS_CF;

                SlicePredicate pred = new SlicePredicate();
                SliceRange range = new SliceRange();
                range.Start = new byte[0];
                range.Finish = new byte[0];
                range.Reversed = false;
                range.Count = int.MaxValue;
                pred.Slice_range = range;


                List<ColumnOrSuperColumn> cols = this.RetrieveAllColumnsInChunks(FOLDER_INDEX_CHUNK_SZ,
                    ownerIdBytes, columnParent, pred, DEFAULT_CONSISTENCY_LEVEL);

                List<InventoryFolderBase> goodIndexFolders = new List<InventoryFolderBase>();

                foreach (ColumnOrSuperColumn col in cols)
                {
                    Dictionary<string, Column> columns = this.IndexColumnsByUTF8Name(col.Super_column.Columns);

                    try
                    {
                        InventoryFolderBase folder = new InventoryFolderBase
                        {
                            ID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(col.Super_column.Name)),
                            Level = (InventoryFolderBase.FolderLevel)columns["level"].Value[0],
                            Name = ByteEncoderHelper.UTF8Encoder.FromByteArray(columns["name"].Value),
                            Owner = ownerId,
                            ParentID = new UUID(ByteEncoderHelper.GuidEncoder.FromByteArray(columns["parent_folder"].Value)),
                            Type = (short)ByteEncoderHelper.Int32Encoder.FromByteArray(columns["type"].Value),
                        };

                        goodIndexFolders.Add(folder);
                    }
                    catch (KeyNotFoundException)
                    {
                        //there is a corruption, this folder can not be read. Ignore since there is
                        //another maint that can fix this that should be run first.
                    }
                }

                List<KeyValuePair<Guid, Guid>> invalidParentChild = new List<KeyValuePair<Guid, Guid>>();

                //for each folder in the index, retrieve it and check for unreadable subfolders
                foreach (InventoryFolderBase indexFolder in goodIndexFolders)
                {
                    try
                    {
                        InventoryFolderBase folder = this.GetFolder(indexFolder.ID);

                        foreach (var subfolder in folder.SubFolders)
                        {
                            try
                            {
                                InventoryFolderBase subFolderDetails = this.GetFolderAttributes(subfolder.ID);
                            }
                            catch (InventoryObjectMissingException)
                            {
                                invalidParentChild.Add(new KeyValuePair<Guid, Guid>(folder.ID.Guid, subfolder.ID.Guid));
                            }
                            catch (InventoryStorageException e)
                            {
                                if (e.InnerException != null && e.InnerException is KeyNotFoundException)
                                {
                                    invalidParentChild.Add(new KeyValuePair<Guid, Guid>(folder.ID.Guid, subfolder.ID.Guid));
                                }
                            }
                            
                        }
                    }
                    catch (Exception e)
                    {
                        //we can't even get the folder, so no subfolders to fix
                        _log.ErrorFormat("[InWorldz.Data.Inventory.Cassandra] Indexed folder {0} could not be retrieved to look for children: {1}", indexFolder.ID, e);
                    }
                }

                _log.InfoFormat("[InWorldz.Data.Inventory.Cassandra][MAINT] Found {0} subfolder indexes to repair", invalidParentChild.Count);

                long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

                //we couldn't read the folder, pull it from the subfolder list of its parent
                foreach (KeyValuePair<Guid, Guid> parentChildKvp in invalidParentChild)
                {
                    byte[] childIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(parentChildKvp.Value);
                    Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

                    GenerateSubfolderIndexDeletion(parentChildKvp.Key, timeStamp, childIdBytes, muts);

                    ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
                    cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
                    {
                        client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);
                        return null;

                    }), KEYSPACE);
                }
            }
            catch (Exception e)
            {
                _log.ErrorFormat("[Inworldz.Data.Inventory.Cassandra] Unable to repair subfolder index: {0}", e);
                throw new InventoryStorageException(e.Message, e);
            }
        }

        /// <summary>
        /// This is a last resort function. To be used only when a folder has become so huge or filled
        /// with tombstones that it is unreadable. Will remove the folder and indexes only. Does
        /// not recurse into the folder to find children
        /// </summary>
        /// <param name="folderId"></param>
        public void Maint_DestroyFolder(Guid userId, Guid folderId)
        {
            long timeStamp = Util.UnixTimeSinceEpochInMicroseconds();

            //block all deletion requests for a folder with a 0 id
            if (folderId == Guid.Empty)
            {
                throw new UnrecoverableInventoryStorageException("Refusing to allow the deletion of the inventory ZERO root folder");
            }

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folderId);
            byte[] ownerIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(userId);

            //then we delete this actual folder
            this.GetSingleFolderDeletionMutations(ownerIdBytes, folderIdBytes, timeStamp, muts);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        private void RemoveFromIndex(Guid userId, Guid folderId, long timeStamp)
        {
            byte[] userIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(userId);
            byte[] folderIdBytes = ByteEncoderHelper.GuidEncoder.ToByteArray(folderId);

            Dictionary<byte[], Dictionary<string, List<Mutation>>> muts = new Dictionary<byte[], Dictionary<string, List<Mutation>>>();

            muts[userIdBytes] = new Dictionary<string, List<Mutation>>();
            muts[userIdBytes][USERFOLDERS_CF] = new List<Mutation>();

            Mutation userFolderMut = new Mutation();
            userFolderMut.Deletion = new Deletion();
            userFolderMut.Deletion.Super_column = folderIdBytes;
            userFolderMut.Deletion.Timestamp = timeStamp;

            muts[userIdBytes][USERFOLDERS_CF].Add(userFolderMut);

            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);

                return null;

            }), KEYSPACE);
        }

        /// <summary>
        /// Provides a way for the unit tests to perform mutations directly
        /// </summary>
        /// <param name="muts"></param>
        internal void PerformMutations(Dictionary<byte[], Dictionary<string, List<Mutation>>> muts)
        {
            ICluster cluster = AquilesHelper.RetrieveCluster(_clusterName);
            cluster.Execute(new ExecutionBlock(delegate(Apache.Cassandra.Cassandra.Client client)
            {
                client.batch_mutate(muts, DEFAULT_CONSISTENCY_LEVEL);
                return null;

            }), KEYSPACE);
        }

        #endregion

        
    }
}
