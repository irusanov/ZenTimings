using System.Collections.Generic;

namespace ZenTimings.Export
{
    /// <summary>
    /// Insertion-ordered string/object map used as the snapshot tree node.
    /// </summary>
    public sealed class SnapshotObject
    {
        public List<KeyValuePair<string, object>> Items { get; } = new List<KeyValuePair<string, object>>();

        public int Count => Items.Count;

        public SnapshotObject Add(string key, object value)
        {
            Items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public bool TryGet(string key, out object value)
        {
            foreach (var item in Items)
            {
                if (item.Key == key)
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
