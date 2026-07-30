namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ToStringFormatAttribute : Attribute
{
    public ToStringFormatAttribute()
    {
    }

    public ToStringFormatAttribute(string format)
    {
        Format = format;
    }

    public string? Format { get; }

    public int MaxLength { get; set; }

    public char MaskChar { get; set; }

    public string? MaskPattern { get; set; }
}
