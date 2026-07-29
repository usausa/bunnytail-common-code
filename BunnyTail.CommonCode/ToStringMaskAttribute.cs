namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ToStringMaskAttribute : Attribute
{
    public int Show { get; set; }
}
