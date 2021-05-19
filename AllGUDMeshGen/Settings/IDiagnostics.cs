using System.Diagnostics.CodeAnalysis;

namespace AllGUD
{
    public class IDiagnostics : IConfigErrors
    {
        virtual public string LogFile { get; set; } = "";
        virtual public bool DetailedLog { get; set; } = false;
        [NotNull]
        virtual public Logger? logger { get; }
    }
}
