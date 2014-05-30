using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum ResourceType {
        Metal,
        MagicSmoke
}

[Serializable]
public class ResourceSet {
        public int Metal;
        public int MagicSmoke;

        public bool ContainsAtLeast(ResourceSet required) {
                return Metal >= required.Metal && MagicSmoke >= required.MagicSmoke;
        }
}