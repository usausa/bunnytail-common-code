namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValueFormatAttribute : Attribute
{
    public ValueFormatAttribute()
    {
    }

    public ValueFormatAttribute(string format)
    {
        Format = format;
    }

    public string? Format { get; set; }

    public int MaxLength { get; set; }

    public bool Mask { get; set; }

    public int MaskShow { get; set; }
}
