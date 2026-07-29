namespace BunnyTail.CommonCode;

public enum ToStringCollectionMode
{
    // Inherit from the upper layer
    Inherit = 0,

    // Output the result of ToString as is
    Raw,

    // Output the elements enclosed in brackets
    Expand,
}
