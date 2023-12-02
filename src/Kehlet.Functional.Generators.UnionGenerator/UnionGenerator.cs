using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Kehlet.Functional.Generators.UnionGenerator;

[Generator]
public class UnionGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "Kehlet.Functional";
    private const string AttributeName = "AutoClosedAttribute";
    private const string AttributeFullName = $"{AttributeNamespace}.{AttributeName}";
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(x => x.AddSource($"{AttributeFullName}.g.cs", SourceText.From(Attribute, Encoding.UTF8)));

        var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeFullName,
            Filter,
            Transform
        );

        context.RegisterSourceOutput(provider, Execute);
    }

    private static bool Filter(SyntaxNode node, CancellationToken _) =>
        node.As<TypeDeclarationSyntax>().Modifiers.Any(token => token.IsKind(SyntaxKind.PartialKeyword));

    private static Data Transform(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        var serializable = context.Attributes.First().ConstructorArguments.FirstOrDefault().Value is true;

        var typeIdentifier = context.TargetNode.As<TypeDeclarationSyntax>()
                                    .Apply(x => x.Identifier + x.TypeParameterList?.ToString());

        var openTypeIdentifier = context.TargetNode.As<TypeDeclarationSyntax>()
                                        .Apply(x => x.Identifier + (x.Arity > 0 ? "<" + new string(',', x.Arity - 1) + ">" : ""));

        var classSymbol = context.TargetSymbol.As<INamedTypeSymbol>();

        var query = from member in classSymbol.GetTypeMembers()
                    let syntax = member.GetSyntax()
                    where syntax.IsPartial() && !syntax.IsStatic()
                    select new CaseType(member.Name, GetCaseDeclaration(syntax), GetArgs(member));

        var members = query.ToImmutableArray();

        var declaration = context.TargetNode.As<TypeDeclarationSyntax>()
                                 .Apply(GetUnionDeclaration);

        var ns = classSymbol.ContainingNamespace.ToString();

        return new(classSymbol.Name, typeIdentifier, openTypeIdentifier, declaration, ns, members, serializable);

        static ImmutableArray<CaseTypeArg> GetArgs(INamedTypeSymbol symbol)
        {
            var constructor = symbol.Constructors.FirstOrDefault(x => x.DeclaredAccessibility is Accessibility.Public);
            if (constructor is null)
            {
                return ImmutableArray<CaseTypeArg>.Empty;
            }

            return constructor.Parameters
                              .Select(x => new CaseTypeArg(x.Type.ToString(), x.Name))
                              .ToImmutableArray();
        }

        static string GetUnionDeclaration(TypeDeclarationSyntax syntax)
        {
            var builder = new StringBuilder();

            if (syntax.Modifiers.Any(x => x.IsKind(SyntaxKind.AbstractKeyword)) is false)
            {
                builder.Append("abstract ");
            }

            builder.Append(syntax.GetDeclarationSyntax());
            builder.Append(syntax.TypeParameterList);

            return builder.ToString();
        }

        static string GetCaseDeclaration(TypeDeclarationSyntax syntax)
        {
            var builder = new StringBuilder();

            if (syntax.Modifiers.Any(x => x.IsKind(SyntaxKind.PublicKeyword)) is false)
            {
                builder.Append("public ");
            }

            if (syntax.Modifiers.Any(x => x.IsKind(SyntaxKind.SealedKeyword)) is false)
            {
                builder.Append("sealed ");
            }

            builder.Append(syntax.GetDeclarationSyntax());
            builder.Append(syntax.TypeParameterList);

            return builder.ToString();
        }
    }

    private static void Execute(SourceProductionContext context, Data data)
    {
        var types = data.NestedTypes
                        .Select(x => $"typeof({data.OpenTypeIdentifier}.{x.Name})")
                        .Apply(x => string.Join(", ", x));

        var memberQuery = from t in data.NestedTypes
                          let s = $"    {t.Declaration}: {data.TypeIdentifier}" + (data.Serializable ? EmitDiscriminator(t.Name) : ";")
                          group s by true
                          into g
                          select string.Join("\r\n\r\n", g);

        var members = memberQuery.FirstOrDefault();

        var consQuery = from t in data.NestedTypes
                        let s = Cons(data.TypeIdentifier, t)
                        group s by true
                        into g
                        select string.Join("\r\n\r\n", g);

        var cons = consQuery.FirstOrDefault();

        var jsonAttr = data.Serializable ? EmitJsonConverterAttribute("UnionJsonConverter") : "";
        var conv = data.Serializable ? EmitJsonConverter(data.Name, data.NestedTypes.Select(x => x.Name).ToImmutableArray()) : "";

        var code = $$"""
            // <auto-generated/>

            using System;
            using ExhaustiveMatching;
            using System.Text.Json;
            using System.Text.Json.Serialization;

            namespace {{data.Namespace}};

            {{jsonAttr}}[Closed({{types}})]
            {{data.Declaration}}
            {
                private {{data.Name}}() { }

            {{members}}
            
                public static partial class Cons
                {
            {{cons}}
                }
            }

            {{conv}}
            """;

        var sourceText = SourceText.From(code, Encoding.UTF8);
        context.AddSource($"{data.Name}.g.cs", sourceText);

        static string Cons(string unionName, CaseType type)
        {
            var builder = new StringBuilder();
            builder.Append($"        public static {unionName} New{type.Name}");

            if (type.Args.IsDefaultOrEmpty)
            {
                builder.Append($$""" { get; } = new {{type.Name}}();""");
            }
            else
            {
                var p = type.Args.Select(x => $"{x.Type} {x.Name}").Apply(x => string.Join(", ", x));
                var a = type.Args.Select(x => $"{x.Name}").Apply(x => string.Join(", ", x));
                builder.Append($"({p}) => new {type.Name}({a});");
            }

            return builder.ToString();
        }

        static string EmitDiscriminator(string name)
        {
            return $$"""
                
                    {
                        public string Kind { get; } = nameof({{name}});
                    }
                """;
        }

        static string EmitJsonConverterAttribute(string converterName)
        {
            return $"[JsonConverter(typeof({converterName}))]\r\n";
        }

        static string EmitJsonConverter(string typeName, ImmutableArray<string> caseTypes)
        {
            var sw = from type in caseTypes
                     select $"            nameof({typeName}.{type}) => JsonSerializer.Deserialize<{typeName}.{type}>(root.GetRawText(), options)!,";

            var str = string.Join("\r\n", sw);

            return $$"""
                file class UnionJsonConverter : JsonConverter<{{typeName}}>
                {
                    public override {{typeName}} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                    {
                        using var jsonDoc = JsonDocument.ParseValue(ref reader);
                        var root = jsonDoc.RootElement;
                
                        if (!root.TryGetProperty("kind", out var typeProp) &&
                            !root.TryGetProperty("Kind", out typeProp))
                        {
                            throw new JsonException("Kind property is missing.");
                        }
                
                        var kind = typeProp.GetString();
                        return kind switch
                        {
                {{str}}
                            _ => throw new JsonException($"Unknown kind: {kind}")
                        };
                    }
                
                    public override void Write(Utf8JsonWriter writer, {{typeName}} value, JsonSerializerOptions options)
                    {
                        var type = value.GetType();
                        writer.WriteStartObject();
                
                        foreach (var prop in type.GetProperties())
                        {
                            writer.WritePropertyName(prop.Name);
                            JsonSerializer.Serialize(writer, prop.GetValue(value), prop.PropertyType, options);
                        }
                
                        writer.WriteEndObject();
                    }
                }
                """;
        }
    }

    private record Data(string Name, string TypeIdentifier, string OpenTypeIdentifier, string Declaration, string Namespace, ImmutableArray<CaseType> NestedTypes, bool Serializable);

    private record CaseTypeArg(string Type, string Name);

    private record CaseType(string Name, string Declaration, ImmutableArray<CaseTypeArg> Args);

    private const string Attribute = $$"""
        using System;

        namespace {{AttributeNamespace}};

        [AttributeUsage(AttributeTargets.Class)]
        public class {{AttributeName}}(bool serializable = false) : Attribute
        {
            public bool IsSerializable => serializable;
        }

        """;
}
