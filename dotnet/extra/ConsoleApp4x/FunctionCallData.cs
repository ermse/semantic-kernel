// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace ConsoleApp4x
{
    /// <summary>
    /// Helper class for deserializing function call data.
    /// This class is designed to be independent of Semantic Kernel,
    /// allowing remote hosts to process function calls without referencing the SK library.
    /// </summary>
    public class FunctionCallData
    {
        public string Id { get; set; }
        public string PluginName { get; set; }
        public string FunctionName { get; set; }

        /// <summary>
        /// The function arguments as a simple dictionary.
        /// This replaces KernelArguments to avoid dependency on Semantic Kernel.
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// Checks if the arguments contain a key with the specified name (case-insensitive).
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            if (Arguments == null)
            {
                return false;
            }

            foreach (var k in Arguments.Keys)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to get a value from the arguments by key (case-insensitive).
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="value">The value if found; otherwise, null.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool TryGetValue(string key, out object value)
        {
            value = null;

            if (Arguments == null)
            {
                return false;
            }

            foreach (var kvp in Arguments)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
