// Types for OpenApiSchemasTests' schema names: a public type and an internal one of the same name, in two namespaces.
namespace GrifballWebApp.Server.Test.SchemaNames.Public
{
    /// <summary>A DTO the API could send.</summary>
    public sealed class HiddenTwin;
}

namespace GrifballWebApp.Server.Test.SchemaNames.Internal
{
    /// <summary>A helper no schema can be made from: it must not make the public one's name ambiguous.</summary>
    internal sealed class HiddenTwin;
}
