using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.AsyncEvents.Generator;

/// <summary>
/// An immutable array that compares by its elements, so a model holding one keeps comparing by
/// value and the incremental pipeline can tell an unchanged model from a changed one.
/// <see cref="ImmutableArray{T}"/> and <see cref="IReadOnlyList{T}"/> compare by reference.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Count => Items.Length;

    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other)
    {
        var mine = Items;
        var theirs = other.Items;
        if (mine.Length != theirs.Length) return false;
        for (var i = 0; i < mine.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(mine[i], theirs[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in Items) hash = (hash * 31) + (item is null ? 0 : item.GetHashCode());
            return hash;
        }
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();
}
