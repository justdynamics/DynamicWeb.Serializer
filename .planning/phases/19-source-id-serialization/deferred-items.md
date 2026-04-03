# Deferred Items - Phase 19

## Pre-existing Test Compilation Errors

The test project `DynamicWeb.Serializer.Tests` has 31 pre-existing compile errors in SqlTable provider test files:

- `Providers/SqlTable/IdentityResolutionTests.cs` - CS0029: string[] to List<string> conversion errors
- `Providers/SqlTable/FlatFileStoreTests.cs` - CS0029: string[] to List<string> conversion errors
- `Providers/SqlTable/SqlTableProviderDeserializeTests.cs` - CS0854: expression tree optional arguments errors

These errors prevent `dotnet test` from running ANY tests in the project. They are not caused by Phase 19 changes.

**Impact:** New tests written in 19-01 cannot be executed until these pre-existing errors are fixed.
**Recommendation:** Fix the SqlTable test files before Phase 20 execution.
