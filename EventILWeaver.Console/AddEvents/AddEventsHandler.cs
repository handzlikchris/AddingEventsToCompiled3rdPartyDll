using System;
using System.IO;
using System.Linq;
using EventILWeaver.Console.RevertToOriginal;
using EventILWeaver.Weaver;
using Mono.Cecil;

namespace EventILWeaver.Console.AddEvents
{
    public class AddEventsHandler : HandlerBase
    {
        private IlEventGenerator _ilEventGenerator;
        private IlEventHookManager _ilEventManager;

        public int Run(AddEventsOptions options)
        {
            foreach (var targetPath in options.TargetDllPaths)
            {
                System.Console.WriteLine($"Processing... {targetPath}");

                if (!CreateCleanCopyFromBackup(targetPath))
                {
                    System.Console.WriteLine($"Unable to {nameof(CreateCleanCopyFromBackup)}, exiting...");
                    return 1;
                }

                CreateRevertToBackupFallbackScript(options);

                using (var assembly = AssemblyDefinition.ReadAssembly(targetPath, new ReaderParameters { ReadWrite = true }))
                {
                    _ilEventGenerator = new IlEventGenerator(assembly);
                    _ilEventManager = new IlEventHookManager(assembly);

                    foreach (var targetDefinition in options.TargetDefinitions)
                    {
                        var propertyType = ResolveSetterValueArgType(targetDefinition.ObjectTypeName, targetDefinition.PropertyName, assembly.MainModule);
                        CreateEventAndWeaveCallAtSetterStart(targetDefinition.ObjectTypeName, targetDefinition.PropertyName, propertyType);
                    }

                    assembly.Write();
                }

                System.Console.WriteLine($"Processed! {targetPath}\r\n\r\n");
            }

            return 0;
        }

        public static string GenerateDefaultSetPropertyEventName(string propertyName) => $"Set{char.ToUpper(propertyName[0]) + propertyName.Substring(1)}Executing";

        private void CreateEventAndWeaveCallAtSetterStart(string typeName, string propName, TypeReference propType)
        {
            var eventName = GenerateDefaultSetPropertyEventName(propName);
            var generatedEvent = _ilEventGenerator.GenerateEvent(propType, eventName);
            var result = _ilEventManager.HookPropertySet(generatedEvent, typeName, propName);
            if (!result.IsSuccess)
                System.Console.WriteLine(result.ErrorMessage);
        }

        private static TypeReference ResolveSetterValueArgType(string typeName, string propertyName, ModuleDefinition module)
        {
            var type = module.Types.Single(t => t.Name == typeName);
            var setMethod = type.Methods.Single(m => m.Name == $"set_{propertyName}");

            return setMethod.Parameters.First().ParameterType;
        }

        private static bool CreateCleanCopyFromBackup(string dllPath)
        {
            return ExecuteWithOptionalRetry(() =>
            {
                var backupPath = CreateBackupFilePath(dllPath);
                if (!File.Exists(backupPath))
                {
                    System.Console.WriteLine("Backup does not exist, creating");
                    File.Copy(dllPath, backupPath);
                    System.Console.WriteLine($"Backup created: '{backupPath}'");
                }

                if (File.Exists(dllPath)) File.Delete(dllPath);

                File.Copy(backupPath, dllPath);
            });
        }

        private static void CreateRevertToBackupFallbackScript(AddEventsOptions options)
        {
            var revertToToBackupCommand = $"\"{AppDomain.CurrentDomain.BaseDirectory}{AppDomain.CurrentDomain.FriendlyName}\"" +
                                          $" {RevertToOriginalOptions.RevertToOriginalVerb} -t \"{string.Join(AddEventsOptions.MultipleDelimiter.ToString(), options.TargetDllPaths)}\"";
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "revert-last-weaving.bat", revertToToBackupCommand);
        }

    }

}
