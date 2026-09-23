using System;
using System.Linq;

// Keep the executable namespace distinct from the production
// A2ZSysIns.SyntheticInspectionLab type.  Otherwise C# resolves the name
// below to this namespace rather than to the referenced production type.
namespace A2ZSysIns.SyntheticInspectionLabRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var seed = 20260915;
            var perScenario = 5;
            if (args.Length > 0) int.TryParse(args[0], out seed);
            if (args.Length > 1) int.TryParse(args[1], out perScenario);
            if (perScenario < 1) perScenario = 1;

            var results = A2ZSysIns.SyntheticInspectionLab.RunBatch(seed, perScenario);
            var passed = results.Count(x => x.Passed);
            Console.WriteLine("A2Z System Inspector — Synthetic Inspection Lab");
            Console.WriteLine("Seed=" + seed + "; scenarios=" + A2ZSysIns.SyntheticInspectionLab.ScenarioNames.Count + "; machines=" + results.Count);
            Console.WriteLine("PASS=" + passed + "; FAIL=" + (results.Count - passed));
            foreach (var group in results.GroupBy(x => x.Scenario))
                Console.WriteLine((group.All(x => x.Passed) ? "PASS" : "FAIL") + "  " + group.Key + "  " + group.Count(x => x.Passed) + "/" + group.Count());

            foreach (var failure in results.Where(x => !x.Passed).SelectMany(x => x.Failures.Select(f => x.Scenario + " [seed " + x.Seed + "]: " + f)))
                Console.WriteLine("  ERROR: " + failure);

            return results.All(x => x.Passed) ? 0 : 1;
        }
    }
}
