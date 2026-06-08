namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenerateEqualityAttribute : Attribute
{
    // Generate == / != operator
    public bool GenerateOperators { get; set; } = true;

    // Compare collection elements using SequenceEqual
    public bool DeepCollectionEquality { get; set; }
}
