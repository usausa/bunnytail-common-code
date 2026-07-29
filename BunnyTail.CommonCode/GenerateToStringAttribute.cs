namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenerateToStringAttribute : Attribute
{
    // Preset of the output settings
    public ToStringStyle Style { get; set; }

    // Type name written before the body
    public ToStringTypeName TypeName { get; set; }

    // Whether the type name includes the runtime type arguments
    public ToStringTypeArgument TypeArgument { get; set; }

    // How a null value is written
    public ToStringNullMode Null { get; set; }

    // String used when Null is ToStringNullMode.Literal
    public string? NullLiteral { get; set; }

    // How a collection value is written
    public ToStringCollectionMode Collection { get; set; }

    // Maximum number of elements written for a collection (0 = inherit, -1 = unlimited)
    public int CollectionLimit { get; set; }

    // Members to write
    public ToStringMemberKind Members { get; set; }

    // Bracket enclosing the body
    public ToStringBracket Bracket { get; set; }

    // Open bracket string, takes precedence over Bracket
    public string? OpenBracket { get; set; }

    // Close bracket string, takes precedence over Bracket
    public string? CloseBracket { get; set; }

    // Space inside the brackets
    public ToStringSpace InnerSpace { get; set; }

    // Space between the type name and the open bracket
    public ToStringSpace TypeNameSpace { get; set; }

    // Separator between members
    public string? Separator { get; set; }

    // Separator between a member name and its value
    public string? Assign { get; set; }

    // Bracket enclosing expanded collection elements
    public ToStringBracket CollectionBracket { get; set; }

    // Open bracket string for collections, takes precedence over CollectionBracket
    public string? CollectionOpenBracket { get; set; }

    // Close bracket string for collections, takes precedence over CollectionBracket
    public string? CollectionCloseBracket { get; set; }

    // Space inside the collection brackets
    public ToStringSpace CollectionInnerSpace { get; set; }

    // Separator between collection elements
    public string? CollectionSeparator { get; set; }
}
