namespace BunnyTail.CommonCode;

public enum ToStringNullMode
{
    // Inherit from the upper layer
    Inherit = 0,

    // Output nothing
    Empty,

    // Output the string specified by NullLiteral
    Literal,
}
