/*
 * Copyright(C) 2009 Inworldz LLC
 * Initial Revision:  2009-12-14 David C. Daeschler
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Data.SimpleDB
{
    public class ConnectionFactory
    {
        private string _dbType;
        private string _connectionString;

        public ConnectionFactory(string dbType, string connectionString)
        {
            _dbType = dbType;
            _connectionString = connectionString;
        }

        public ISimpleDB GetConnection()
        {
            switch (_dbType)
            {
                case "MySQL":
                    return new MySQLSimpleDB(_connectionString);

                default:
                    throw new NotSupportedException("Database type not supported");
            }
        }
    }
}
