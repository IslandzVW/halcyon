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
using System.IO;
using System.Xml;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// LLSDSerializationDictionary is a light(er) weight serializer from Aurora-sim
    /// </summary>
    public class LLSDSerializationDictionary
    {
        private MemoryStream sw = new MemoryStream();
        private XmlTextWriter writer;

        public LLSDSerializationDictionary()
        {
            writer = new XmlTextWriter(sw, Encoding.UTF8);
            writer.WriteStartElement(String.Empty, "llsd", String.Empty);
        }

        public void WriteStartMap(string name)
        {
            writer.WriteStartElement(String.Empty, "map", String.Empty);
        }

        public void WriteEndMap()
        {
            writer.WriteEndElement();
        }

        public void WriteStartArray(string name)
        {
            writer.WriteStartElement(String.Empty, "array", String.Empty);
        }

        public void WriteEndArray()
        {
            writer.WriteEndElement();
        }

        public void WriteKey(string key)
        {
            writer.WriteStartElement(String.Empty, "key", String.Empty);
            writer.WriteString(key);
            writer.WriteEndElement();
        }

        public void WriteElement(object value)
        {
            Type t = value.GetType();
            if (t == typeof(bool))
            {
                writer.WriteStartElement(String.Empty, "boolean", String.Empty);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
            else if (t == typeof(int))
            {
                writer.WriteStartElement(String.Empty, "integer", String.Empty);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
            else if (t == typeof(uint))
            {
                writer.WriteStartElement(String.Empty, "integer", String.Empty);
                writer.WriteValue(value.ToString());
                writer.WriteEndElement();
            }
            else if (t == typeof(short))
            {
                writer.WriteStartElement(String.Empty, "integer", String.Empty);
                writer.WriteValue(value.ToString());
                writer.WriteEndElement();
            }
            else if (t == typeof(ushort))
            {
                writer.WriteStartElement(String.Empty, "integer", String.Empty);
                writer.WriteValue(value.ToString());
                writer.WriteEndElement();
            }
            else if (t == typeof(float))
            {
                writer.WriteStartElement(String.Empty, "real", String.Empty);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
            else if (t == typeof(double))
            {
                writer.WriteStartElement(String.Empty, "real", String.Empty);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
            else if (t == typeof(string))
            {
                writer.WriteStartElement(String.Empty, "string", String.Empty);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
            else if (t == typeof(UUID))
            {
                writer.WriteStartElement(String.Empty, "uuid", String.Empty);
                writer.WriteValue(value.ToString()); //UUID has to be string!
                writer.WriteEndElement();
            }
            else if (t == typeof(DateTime))
            {
                writer.WriteStartElement(String.Empty, "date", String.Empty);
                writer.WriteValue(AsString((DateTime)value));
                writer.WriteEndElement();
            }
            else if (t == typeof(Uri))
            {
                writer.WriteStartElement(String.Empty, "uri", String.Empty);
                writer.WriteValue(((Uri)value).ToString());//URI has to be string
                writer.WriteEndElement();
            }
            else if (t == typeof(byte[]))
            {
                writer.WriteStartElement(String.Empty, "binary", String.Empty);
                writer.WriteStartAttribute(String.Empty, "encoding", String.Empty);
                writer.WriteString("base64");
                writer.WriteEndAttribute();
                writer.WriteValue(Convert.ToBase64String((byte[])value)); //Has to be base64
                writer.WriteEndElement();
            }
            t = null;
        }

        public object this[string name]
        {
            set
            {
                writer.WriteStartElement(String.Empty, "key", String.Empty);
                writer.WriteString(name);
                writer.WriteEndElement();

                this.WriteElement(value);
            }
        }

        public byte[] GetSerializer()
        {
            writer.WriteEndElement();
            writer.Close();

            byte[] array = sw.ToArray();
            /*byte[] newarr = new byte[array.Length - 3];
            Array.Copy(array, 3, newarr, 0, newarr.Length);
            writer = null;
            sw = null;
            array = null;*/
            return array;
        }

        private string AsString(DateTime value)
        {
            string format;
            if (value.Millisecond > 0)
                format = "yyyy-MM-ddTHH:mm:ss.ffZ";
            else
                format = "yyyy-MM-ddTHH:mm:ssZ";
            return value.ToUniversalTime().ToString(format);
        }
    }

}
