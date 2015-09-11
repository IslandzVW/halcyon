/*
 * Copyright(C) 2009 Inworldz LLC
 * Initial Revision:  2009-12-14 David C. Daeschler
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace OpenSim.Data.SimpleDB
{
    /// <summary>
    /// Returns a simple interface to any DB
    /// </summary>
    public interface ISimpleDB : IDisposable
    {
        /// <summary>
        /// Specifies the time alloted before a query is considered timed out
        /// </summary>
        int QueryTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Closes this connection
        /// </summary>
        void CloseConnection();

        /// <summary>
        /// Executes a query and the returns the results in a data reader
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        IDataReader QueryAndUseReader(string queryText);

        /// <summary>
        /// Executes a query and then returns the results in a data reader
        /// </summary>
        /// <param name="queryText">The query text.</param>
        /// <param name="parms">Query parameters to be replaced in the query text</param>
        /// <returns></returns>
        IDataReader QueryAndUseReader(string queryText, Dictionary<string, object> parms);

        /// <summary>
        /// Runs the given query and returns a result set
        /// </summary>
        /// <param name="queryText">The query text.</param>
        /// <returns>
        /// A result set
        /// </returns>
        List<Dictionary<string, string>> QueryWithResults(string queryText);

        /// <summary>
        /// Runs the given query and returns a result set
        /// </summary>
        /// <param name="queryText">The query text.</param>
        /// <param name="parms">Query parameters to be replaced in the query text</param>
        /// <returns>
        /// A result set
        /// </returns>
        List<Dictionary<string, string>> QueryWithResults(string queryText, Dictionary<string, object> parms);

        /// <summary>
        /// Runs the given query
        /// </summary>
        /// <param name="queryText">The query text.</param>
        void QueryNoResults(string queryText);

        /// <summary>
        /// Runs the given query with parameters
        /// </summary>
        /// <param name="queryText">The query text.</param>
        /// <param name="queryText">Query parameters to be replaced in the query text</param>
        void QueryNoResults(string queryText, Dictionary<string, object> parms);

        /// <summary>
        /// Begins a transaction on this connection
        /// </summary>
        /// <returns>The new transaction</returns>
        ITransaction BeginTransaction();
    }
}
