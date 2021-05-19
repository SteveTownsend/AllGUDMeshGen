namespace AllGUD
{
    public class ISkeleton : IConfigErrors
    {
        virtual public string InputFolder { get; set; } = "";
        virtual public string OutputFolder { get; set; } = "";
    }
}
