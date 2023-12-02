namespace Kehlet.Functional.Generators.UnionGenerator.Sample;

[AutoClosed(true)]
public partial record TokenKind
{
    partial record Name(string Value);

    partial record Age(int Value);

    static partial class Cons
    {
        public static TokenKind NewAge(long age) => NewAge((int) age);
    }
}
