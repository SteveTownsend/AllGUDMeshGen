using System;
using System.Collections.Generic;

namespace AllGUD
{
    public class IMeshes : IConfigErrors
    {
        virtual public string InputFolder { get; set; } = "";
        virtual public string OutputFolder { get; set; } = "";
        virtual public bool MirrorStaves { get; set; } = true;
        virtual public List<string> NifBlackList { get; set; } = new();
        virtual public List<string> NifWhiteList { get; set; } = new();
        virtual public bool IsNifValid(string nifPath)
        {
            throw new InvalidOperationException("must override");
        }
    }
}
