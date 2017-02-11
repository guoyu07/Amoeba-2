﻿using System.Collections.Generic;
using Omnius.Collections;

namespace Amoeba.Core
{
    public sealed class HashCollection : FilteredList<Hash>
    {
        public HashCollection() : base() { }
        public HashCollection(int capacity) : base(capacity) { }
        public HashCollection(IEnumerable<Hash> collections) : base(collections) { }

        protected override bool Filter(Hash item)
        {
            if (item == null) return true;

            return false;
        }
    }
}
