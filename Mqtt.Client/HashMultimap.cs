using System.Collections.Generic;

namespace Mqtt.Client
{
    public class HashMultimap<KT,VT> : Dictionary<KT, List<VT>>
    {
        object lockthis = new object();
        public HashMultimap():
            base()
        {
        }

        public new void Remove(KT key)
        {
            lock (lockthis)
            {
                base.Remove(key);
            }
        }

        public void Remove(KT key, VT value)
        {
            lock (lockthis)
            {
                if (ContainsKey(key))
                {
                    var list = this[key];
                    if (list.Contains(value))
                    {
                        list.Remove(value);
                    }
                    if(list.Count == 0)
                    {
                        base.Remove(key);
                    }
                }
            }
        }

        public List<VT> Get(KT key)
        {
            lock (lockthis)
            {
                if (ContainsKey(key))
                {
                    return new List<VT>(this[key]);
                }
                return null;
            }
        }

        public void Put(KT key, VT item)
        {
            lock (lockthis)
            {
                if (!ContainsKey(key))
                {
                    Add(key, new List<VT>() { item });
                }
                else
                {
                    this[key].Add(item);
                }
            }
        }

        public new List<VT> Values
        {
            get
            {
                lock (lockthis)
                {
                    List<VT> result = new List<VT>();
                    foreach(var list in base.Values)
                    {
                        result.AddRange(list);
                    }
                    return result;
                }
            }
        }

        public new void Clear()
        {
            lock (lockthis)
            {
                base.Clear();
            }
        }

        public new bool ContainsKey(KT key)
        {
            lock (lockthis)
            {
                return base.ContainsKey(key);
            }
        }
    }
}
