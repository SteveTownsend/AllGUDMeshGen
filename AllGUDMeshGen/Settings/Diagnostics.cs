using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mutagen.Bethesda.Synthesis.Settings;

namespace AllGUD
{
    public class Diagnostics : IDiagnostics
    {
        [SynthesisSettingName("Log File")]
        [SynthesisTooltip("This must be a valid filename on a valid path for your PC. Use / separators between path components. Leave blank to just get Console output.")]
        [SynthesisDescription("Name of log file for diagnostic output.")]
        override public string LogFile { get; set; } = "";
        [SynthesisSettingName("Detailed Output")]
        [SynthesisTooltip("Set true to include detailed output on transformed mesh block handling. Typically, false is adequate and preferable.")]
        [SynthesisDescription("Flag to trigger more verbose output to console and file.")]
        override public bool DetailedLog { get; set; } = false;

        private Logger? _logger;
        [NotNull]
        override public Logger logger
        {
            get
            {
                if (_logger == null)
                {

                    _logger = new Logger(LogFile);
                    if (!string.IsNullOrEmpty(LogFile))
                    {
                        _logger.WriteLine("Recording progress in log file {0} as well as to console", LogFile);
                    }
                }
                return _logger;
            }
        }

        override public List<string> GetConfigErrors()
        {
            List<string> errors = new List<string>();
            try
            {
                LogFile = Helper.EnsureOutputFileIsValid(LogFile);
            }
            catch (Exception e)
            {
                errors.Add(e.GetBaseException().ToString());
            }
            return errors;
        }
    }
}
