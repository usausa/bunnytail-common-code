# BunnyTail.CommonCode

[![NuGet](https://img.shields.io/nuget/v/BunnyTail.CommonCode.svg)](https://www.nuget.org/packages/BunnyTail.CommonCode)

## Reference

Add reference to BunnyTail.CommonCode to csproj.

```xml
  <ItemGroup>
    <PackageReference Include="BunnyTail.CommonCode" Version="1.2.0" />
  </ItemGroup>
```

---

## ToString

Generates a `ToString()` implementation. Collections are expanded and `null` is written as a literal so that the output is useful for logging, and every part of it is configurable. `Style = ToStringStyle.Record` switches the output to be identical to the one the compiler generates for a `record`.

### Source

```csharp
[GenerateToString]
public partial class Data
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int[] Values { get; set; } = default!;

    [IgnoreToString]
    public int Ignore { get; set; }
}
```

### Result

```csharp
var data = new Data { Id = 123, Name = null, Values = [1, 2] };
var str = data.ToString();
Assert.Equal("Data { Id = 123, Name = null, Values = [1, 2] }", str);
```

Public properties are written, static members and indexers are excluded, and members declared in base types come first. A member hidden with `new` is written only once, using the most derived declaration.

For a generic type the runtime type arguments are written, so `Data<T>` produces `Data<int> { ... }`. The name is built once per closed generic type and cached in a `static readonly` field, so `ToString()` itself pays no cost.

### Style

`Style` selects a preset, and individual options override it.

| Option | `Default` | `Record` |
|---|---|---|
| `TypeName` | `Simple` | `Simple` |
| `TypeArgument` | `Include` | `None` |
| `Null` | `Literal` (`null`) | `Empty` |
| `Collection` | `Expand` | `Raw` |
| `Members` | `Property` | `PropertyAndField` |
| `Bracket` / `InnerSpace` / `TypeNameSpace` | `Brace` / `Space` / `Space` | same as `Default` |
| `Separator` / `Assign` | `", "` / `" = "` | same as `Default` |
| `CollectionBracket` / `CollectionInnerSpace` / `CollectionSeparator` | `Square` / `None` / `", "` | same as `Default` |

```csharp
[GenerateToString(Style = ToStringStyle.Record)]
public partial class RecordLikeData
{
    public string? Name { get; set; }    // Name =
    public int[]? Values { get; set; }   // Values = System.Int32[]
}
```

The rules that are not part of a preset are always applied: base type members come first, static members and indexers are excluded, and the inner space is collapsed when there is no member to write.

### Type attribute options

| Property | Type | Description |
|---|---|---|
| `Style` | `ToStringStyle` | `Default` / `Record` preset |
| `TypeName` | `ToStringTypeName` | `None` / `Simple` / `Full` |
| `TypeArgument` | `ToStringTypeArgument` | `None` / `Include`, whether the runtime type arguments are written |
| `Null` | `ToStringNullMode` | `Empty` / `Literal` |
| `NullLiteral` | `string` | String used when `Null` is `Literal` |
| `Collection` | `ToStringCollectionMode` | `Raw` / `Expand` |
| `CollectionLimit` | `int` | Maximum number of elements (`-1` = unlimited), the rest becomes `...` |
| `Members` | `ToStringMemberKind` | `Property` / `PropertyAndField` |
| `Bracket` | `ToStringBracket` | `None` / `Brace` / `Parenthesis` / `Square` / `Angle` |
| `OpenBracket` / `CloseBracket` | `string` | Arbitrary bracket, takes precedence over `Bracket` |
| `InnerSpace` | `ToStringSpace` | Space inside the brackets |
| `TypeNameSpace` | `ToStringSpace` | Space between the type name and the open bracket |
| `Separator` | `string` | Separator between members |
| `Assign` | `string` | Separator between a member name and its value |
| `CollectionBracket` | `ToStringBracket` | Bracket for expanded collections |
| `CollectionOpenBracket` / `CollectionCloseBracket` | `string` | Arbitrary bracket for expanded collections |
| `CollectionInnerSpace` | `ToStringSpace` | Space inside the collection brackets |
| `CollectionSeparator` | `string` | Separator between collection elements |

Every enum option has `Inherit` as its default, which means the value is taken from the upper layer.

### Output layout

```
[TypeName][TypeNameSpace][OpenBracket][InnerSpace][Name][Assign][Value][Separator]...[InnerSpace][CloseBracket]
```

When there is no member to write, the two inner spaces are collapsed into one, so `Data { }` is produced instead of `Data {  }`.

| Setting | Output |
|---|---|
| default | `Data { Id = 1, Name = x }` |
| `TypeArgument = None` (on `Data<T>`) | `Data { Id = 1, Name = x }` instead of `Data<int> { ... }` |
| `Bracket = Parenthesis, InnerSpace = None, TypeNameSpace = None` | `Data(Id = 1, Name = x)` |
| `Bracket = Square, InnerSpace = None` | `Data [Id = 1, Name = x]` |
| `TypeName = None` | `{ Id = 1, Name = x }` |
| `TypeName = None, Bracket = None` | `Id = 1, Name = x` |
| `OpenBracket = "<<", CloseBracket = ">>", Separator = " \| ", Assign = ":"` | `Data << Id:1 \| Name:x >>` |

### Project settings

The same options can be set for the whole project with MSBuild properties named `CommonCodeGeneratorToString` + the option name.

```xml
<PropertyGroup>
  <CommonCodeGeneratorToStringStyle>Record</CommonCodeGeneratorToStringStyle>
  <CommonCodeGeneratorToStringBracket>Parenthesis</CommonCodeGeneratorToStringBracket>
  <CommonCodeGeneratorToStringCollectionLimit>10</CommonCodeGeneratorToStringCollectionLimit>
</PropertyGroup>
```

Settings are resolved in this order, and later ones win.

1. The `Default` preset
2. The preset selected by `CommonCodeGeneratorToStringStyle`
3. Individual MSBuild properties
4. The preset selected by `Style` on the type attribute
5. Individual properties on the type attribute

Specifying `Style` on the type attribute resets the settings to that preset, so individual MSBuild properties are not applied to that type.

### Property attributes

| Attribute | Description |
|---|---|
| `[IgnoreToString]` | Exclude the member from output |
| `[ToStringFormat(format)]` | Apply a format string (`AppendFormatted(value, format)`) |
| `[ToStringMaxLength(length)]` | Truncate the stringified value to the given length |
| `[ToStringMask]` / `[ToStringMask(Show = n)]` | Mask the value; with `Show`, reveal only the last `n` characters |

When combined, the priority is `Mask` > collection expansion > `MaxLength` > `Format` (`MaxLength` and `Format` can be combined). `[ToStringMask]` also wins over `Collection = Expand`, so the elements of a masked collection are never written. These attributes can be applied to both properties and fields, and `null` values follow the `Null` setting.

```csharp
[GenerateToString]
public partial class User
{
    public int Id { get; set; }

    [ToStringMask]
    public string Password { get; set; } = default!;     // Password = ***

    [ToStringMask(Show = 2)]
    public string Token { get; set; } = default!;        // Token = ***34

    [ToStringFormat("yyyy-MM-dd")]
    public DateTime BirthDate { get; set; }              // BirthDate = 2020-01-02

    [ToStringMaxLength(20)]
    public string Description { get; set; } = default!;  // truncated to 20 chars
}
```

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTCC0101 | Warning | Type must be partial |

---

## Equality

Generates `IEquatable<T>`, `Equals`, `GetHashCode`, and optional equality operators.

### Source

```csharp
[GenerateEquality]
public partial class OrderData
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;

    [IgnoreEquality]
    public DateTime UpdatedAt { get; init; }
}

// Deep collection comparison enabled, operators generated
[GenerateEquality(GenerateOperators = true, DeepCollectionEquality = true)]
public sealed partial class TaggedData
{
    public string Name { get; init; } = default!;
    public string[] Tags { get; init; } = [];
}
```

### Attribute options

| Property | Default | Description |
|---|---|---|
| `GenerateOperators` | `true` | Emit `==` and `!=` operators |
| `DeepCollectionEquality` | `false` | Use `SequenceEqual` for collection properties |

Equality and hash code are computed from all reachable public properties, including those inherited from base types (flattened). `base.Equals` / `base.GetHashCode` are not called.

### Result

```csharp
var a = new OrderData { Id = 1, Name = "x" };
var b = new OrderData { Id = 1, Name = "x", UpdatedAt = DateTime.Now };
Assert.True(a.Equals(b)); // UpdatedAt is ignored
```

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTCC0201 | Warning | Type must be partial |
| BTCC0202 | Warning | No public properties found for equality comparison |

---

## CompareTo

Generates `IComparable<T>` and relational operators using properties marked with `[CompareKey]`.

### Source

```csharp
[GenerateCompareTo]
public partial class PersonData
{
    [CompareKey(Order = 1)]
    public string LastName { get; init; } = default!;

    [CompareKey(Order = 2)]
    public string FirstName { get; init; } = default!;

    public int Age { get; init; }
}
```

### Attribute options

| Property | Default | Description |
|---|---|---|
| `GenerateOperators` | `true` | Emit `<`, `>`, `<=`, `>=` operators |

### Result

```csharp
var a = new PersonData { LastName = "Adams", FirstName = "Alice" };
var b = new PersonData { LastName = "Zorn",  FirstName = "Bob"   };
Assert.True(a < b);
```

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTCC0501 | Warning | Type must be partial |
| BTCC0502 | Warning | No `[CompareKey]` properties found |

---

## DeepClone

Generates a `DeepClone()` method.
The target type **must implement `IDeepCloneable<T>`**.

### Source

```csharp
[GenerateDeepClone]
public partial class DocumentData : IDeepCloneable<DocumentData>
{
    public string Title { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public int[] Scores { get; set; } = [];
    public AuthorData Owner { get; set; } = default!;

    [ShallowClone]   // copy reference as-is
    public object? ExtraRef { get; set; }

    [CloneIgnore]    // omit from clone entirely
    public int CacheKey { get; set; }
}

[GenerateDeepClone]
public partial class AuthorData : IDeepCloneable<AuthorData>
{
    public string Name { get; set; } = default!;
}
```

### Clone strategy per property type

| Type | Strategy |
|---|---|
| Value type / `string` | Direct copy |
| `IDeepCloneable<T>` | `.DeepClone()` |
| Array | `Array.Clone()` |
| `List<T>` | `new List<T>(original)` |
| Other reference | Shallow (with `[ShallowClone]`) |

### Result

```csharp
var clone = doc.DeepClone();
clone.Tags.Add("new");
Assert.Equal(2, doc.Tags.Count);  // original unchanged
```

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTCC0301 | Warning | Type must be partial |
| BTCC0302 | Warning | Type must implement `IDeepCloneable<T>` |
| BTCC0303 | Warning | Property type does not support deep clone; use `[ShallowClone]` to suppress |

---

## DelegateTo

Generates forwarding members that delegate method and property calls to an attributed field or property.

### Source

```csharp
public interface ISimpleService
{
    string GetMessage();
    void Reset();
    int Count { get; set; }
}

[GenerateDelegateTo]
public partial class LoggingService : ISimpleService
{
    [DelegateTo]
    private readonly SimpleServiceImpl _inner = new();
}
```

### Result

```csharp
ISimpleService svc = new LoggingService();
svc.Count = 5;
Assert.Equal("Hello-5", svc.GetMessage());
```

The generator will not emit a member if the containing type already defines it, allowing manual overrides.

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTCC0401 | Warning | Type must be partial |
| BTCC0402 | Warning | No `[DelegateTo]` field or property found |
| BTCC0403 | Warning | `InterfaceType` must be an interface implemented by the delegate member type |

