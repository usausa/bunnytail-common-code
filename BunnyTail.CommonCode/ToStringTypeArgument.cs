namespace BunnyTail.CommonCode;

public enum ToStringTypeArgument
{
    // Inherit from the upper layer
    Inherit = 0,

    // No type argument
    None,

    // Runtime type arguments, such as Data<int>
    Include,
}
