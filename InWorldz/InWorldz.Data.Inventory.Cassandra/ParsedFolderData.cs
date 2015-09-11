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
using Apache.Cassandra;
using Aquiles.Helpers.Encoders;

namespace InWorldz.Data.Inventory.Cassandra
{
    /// <summary>
    /// Walks through a full column set retrieved from a folder CF
    /// and sorts the types of data stored
    /// </summary>
    internal class ParsedFolderData
    {
        private ColumnOrSuperColumn _propertiesSc;
        private ColumnOrSuperColumn _subfoldersSc;

        private List<ColumnOrSuperColumn> _items;

        public ColumnOrSuperColumn Properties
        {
            get
            {
                return _propertiesSc;
            }
        }
        
        public ColumnOrSuperColumn SubFolders
        {
            get
            {
                return _subfoldersSc;
            }
        }

        public List<ColumnOrSuperColumn> Items
        {
            get
            {
                return _items;
            }
        }


        public ParsedFolderData(List<ColumnOrSuperColumn> allColumns)
        {
            byte[] propertiesKey = ByteEncoderHelper.UTF8Encoder.ToByteArray("properties");
            byte[] subFoldersKey = ByteEncoderHelper.UTF8Encoder.ToByteArray("sub_folders");

            bool propsFound = false;
            bool subFoldersFound = false;

            _items = new List<ColumnOrSuperColumn>();
            foreach (ColumnOrSuperColumn col in allColumns)
            {
                if (!propsFound && col.Super_column.Name.SequenceEqual(propertiesKey))
                {
                    //properties
                    propsFound = true;
                    _propertiesSc = col;
                }
                else if (!subFoldersFound && col.Super_column.Name.SequenceEqual(subFoldersKey))
                {
                    subFoldersFound = true;
                    _subfoldersSc = col;
                }
                else
                {
                    _items.Add(col);
                }
            }
        }
    }
}
