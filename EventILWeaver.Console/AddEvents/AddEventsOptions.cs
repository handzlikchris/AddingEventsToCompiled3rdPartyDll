using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace EventILWeaver.Console.AddEvents
{
    public class TargetDefinition
    {
        public string ObjectTypeName { get; set; }
        public string PropertyName { get; set; }

        public TargetDefinition(string objectTypeName, string propertyName)
        {
            ObjectTypeName = objectTypeName;
            PropertyName = propertyName;
        }
    }

    [Verb("add-events", HelpText = "Weave IL instructions to library")]
    public class AddEventsOptions
    {
        public const char MultipleDelimiter = ';';

        private const string TargetDefinitionHelpText = "Weaving target definitions in form: ObjectTypeName-PropertyName-PropertyTypeName, " +
                                                        "delimited with ';' for multiple values, eg. 'Transform-position;Transform-rotation'";
        public const string TargetDllPathHelpText = "Location of DLL that will be weaved, multiple paths delimited with ;'";

        private IEnumerable<string> _targetDefinitionsRaw;

        [Option('t',"target-dll-paths", Separator = MultipleDelimiter, Required = true, HelpText = TargetDllPathHelpText)]
        public IEnumerable<string> TargetDllPaths { get; set; }

        [Option("target-definitions", Required = true, Separator = MultipleDelimiter, HelpText = TargetDefinitionHelpText)]
        public IEnumerable<string> TargetDefinitionsRaw
        {
            get => _targetDefinitionsRaw;
            set
            {
                _targetDefinitionsRaw = value;
                if (_targetDefinitionsRaw != null && TargetDefinitions == null)
                {
                    ParseTargetDefinitions();
                }
            }
        }

        public List<TargetDefinition> TargetDefinitions { get; set; }

        private void ParseTargetDefinitions()
        {
            TargetDefinitions = TargetDefinitionsRaw.Select(r =>
            {
                var splitted = r.Split(new[] {"-"}, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length != 2)
                    throw new Exception($"Unable to parse {nameof(TargetDefinitionsRaw)}, make sure values are in correct format.\r\n{TargetDefinitionHelpText}");

                return new TargetDefinition(splitted[0], splitted[1]);
            }).ToList();
        }
    }
}