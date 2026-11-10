using System;
using System.Collections.Generic;
using System.Linq;

namespace TidyThreads
{
    /// <summary>
    /// The plan's test scenarios T1–T17 (no T8), named "T&lt;n&gt; &lt;plan title&gt;", and nothing else. Each setup builds
    /// its own fixtures on the current (quicktest) map. Per-phase scenarios live in Scenarios.P&lt;n&gt;.cs.
    /// </summary>
    public static partial class Scenarios
    {
        public static readonly List<(string name, Action setUp)> All = new List<(string, Action)>
        {
        };

        /// <summary>
        /// Runs the scenarios <paramref name="filter"/> names (comma-separated). A part equal to a whole scenario name
        /// selects only that scenario; any other part is a substring. An empty filter is never sent (AGENTS.md).
        /// </summary>
        public static void RunAll(string filter, bool testSaveLoad, Action<int, List<string>> done)
        {
            string[] parts = (filter ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            bool Exact(string part) => All.Any(s => string.Equals(s.name, part, StringComparison.OrdinalIgnoreCase));
            bool Selected(string name) => parts.Any(p => Exact(p)
                ? string.Equals(name, p, StringComparison.OrdinalIgnoreCase)
                : name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
            Scenario.RunAll(All.Where(s => parts.Length == 0 || Selected(s.name)), testSaveLoad, done);
        }
    }
}
