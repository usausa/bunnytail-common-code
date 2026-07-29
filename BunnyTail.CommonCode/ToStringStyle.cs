namespace BunnyTail.CommonCode;

public enum ToStringStyle
{
    // Inherit from the upper layer
    Inherit = 0,

    // Type name with type arguments, null literal and expanded collections
    Default,

    // Compatible with the record output
    Record,
}
