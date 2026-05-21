namespace TheBookOfLong;

public readonly struct SymbolicSourceResolution
{
    public SymbolicSourceResolution(bool hasBaseMaxId, int baseMaxId, int maxAssignedId)
    {
        HasBaseMaxId = hasBaseMaxId;
        BaseMaxId = baseMaxId;
        MaxAssignedId = maxAssignedId;
    }

    public bool HasBaseMaxId { get; }

    public int BaseMaxId { get; }

    public int MaxAssignedId { get; }
}
