Companion source generator to `ExhaustiveMatching.Analyzer`  
Use `Kehlet.Functional.UnionAttribute` on the enclosing type to close the union and apply `ExhaustiveMatching.ClosedAttribute` for the analyzer.  
- Containing type will be marked abstract and get a private constructor.
- Contained types will be marked public and sealed.
- Contained types will inherit from the containing type.
- Containing type will have ClosedAttribute applied for all contained types.
