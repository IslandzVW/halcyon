/*
 * Copyright(C) 2009 Inworldz LLC
 * Initial Revision:  2009-12-14 David C. Daeschler
 */

using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;
using System.Threading;

namespace OpenSim.Data.SimpleDB
{
    ///
    /// <summary>Reprensents a MySQL datasource</summary>
    /// 
    public class MySQLSimpleDB : ISimpleDB
    {
        private string _connectionString;
        private MySqlConnection _conn;
        private int _commandTimeout = 30;
        private Random _rand = new Random();

        /// <summary>
        /// The minimum amount of time to pause before retrying a transaction the
        /// first time
        /// </summary>
        private const int MIN_RETRY_WAIT_INTERVAL = 3;

        /// <summary>
        /// The maximum amount of time to pause before retrying a transaction the
        /// first time
        /// </summary>
        private const int MAX_RETRY_WAIT_INTERVAL = 6;

        /// <summary>
        /// Maximum number of times to retry the transaction before giving up
        /// </summary>
        private const int MAX_RETRIES = 3;


        enum RetryableErrors
        {
            ER_LOCK_DEADLOCK = 1213,
            ER_LOCK_WAIT_TIMEOUT = 1205
        }

        public int QueryTimeout
        {
            get
            {
                return _commandTimeout;
            }

            set
            {
                _commandTimeout = value;
            }
        }

        internal MySqlConnection ConnectionImpl
        {
            get
            {
                return _conn;
            }
        }

        public MySQLSimpleDB(string connectionString)
        {
            _connectionString = connectionString;
            this.OpenConnection();
        }

        public void CloseConnection()
        {
            _conn.Close();
        }

        private void OpenConnection()
        {
            if (_conn == null)
            {
                _conn = new MySqlConnection(_connectionString);
            }
            
            _conn.Open();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SET NAMES utf8;";
                cmd.ExecuteNonQuery();
            }
        }

        private void CheckConnection()
        {
            //ping will return false if the connection has failed
            if (! _conn.Ping() || _conn.State != ConnectionState.Open)
            {
                _conn.Close();
                this.OpenConnection();
            }
        }

        private MySqlCommand CreateCommand()
        {
            MySqlCommand cmd = _conn.CreateCommand();
            cmd.CommandTimeout = _commandTimeout;

            return cmd;
        }

        /// <summary>
        /// Converts a data reader to a result set
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        private List<Dictionary<string, string>> ReaderToResultSet(IDataReader r)
        {
            List<Dictionary<string, string>> resultSet = new List<Dictionary<string, string>>();

            using (r)
            {
                while (r.Read())
                {
                    Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        if (!r.IsDBNull(i)) row[r.GetName(i)] = r.GetString(i);
                        else row[r.GetName(i)] = null;
                    }

                    resultSet.Add(row);
                }

                r.Close();
            }
            return resultSet;
        }

        public List<Dictionary<string, string>> QueryWithResults(string queryText)
        {
            return this.ReaderToResultSet(this.QueryAndUseReader(queryText));
        }

        public List<Dictionary<string, string>> QueryWithResults(string queryText, Dictionary<string, object> parms)
        {
            return this.ReaderToResultSet(this.QueryAndUseReader(queryText, parms));
        }

        public IDataReader QueryAndUseReader(string queryText)
        {
            this.CheckConnection();

            MySqlCommand cmd = this.CreateCommand();

            using (cmd)
            {
                cmd.CommandText = queryText;
                return this.ExecuteReaderRetryable(cmd);
            }
        }

        public IDataReader QueryAndUseReader(string queryText, Dictionary<string, object> parms)
        {
            this.CheckConnection();

            MySqlCommand cmd = this.CreateCommand();

            using (cmd)
            {
                cmd.CommandText = queryText;

                foreach (KeyValuePair<string, object> parm in parms)
                {
                    cmd.Parameters.AddWithValue(parm.Key, parm.Value);
                }

                return this.ExecuteReaderRetryable(cmd);
            }
        }

        public void QueryNoResults(string queryText)
        {
            this.CheckConnection();

            MySqlCommand cmd = this.CreateCommand();

            using (cmd)
            {
                cmd.CommandText = queryText;

                this.ExecuteNonQueryRetryable(cmd);
            }
        }

        public void QueryNoResults(string queryText, Dictionary<string, object> parms)
        {
            this.CheckConnection();

            MySqlCommand cmd = this.CreateCommand();

            using (cmd)
            {
                cmd.CommandText = queryText;

                foreach (KeyValuePair<string, object> parm in parms)
                {
                    cmd.Parameters.AddWithValue(parm.Key, parm.Value);
                }

                this.ExecuteNonQueryRetryable(cmd);
            }
        }

        private void ExecuteNonQueryRetryable(MySqlCommand cmd)
        {
            int retryCount = 0;
            int lastRetryWait = 0;

            while (true)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                    return;
                }
                catch (MySqlException e)
                {
                    bool containsDeadlock = false;
                    bool containsTimeout = false;

                    if (e.InnerException != null)
                    {
                        containsDeadlock = e.InnerException.Message.ToLower().Contains("deadlock");
                        containsTimeout = e.InnerException.Message.ToLower().Contains("timeout");
                    }

                    if (retryCount == MAX_RETRIES ||
                        (e.Number != (int)RetryableErrors.ER_LOCK_DEADLOCK &&
                         e.Number != (int)RetryableErrors.ER_LOCK_WAIT_TIMEOUT &&
                         !containsDeadlock &&
                         !containsTimeout))
                    {
                        throw;
                    }

                    e = null;
                    retryCount++;
                    lastRetryWait = this.RetryWait(lastRetryWait);
                }
            }
        }

        private int RetryWait(int lastRetryWait)
        {
            int waitTime = _rand.Next(MIN_RETRY_WAIT_INTERVAL, MAX_RETRY_WAIT_INTERVAL) + lastRetryWait;
            Thread.Sleep(waitTime * 1000);

            return waitTime;
        }

        /// <summary>
        /// Todo:  this method is a copy and paste of ExecuteNonQueryRetryable, however 
        /// every time I encapsulated it, it made the code a lot more difficult to read.
        /// refactor if possible
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private IDataReader ExecuteReaderRetryable(MySqlCommand cmd)
        {
            int retryCount = 0;
            int lastRetryWait = 0;

            while (true)
            {
                try
                {
                    return cmd.ExecuteReader();
                }
                catch (MySqlException e)
                {
                    bool containsDeadlock = false;
                    bool containsTimeout = false;

                    if (e.InnerException != null)
                    {
                        containsDeadlock = e.InnerException.Message.ToLower().Contains("deadlock");
                        containsTimeout = e.InnerException.Message.ToLower().Contains("timeout");
                    }

                    if (retryCount == MAX_RETRIES ||
                        (e.Number != (int) RetryableErrors.ER_LOCK_DEADLOCK &&
                         e.Number != (int) RetryableErrors.ER_LOCK_WAIT_TIMEOUT &&
                         !containsDeadlock && 
                         !containsTimeout))
                    {
                        throw;
                    }

                    e = null;
                    retryCount++;
                    lastRetryWait = this.RetryWait(lastRetryWait);
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _conn.Close();
            _conn.Dispose();
        }

        #endregion

        public ITransaction BeginTransaction()
        {
            return new MySQLTransaction(this);
        }
    }
}
