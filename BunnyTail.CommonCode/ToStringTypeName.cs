namespace BunnyTail.CommonCode;

public enum ToStringTypeName
{
    // Inherit from the upper layer
    Inherit = 0,

    // No type name
    None,

    // Type name without namespace, containing types and type arguments
    Simple,

    // Type name with namespace and containing types
    Full,
}
