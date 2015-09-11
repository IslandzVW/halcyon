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
using System.Data.SQLite;
using System.IO;
using OpenSim.Framework;

namespace InWorldz.PhysxPhysics.Meshing
{
    internal class HacdHullCache
    {
        const string HULL_CACHE_FILENAME = "hacd_hulls.cache.db3";

        /// <summary>
        /// Maximum number of items to hold in the LRU cache
        /// </summary>
        const int MEM_CACHE_ITEMS = 16;

        /// <summary>
        /// The connection to the databases is kept open for the duration of the application
        /// </summary>
        private SQLiteConnection _connection;

        /// <summary>
        /// A small LRU cache just to try and deal with people manipulating sizes of rezzed prims
        /// </summary>
        private LRUCache<ulong, HacdConvexHull[]> _memCache = new LRUCache<ulong, HacdConvexHull[]>(MEM_CACHE_ITEMS);


        public HacdHullCache()
        {
            this.SetupAndOpenDatabase();
        }

        private void SetupAndOpenDatabase()
        {
            _connection = new SQLiteConnection(String.Format("Data Source={0}", HULL_CACHE_FILENAME));
            _connection.Open();

            const string INITIALIZED_QUERY = "SELECT COUNT(*) AS tbl_count " +
                                                "FROM sqlite_master " +
                                                "WHERE type = 'table' AND tbl_name='Hulls';";

            using (SQLiteCommand cmd = new SQLiteCommand(INITIALIZED_QUERY, _connection))
            {
                long count = (long)cmd.ExecuteScalar();

                if (count == 0)
                {
                    const string SETUP_QUERY =
                        "BEGIN;" +

                        "CREATE TABLE Hulls (" +
                        "   hash INTEGER NOT NULL, " +
                        "   hull_data BLOB " +
                        ");" +

                        "CREATE INDEX hulls_hash_index ON Hulls(hash);" +
                        
                        "COMMIT;";

                    using (SQLiteCommand createCmd = new SQLiteCommand(SETUP_QUERY, _connection))
                    {
                        createCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public bool TryGetHulls(ulong meshHash, out HacdConvexHull[] cachedHulls)
        {
            HacdConvexHull[] memHulls;
            if (_memCache.TryGetValue(meshHash, out memHulls))
            {
                //we must clone here because the hulls will be scaled when they're used which would
                //change the values in this, inmemory, unscaled array
                cachedHulls = HacdConvexHull.CloneHullArray(memHulls);
                return true;
            }

            const string FIND_QRY = "SELECT hull_data FROM Hulls WHERE hash = @hhash";
            using (SQLiteCommand cmd = new SQLiteCommand(FIND_QRY, _connection))
            {
                SQLiteParameter hashParam = cmd.CreateParameter();
                hashParam.ParameterName = "@hhash";
                hashParam.DbType = System.Data.DbType.Int64;
                hashParam.Value = (Int64)meshHash;

                cmd.Parameters.Add(hashParam);

                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        cachedHulls = null;
                        return false;
                    }

                    List<HacdConvexHull> retHulls = new List<HacdConvexHull>();
                    while (reader.Read())
                    {
                        byte[] buffer = (byte[])reader[0];

                        using (MemoryStream hullStream = new MemoryStream(buffer))
                        {
                            HacdConvexHull hull = ProtoBuf.Serializer.Deserialize<HacdConvexHull>(hullStream);
                            if (hull == null) throw new Exception("Protobuf deserialization of convex hull failed");

                            hull.FillVerticesFromRaw();
                            retHulls.Add(hull);
                        }
                    }

                    cachedHulls = retHulls.ToArray();
                    _memCache.Add(meshHash, HacdConvexHull.CloneHullArray(cachedHulls));
                    return true;
                }
            }
        }

        internal void CacheHulls(ulong meshHash, HacdConvexHull[] retHulls)
        {
            //write each state to disk
            SQLiteTransaction transaction = _connection.BeginTransaction();

            try
            {
                this.ClearExistingHulls(meshHash);

                foreach (HacdConvexHull hull in retHulls)
                {
                    this.WriteHullRow(meshHash, hull);
                    hull._rawVerts = null;
                }

                transaction.Commit();

                _memCache.Add(meshHash, HacdConvexHull.CloneHullArray(retHulls));
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void ClearExistingHulls(ulong meshHash)
        {
            const string CLEAR_CMD = "DELETE FROM Hulls WHERE hash = @hhash;";

            using (SQLiteCommand cmd = new SQLiteCommand(CLEAR_CMD, _connection))
            {
                SQLiteParameter hashParam = cmd.CreateParameter();
                hashParam.ParameterName = "@hhash";
                hashParam.DbType = System.Data.DbType.Int64;
                hashParam.Value = (Int64)meshHash;

                cmd.Parameters.Add(hashParam);

                cmd.ExecuteNonQuery();
            }
        }

        private void WriteHullRow(ulong meshHash, HacdConvexHull hull)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, hull);

                const string INSERT_CMD = "INSERT INTO Hulls(hash, hull_data) VALUES(@hhash, @hullData)";
                using (SQLiteCommand cmd = new SQLiteCommand(INSERT_CMD, _connection))
                {
                    SQLiteParameter hashParam = cmd.CreateParameter();
                    hashParam.ParameterName = "@hhash";
                    hashParam.DbType = System.Data.DbType.Int64;
                    hashParam.Value = (Int64)meshHash;

                    SQLiteParameter hullDataParam = cmd.CreateParameter();
                    hullDataParam.ParameterName = "@hullData";
                    hullDataParam.Value = ms.ToArray();

                    cmd.Parameters.Add(hashParam);
                    cmd.Parameters.Add(hullDataParam);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
