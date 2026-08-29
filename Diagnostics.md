# Diagnostics

## ToString

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0101 | ❌ Error | `[GenerateToString]` type is not declared partial | Declare the type as `partial` |
| BTCC0102 | ⚠️ Warning | `[ToStringFormat]` has no effect because the member is excluded by `[IgnoreToString]` | Remove `[ToStringFormat]`, or remove `[IgnoreToString]` |
| BTCC0103 | ⚠️ Warning | `MaskChar` has no effect because `MaskPattern` takes precedence | Remove `MaskChar`, or remove `MaskPattern` |
| BTCC0104 | ⚠️ Warning | `[ToStringFormat]` has no effective setting | Set a format option, or remove the attribute |

## Equality

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0201 | ❌ Error | `[GenerateEquality]` type is not declared partial | Declare the type as `partial` |
| BTCC0202 | ❌ Error | Type has no public property to compare | Add a public property, or remove `[GenerateEquality]` |

## DeepClone

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0301 | ❌ Error | `[GenerateDeepClone]` type is not declared partial | Declare the type as `partial` |
| BTCC0302 | ❌ Error | `[GenerateDeepClone]` type does not implement `IDeepCloneable<T>` | Implement `IDeepCloneable<T>` on the type |
| BTCC0303 | ⚠️ Warning | Property type does not implement `IDeepCloneable<T>` | Implement `IDeepCloneable<T>` on the property type, or mark the property with `[ShallowClone]` |

## DelegateTo

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0401 | ❌ Error | `[GenerateDelegateTo]` type is not declared partial | Declare the type as `partial` |
| BTCC0402 | ❌ Error | Type has no field or property marked with `[DelegateTo]` | Mark a field or property with `[DelegateTo]` |
| BTCC0403 | ⚠️ Warning | `[DelegateTo]` `InterfaceType` is not an interface implemented by the delegate member type | Specify an interface that the delegate member type implements |

## CompareTo

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTCC0501 | ❌ Error | `[GenerateCompareTo]` type is not declared partial | Declare the type as `partial` |
| BTCC0502 | ❌ Error | Type has no property marked with `[CompareKey]` | Mark at least one property with `[CompareKey]` |
