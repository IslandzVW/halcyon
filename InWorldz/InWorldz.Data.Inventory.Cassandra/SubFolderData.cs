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
using OpenSim.Framework;
using Aquiles.Helpers.Encoders;
using System.Net;

namespace InWorldz.Data.Inventory.Cassandra
{
    internal struct SubFolderData
    {
        private const byte VERSION = 1;
        private const int OVERHEAD = 2;

        public string Name;
        public short Type;

        public static SubFolderData FromByteArray(byte[] data)
        {
            if (data[0] == VERSION) //most current storage version
            {
                byte nameLen = data[1];

                string name = Util.UTF8.GetString(data, 2, nameLen);
                short type = BitConverter.ToInt16(data, 2 + nameLen);
                type = IPAddress.NetworkToHostOrder(type);

                return new SubFolderData { Name = name, Type = type };
            }

            return new SubFolderData();
        }

        public static byte[] Encode(string name, short type)
        {
            byte[] nameBytes = Util.UTF8.GetBytes(Util.TruncateString(name, 255));
            byte[] typeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(type));

            byte[] ret = new byte[nameBytes.Length + typeBytes.Length + OVERHEAD];
            
            //[ver:1][folder_name_len_prefix:1][folderName:var][type:int]
            ret[0] = VERSION;
            ret[1] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, ret, 2, nameBytes.Length);
            Array.Copy(typeBytes, 0, ret, 2 + nameBytes.Length, typeBytes.Length);

            return ret;
        }
    }
}
