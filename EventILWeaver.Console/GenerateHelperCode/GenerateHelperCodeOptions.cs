using System.Collections.Generic;
using CommandLine;
using EventILWeaver.Console.AddEvents;

namespace EventILWeaver.Console.GenerateHelperCode
{
    [Verb("generate-helper-code", HelpText = "Generates helper code for auto-generated events that abstracts direct event access, " +
                                             "in case when DLL is not weaved with events it'll not fail to build but fallback to specific code instead, eg. logging.")]
    public class GenerateHelperCodeOptions
    {
        [Option('t', "target-dll-path", Required = true, HelpText = AddEventsOptions.TargetDllPathHelpText)]
        public string TargetDllPath { get; set; }

        [Option('o', "output-file", Required = true, HelpText = "Output file where generated code will be saved")]
        public string OutputFile { get; set; }

        [Option('n', "namespace", Required = true, HelpText = "Namespace to be used for generated code")]
        public string Namespace { get; set; }

        [Option("enabled-build-symbol", Required = true, HelpText = "Helper code will be conditionally compiled, based on that symbol. This is a fallback mechanism in case DLL is not weaved but code should still build")]
        public string EnabledBuildSymbol { get; set; }

        [Option("using-statements", Required = false, Separator = ':', HelpText = "Using statements to be included on top of the file, delimited with :")]
        public IEnumerable<string> UsingStatements { get; set; }

        [Option("include-custom-code-when-no-build-symbol", Required = false, HelpText = "This code will be injected to methods that'll be executed if build symbol is not specified " +
                                                                                         "(which could mean library is not weaved for whatever reason). You could add any code, eg. some logging")]
        public string IncludeCustomCodeWhenNoBuildSymbol { get; set; }

    }
}