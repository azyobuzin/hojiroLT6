using System.Collections.Generic;
using System.Collections.Immutable;

namespace WagahighChoices
{
    internal static class Extensions
    {
        public static ImmutableArray<T> ToReversedImmutableArray<T>(this IReadOnlyCollection<T> source)
        {
            var builder = ImmutableArray.CreateBuilder<T>(source.Count);
            builder.AddRange(source);
            builder.Reverse();
            return builder.MoveToImmutable();
        }
    }
}
