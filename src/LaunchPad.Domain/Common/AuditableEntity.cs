namespace LaunchPad.Domain.Common;

public abstract class Entity
{
    public byte[]? RowVersion { get; set; }
}
