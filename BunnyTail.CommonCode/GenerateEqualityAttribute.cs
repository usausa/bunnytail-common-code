namespace BunnyTail.CommonCode;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenerateEqualityAttribute : Attribute
{
    // == / != 演算子を生成するか (既定: true)
    public bool GenerateOperators { get; set; } = true;

    // コレクション要素を SequenceEqual で比較するか (既定: false)
    public bool DeepCollectionEquality { get; set; }
}
