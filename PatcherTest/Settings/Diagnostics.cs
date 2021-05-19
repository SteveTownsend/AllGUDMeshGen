using System.Collections.Generic;
using AllGUD;

namespace PatcherTest
{
    public class Diagnostics : IDiagnostics, IConfigErrors
    {
        public string LogFile { get; set; } = "";
        public bool DetailedLog { get; set; } = false;

        private Logger? _logger;
        public Logger logger
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

        public List<string> GetConfigErrors()
        {
            return new List<string>();
        }
    }
}
