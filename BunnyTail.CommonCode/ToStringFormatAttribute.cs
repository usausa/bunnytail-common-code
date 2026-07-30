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

    // Format string applied to the value
    public string? Format { get; }

    // Maximum length of the formatted value, 0 means unlimited
    public int MaxLength { get; set; }

    // Character repeated over the whole value, so '*' turns secret into ******
    public char MaskChar { get; set; }

    // Text written instead of the value, so "####****####" turns 4111111111111111 into 4111****1111.
    // A leading or trailing run of '#' keeps that many characters of the original value visible.
    // Takes precedence over MaskChar.
    public string? MaskPattern { get; set; }
}
