namespace SuperRender.Core.Style;

public readonly struct Specificity : IComparable<Specificity>, IEquatable<Specificity>
{
    public int Ids { get; init; }
    public int Classes { get; init; }
    public int Elements { get; init; }

    public int CompareTo(Specificity other)
    {
        var cmp = Ids.CompareTo(other.Ids);
        if (cmp != 0) return cmp;
        cmp = Classes.CompareTo(other.Classes);
        if (cmp != 0) return cmp;
        return Elements.CompareTo(other.Elements);
    }

    public bool Equals(Specificity other)
        => Ids == other.Ids && Classes == other.Classes && Elements == other.Elements;

    public override bool Equals(object? obj) => obj is Specificity s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Ids, Classes, Elements);

    public static bool operator ==(Specificity left, Specificity right) => left.Equals(right);
    public static bool operator !=(Specificity left, Specificity right) => !left.Equals(right);
    public static bool operator <(Specificity left, Specificity right) => left.CompareTo(right) < 0;
    public static bool operator >(Specificity left, Specificity right) => left.CompareTo(right) > 0;
    public static bool operator <=(Specificity left, Specificity right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Specificity left, Specificity right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"({Ids},{Classes},{Elements})";
}
