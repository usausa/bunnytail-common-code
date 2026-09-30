# Diagnostics

## Common

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0001 | ❌ Error | The name of a generated type differs only in case from another in the same namespace, so its source is not generated (generated file names are compared ignoring case) | Rename one of the types |

## ToString

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0101 | ❌ Error | `[GenerateToString]` type or a type containing it is not declared partial, or the type is `file`-local | Declare the type and its containing types as `partial`, and the type without `file` |
| BTCC0102 | ⚠️ Warning | `[ToStringFormat]` has no effect because the member is excluded by `[IgnoreToString]` | Remove `[ToStringFormat]`, or remove `[IgnoreToString]` |
| BTCC0103 | ⚠️ Warning | `MaskChar` has no effect because `MaskPattern` takes precedence | Remove `MaskChar`, or remove `MaskPattern` |
| BTCC0104 | ⚠️ Warning | `[ToStringFormat]` has no effective setting | Set a format option, or remove the attribute |
| BTCC0105 | ⚠️ Warning | A `CommonCodeGeneratorToString*` MSBuild property has a value that cannot be read, so its default is used | Set one of the values listed in the README |
| BTCC0106 | ❌ Error | A base type seals `ToString()`, so it cannot be generated | Remove `[GenerateToString]`, or unseal `ToString()` in the base type |
| BTCC0107 | ❌ Error | A member of a ref struct type other than `Span<char>` / `ReadOnlySpan<char>` cannot be written | Mark the member with `[IgnoreToString]` |

## Equality

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0201 | ❌ Error | `[GenerateEquality]` type or a type containing it is not declared partial, or the type is `file`-local | Declare the type and its containing types as `partial`, and the type without `file` |
| BTCC0202 | ❌ Error | Type has no public property to compare | Add a public property, or remove `[GenerateEquality]` |
| BTCC0203 | ❌ Error | A property of a ref struct type cannot be compared | Mark the property with `[IgnoreEquality]` |

## DeepClone

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0301 | ❌ Error | `[GenerateDeepClone]` type or a type containing it is not declared partial, or the type is `file`-local | Declare the type and its containing types as `partial`, and the type without `file` |
| BTCC0302 | ❌ Error | `[GenerateDeepClone]` type does not implement `IDeepCloneable<T>` | Implement `IDeepCloneable<T>` on the type |
| BTCC0303 | ⚠️ Warning | Property type does not implement `IDeepCloneable<T>` whose `T` is assignable to the property (the value is copied shallow) | Implement `IDeepCloneable<T>` on the property type, or mark the property with `[ShallowClone]` |
| BTCC0304 | ❌ Error | `[GenerateDeepClone]` is used on a ref struct, which cannot implement `IDeepCloneable<T>` | Remove `[GenerateDeepClone]` |

## DelegateTo

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0401 | ❌ Error | `[GenerateDelegateTo]` type or a type containing it is not declared partial, or the type is `file`-local | Declare the type and its containing types as `partial`, and the type without `file` |
| BTCC0402 | ❌ Error | Type has no field or property marked with `[DelegateTo]` | Mark a field or property with `[DelegateTo]` |
| BTCC0403 | ❌ Error | `[DelegateTo]` `InterfaceType` is not an interface implemented by the delegate member type | Specify an interface that the delegate member type implements |

## CompareTo

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0501 | ❌ Error | `[GenerateCompareTo]` type or a type containing it is not declared partial, or the type is `file`-local | Declare the type and its containing types as `partial`, and the type without `file` |
| BTCC0502 | ❌ Error | Type has no property marked with `[CompareKey]` | Mark at least one property with `[CompareKey]` |
| BTCC0503 | ❌ Error | Key type is a struct, a sealed class or an array that implements neither `IComparable<T>` nor `IComparable`, or a ref struct, so `Comparer<T>.Default` cannot compare it (it throws at run time) | Implement `IComparable<T>` on the key type, or use another key |
