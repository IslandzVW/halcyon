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
 * Beth Reischl - 2/28/2010
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
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Data.SimpleDB;


namespace OpenSim.Region.CoreModules.Avatar.Search
{
    public class AvatarSearchModule : IRegionModule
    {
        //
        // Log module
        //
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private ConnectionFactory _connFactory;

//        private IConfigSource profileConfig;
        private List<Scene> m_Scenes = new List<Scene>();
        //private string m_SearchServer = String.Empty;
        private bool m_Enabled = true;

        // NOTE: RDB-related code is all grouped here and is copied in LandManagementModule (for now).
        // This needs to be factored. I'd like to see MySQLSimpleDB be extended to 
        // a MySQLSimpleRegionDB class that wraps the underlying single-db calls into combined calls
        // and redirects queries to the correct database.
        private string _rdbConnectionTemplate;
        private string _rdbConnectionTemplateDebug;
        private readonly TimeSpan RDB_CACHE_TIMEOUT = TimeSpan.FromHours(4);
        private DateTime _rdbCacheTime;
        private List<string> _rdbHostCache = new List<string>();

        // Filter land search results to online regions to remove dead parcels.
        private const int REGIONS_CACHE_TIME = 300;     // regions update land search results filters once every 5 minutes
        private List<UUID> mRegionsOnline = new List<UUID>();       // Count of 0 means no filter, initially.
        private DateTime mRegionsOnlineStamp = DateTime.MinValue;   // first reference requires an update

        // status flags embedded in search replay messages of classifieds, events, groups, and places.
        // Places
        private const uint STATUS_SEARCH_PLACES_NONE                = 0x0;
        private const uint STATUS_SEARCH_PLACES_BANNEDWORD = 0x1 << 0;
        private const uint STATUS_SEARCH_PLACES_SHORTSTRING = 0x1 << 1;
        private const uint STATUS_SEARCH_PLACES_FOUNDNONE = 0x1 << 2;
        private const uint STATUS_SEARCH_PLACES_SEARCHDISABLED = 0x1 << 3;
        private const uint STATUS_SEARCH_PLACES_ESTATEEMPTY = 0x1 << 4;
        // Events
        private const uint STATUS_SEARCH_EVENTS_NONE = 0x0;
        private const uint STATUS_SEARCH_EVENTS_BANNEDWORD = 0x1 << 0;
        private const uint STATUS_SEARCH_EVENTS_SHORTSTRING = 0x1 << 1;
        private const uint STATUS_SEARCH_EVENTS_FOUNDNONE = 0x1 << 2;
        private const uint STATUS_SEARCH_EVENTS_SEARCHDISABLED = 0x1 << 3;
        private const uint STATUS_SEARCH_EVENTS_NODATEOFFSET = 0x1 << 4;
        private const uint STATUS_SEARCH_EVENTS_NOCATEGORY = 0x1 << 5;
        private const uint STATUS_SEARCH_EVENTS_NOQUERY = 0x1 << 6;

        //Classifieds
        private const uint STATUS_SEARCH_CLASSIFIEDS_NONE = 0x0;
        private const uint STATUS_SEARCH_CLASSIFIEDS_BANNEDWORD = 0x1 << 0;
        private const uint STATUS_SEARCH_CLASSIFIEDS_SHORTSTRING = 0x1 << 1;
        private const uint STATUS_SEARCH_CLASSIFIEDS_FOUNDNONE = 0x1 << 2;
        private const uint STATUS_SEARCH_CLASSIFIEDS_SEARCHDISABLED = 0x1 << 3;

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (!m_Enabled)
                return;

            IConfig myConfig = config.Configs["Startup"];
            string connstr = myConfig.GetString("core_connection_string", String.Empty);
            _rdbConnectionTemplate = myConfig.GetString("rdb_connection_template", String.Empty);
            if (!String.IsNullOrWhiteSpace(_rdbConnectionTemplate))
            {
                if (!_rdbConnectionTemplate.ToLower().Contains("data source"))
                {
                    _rdbConnectionTemplate = "Data Source={0};" + _rdbConnectionTemplate;
                }
            }
            _rdbConnectionTemplateDebug = myConfig.GetString("rdb_connection_template_debug", String.Empty);
            if (!String.IsNullOrWhiteSpace(_rdbConnectionTemplateDebug))
            {
                if (!_rdbConnectionTemplateDebug.ToLower().Contains("data source"))
                {
                    _rdbConnectionTemplateDebug = "Data Source={0};" + _rdbConnectionTemplateDebug;
                }
            }
            _connFactory = new ConnectionFactory("MySQL", connstr);

            CacheRdbHosts();

            if (!m_Scenes.Contains(scene))
                m_Scenes.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
        }

        private void CacheRdbHosts()
        {
            using (ISimpleDB db = _connFactory.GetConnection())
            {
                CacheRdbHosts(db);
            }
        }

        private void CacheRdbHosts(ISimpleDB db)
        {
            List<Dictionary<string, string>> hostNames = db.QueryWithResults("SELECT host_name FROM RdbHosts");

            _rdbHostCache.Clear();
            foreach (var hostNameDict in hostNames)
            {
                _rdbHostCache.Add(hostNameDict["host_name"]);
            }

            _rdbCacheTime = DateTime.Now;
        }

        private void RefreshList(ISimpleDB rdb, string query, Dictionary<string, object> parms, List<Dictionary<string, string>> results)
        {
            results.AddRange(rdb.QueryWithResults(query, parms));
        }

        private const int MAX_RESULTS = 100;

        class RDBConnectionQuery
        {
            private const int QUERY_SIZE = 50;
            private ISimpleDB m_rdb = null;
            private string m_query = String.Empty;
            private Dictionary<string, object> m_parms = null;
            private int m_offset = 0;
            private List<Dictionary<string, string>> m_results = new List<Dictionary<string, string>>();

            public RDBConnectionQuery(ISimpleDB rdb, string query, uint queryFlags, Dictionary<string, object> parms)
            {
                m_rdb = rdb;
                m_query = query;
                m_parms = parms;
                RefreshResults();
            }

            private void RefreshResults()
            {
                string limited_query = m_query + " LIMIT " + m_offset.ToString() + ", " + QUERY_SIZE.ToString();
                m_results.AddRange(m_rdb.QueryWithResults(limited_query, m_parms));
            }

            public Dictionary<string, string> GetNext()
            {
                if (m_results.Count < 1)
                {
                    // Don't auto-fetch the next block when there are none, otherwise at the end we'll keep trying to fetch more.
                    return null;
                }

                return m_results[0];
            }

            public void Consume()
            {
                m_results.RemoveAt(0);
                if (m_results.Count < 1)
                {
                    // We transitioned from having results to an empty list. Update this once.
                    m_offset += QUERY_SIZE; // advance the query to the next block
                    RefreshResults();
                }
            }
        }   // class RDBQuery

        private Dictionary<string, string> ConsumeNext(List<RDBConnectionQuery> rdbQueries, uint queryFlags)
        {
            Dictionary<string, string> best = null;
            RDBConnectionQuery bestDB = null;
            float bestValue = 0f;
            string bestText = String.Empty;

            foreach (RDBConnectionQuery rdb in rdbQueries)
            {
                // we need to compare the first item in each list to see which one comes next
                Dictionary<string, string> first = rdb.GetNext();
                if (first == null) continue;

                bool ascending = ((queryFlags & (uint)DirFindFlags.SortAsc) != 0);
                bool better = false;
                if ((queryFlags & (uint)DirFindFlags.NameSort) != 0)
                {
                    if (first.ContainsKey("Name"))
                    {
                        string name = first["Name"];
                        better = (name.CompareTo(bestText) < 0);
                        if (!ascending)
                            better = !better;
                        if (better || (best == null))
                        {
                            best = first;
                            bestDB = rdb;
                            bestText = name;
                        }
                    }
                }
                else if ((queryFlags & (uint)DirFindFlags.PricesSort) != 0)
                {
                    if (first.ContainsKey("SalePrice"))
                    {
                        float price = (float)Convert.ToInt32(first["SalePrice"]);
                        better = (price < bestValue);
                        if (!ascending)
                            better = !better;
                        if (better || (best == null))
                        {
                            best = first;
                            bestDB = rdb;
                            bestValue = price;
                        }
                    }
                }
                else if ((queryFlags & (uint)DirFindFlags.PerMeterSort) != 0)
                {
                    if (first.ContainsKey("SalePrice") && first.ContainsKey("Area"))
                    {
                        float price = (float)Convert.ToInt32(first["SalePrice"]);
                        float area = (float)Convert.ToInt32(first["Area"]);
                        float ppm = -1.0f;
                        if (area > 0)
                            ppm = price / area;
                        better = (ppm < bestValue);
                        if (!ascending)
                            better = !better;
                        if (better || (best == null))
                        {
                            best = first;
                            bestDB = rdb;
                            bestValue = ppm;
                        }
                    }
                }
                else if ((queryFlags & (uint)DirFindFlags.AreaSort) != 0)
                {
                    if (first.ContainsKey("Area"))
                    {
                        float area = (float)Convert.ToInt32(first["Area"]);
                        better = (area < bestValue);
                        if (!ascending)
                            better = !better;
                        if (better || (best == null))
                        {
                            best = first;
                            bestDB = rdb;
                            bestValue = area;
                        }
                    }
                }
                else // any order will do
                {
                    // just grab the first one available
                    best = first;
                    bestDB = rdb;
                    break;
                }
            }

            if (best != null)
                bestDB.Consume();
            return best;
        }

        private List<Dictionary<string, string>> DoLandQueryAndCombine(IClientAPI remoteClient, ISimpleDB coreDb, 
                                                                    string query, Dictionary<string, object> parms,
                                                                    uint queryFlags, int queryStart, int queryEnd, 
                                                                    List<UUID> regionsToInclude)
        {
            string[] rdbHosts = this.CheckAndRetrieveRdbHostList(coreDb);
            if (rdbHosts.Length == 0)
            {
                // RDB not configured.  Fall back to core db.
                return coreDb.QueryWithResults(query, parms);
            }

            List<RDBConnectionQuery> rdbQueries = new List<RDBConnectionQuery>();
            int whichDB = 0;
            foreach (string host in rdbHosts)
            {
                // Initialize the RDB connection and initial results lists

                ConnectionFactory rdbFactory;
                if ((++whichDB == 1) || String.IsNullOrWhiteSpace(_rdbConnectionTemplateDebug))
                    rdbFactory = new ConnectionFactory("MySQL", String.Format(_rdbConnectionTemplate, host));
                else  // Special debugging support for multiple RDBs on one machine ("inworldz_rdb2", etc)
                    rdbFactory = new ConnectionFactory("MySQL", String.Format(_rdbConnectionTemplateDebug, host, whichDB));
                RDBConnectionQuery rdb = new RDBConnectionQuery(rdbFactory.GetConnection(), query, queryFlags, parms);
                rdbQueries.Add(rdb);
            }

            List<Dictionary<string, string>> finalList = new List<Dictionary<string, string>>();
            int current = 0;
            Dictionary<string, string> result = null;
            while ((result = ConsumeNext(rdbQueries, queryFlags)) != null)
            {
                UUID regionUUID = new UUID(result["RegionUUID"]);
                // When regionsToInclude.Count==0, it means do not filter by regions.
                if ((regionsToInclude.Count == 0) || regionsToInclude.Contains(regionUUID))
                {
                    // 0-based numbering
                    if ((current >= queryStart) && (current <= queryEnd))
                        finalList.Add(result);
                    current++;
                }
            }

            return finalList;
        }

        private string[] CheckAndRetrieveRdbHostList(ISimpleDB coreDb)
        {
            lock (_rdbHostCache)
            {
                if (DateTime.Now - _rdbCacheTime >= RDB_CACHE_TIMEOUT)
                {
                    //recache
                    CacheRdbHosts(coreDb);
                }

                return _rdbHostCache.ToArray();
            }
        }
        // NOTE: END OF RDB-related code copied in LandManagementModule (for now).

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
            get { return "SearchModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        /// New Client Event Handler
        private void OnNewClient(IClientAPI client)
        {
            // Subscribe to messages
            client.OnDirPlacesQuery += DirPlacesQuery;
            client.OnDirFindQuery += DirFindQuery;
            client.OnDirPopularQuery += DirPopularQuery;
            client.OnDirLandQuery += DirLandQuery;
            client.OnDirClassifiedQuery += DirClassifiedQuery;
            // Response after Directory Queries
            client.OnEventInfoRequest += EventInfoRequest;
            client.OnClassifiedInfoRequest += ClassifiedInfoRequest;

        }

        /// <summary>
        /// Return the current regions UP after possibly refreshing if stale.
        /// </summary>
        /// <returns>
        /// null if could not fetch the list (error, unreliable list)
        /// a list (possibly empty although it should always find this one) otherwise
        /// </returns>
        protected List<UUID> UpdateRegionsList(ISimpleDB coredb)
        {
            List<UUID> regions = new List<UUID>();

            lock (mRegionsOnline)
            {
                // The viewer fires 3 parallel requests together.
                // Protect against multiple initial parallel requests by using
                // the lock to block the other threads during the refresh. They need it anyway.

                // If we're up to date though, just return it.
                if ((DateTime.Now - mRegionsOnlineStamp).TotalSeconds < REGIONS_CACHE_TIME)
                    return mRegionsOnline;

                List<Dictionary<string, string>> results = coredb.QueryWithResults("SELECT uuid FROM regions LIMIT 999999999999");  // no limit
                foreach (var row in results)
                {
                    regions.Add(new UUID(row["UUID"]));
                }

                // Now stamp it inside the lock so that if we reenter, 
                // second caller will not make another request.
                mRegionsOnline = regions;
                mRegionsOnlineStamp = DateTime.Now;
            }
            return regions;
        }

        protected void DirPlacesQuery(IClientAPI remoteClient, UUID queryID, string queryText, int queryFlags, int category, string simName,
                int queryStart)
        {
//            m_log.DebugFormat("[LAND SEARCH]: In Places Search for queryText: {0} with queryFlag: {1} with category {2} for simName {3}",
//                queryText, queryFlags.ToString("X"), category.ToString(), simName);

            queryText = queryText.Trim();   // newer viewers sometimes append a space

            string query = String.Empty;

            //string newQueryText = "%" + queryText + "%";
            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?searchText", queryText);
            parms.Add("?searchFlags", queryFlags);
            if (category > 0)
                parms.Add("?searchCategory", category);
            //parms.Add("?searchSimName", simName);

            Single dwell = 0;

            int count = MAX_RESULTS + 1;    // +1 so that the viewer knows to enable the NEXT button (it seems)
            int i = 0;

            int queryEnd = queryStart + count - 1;  // 0-based

            query = "select * from land where LandFlags & "+ParcelFlags.ShowDirectory.ToString("d");
            if (category > 0)
                query += " AND Category=?searchCategory";
            query += " AND (Name REGEXP ?searchText OR Description REGEXP ?searchText) order by Name, Description";

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                List<UUID> regionsToInclude = UpdateRegionsList(db);
                List<Dictionary<string, string>> results = DoLandQueryAndCombine(remoteClient, db, query, parms, 0, queryStart, queryEnd, regionsToInclude);
                DirPlacesReplyData[] data = new DirPlacesReplyData[results.Count < 1 ? 1 : results.Count];
                foreach (Dictionary<string, string> row in results)
                {
                    bool auction = false;
                    data[i] = new DirPlacesReplyData();
                    data[i].parcelID = new UUID(row["uuid"]);
                    data[i].name = row["name"];
                    data[i].forSale = (Convert.ToInt16(row["SalePrice"]) > 0);
                    data[i].auction = auction;
                    data[i].dwell = dwell;
                    data[i].Status = STATUS_SEARCH_PLACES_NONE; // 0, success
                    i++;
                }

                if (results.Count == 0)
                {
                    data[0] = new DirPlacesReplyData();
                    data[0].parcelID = UUID.Zero;
                    data[0].name = String.Empty;
                    data[0].forSale = false;
                    data[0].auction = false;
                    data[0].dwell = 0;
                    data[0].Status = STATUS_SEARCH_PLACES_FOUNDNONE;    // empty results
                }

                remoteClient.SendDirPlacesReply(queryID, data);
            }
        }

        public void DirPopularQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags)
        {
            // This is not even in place atm, and really needs just a complete 
            // rewrite as Popular places doesn't even take Traffic into consideration.

            remoteClient.SendAgentAlertMessage(
                        "We're sorry, Popular Places is not in place yet!", false);
            /*
            Hashtable ReqHash = new Hashtable();
            ReqHash["flags"] = queryFlags.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "dir_popular_query");

            if (!Convert.ToBoolean(result["success"]))
            {
                remoteClient.SendAgentAlertMessage(
                        result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            int count = dataArray.Count;
            if (count > 100)
                count = MAX_RESULTS + 1;    // +1 so that the viewer knows to enable the NEXT button (it seems)

            DirPopularReplyData[] data = new DirPopularReplyData[count];

            int i = 0;

            foreach (Object o in dataArray)
            {
                Hashtable d = (Hashtable)o;

                data[i] = new DirPopularReplyData();
                data[i].parcelID = new UUID(d["parcel_id"].ToString());
                data[i].name = d["name"].ToString();
                data[i].dwell = Convert.ToSingle(d["dwell"]);
                i++;
                if (i >= count)
                    break;
            }

            remoteClient.SendDirPopularReply(queryID, data);
             */
        }

        public void DirLandQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags, uint searchType, int price, int area,
                int queryStart)
        {
            //m_log.DebugFormat("[LAND SEARCH]: In Land Search, queryFlag = " + queryFlags.ToString("X"));
            string query = String.Empty;
            int count = MAX_RESULTS + 1;    // +1 so that the viewer knows to enable the NEXT button (it seems)
            int queryEnd = queryStart + count - 1;  // 0-based
            int i = 0;
            string sqlTerms = String.Empty;

            if ((queryFlags & ((uint)DirFindFlags.NameSort|(uint)DirFindFlags.AreaSort|(uint)DirFindFlags.PricesSort|(uint)DirFindFlags.PerMeterSort)) == 0)
            {
                // No sort options specified.  Substitute price per meter sort for Land Sales.
                queryFlags |= (uint)DirFindFlags.PerMeterSort;
                queryFlags |= (uint)DirFindFlags.SortAsc;
            }

            bool checkPriceFlag = Convert.ToBoolean(queryFlags & (uint)DirFindFlags.LimitByPrice);
            bool checkAreaFlag = Convert.ToBoolean(queryFlags & (uint)DirFindFlags.LimitByArea);
            bool checkPGFlag = Convert.ToBoolean(queryFlags & (uint)DirFindFlags.IncludePG);
            bool checkMatureFlag = Convert.ToBoolean(queryFlags & (uint)DirFindFlags.IncludeMature);
            bool checkAdultFlag = Convert.ToBoolean(queryFlags & (uint)DirFindFlags.IncludeAdult);

            //sqlTerms = "select parcelUUID, parcelname, area, saleprice from parcelsales";
            sqlTerms = "select land.UUID, land.RegionUUID, land.Name, land.Area, land.SalePrice, regionsettings.maturity from land LEFT JOIN regionsettings ON land.RegionUUID=regionsettings.regionUUID";

            // Limit "Land Sales" returns to parcels for sale.
            sqlTerms += " where (LandFlags & " + ((int)ParcelFlags.ForSale).ToString() + ")";
            // Limit "Land Sales" returns to parcels visible in search.
            sqlTerms += " and (LandFlags & " + ((int)ParcelFlags.ShowDirectory).ToString() + ")";

            if (!(checkPGFlag || checkMatureFlag || checkAdultFlag))
            {
                // nothing to search for
                remoteClient.SendAgentAlertMessage("You must specify a search with at least one maturity level checked.", false);
                remoteClient.SendDirLandReply(queryID, new DirLandReplyData[1]);
                return;
            }

            if (searchType == 2)
            {
                remoteClient.SendAgentAlertMessage("Auctions not available.", false);
                remoteClient.SendDirLandReply(queryID, new DirLandReplyData[1]);
                return;
            } 
            /*else if (searchType == 8)
            {
                sqlTerms += " AND parentestate='1' ";
            }
            else if (searchType == 16)
            {
                sqlTerms += " AND parentestate>'1' ";
            }*/

            int maturities = 0;
            sqlTerms += " and (";
            if (checkPGFlag)
            {
                sqlTerms += "regionsettings.maturity='0'";
                maturities++;
            }
            if (checkMatureFlag)
            {
                if (maturities > 0) sqlTerms += " or ";
                sqlTerms += "regionsettings.maturity='1'";
                maturities++;
            }
            if (checkAdultFlag)
            {
                if (maturities > 0) sqlTerms += " or ";
                sqlTerms += "regionsettings.maturity='2'";
                maturities++;
            }
            sqlTerms += ")";

            if (checkPriceFlag && (price > 0))
            {
                sqlTerms += " and (land.SalePrice<=" + Convert.ToString(price) + " AND land.SalePrice >= 1) ";
            }

            if (checkAreaFlag && (area > 0))
            {
                sqlTerms += " and land.Area>=" + Convert.ToString(area);
            }

            string order = ((queryFlags & (uint)DirFindFlags.SortAsc) != 0) ? "ASC" : "DESC";
            string norder = ((queryFlags & (uint)DirFindFlags.SortAsc) == 0) ? "ASC" : "DESC";
            if ((queryFlags & (uint)DirFindFlags.NameSort) != 0)
            {
                sqlTerms += " order by land.Name " + order;
            }
            else if ((queryFlags & (uint)DirFindFlags.PerMeterSort) != 0)
            {
                sqlTerms += " and land.Area > 0 order by land.SalePrice / land.Area " + order + ", land.SalePrice " + order + ", land.Area " + norder + ", land.Name ASC";
            }
            else if ((queryFlags & (uint)DirFindFlags.PricesSort) != 0)
            {
                sqlTerms += " order by land.SalePrice " + order + ", land.Area " + norder + ", land.Name ASC";
            }
            else if ((queryFlags & (uint)DirFindFlags.AreaSort) != 0)
            {
                sqlTerms += " order by land.Area " + order + ", land.SalePrice ASC, land.Name ASC";
            }

            Dictionary<string, object> parms = new Dictionary<string, object>();
            query = sqlTerms;   // nothing extra to add anymore
//            m_log.Debug("Query is: " + query);
            List<Dictionary<string, string>> results;
            using (ISimpleDB db = _connFactory.GetConnection())
            {
                List<UUID> regionsToInclude = UpdateRegionsList(db);
                results = DoLandQueryAndCombine(remoteClient, db, query, parms, queryFlags, queryStart, queryEnd, regionsToInclude);
            }
            if (results.Count < count)
                count = results.Count;  // no Next button
            if (count < 1)
                count = 1;  // a single empty DirLandReplyData will just cause the viewer to report no results found.
            DirLandReplyData[] data = new DirLandReplyData[count];
            foreach (Dictionary<string, string> row in results)
            {
                bool forSale = true;
                bool auction = false;
                data[i] = new DirLandReplyData();
                data[i].parcelID = new UUID(row["UUID"]);
                data[i].name = row["Name"].ToString();
                data[i].auction = auction;
                data[i].forSale = forSale;
                data[i].salePrice = Convert.ToInt32(row["SalePrice"]);
                data[i].actualArea = Convert.ToInt32(row["Area"]);
                i++;
                if (i >= count)
                    break;
            }

            remoteClient.SendDirLandReply(queryID, data);
        }

        public void DirFindQuery(IClientAPI remoteClient, UUID queryID,
                string queryText, uint queryFlags, int queryStart)
        {
            if ((queryFlags & 1) != 0)
            {
                DirPeopleQuery(remoteClient, queryID, queryText, queryFlags,
                        queryStart);
                return;
            }
            else if ((queryFlags & 32) != 0)
            {
                DirEventsQuery(remoteClient, queryID, queryText, queryFlags,
                        queryStart);
                return;
            }
        }

        public void DirPeopleQuery(IClientAPI remoteClient, UUID queryID,
                string queryText, uint queryFlags, int queryStart)
        {
            queryText = queryText.Trim();   // newer viewers sometimes append a space

            List<AvatarPickerAvatar> AvatarResponses =
                    new List<AvatarPickerAvatar>();
            AvatarResponses = m_Scenes[0].SceneGridService.
                    GenerateAgentPickerRequestResponse(queryID, queryText);

            DirPeopleReplyData[] data =
                    new DirPeopleReplyData[AvatarResponses.Count];

            int i = 0;
            foreach (AvatarPickerAvatar item in AvatarResponses)
            {
                data[i] = new DirPeopleReplyData();

                data[i].agentID = item.AvatarID;
                data[i].firstName = item.firstName;
                data[i].lastName = item.lastName;
                data[i].group = String.Empty;
                data[i].online = false;
                data[i].reputation = 0;
                i++;
            }

            remoteClient.SendDirPeopleReply(queryID, data);
        }

        public void DirEventsQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart)
        {
            // Flags are going to be 0 or 32 for Mature
            // We also know text comes in 3 segments X|Y|Text where X is the day difference from 
            // the current day, Y is the category to search, Text is the user input for search string
            // so let's 'split up the queryText to get our values we need first off
            string eventTime = String.Empty;
            string eventCategory = String.Empty;
            string userText = String.Empty;

            queryText = queryText.Trim();   // newer viewers sometimes append a space

            string[] querySplode = queryText.Split(new Char[] { '|' });
            if (querySplode.Length > 0)
                eventTime = querySplode[0];
            if (querySplode.Length > 1)
                eventCategory = querySplode[1];
            if (querySplode.Length > 0)
            {
                userText = querySplode[2];
                userText = userText.Trim();   // newer viewers sometimes append a space
            }

            // Ok we have values, now we need to do something with all this lovely information
            string query = String.Empty;
            string searchStart = Convert.ToString(queryStart);
            int count = 100;
            DirEventsReplyData[] data = new DirEventsReplyData[count];
            string searchEnd = Convert.ToString(queryStart + count);
            int i = 0;
            int unixEventUpcomingEndDateToCheck = 0;

            int eventTimeAmt = 0;
            int unixEventDateToCheckMidnight = 0;
            int unixEventEndDateToCheckMidnight = 0;

            string sqlAddTerms = String.Empty;

            DateTime saveNow = DateTime.Now;
            int startDateCheck;

            // Quick catch to see if the eventTime is set to "u" for In Progress & Upcoming Radio button
            if (eventTime == "u")
            {
                DateTime eventUpcomingEndDateToCheck = saveNow.AddDays(+7);
                DateTime eventUpcomingEndDateToCheckMidnight = new DateTime(eventUpcomingEndDateToCheck.Year, eventUpcomingEndDateToCheck.Month, eventUpcomingEndDateToCheck.Day);
                unixEventUpcomingEndDateToCheck = Util.ToUnixTime(eventUpcomingEndDateToCheckMidnight);

                // for "in progress" events, show everything that has started within the last three hours (arbitrary)
                startDateCheck = Util.ToUnixTime(saveNow.AddHours(-3.0));
                sqlAddTerms = " where (dateUTC>=?startDateCheck AND dateUTC<=?unixEventUpcomingEndDateToCheck)";
            }
            else
            {
                // we need the current date, in order to subtract/add the days to view, or see the week upcoming.
                // this will probably be some really ugly code :)
                startDateCheck = Util.ToUnixTime(saveNow);
                eventTimeAmt = Convert.ToInt16(eventTime);
                DateTime eventDateToCheck = saveNow.AddDays(Convert.ToInt16(eventTime));
                DateTime eventEndDateToCheck = new DateTime();
                if (eventTime == "0")
                {
                    eventEndDateToCheck = saveNow.AddDays(+2);
                }
                else
                {
                    eventEndDateToCheck = saveNow.AddDays(eventTimeAmt  + 1);
                }
                // now truncate the information so we get the midnight value (and yes David, I'm sure there's an 
                // easier way to do this, but this will work for now :)  )
                DateTime eventDateToCheckMidnight = new DateTime(eventDateToCheck.Year, eventDateToCheck.Month, eventDateToCheck.Day);
                DateTime eventEndDateToCheckMidnight = new DateTime(eventEndDateToCheck.Year, eventEndDateToCheck.Month, eventEndDateToCheck.Day);


                // this is the start unix timestamp to look for, we still need the end which is the next day, plus
                // we need the week end unix timestamp for the In Progress & upcoming radio button
                unixEventDateToCheckMidnight = Util.ToUnixTime(eventDateToCheckMidnight);
                unixEventEndDateToCheckMidnight = Util.ToUnixTime(eventEndDateToCheckMidnight);

                sqlAddTerms = " where (dateUTC>=?unixEventDateToCheck AND dateUTC<=?unixEventEndDateToCheckMidnight)";
            }

            if ((queryFlags & ((uint)DirFindFlags.IncludeAdult|(uint)DirFindFlags.IncludeAdult|(uint)DirFindFlags.IncludeAdult)) == 0)
            {
                // don't just give them an empty list...
                remoteClient.SendAlertMessage("You must included at least one maturity rating.");
                remoteClient.SendDirEventsReply(queryID, data);
                return;
            }

            // Event DB storage does not currently support adult events, so if someone asks for adult, search mature for now.
            if ((queryFlags & (uint)DirFindFlags.IncludeAdult) != 0)
                queryFlags |= (uint)DirFindFlags.IncludeMature;

            if ((queryFlags & (uint)DirFindFlags.IncludeMature) != 0)
            {
                // they included mature and possibly others
                if ((queryFlags & (uint)DirFindFlags.IncludePG) == 0)
                    sqlAddTerms += " AND mature='true'";   // exclude PG
            }
            if ((queryFlags & (uint)DirFindFlags.IncludePG) != 0)
            {
                // they included PG and possibly others
                if ((queryFlags & (uint)DirFindFlags.IncludeMature) == 0)
                    sqlAddTerms += " AND mature='false'";  // exclude mature
            }

            if (eventCategory != "0")
            {
                sqlAddTerms += " AND category=?category";
            }

            if(!String.IsNullOrEmpty(userText))
            {
                sqlAddTerms += " AND (description LIKE ?userText OR name LIKE ?userText)";
            }

            // Events results should come back sorted by date
            sqlAddTerms += " order by dateUTC ASC";

             query = "select owneruuid, name, eventid, dateUTC, eventflags from events" + sqlAddTerms + " limit " + searchStart + ", " + searchEnd + String.Empty;

             Dictionary<string, object> parms = new Dictionary<string, object>();
             parms.Add("?startDateCheck", Convert.ToString(startDateCheck));
             parms.Add("?unixEventUpcomingEndDateToCheck", Convert.ToString(unixEventUpcomingEndDateToCheck));
             parms.Add("?unixEventDateToCheck", Convert.ToString(unixEventDateToCheckMidnight));
             parms.Add("?unixEventEndDateToCheckMidnight", Convert.ToString(unixEventEndDateToCheckMidnight));
             parms.Add("?category", eventCategory);
             parms.Add("?userText", "%" + Convert.ToString(userText) + "%");

             using (ISimpleDB db = _connFactory.GetConnection())
             {
                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> row in results)
                {
                    data[i] = new DirEventsReplyData();
                    data[i].ownerID = new UUID(row["owneruuid"].ToString());
                    data[i].name = row["name"].ToString();
                    data[i].eventID = Convert.ToUInt32(row["eventid"]);

                    // need to convert the unix timestamp we get into legible date for viewer
                    DateTime localViewerEventTime = Util.UnixToLocalDateTime(Convert.ToInt32(row["dateUTC"]));
                    string newSendingDate = String.Format("{0:MM/dd hh:mm tt}", localViewerEventTime);

                    data[i].date = newSendingDate;
                    data[i].unixTime = Convert.ToUInt32(row["dateUTC"]);
                    data[i].eventFlags = Convert.ToUInt32(row["eventflags"]);
                     i++;
                    if (i >= count)
                        break;
                }
             }
            remoteClient.SendDirEventsReply(queryID, data);
        }

        public void DirClassifiedQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, uint category,
                int queryStart)
        {
            // This is pretty straightforward here, get the input, set up the query, run it through, send back to viewer.
            string query = String.Empty;
            string sqlAddTerms = String.Empty;
            string userText = queryText.Trim(); // newer viewers sometimes append a space

            string searchStart = Convert.ToString(queryStart);
            int count = MAX_RESULTS + 1;    // +1 so that the viewer knows to enable the NEXT button (it seems)
            string searchEnd = Convert.ToString(queryStart + count);
            int i = 0;

            // There is a slight issue with the parcel data not coming from land first so the
            //  parcel information is never displayed correctly within in the classified ad.

            //stop blank queries here before they explode mysql
            if (String.IsNullOrEmpty(userText))
            {
                remoteClient.SendDirClassifiedReply(queryID, new DirClassifiedReplyData[0]);
                return;
            }

            if (queryFlags == 0)
            {
                sqlAddTerms = " AND (classifiedflags='2' OR classifiedflags='34') ";
            }

            if (category != 0)
            {
                sqlAddTerms = " AND category=?category ";
            }

             Dictionary<string, object> parms = new Dictionary<string, object>();
             parms.Add("?matureFlag", queryFlags);
             parms.Add("?category", category);
             parms.Add("?userText", userText);

            // Ok a test cause the query pulls fine direct in MySQL, but not from here, so WTF?!
             //query = "select classifieduuid, name, classifiedflags, creationdate, expirationdate, priceforlisting from classifieds " +
             //        "where name LIKE '" + userText + "' OR description LIKE '" + userText + "' " + sqlAddTerms;

            query = "select classifieduuid, name, classifiedflags, creationdate, expirationdate, priceforlisting from classifieds " +
                    "where (description REGEXP ?userText OR name REGEXP ?userText) " +sqlAddTerms + " order by priceforlisting DESC limit " + searchStart + ", " + searchEnd + String.Empty;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                count = results.Count;
                DirClassifiedReplyData[] data = new DirClassifiedReplyData[count];
                foreach (Dictionary<string, string> row in results)
                {
                    data[i] = new DirClassifiedReplyData();
                    data[i].classifiedID = new UUID(row["classifieduuid"].ToString());
                    data[i].name = row["name"].ToString();
                    data[i].classifiedFlags = Convert.ToByte(row["classifiedflags"]);
                    data[i].creationDate = Convert.ToUInt32(row["creationdate"]);
                    data[i].expirationDate = Convert.ToUInt32(row["expirationdate"]);
                    data[i].price = Convert.ToInt32(row["priceforlisting"]);
                    i++;
                }
                remoteClient.SendDirClassifiedReply(queryID, data);
            }
        }

        public void EventInfoRequest(IClientAPI remoteClient, uint queryEventID)
        {

            EventData data = new EventData();
            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?eventID", queryEventID);

            string query = "select * from events where eventid=?eventID";

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> row in results)
                {
                    data.eventID = Convert.ToUInt32(row["eventid"]);
                    data.creator = row["creatoruuid"].ToString();
                    data.name = row["name"].ToString();
                    // need to convert this from the integer it is to the 
                    // real string value so it shows correctly
                    if (row["category"] == "18")
                        data.category = "Discussion";
                    if (row["category"] == "19")
                        data.category = "Sports";
                    if (row["category"] == "20")
                        data.category = "Live Music";
                    if (row["category"] == "22")
                        data.category = "Commercial";
                    if (row["category"] == "23")
                        data.category = "Nightlife/Entertainment";
                    if (row["category"] == "24")
                        data.category = "Games/Contests";
                    if (row["category"] == "25")
                        data.category = "Pageants";
                    if (row["category"] == "26")
                        data.category = "Education";
                    if (row["category"] == "27")
                        data.category = "Arts and Culture";
                    if (row["category"] == "28")
                        data.category = "Charity/Support Groups";
                    if (row["category"] == "29")
                        data.category = "Miscellaneous";

                    data.description = row["description"].ToString();

                    // do something here with date to format it correctly for what
                    // the viewer needs!
                    //data.date = row["date"].ToString();

                    // need to convert the unix timestamp we get into legible date for viewer
                    DateTime localViewerEventTime = Util.UnixToLocalDateTime(Convert.ToInt32(row["dateUTC"])); 
                    string newSendingDate = String.Format("{0:yyyy-MM-dd HH:MM:ss}", localViewerEventTime);

                    data.date = newSendingDate;
                    
                    data.dateUTC = Convert.ToUInt32(row["dateUTC"]);
                    data.duration = Convert.ToUInt32(row["duration"]);
                    data.cover = Convert.ToUInt32(row["covercharge"]);
                    data.amount = Convert.ToUInt32(row["coveramount"]);
                    data.simName = row["simname"].ToString();
                    Vector3.TryParse(row["globalPos"].ToString(), out data.globalPos);
                    data.eventFlags = Convert.ToUInt32(row["eventflags"]);

                }
            }
            
            remoteClient.SendEventInfoReply(data);
            
        }

        public void ClassifiedInfoRequest(UUID queryClassifiedID, IClientAPI remoteClient)
        {
            // okies this is pretty simple straightforward stuff as well... pull the info from the 
            // db based on the Classified ID we got back from viewer from the original query above
            // and send it back.

            //ClassifiedData data = new ClassifiedData();

            string query =  "select * from classifieds where classifieduuid=?classifiedID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?classifiedID", queryClassifiedID);

            Vector3 globalPos = new Vector3();

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> row in results)
                {
                    Vector3.TryParse(row["posglobal"].ToString(), out globalPos);

            //remoteClient.SendClassifiedInfoReply(data);
                    
                    remoteClient.SendClassifiedInfoReply(
                            new UUID(row["classifieduuid"].ToString()),
                            new UUID(row["creatoruuid"].ToString()),
                            Convert.ToUInt32(row["creationdate"]),
                            Convert.ToUInt32(row["expirationdate"]),
                            Convert.ToUInt32(row["category"]),
                            row["name"].ToString(),
                            row["description"].ToString(),
                            new UUID(row["parceluuid"].ToString()),
                            Convert.ToUInt32(row["parentestate"]),
                            new UUID(row["snapshotuuid"].ToString()),
                            row["simname"].ToString(),
                            globalPos,
                            row["parcelname"].ToString(),
                            Convert.ToByte(row["classifiedflags"]),
                            Convert.ToInt32(row["priceforlisting"]));
                     
                }
            }

        }
             
    }
}
