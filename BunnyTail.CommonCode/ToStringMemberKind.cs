namespace BunnyTail.CommonCode;

public enum ToStringMemberKind
{
    // Inherit from the upper layer
    Inherit = 0,

    // Public properties only
    Property,

    // Public properties and public fields
    PropertyAndField,
}
