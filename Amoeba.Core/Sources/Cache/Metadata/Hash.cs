﻿using System;
using System.IO;
using System.Runtime.Serialization;
using Omnius.Io;
using Omnius.Utilities;
using Omnius.Serialization;
using Omnius.Base;

namespace Amoeba.Core
{
    [DataContract(Name = "Hash")]
    public sealed class Hash : ItemBase<Hash>, IHash
    {
        private HashAlgorithm _algorithm;
        private byte[] _value;

        private readonly int _hashCode;

        public static readonly int MaxValueLength = 32;

        public Hash(HashAlgorithm algorithm, byte[] value)
        {
            this.Algorithm = algorithm;
            this.Value = value;

            _hashCode = (this.Value != null) ? ItemUtils.GetHashCode(this.Value) : 0;
        }

        protected override void ProtectedImport(Stream stream, BufferManager bufferManager, int depth)
        {
            using (var reader = new ItemStreamReader(stream, bufferManager))
            {
                this.Algorithm = (HashAlgorithm)reader.GetInt();
                this.Value = reader.GetBytes();
            }
        }

        protected override Stream Export(BufferManager bufferManager, int depth)
        {
            using (var writer = new ItemStreamWriter(bufferManager))
            {
                writer.Write((int)this.Algorithm);
                writer.Write(this.Value);

                return writer.GetStream();
            }
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override bool Equals(object obj)
        {
            if ((object)obj == null || !(obj is Hash)) return false;

            return this.Equals((Hash)obj);
        }

        public override bool Equals(Hash other)
        {
            if ((object)other == null) return false;
            if (object.ReferenceEquals(this, other)) return true;

            if (this.Algorithm != other.Algorithm
                || (this.Value == null) != (other.Value == null))
            {
                return false;
            }

            if (this.Value != null && other.Value != null)
            {
                if (!Unsafe.Equals(this.Value, other.Value)) return false;
            }

            return true;
        }

        #region IHash

        [DataMember(Name = "Algorithm")]
        public HashAlgorithm Algorithm
        {
            get
            {
                return _algorithm;
            }
            private set
            {
                if (!Enum.IsDefined(typeof(HashAlgorithm), value))
                {
                    throw new ArgumentException();
                }
                else
                {
                    _algorithm = value;
                }
            }
        }

        [DataMember(Name = "Value")]
        public byte[] Value
        {
            get
            {
                return _value;
            }
            private set
            {
                if (value != null && value.Length > Hash.MaxValueLength)
                {
                    throw new ArgumentException();
                }
                else
                {
                    _value = value;
                }
            }
        }

        #endregion
    }
}
