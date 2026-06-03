namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreEqualityAttribute : Attribute
{
}
