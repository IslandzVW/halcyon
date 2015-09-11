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

namespace OpenSim.Framework
{
    /// <summary>
    /// A singleton to provide applications with a single point of contact to request application wide 
    /// provider interfaces
    /// </summary>
    public sealed class ProviderRegistry : IRegistryCore
    {
        private static readonly ProviderRegistry _Instance = new ProviderRegistry();
        private RegistryCore _workerCore = new RegistryCore();
        private Dictionary<KeyValuePair<Type, string>, object> _namedInterfaces = new Dictionary<KeyValuePair<Type, string>, object>(new TypeStringPairComparer());

        private class TypeStringPairComparer : IEqualityComparer<KeyValuePair<Type, string>>
        {
            public bool Equals(KeyValuePair<Type, string> x, KeyValuePair<Type, string> y)
            {
                return x.Value == y.Value && x.Key == y.Key;
            }
            public int GetHashCode(KeyValuePair<Type, string> obj)
            {
                return obj.Key.GetHashCode() * obj.Value.GetHashCode();
            }
        }

        private ProviderRegistry() 
        { 
        }

        public static ProviderRegistry Instance
        {
            get
            {
                return _Instance;
            }
        }

        #region IRegistryCore Members

        public T Get<T>()
        {
            return _workerCore.Get<T>();
        }

        public T GetNamedInterface<T>(string ifname)
        {
            lock (_namedInterfaces)
            {
                return (T)_namedInterfaces[new KeyValuePair<Type, string>(typeof(T), ifname)];
            }
        }

        public bool TryGetNamedInterface<T>(string ifname, out T outif)
        {
            lock (_namedInterfaces)
            {
                object ret;
                bool has = _namedInterfaces.TryGetValue(new KeyValuePair<Type, string>(typeof(T), ifname), out ret);

                outif = (T)ret;
                return has;
            }
        }

        public void RegisterInterface<T>(T iface)
        {
            _workerCore.RegisterInterface<T>(iface);
        }

        public void RegisterNamedInterface<T>(T iface, string name)
        {
            lock (_namedInterfaces)
            {
                _namedInterfaces.Add(new KeyValuePair<Type, string>(typeof(T), name), iface);
            }
        }

        public bool TryGet<T>(out T iface)
        {
            return _workerCore.TryGet<T>(out iface);
        }

        public void StackModuleInterface<M>(M mod)
        {
            _workerCore.StackModuleInterface(mod);
        }

        public T[] RequestModuleInterfaces<T>()
        {
            return _workerCore.RequestModuleInterfaces<T>();
        }

        #endregion
    }

}
