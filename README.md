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

### Source

```csharp
[GenerateToString]
public partial class Data
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public int[] Values { get; set; } = default!;

    [IgnoreToString]
    public int Ignore { get; set; }
}
```

### Result

```csharp
var data = new Data { Id = 123, Name = "xyz", Values = [1, 2] };
var str = data.ToString();
Assert.Equal("{ Id = 123, Name = xyz, Values = [1, 2] }", str);
```

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
| `CallBase` | `false` | Call `base.Equals` / `base.GetHashCode` |

### Result

```csharp
var a = new OrderData { Id = 1, Name = "x" };
var b = new OrderData { Id = 1, Name = "x", UpdatedAt = DateTime.Now };
Assert.True(a.Equals(b)); // UpdatedAt is ignored
```

### Diagnostics

| ID | Severity | Description |
|---|---|---|
| BTTS0101 | Warning | Type must be partial |
| BTTS0102 | Warning | No public properties found for equality comparison |

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
| BTTS0401 | Warning | Type must be partial |
| BTTS0402 | Warning | No `[CompareKey]` properties found |

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
| BTTS0201 | Warning | Type must be partial |
| BTTS0202 | Warning | Type must implement `IDeepCloneable<T>` |
| BTTS0203 | Warning | Property type does not support deep clone; use `[ShallowClone]` to suppress |

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
| BTTS0301 | Warning | Type must be partial |
| BTTS0302 | Warning | No `[DelegateTo]` field or property found |


### Source

```csharp
[GenerateToString]
public partial class Data
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public int[] Values { get; set; } = default!;

    [IgnoreToString]
    public int Ignore { get; set; }
}
```

### Result

```csharp
var data = new Data { Id = 123, Name = "xyz", Values = [1, 2] };
var str = data.ToString();
Assert.Equal("{ Id = 123, Name = xyz, Values = [1, 2] }", str);
```

