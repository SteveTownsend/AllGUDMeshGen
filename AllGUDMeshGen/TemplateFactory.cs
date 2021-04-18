using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nifly;

namespace AllGUD
{
    class TemplateFactory
    {
        // Reverse-engineered from the AllGUD file using NifSkope to introspect. Format also verified vs DawnBreaker NIF.
        // [The AllGUD file gets the Prn ExtraData wrong.]
        public static NifFile CreateSSE(string prnValue)
        {
            NifFile sseTemplate = new NifFile();
            sseTemplate.CreateNamedBSFadeNode(NiVersion.getSSE(), "AllGUDTemplate");

            // we know there is only one NiNode - add the required NiStringExtraData to it
            sseTemplate.AddStringExtraDataToNode(0, "Prn", prnValue);

            return sseTemplate;
        }
    }
}
