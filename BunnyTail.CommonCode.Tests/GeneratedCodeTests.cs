namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

public class GeneratedCodeTests
{
    // ------------------------------------------------------------
    // Member selection
    // ------------------------------------------------------------

    [Fact]
    public void StaticMembersAreNotUsed()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            [GenerateToString]
            public partial class Money
            {
                public static Money Zero { get; } = new();

                public decimal Amount { get; set; }
            }

            [GenerateDeepClone]
            public partial class Settings : IDeepCloneable<Settings>
            {
                public static int Version { get; set; }

                public int Value { get; set; }
            }

            public class KeyedBase
            {
                [CompareKey]
                public int Id { get; set; }
            }

            [GenerateCompareTo]
            public partial class KeyedDerived : KeyedBase
            {
                [CompareKey]
                public static int Bias { get; set; }

                [CompareKey(Order = 1)]
                public string Name { get; set; } = "";
            }

            [GenerateCompareTo]
            public partial class KeyedByField
            {
                [CompareKey]
                public int Id;
            }

            public static class Program
            {
                public static string Run() =>
                    new Money { Amount = 1 }.Equals(new Money { Amount = 1 }) + "/" +
                    new Settings { Value = 3 }.DeepClone().Value + "/" +
                    new KeyedDerived { Id = 2, Name = "a" }.CompareTo(new KeyedDerived { Id = 1, Name = "b" }) + "/" +
                    new KeyedByField { Id = 1 }.CompareTo(new KeyedByField { Id = 2 });
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("True/3/1/-1", result);
    }

    [Fact]
    public void MemberHiddenByDerivedDeclarationIsNotUsed()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public class Base
            {
                public int Value { get; set; }

                public string Label { get; set; } = "";
            }

            [GenerateToString]
            public partial class DerivedStatic : Base
            {
                public static new int Value { get; set; }
            }

            [GenerateEquality]
            public partial class DerivedField : Base
            {
                public new long Label;
            }

            public static class Program
            {
                public static string Run() =>
                    new DerivedStatic { Label = "x" } + "/" +
                    new DerivedField { Value = 1, Label = 1 }.Equals(new DerivedField { Value = 1, Label = 2 });
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("DerivedStatic { Label = x }/True", result);
    }

    [Fact]
    public void KeywordNamesAreEscaped()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            [GenerateEquality]
            public partial class Style
            {
                public string @class { get; set; } = "";
            }

            public interface IHandler
            {
                string Handle(string @event);
            }

            public sealed class Handler : IHandler
            {
                public string Handle(string @event) => "handled:" + @event;
            }

            [GenerateDelegateTo]
            public partial class HandlerFacade : IHandler
            {
                [DelegateTo]
                private readonly IHandler @base = new Handler();
            }

            public static class Program
            {
                public static string Run() =>
                    new Style { @class = "x" } + "/" + new Style { @class = "x" }.Equals(new Style { @class = "x" }) + "/" + new HandlerFacade().Handle("e");
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Style { class = x }/True/handled:e", result);
    }

    [Fact]
    public void NullableTypeArgumentsAreKept()
    {
        // Arrange
        const string source =
            """
            #nullable enable
            using System.Collections.Generic;
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public partial class Doc
            {
                public Dictionary<string, object?> Values { get; init; } = new();

                public List<string?> Tags { get; init; } = new();
            }

            [GenerateDeepClone]
            public partial class Draft : IDeepCloneable<Draft>
            {
                public List<string?>? Tags { get; set; } = new();

                public string?[] Names { get; set; } = [];
            }

            [GenerateCompareTo]
            public partial class Key
            {
                [CompareKey]
                public string? Name { get; set; }
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // ToString
    // ------------------------------------------------------------

    [Fact]
    public void ToStringOfValueTypesIsNotNullChecked()
    {
        // Arrange
        const string source =
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Shapes
            {
                public (int X, int Y) Point { get; set; }

                public KeyValuePair<string, int> Pair { get; set; }

                public ImmutableArray<int> Items { get; set; } = ImmutableArray<int>.Empty;
            }

            [GenerateToString]
            public partial class Config
            {
                public Dictionary<string, int> Values { get; set; } = new() { ["a"] = 1 };

                public List<(int X, int Y)> Points { get; set; } = [(1, 2)];
            }

            public static class Program
            {
                public static string Run() => new Shapes { Point = (1, 2), Pair = new("a", 1) } + "/" + new Config();
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Shapes { Point = (1, 2), Pair = [a, 1], Items = [] }/Config { Values = [[a, 1]], Points = [(1, 2)] }", result);
    }

    [Fact]
    public void ToStringLiteralWithControlCharacterIsEscaped()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Masked
            {
                [ToStringFormat(MaskPattern = "a\nb")]
                public string Secret { get; set; } = "";
            }

            public static class Program
            {
                public static string Run() => new Masked().ToString();
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Masked { Secret = a\nb }", result);
    }

    // ------------------------------------------------------------
    // DeepClone
    // ------------------------------------------------------------

    [Fact]
    public void DeepCloneOfDerivedTypeCopiesBaseMembersAndIsVirtual()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Person : IDeepCloneable<Person>
            {
                public string Name { get; set; } = "";
            }

            [GenerateDeepClone]
            public partial class Employee : Person, IDeepCloneable<Employee>
            {
                public string Company { get; set; } = "";
            }

            public static class Program
            {
                public static string Run()
                {
                    var employee = new Employee { Name = "Alice", Company = "Contoso" };
                    var clone = employee.DeepClone();
                    Person person = employee;
                    var viaBase = person.DeepClone();
                    return clone.Name + "/" + clone.Company + "/" + viaBase.GetType().Name + "/" + ((Employee)viaBase).Company;
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Alice/Contoso/Employee/Contoso", result);
    }

    [Fact]
    public void DeepCloneSetsRequiredAndInitMembersInInitializer()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Customer : IDeepCloneable<Customer>
            {
                public required string Name { get; set; }

                public int Age { get; set; }

                public string Code { get; init; } = "";
            }

            public static class Program
            {
                public static string Run()
                {
                    var clone = new Customer { Name = "Bob", Age = 3, Code = "c1" }.DeepClone();
                    return clone.Name + "/" + clone.Age + "/" + clone.Code;
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Bob/3/c1", result);
    }

    [Fact]
    public void DeepCloneCopiesElementsOfGetOnlyList()
    {
        // Arrange
        const string source =
            """
            using System;
            using System.Collections.Generic;
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Basket : IDeepCloneable<Basket>
            {
                public List<string> Items { get; } = ["initial"];
            }

            public static class Program
            {
                public static string Run()
                {
                    var basket = new Basket();
                    basket.Items.Clear();
                    basket.Items.Add("apple");
                    basket.Items.Add("pear");
                    var clone = basket.DeepClone();
                    clone.Items.Add("x");
                    return String.Join(",", clone.Items) + "/" + String.Join(",", basket.Items);
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("apple,pear,x/apple,pear", result);
    }

    [Fact]
    public void DeepCloneOfConstrainedTypeParameterChecksNull()
    {
        // Arrange
        const string source =
            """
            using System;
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Leaf : IDeepCloneable<Leaf>
            {
                public int Id { get; set; }
            }

            [GenerateDeepClone]
            public partial class Box<T> : IDeepCloneable<Box<T>>
                where T : IDeepCloneable<T>
            {
                public T Value { get; set; } = default!;
            }

            public static class Program
            {
                public static string Run()
                {
                    var empty = new Box<Leaf>().DeepClone();
                    var leaf = new Leaf { Id = 1 };
                    var full = new Box<Leaf> { Value = leaf }.DeepClone();
                    return (empty.Value is null) + "/" + full.Value.Id + "/" + ReferenceEquals(full.Value, leaf);
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("True/1/False", result);
    }

    [Fact]
    public void DeepClonePropertyOfDerivedTypeIsCopiedShallow()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Node : IDeepCloneable<Node>
            {
                public string Name { get; set; } = "";
            }

            public class SpecialNode : Node
            {
            }

            [GenerateDeepClone]
            public partial class Tree : IDeepCloneable<Tree>
            {
                public SpecialNode Root { get; set; } = new();
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Equal(["BTCC0303"], problems);
    }

    // ------------------------------------------------------------
    // DelegateTo
    // ------------------------------------------------------------

    [Fact]
    public void DelegateToRepeatsSignature()
    {
        // Arrange
        const string source =
            """
            using System;
            using System.Collections.Generic;
            using System.Diagnostics.CodeAnalysis;
            using System.Linq;
            using BunnyTail.CommonCode;

            namespace Test;

            public interface IMisc
            {
                string? Find(string? key);

                bool TryGet(string key, [NotNullWhen(true)] out string? value);

                T Create<T>()
                    where T : class, new();

                int Sum(params int[] values);

                string Log(string message, int level = 0);

                int Peek(ref readonly int value);

                event EventHandler? Changed;
            }

            public sealed class Misc : IMisc
            {
                public string? Find(string? key) => key;

                public bool TryGet(string key, [NotNullWhen(true)] out string? value)
                {
                    value = key;
                    return true;
                }

                public T Create<T>()
                    where T : class, new() => new();

                public int Sum(params int[] values) => values.Sum();

                public string Log(string message, int level = 0) => message + ":" + level;

                public int Peek(ref readonly int value) => value;

                public event EventHandler? Changed;

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }

            [GenerateDelegateTo]
            public partial class MiscFacade : IMisc
            {
                [DelegateTo]
                private readonly Misc inner = new();

                public void Raise() => inner.Raise();
            }

            public static class Program
            {
                public static string Run()
                {
                    var facade = new MiscFacade();
                    var raised = 0;
                    facade.Changed += (_, _) => raised++;
                    facade.Raise();
                    var value = 5;
                    return (facade.Find(null) is null) + "/" + facade.TryGet("k", out var found) + found + "/" + facade.Create<List<int>>().Count + "/" +
                           facade.Sum(1, 2, 3) + "/" + facade.Log("m") + "/" + facade.Peek(in value) + "/" + raised;
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("True/Truek/0/6/m:0/5/1", result);
    }

    [Fact]
    public void DelegateToCallsExplicitAndDefaultImplementationThroughInterface()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public interface IGreeter
            {
                string Hello();

                string Bye() => "bye";

                static string Describe() => "greeter";
            }

            public sealed class Greeter : IGreeter
            {
                string IGreeter.Hello() => "hello";
            }

            [GenerateDelegateTo]
            public partial class GreeterFacade : IGreeter
            {
                [DelegateTo]
                private readonly Greeter inner = new();
            }

            public static class Program
            {
                public static string Run()
                {
                    var facade = new GreeterFacade();
                    IGreeter greeter = facade;
                    return facade.Hello() + "/" + facade.Bye() + "/" + greeter.Bye() + "/" + IGreeter.Describe();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("hello/bye/bye/greeter", result);
    }

    [Fact]
    public void DelegateToImplementsMemberDifferingOnlyInTypeExplicitly()
    {
        // Arrange
        const string source =
            """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using BunnyTail.CommonCode;

            namespace Test;

            public sealed class Counter : IEnumerator, IEnumerator<int>
            {
                private int current;

                public int Current => current;

                object IEnumerator.Current => current;

                public bool MoveNext() => ++current < 3;

                public void Reset() => current = 0;

                public void Dispose()
                {
                }
            }

            [GenerateDelegateTo]
            public partial class CounterFacade : IEnumerator<int>
            {
                [DelegateTo]
                private readonly Counter inner = new();
            }

            [GenerateDelegateTo]
            public partial class Tags : IEnumerable<string>
            {
                [DelegateTo(InterfaceType = typeof(IEnumerable<string>))]
                private readonly List<string> items = ["a", "b"];
            }

            [GenerateDelegateTo]
            public partial class Names
            {
                [DelegateTo(InterfaceType = typeof(IReadOnlyList<string>))]
                private readonly List<string> items = ["a", "b"];
            }

            public static class Program
            {
                public static string Run()
                {
                    var counter = new CounterFacade();
                    counter.MoveNext();
                    IEnumerator enumerator = counter;
                    var tags = new List<string>();
                    foreach (var tag in (IEnumerable)new Tags())
                    {
                        tags.Add((string)tag);
                    }

                    var names = new Names();
                    return counter.Current + "/" + enumerator.Current + "/" + String.Join(",", tags) + "/" + names[1] + names.Count;
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("1/1/a,b/b2", result);
    }

    [Fact]
    public void DelegateToGeneratesSharedMemberOnce()
    {
        // Arrange
        const string source =
            """
            using System;
            using BunnyTail.CommonCode;

            namespace Test;

            public interface IReader : IDisposable
            {
                string Read();
            }

            public interface IWriter : IDisposable
            {
                void Write(string value);
            }

            public sealed class Reader : IReader
            {
                public bool Disposed { get; private set; }

                public string Read() => "r";

                public void Dispose() => Disposed = true;
            }

            public sealed class Writer : IWriter
            {
                public bool Disposed { get; private set; }

                public string Last { get; private set; } = "";

                public void Write(string value) => Last = value;

                public void Dispose() => Disposed = true;
            }

            [GenerateDelegateTo]
            public partial class Channel : IReader, IWriter
            {
                [DelegateTo]
                private readonly IReader reader;

                [DelegateTo]
                private readonly IWriter writer;

                public Channel(IReader reader, IWriter writer)
                {
                    this.reader = reader;
                    this.writer = writer;
                }
            }

            public static class Program
            {
                public static string Run()
                {
                    var reader = new Reader();
                    var writer = new Writer();
                    var channel = new Channel(reader, writer);
                    channel.Write("x");
                    channel.Dispose();
                    return reader.Disposed + "/" + writer.Disposed + "/" + channel.Read() + "/" + writer.Last;
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("True/False/r/x", result);
    }

    [Fact]
    public void DelegateToSkipsMembersTheTypeAlreadyHas()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            #nullable disable
            public interface ILegacyStore
            {
                void Save(string key);
            }
            #nullable restore

            public interface IStore
            {
                void Set<T>(T value);

                void Move((int X, int Y) delta);
            }

            public sealed class Store : IStore, ILegacyStore
            {
                public void Set<T>(T value)
                {
                }

                public void Move((int X, int Y) delta)
                {
                }

                public void Save(string key)
                {
                }
            }

            [GenerateDelegateTo]
            public partial class StoreFacade : IStore, ILegacyStore
            {
                [DelegateTo]
                private readonly Store inner = new();

                public void Set<TValue>(TValue value)
                {
                }

                public void Save(string? key)
                {
                }
            }

            [GenerateDelegateTo]
            public partial class MoveFacade
            {
                [DelegateTo(InterfaceType = typeof(IStore))]
                private readonly Store inner = new();

                public void Move((int, int) delta)
                {
                }
            }

            public interface IService
            {
                string GetMessage();

                string Name();
            }

            public sealed class ServiceCore : IService
            {
                public string GetMessage() => "core";

                public string Name() => "core";
            }

            public abstract class ServiceBase
            {
                public virtual string GetMessage() => "base";
            }

            [GenerateDelegateTo]
            public partial class Service : ServiceBase, IService
            {
                [DelegateTo]
                private readonly IService inner = new ServiceCore();
            }

            [GenerateDelegateTo]
            public partial class Manual : IService
            {
                [DelegateTo]
                private readonly ServiceCore inner = new();

                public string GetMessage() => "manual";

                public string Name() => "manual";
            }

            [GenerateDelegateTo]
            public partial class Generic<T>
                where T : IService
            {
                [DelegateTo]
                private readonly T inner;

                public Generic(T inner)
                {
                    this.inner = inner;
                }
            }

            public static class Program
            {
                public static string Run() =>
                    ((IService)new Service()).GetMessage() + "/" + new Service().Name() + "/" + new Manual().GetMessage() + "/" + new Generic<ServiceCore>(new ServiceCore()).GetMessage();
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("base/core/manual/core", result);
    }

    // ------------------------------------------------------------
    // Ref struct
    // ------------------------------------------------------------

    [Fact]
    public void RefStructCharSpanIsWritten()
    {
        // Arrange
        const string source =
            """
            using System;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public ref partial struct Data
            {
                public ReadOnlySpan<char> Name { get; set; }

                public int Id { get; set; }
            }

            public static class Program
            {
                public static string Run() => new Data { Name = "abc".AsSpan(), Id = 1 }.ToString();
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("Data { Name = abc, Id = 1 }", result);
    }

    [Fact]
    public void EqualityOnRefStructCompares()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public ref partial struct Point
            {
                public int X { get; set; }

                public int Y { get; set; }
            }

            public static class Program
            {
                public static string Run()
                {
                    var a = new Point { X = 1, Y = 2 };
                    var b = new Point { X = 1, Y = 2 };
                    var c = new Point { X = 1, Y = 3 };
                    return $"{a.Equals(b)} {a == c} {a != c} {a.Equals(null)} {a.GetHashCode() == b.GetHashCode()}";
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("True False True False True", result);
    }

    [Fact]
    public void CompareToOnRefStructCompares()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateCompareTo]
            public ref partial struct Version
            {
                [CompareKey(Order = 1)]
                public int Major { get; set; }

                [CompareKey(Order = 2)]
                public int Minor { get; set; }
            }

            public static class Program
            {
                public static string Run()
                {
                    var a = new Version { Major = 1, Minor = 2 };
                    var b = new Version { Major = 1, Minor = 3 };
                    return $"{a.CompareTo(b)} {a < b} {a >= b}";
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Execute(source);

        // Assert
        Assert.Equal("-1 True False", result);
    }

    // ------------------------------------------------------------
    // Obsolete and generated names
    // ------------------------------------------------------------

    [Fact]
    public void ObsoleteMembersCompileWithoutWarning()
    {
        // Arrange
        const string source =
            """
            using System;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            [GenerateEquality]
            [GenerateDeepClone]
            [GenerateCompareTo]
            public partial class Data : IDeepCloneable<Data>
            {
                [CompareKey]
                public int Id { get; set; }

                [Obsolete("old")]
                [CompareKey]
                public string? Old { get; set; }

                [Obsolete("removed", true)]
                public string? Removed { get; set; }
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);
        var generated = GeneratorTestHelper.GetAllGeneratedSource(source);

        // Assert
        Assert.Empty(problems);
        Assert.Contains("this.Old", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Removed", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericTypeWithDeepCollectionEqualityCompilesWithoutWarning()
    {
        // Arrange
        const string source =
            """
            using System.Collections.Generic;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality(DeepCollectionEquality = true)]
            public partial class Data<T>
            {
                public List<T> Items { get; set; } = [];

                public HashSet<T> Set { get; set; } = [];
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }

    [Fact]
    public void SkipLocalsInitIsNotWrittenWithoutUnsafeCode()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run<ToStringGenerator>(source);

        // Assert
        Assert.Empty(result.Problems);
        Assert.DoesNotContain("SkipLocalsInit", result.AllGeneratedText, StringComparison.Ordinal);
    }
}
