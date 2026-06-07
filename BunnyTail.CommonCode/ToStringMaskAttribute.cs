namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ToStringMaskAttribute : Attribute
{
    public int Show { get; set; }
}
