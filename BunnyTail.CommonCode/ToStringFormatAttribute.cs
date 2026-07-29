namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ToStringFormatAttribute : Attribute
{
    public ToStringFormatAttribute(string format)
    {
        Format = format;
    }

    public string Format { get; }
}
