namespace BunnyTail.CommonCode;

public enum ToStringBracket
{
    // Inherit from the upper layer
    Inherit = 0,

    // No bracket
    None,

    // { }
    Brace,

    // ( )
    Parenthesis,

    // [ ]
    Square,

    // < >
    Angle,
}
