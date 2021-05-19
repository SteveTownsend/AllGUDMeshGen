using System.Diagnostics.CodeAnalysis;

namespace AllGUD
{
    public class ISettings : IConfigErrors
    {
        [NotNull]
        virtual public IDiagnostics? diagnostics { get; set; }
        [NotNull]
        virtual public ISkeleton? skeleton { get; set; }
        [NotNull]
        virtual public IMeshes? meshes { get; set; }
    }
}
