namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CompareKeyAttribute : Attribute
{
    public int Order { get; set; }
}
