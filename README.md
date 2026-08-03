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

Generates a `ToString()` implementation. Collections are expanded and `null` is written as a literal so that the output is useful for logging. The format is configured per project with MSBuild properties, and `Style` set to `Record` switches the output to be identical to the one the compiler generates for a `record`.

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

### Project settings

The format is configured with MSBuild properties named `CommonCodeGeneratorToString` + the option name. `Style` selects a preset and the individual properties override it.

```xml
<PropertyGroup>
  <CommonCodeGeneratorToStringStyle>Record</CommonCodeGeneratorToStringStyle>
  <CommonCodeGeneratorToStringBracket>Parenthesis</CommonCodeGeneratorToStringBracket>
  <CommonCodeGeneratorToStringCollectionLimit>10</CommonCodeGeneratorToStringCollectionLimit>
</PropertyGroup>
```

| Option | Values | `Default` preset | `Record` preset |
|---|---|---|---|
| `Style` | `Default` / `Record` | — | — |
| `TypeName` | `None` / `Simple` / `Full` | `Simple` | `Simple` |
| `TypeArgument` | `None` / `Include` | `Include` | `None` |
| `Null` | `Empty` / `Literal` | `Literal` | `Empty` |
| `NullLiteral` | any string | `null` | `null` |
| `Collection` | `Raw` / `Expand` | `Expand` | `Raw` |
| `CollectionLimit` | `-1` = unlimited, the rest becomes `...` | `-1` | `-1` |
| `Members` | `Property` / `PropertyAndField` | `Property` | `PropertyAndField` |
| `Bracket` | `None` / `Brace` / `Parenthesis` / `Square` / `Angle` | `Brace` | `Brace` |
| `OpenBracket` / `CloseBracket` | any string, takes precedence over `Bracket` | — | — |
| `InnerSpace` | `None` / `Space` | `Space` | `Space` |
| `TypeNameSpace` | `None` / `Space` | `Space` | `Space` |
| `Separator` | any string | `", "` | `", "` |
| `Assign` | any string | `" = "` | `" = "` |
| `CollectionBracket` | same as `Bracket` | `Square` | `Square` |
| `CollectionOpenBracket` / `CollectionCloseBracket` | any string | — | — |
| `CollectionInnerSpace` | `None` / `Space` | `None` | `None` |
| `CollectionSeparator` | any string | `", "` | `", "` |

An unset property inherits the value from the preset. The rules that are not part of a preset are always applied: base type members come first, static members and indexers are excluded, and the inner space is collapsed when there is no member to write.

MSBuild trims the surrounding whitespace of a property value, so wrap a string value in double quotes to keep it. The outer quotes are removed and the rest is used as is.

```xml
<PropertyGroup>
  <CommonCodeGeneratorToStringSeparator>" | "</CommonCodeGeneratorToStringSeparator>
</PropertyGroup>
```

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

Angle brackets need to be XML escaped in a csproj, for example `<CommonCodeGeneratorToStringOpenBracket>&lt;&lt;</CommonCodeGeneratorToStringOpenBracket>`.

### Member attributes

`[IgnoreToString]` excludes a member from the output. `[ToStringFormat]` controls how the value of a single member is written, and both can be applied to properties and fields.

| Property | Type | Description |
|---|---|---|
| `Format` | `string` | Format string applied to the value, given as the positional argument |
| `MaxLength` | `int` | Truncate the formatted value to the given length, `0` = unlimited |
| `MaskChar` | `char` | Character repeated over the whole value |
| `MaskPattern` | `string` | Text written instead of the value, see below |

```csharp
[GenerateToString]
public partial class User
{
    public int Id { get; set; }

    [IgnoreToString]
    public int Revision { get; set; }                     // not written

    [ToStringFormat(MaskChar = '*')]
    public string Password { get; set; } = default!;      // Password = ****** (same length)

    [ToStringFormat(MaskPattern = "***")]
    public string Answer { get; set; } = default!;        // Answer = ***

    [ToStringFormat(MaskPattern = "[REDACTED]")]
    public string Secret { get; set; } = default!;        // Secret = [REDACTED]

    [ToStringFormat(MaskPattern = "***##")]
    public string Token { get; set; } = default!;         // Token = ***34

    [ToStringFormat(MaskPattern = "####****####")]
    public string Card { get; set; } = default!;          // Card = 4111****1111

    [ToStringFormat("yyyy-MM-dd")]
    public DateTime BirthDate { get; set; }               // BirthDate = 2020-01-02

    [ToStringFormat(MaxLength = 20)]
    public string Description { get; set; } = default!;   // truncated to 20 chars

    [ToStringFormat("000000", MaxLength = 3)]
    public int Number { get; set; }                       // 7 -> "000007" -> "000"
}
```

#### Masking

`MaskChar` is the simple form: the whole value becomes that character repeated over its length. `MaskPattern` is the pattern form: a leading or trailing run of `#` keeps that many characters of the original value visible, and the part between the two runs is written as is. The pattern is parsed at generation time, so there is no runtime cost.

| Setting | Value | Output |
|---|---|---|
| `MaskChar = '*'` | `secret` | `******` |
| `MaskChar = '.'` | `abc` | `...` |
| `MaskPattern = "***"` | `secret` | `***` |
| `MaskPattern = "[REDACTED]"` | `secret` | `[REDACTED]` |
| `MaskPattern = "***##"` | `abcd1234` | `***34` |
| `MaskPattern = "####****####"` | `4111111111111111` | `4111****1111` |

`MaskChar` preserves the length of the value while `MaskPattern` does not, so use `MaskPattern` when the length itself must not leak. `MaskPattern` wins when both are set.

A value not longer than the total kept length is written as the mask text only, so a short value never leaks an original character. A `MaskPattern` without `#` does not stringify the value at all and only the null check remains.

#### Combining the settings

`Format`, masking and `MaxLength` form a pipeline in that order, so any combination of them works.

| Setting | Value | Format | Mask | MaxLength |
|---|---|---|---|---|
| `[ToStringFormat("000000", MaskChar = '*', MaxLength = 4)]` | `7` | `000007` | `******` | `****` |
| `[ToStringFormat(MaskPattern = "####****####", MaxLength = 8)]` | `4111111111111111` | — | `4111****1111` | `4111****` |
| `[ToStringFormat("000000", MaxLength = 3)]` | `7` | `000007` | — | `000` |

The masked length is known at generation time, so the truncation costs nothing at runtime.

Collection expansion is an alternative rendering rather than a stage of this pipeline: masking overrides it, so the elements of a masked collection are never written, and `CollectionLimit` plays the role of `MaxLength` inside an expanded collection. A `null` value follows the `Null` setting and is neither masked nor truncated.

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
