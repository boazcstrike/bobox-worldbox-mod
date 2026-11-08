using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BoBox.Planner
{
    /// <summary>Defensive copies for immutable planner and rules types, so a caller's later edits never leak in.</summary>
    public static class MapCopy
    {
        /// <summary>A read-only copy: a caller that casts the result to <see cref="Dictionary{TKey,TValue}"/> gets an invalid cast.</summary>
        public static IReadOnlyDictionary<TKey, TValue> Of<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
        {
            var copy = new Dictionary<TKey, TValue>();
            if (source != null)
            {
                foreach (KeyValuePair<TKey, TValue> pair in source)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return new ReadOnlyDictionary<TKey, TValue>(copy);
        }
    }
}
