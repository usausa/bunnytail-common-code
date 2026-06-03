namespace BunnyTail.CommonCode;

using System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class DelegateToAttribute : Attribute
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type? InterfaceType { get; set; }
}
