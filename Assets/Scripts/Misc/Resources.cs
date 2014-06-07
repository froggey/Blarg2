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

        public override string ToString() {
                return string.Format("{{ Metal: {0}, MagicSmoke: {1} }}", Metal, MagicSmoke);
        }

        public static ResourceSet operator +(ResourceSet lhs, ResourceSet rhs) {
                return new ResourceSet { Metal = lhs.Metal + rhs.Metal, MagicSmoke = lhs.MagicSmoke + rhs.MagicSmoke };
        }

        public static ResourceSet operator -(ResourceSet lhs, ResourceSet rhs) {
                return new ResourceSet { Metal = lhs.Metal - rhs.Metal, MagicSmoke = lhs.MagicSmoke - rhs.MagicSmoke };
        }
}