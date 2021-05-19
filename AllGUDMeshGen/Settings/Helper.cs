using System;
using System.IO;

namespace AllGUD
{
    class Helper
    {
        private static string AsAbsolutePath(string path)
        {
            if (String.IsNullOrEmpty(path))
                return String.Empty;
            path = Path.GetFullPath(path).Replace('\\', '/');
            if (!path.EndsWith('/'))
                path += '/';
            return path;
        }

        public static string EnsureInputPathIsValid(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                // validate and normalize
                path = AsAbsolutePath(path);
                if (!Directory.Exists(path))
                {
                    path = "INVALID: " + path;
                }
            }
            return path;
        }
        public static string EnsureOutputPathIsValid(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                // validate and normalize
                path = AsAbsolutePath(path);
                if (!Directory.Exists(Path.GetPathRoot(path)))
                {
                    path = "INVALID: " + path;
                }
            }
            return path;
        }

        public static string EnsureOutputFileIsValid(string file)
        {
            if (!String.IsNullOrEmpty(file))
            {
                // validate and normalize
                file = AsAbsolutePath(file);
                if (!Directory.Exists(Path.GetDirectoryName(file)))
                {
                    file = "INVALID: " + file;
                }
            }
            return file;
        }
    }
}
