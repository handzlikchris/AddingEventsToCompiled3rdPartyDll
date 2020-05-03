using System.Collections.Generic;
using CommandLine;
using EventILWeaver.Console.AddEvents;

namespace EventILWeaver.Console.RevertToOriginal
{
    [Verb(RevertToOriginalVerb, HelpText = "Revert to original library.")]
    public class RevertToOriginalOptions
    {
        public const string RevertToOriginalVerb = "revert-to-original";

        [Option('t', "target-dll-paths", Separator = AddEventsOptions.MultipleDelimiter, Required = true, HelpText = AddEventsOptions.TargetDllPathHelpText)]
        public IEnumerable<string> TargetDllPaths { get; set; }

    }
}