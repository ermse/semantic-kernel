using System;
using System.IO;

namespace MySkUtils
{
    public static class LlmHttpDebugHelper
    {
        /// <summary>
        /// DumpDebugContentAsync
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static void DumpDebugContentAsync(this string str)
        {
            try
            {
                if (string.Equals(Environment.MachineName, "THC-WS-SE5", StringComparison.Ordinal))
                {
                    string path = Environment.GetEnvironmentVariable("DebugLlmHttpContent", EnvironmentVariableTarget.Machine);
                    if (IsParentDirectoryExists(path))
                    {
                        File.WriteAllText(path, str);
                    }
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Determines whether the specified path is valid and directory part exists.
        /// </summary>
        /// <remarks>This method checks if the provided path is non-empty and whether the directory part
        /// of the path exists. If the path is invalid due to format issues or unsupported characters, the method
        /// returns <see langword="false"/>.</remarks>
        /// <param name="path">The path to validate, which may include a directory and filename.</param>
        /// <returns><see langword="true"/> if the path is non-empty and the directory part of the path exists; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool IsParentDirectoryExists(this string path)
        {
            // Check if the environment variable is set and not empty
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                // Get the directory part of the path
                string directory = Path.GetDirectoryName(path);

                // Check if the directory part exists
                // If directory is null or empty (e.g., filename only), consider current directory
                if (string.IsNullOrEmpty(directory))
                {
                    return false; // Current directory always exists
                }

                return Directory.Exists(directory);
            }
            catch (Exception)
            {
                // Invalid path characters or format
                return false;
            }
        }
    }

}
