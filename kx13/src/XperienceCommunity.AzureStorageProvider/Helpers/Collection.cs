using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AzureStorageProvider.Helpers
{
    public class Collection<TModel, TCollection> : Singleton<TCollection>
        where TModel : IObjectWithPath<TModel>, new()
        where TCollection : new()
    {
        protected static ConcurrentDictionary<string, TModel> items = new ConcurrentDictionary<string, TModel>();

        public Collection()
        {
        }

        public virtual TModel GetOrCreate(string name) => items.GetOrAdd(name, n => new TModel().Initialize(n));

        public TModel TryGet(string name)
        {
            items.TryGetValue(name, out var item);

            return item;
        }

        public bool Contains(string name) => items.ContainsKey(name);

        public IEnumerable<TModel> GetStartingWith(string path, bool flat)
        {
            var condition = new Func<TModel, bool>(i => i.Path.StartsWith(path));
            if (flat)
            {
                path = AzurePathHelper.GetValidPathForwardSlashes(path);
                condition = i => i.Path.StartsWith(path) && AzurePathHelper.GetBlobDirectory(i.Path) == path;
            }

            // need to use Values to work on top of moment-in-time snapshot
            return items.Values.Where(condition);
        }

        public void AddRangeDistinct(IEnumerable<TModel> data)
        {
            foreach (var item in data)
            {
                // in case item already exists, keep the old one
                items.AddOrUpdate(item.Path, p => item, (p, oldItem) => item);
            }
        }

        public void ForAll(Func<TModel, bool> condition, Action<TModel> function)
        {
            var keys = items.Keys.ToList();

            foreach (string key in keys)
            {
                var item = items[key];
                if (condition(item))
                {
                    function(item);
                }
            }
        }
    }
}
