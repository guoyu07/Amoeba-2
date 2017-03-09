﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Xml;
using Omnius.Base;
using Omnius.Collections;
using Omnius.Configuration;
using Omnius.Io;
using Omnius.Utilities;

namespace Amoeba.Service
{
    class CatharsisManager : StateManagerBase, ISettings
    {
        private BufferManager _bufferManager;

        private Settings _settings;

        private Ipv4CatharsisConfig _ipv4Config;

        private HashSet<SearchRange<Ipv4>> _ipv4RangeSet;

        private WatchTimer _watchTimer;
        private WatchTimer _updateTimer;

        private VolatileHashDictionary<Ipv4, bool> _ipv4ResultMap;

        private volatile ManagerState _state = ManagerState.Stop;

        private readonly object _lockObject = new object();
        private volatile bool _disposed;

        public CatharsisManager(string configPath, BufferManager bufferManager)
        {
            _bufferManager = bufferManager;

            _settings = new Settings(configPath);

            _watchTimer = new WatchTimer(this.WatchThread);
            _updateTimer = new WatchTimer(() => _ipv4ResultMap?.Update());
            _updateTimer.Start(new TimeSpan(0, 30, 0));

            _ipv4ResultMap = new VolatileHashDictionary<Ipv4, bool>(new TimeSpan(0, 30, 0));
        }

        public Ipv4CatharsisConfig Ipv4Config
        {
            get
            {
                lock (_lockObject)
                {
                    return _ipv4Config;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _ipv4Config = value;
                }

                _watchTimer.Run();
            }
        }

        public bool Check(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var ipv4 = new Ipv4(ipAddress);

                lock (_lockObject)
                {
                    return _ipv4ResultMap.GetOrAdd(ipv4, (_) => _ipv4RangeSet.Any(n => n.Verify(ipv4)));
                }
            }

            return true;
        }

        private void WatchThread()
        {
            try
            {
                this.UpdateIpv4RangeSet();
            }
            catch (Exception)
            {

            }
        }

        private void UpdateIpv4RangeSet()
        {
            for (;;)
            {
                Ipv4CatharsisConfig ipv4Config;

                lock (_lockObject)
                {
                    ipv4Config = _ipv4Config;
                }

                var ipv4RangeSet = new HashSet<SearchRange<Ipv4>>();

                {
                    // path
                    foreach (var path in ipv4Config.Paths)
                    {
                        try
                        {
                            using (var stream = new FileStream(path, FileMode.OpenOrCreate))
                            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                            {
                                ipv4RangeSet.UnionWith(this.ParseIpv4Ranges(reader));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e);
                        }
                    }

                    // Url
                    foreach (var url in ipv4Config.Urls)
                    {
                        try
                        {
                            using (var stream = this.GetStream(url))
                            using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                            using (var reader = new StreamReader(gzipStream))
                            {
                                ipv4RangeSet.UnionWith(this.ParseIpv4Ranges(reader));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e);
                        }
                    }
                }

                lock (_lockObject)
                {
                    if (_ipv4Config != ipv4Config) continue;

                    _ipv4RangeSet.Clear();
                    _ipv4RangeSet.UnionWith(ipv4RangeSet);

                    _ipv4ResultMap.Clear();

                    return;
                }
            }
        }

        private IEnumerable<SearchRange<Ipv4>> ParseIpv4Ranges(StreamReader reader)
        {
            var list = new List<SearchRange<Ipv4>>();

            using (reader)
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var index = line.LastIndexOf(':');
                    if (index == -1) continue;

                    var items = line.Substring(index + 1).Split('-');
                    if (items.Length != 2) return null;

                    var ipv4s = new Ipv4[2];
                    ipv4s[0] = new Ipv4(IPAddress.Parse(items[0]));
                    ipv4s[1] = new Ipv4(IPAddress.Parse(items[1]));

                    int c = ipv4s[0].CompareTo(ipv4s[1]);

                    if (c < 0)
                    {
                        list.Add(new SearchRange<Ipv4>(ipv4s[0], ipv4s[1]));
                    }
                    else
                    {
                        list.Add(new SearchRange<Ipv4>(ipv4s[1], ipv4s[0]));
                    }
                }
            }

            return list;
        }

        private Stream GetStream(string url)
        {
            var bufferStream = new BufferStream(_bufferManager);

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = true;
                request.Headers.Add("Pragma", "no-cache");
                request.Headers.Add("Cache-Control", "no-cache");
                request.Timeout = 1000 * 30;
                request.ReadWriteTimeout = 1000 * 30;

                using (WebResponse response = request.GetResponse())
                {
                    if (response.ContentLength > 1024 * 1024 * 32) throw new Exception("too large");

                    using (Stream stream = response.GetResponseStream())
                    using (var safeBuffer = _bufferManager.CreateSafeBuffer(1024 * 4))
                    {
                        int length;

                        while ((length = stream.Read(safeBuffer.Value, 0, safeBuffer.Value.Length)) > 0)
                        {
                            bufferStream.Write(safeBuffer.Value, 0, length);

                            if (bufferStream.Length > 1024 * 1024 * 32) throw new Exception("too large");
                        }
                    }

                    if (response.ContentLength != -1 && bufferStream.Length != response.ContentLength)
                    {
                        return null;
                    }

                    bufferStream.Seek(0, SeekOrigin.Begin);
                    return bufferStream;
                }
            }
            catch (Exception)
            {
                bufferStream.Dispose();

                throw;
            }
        }

        public override ManagerState State
        {
            get
            {
                return _state;
            }
        }

        private readonly object _stateLock = new object();

        public override void Start()
        {
            lock (_stateLock)
            {
                lock (_lockObject)
                {
                    if (this.State == ManagerState.Start) return;
                    _state = ManagerState.Start;

                    _watchTimer.Start(new TimeSpan(0, 0, 0), new TimeSpan(3, 0, 0));
                }
            }
        }

        public override void Stop()
        {
            lock (_stateLock)
            {
                lock (_lockObject)
                {
                    if (this.State == ManagerState.Stop) return;
                    _state = ManagerState.Stop;
                }

                _watchTimer.Stop();
            }
        }

        #region ISettings

        public void Load()
        {
            lock (_lockObject)
            {
                int version = _settings.Load("Version", () => 0);

                _ipv4Config = _settings.Load("Ipv4Config", () => new Ipv4CatharsisConfig(null, null));

                _ipv4RangeSet = _settings.Load("Ipv4RangeSet", () => new HashSet<SearchRange<Ipv4>>());
            }
        }

        public void Save()
        {
            lock (_lockObject)
            {
                _settings.Save("Version", 0);

                _settings.Save("Ipv4Config", _ipv4Config);

                _settings.Save("Ipv4RangeSet", _ipv4RangeSet);
            }
        }

        #endregion

        [DataContract(Name = nameof(Ipv4))]
        struct Ipv4 : IComparable<Ipv4>, IEquatable<Ipv4>
        {
            private uint _value;

            public Ipv4(IPAddress ipAddress)
            {
                if (ipAddress.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException(nameof(ipAddress));
                _value = NetworkConverter.ToUInt32(ipAddress.GetAddressBytes());
            }

            [DataMember(Name = nameof(Value))]
            private uint Value
            {
                get
                {
                    return _value;
                }
                set
                {
                    _value = value;
                }
            }

            public override int GetHashCode()
            {
                return (int)this.Value;
            }

            public override bool Equals(object obj)
            {
                if ((object)obj == null || !(obj is Ipv4)) return false;

                return this.Equals((Ipv4)obj);
            }

            public bool Equals(Ipv4 other)
            {
                if (this.Value != other.Value)
                {
                    return false;
                }

                return true;
            }

            public int CompareTo(Ipv4 other)
            {
                return this.Value.CompareTo(other.Value);
            }

            public override string ToString()
            {
                return new IPAddress(NetworkConverter.GetBytes(this.Value)).ToString();
            }
        }

        [DataContract(Name = nameof(SearchRange<T>))]
        struct SearchRange<T> : IEquatable<SearchRange<T>>
            where T : IComparable<T>, IEquatable<T>
        {
            private T _min;
            private T _max;

            public SearchRange(T min, T max)
            {
                _min = min;
                _max = (max.CompareTo(_min) < 0) ? _min : max;
            }

            [DataMember(Name = nameof(Min))]
            public T Min
            {
                get
                {
                    return _min;
                }
                private set
                {
                    _min = value;
                }
            }

            [DataMember(Name = nameof(Max))]
            public T Max
            {
                get
                {
                    return _max;
                }
                private set
                {
                    _max = value;
                }
            }

            public bool Verify(T value)
            {
                if (value.CompareTo(this.Min) < 0 || value.CompareTo(this.Max) > 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            public override int GetHashCode()
            {
                return this.Min.GetHashCode() ^ this.Max.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if ((object)obj == null || !(obj is SearchRange<T>)) return false;

                return this.Equals((SearchRange<T>)obj);
            }

            public bool Equals(SearchRange<T> other)
            {
                if (!this.Min.Equals(other.Min) || !this.Max.Equals(other.Max))
                {
                    return false;
                }

                return true;
            }

            public override string ToString()
            {
                return string.Format("Min = {0}, Max = {1}", this.Min, this.Max);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                if (_watchTimer != null)
                {
                    _watchTimer.Dispose();
                    _watchTimer = null;
                }

                if (_updateTimer != null)
                {
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }
            }
        }
    }
}
