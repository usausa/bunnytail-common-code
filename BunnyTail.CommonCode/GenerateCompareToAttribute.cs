namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenerateCompareToAttribute : Attribute
{
    public bool GenerateOperators { get; set; } = true;
}
