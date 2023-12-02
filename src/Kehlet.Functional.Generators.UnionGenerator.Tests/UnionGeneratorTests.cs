using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;
using static VerifyXunit.Verifier;

namespace Kehlet.Functional.Generators.UnionGenerator.Tests;

[UsesVerify]
public class UnionGeneratorTests
{
    private const string UnionClassText = """
        namespace TestNamespace;
        
        [Kehlet.Functional.Union(true)]
        public partial record TokenKind
        {
            partial record Number(string Value);
            partial record Plus;
            partial record Minus;
        }
        
        [Kehlet.Functional.Union]
        public partial class Option<TValue>
        {
            partial class Some(TValue Value);
            partial class None;
        }
        
        """;

    [Fact]
    public Task Driver()
    {
        var driver = GeneratorDriver();
        return Verify(driver);
    }

    [Fact]
    public Task RunResults()
    {
        var driver = GeneratorDriver();

        var runResults = driver.GetRunResult();
        return Verify(runResults);
    }

    [Fact]
    public Task RunResult()
    {
        var driver = GeneratorDriver();

        var runResult = driver.GetRunResult().Results.Single();
        return Verify(runResult);
    }

    private static GeneratorDriver GeneratorDriver()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Generators.UnionGenerator),
            new[] { CSharpSyntaxTree.ParseText(UnionClassText) },
            new[]
            {
                // To support 'System.Attribute' inheritance, add reference to 'System.Private.CoreLib'.
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            }
        );
        var generator = new UnionGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation);
    }
}
