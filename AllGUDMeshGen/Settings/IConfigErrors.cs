using System;
using System.Collections.Generic;

namespace AllGUD
{
    public class IConfigErrors
    {
        virtual public List<string> GetConfigErrors()
        {
            throw new InvalidOperationException("must override");
        }
    }
}
