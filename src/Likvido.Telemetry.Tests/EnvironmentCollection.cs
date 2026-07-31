using Xunit;

namespace Likvido.Telemetry.Tests;

/// <summary>
/// Every test class here reads or writes process-wide environment variables, so they must not run
/// concurrently with each other. xunit serialises tests within a single collection, and puts each
/// class in its own collection by default — which would let two classes in this assembly race. Naming
/// one collection and applying it to all of them is what actually makes the serialisation hold.
/// </summary>
[CollectionDefinition(Name)]
public class EnvironmentCollection
{
    public const string Name = "Environment variables";
}
