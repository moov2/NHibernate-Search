using System.Collections.Generic;

namespace NHibernate.Search.Util
{
    public static class ISetExtensions
    {
        public static void AddAll<T>(this ISet<T> set, IEnumerable<T> items)
        {
            if(items == null) return;
            if(set == null) return;

            foreach (var item in items)
            {
                set.Add(item);
            }
        }
    }
}
