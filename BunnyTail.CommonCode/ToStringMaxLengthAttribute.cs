namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ToStringMaxLengthAttribute : Attribute
{
    public ToStringMaxLengthAttribute(int maxLength)
    {
        MaxLength = maxLength;
    }

    public int MaxLength { get; }
}
