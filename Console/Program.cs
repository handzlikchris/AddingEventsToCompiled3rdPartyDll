using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Console
{
    public class IlEventHookManager
    {
        private readonly AssemblyDefinition _assembly;
        private readonly ModuleDefinition _module;

        public IlEventHookManager(AssemblyDefinition assembly)
        {
            _assembly = assembly;
            _module = _assembly.MainModule;
        }


        public void HookPropertySet(GenerateEventResult generateEventResult, string addToType, string propertyName)
        {
            var addEventToType = _module.Types.Single(t => t.Name == addToType);

            addEventToType.Fields.Add(generateEventResult.FieldDefinition);
            addEventToType.Methods.Add(generateEventResult.EventDefinition.AddMethod);
            addEventToType.Methods.Add(generateEventResult.EventDefinition.RemoveMethod);
            addEventToType.Events.Add(generateEventResult.EventDefinition);
            System.Console.WriteLine($"Added event: {generateEventResult.EventDefinition.Name} to '{addEventToType.Name}'");

            InjectEventCallAtPropertySetterStart(propertyName, addEventToType, generateEventResult);
            System.Console.WriteLine($"Event: {generateEventResult.EventDefinition.Name} will be called on start property-setter '{addEventToType.Name}:{propertyName}'");
        }

        private void InjectEventCallAtPropertySetterStart(string propertyName, TypeDefinition addEventToType, GenerateEventResult generateEventResult)
        {
            //change existing method to call into event
            var setMethod = addEventToType.Methods.Single(m => m.Name == $"set_{propertyName}");
            var il = setMethod.Body.GetILProcessor();
            var firstInstruction = setMethod.Body.Instructions.First();

            var loadThisArgForEventCall = il.Create(OpCodes.Ldarg_0);
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldfld, generateEventResult.FieldDefinition));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Dup));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Brtrue, loadThisArgForEventCall));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Pop));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Br, firstInstruction));

            il.InsertBefore(firstInstruction, loadThisArgForEventCall);
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_1));

            var genericInvoke = CreateGenericInvokeMethodReference(generateEventResult.GenericHandlerParamType, generateEventResult.FieldDefinition);
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, genericInvoke));
        }

        private MethodReference CreateGenericInvokeMethodReference(TypeReference eventHandlerGenericParamType, FieldDefinition eventField)
        {
            var invokeMethod = _module.ImportReference(eventField.FieldType.Resolve().Methods
                .Single(m => m.Name == nameof(EventHandler.Invoke)));

            var genericInvoke = MakeHostInstanceGeneric(invokeMethod, eventHandlerGenericParamType);
            return genericInvoke;
        }

        private static MethodReference MakeHostInstanceGeneric(MethodReference self, params TypeReference[] arguments)
        {
            var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType.MakeGenericInstanceType(arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new
                    ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new
                    GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

    }

    public class IlEventGenerator
    {
        private readonly AssemblyDefinition _assembly;
        private readonly ModuleDefinition _module;
        private readonly CustomAttribute _compilerGeneratedAttribute;
        private readonly TypeReference _delegateType;
        private readonly MethodReference _delegateCombineMethod;
        private readonly TypeReference _interlockedType;
        private readonly MethodReference _interlockedCompareExchangeMethod;
        private readonly MethodReference _delegateRemoveMethod;

        public IlEventGenerator(AssemblyDefinition assembly)
        {
            _assembly = assembly;
            _module = _assembly.MainModule;
            _compilerGeneratedAttribute = CreateCompilerGeneratedAttibute(_module);

            _delegateType = _module.ImportReference(typeof(Delegate));
            _delegateCombineMethod = _module.ImportReference(_delegateType.Resolve().Methods
                .First(m => m.Name == nameof(Delegate.Combine) && m.Parameters.Count == 2));
            _delegateRemoveMethod = _module.ImportReference(_delegateType.Resolve().Methods
                .First(m => m.Name == nameof(Delegate.Remove) && m.Parameters.Count == 2));

            _interlockedType = _module.ImportReference(typeof(Interlocked));
            _interlockedCompareExchangeMethod = _module.ImportReference(_interlockedType.Resolve().Methods.First(m =>
                m.Name == nameof(Interlocked.CompareExchange) && m.GenericParameters.Count == 1 &&
                m.Parameters.Count == 3));
        }

        public GenerateEventResult GenerateEvent(string eventHandlerGenericParamType, string eventName)
        {
            var handlerType = _module.ImportReference(typeof(EventHandler<>));
            var handlerGenericParamType = _module.Types.Single(t => t.Name == eventHandlerGenericParamType);

            var genericHandlerType = new GenericInstanceType(handlerType);
            genericHandlerType.GenericArguments.Add(handlerGenericParamType);
            var genericHandlerTypeResolved = _module.ImportReference(genericHandlerType);

            var eventField = new FieldDefinition(eventName, FieldAttributes.Private, genericHandlerTypeResolved);
            eventField.CustomAttributes.Add(_compilerGeneratedAttribute);

            var addMethod = GenerateAddHandlerMethod(eventName, genericHandlerTypeResolved, eventField);
            var removeMethod = GenerateEventRemoveMethod(eventName, genericHandlerTypeResolved, eventField);
            var ev = new EventDefinition(eventName, Mono.Cecil.EventAttributes.None, genericHandlerTypeResolved)
            {
                AddMethod = addMethod, 
                RemoveMethod = removeMethod
            };
            
            return new GenerateEventResult(ev, eventField, genericHandlerTypeResolved, handlerGenericParamType);
        }


        private MethodDefinition GenerateEventRemoveMethod(string eventName, TypeReference genericHandlerTypeResolved,
            FieldDefinition eventField)
        {
            var removeMethod = new MethodDefinition($"remove_{eventName}",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName, 
                _module.TypeSystem.Void);
            var removeMethodParameter = new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, genericHandlerTypeResolved);
            removeMethod.Parameters.Add(removeMethodParameter);
            removeMethod.CustomAttributes.Add(_compilerGeneratedAttribute);

            //removeMethod.Body.MaxStackSize = 3;
            //removeMethod.Body.InitLocals = true;

            removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
            removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
            removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));

            var il = removeMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldfld, eventField));
            il.Append(il.Create(OpCodes.Stloc_0));

            var loopStart = il.Create(OpCodes.Ldloc_0);
            il.Append(loopStart);
            il.Append(il.Create(OpCodes.Stloc_1));
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Ldarg_1));

            il.Append(il.Create(OpCodes.Call, _delegateRemoveMethod));
            il.Append(il.Create(OpCodes.Castclass, genericHandlerTypeResolved));

            il.Append(il.Create(OpCodes.Stloc_2));
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldflda, eventField));
            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Call, GenerateGenericInterlockedCompareMethod(genericHandlerTypeResolved)));
            il.Append(il.Create(OpCodes.Stloc_0));
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Bne_Un_S, loopStart));

            il.Append(il.Create(OpCodes.Ret));
            return removeMethod;
        }

        private MethodDefinition GenerateAddHandlerMethod(string eventName, TypeReference genericHandlerTypeResolved, FieldDefinition eventField)
        {
            var addMethod = new MethodDefinition($"add_{eventName}",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName, 
                _module.TypeSystem.Void);
            var addMethodParameter = new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, genericHandlerTypeResolved);
            addMethod.Parameters.Add(addMethodParameter);
            addMethod.CustomAttributes.Add(_compilerGeneratedAttribute);

            //addMethod.Body.MaxStackSize = 3;
            //addMethod.Body.InitLocals = true;

            addMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
            addMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
            addMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));

            var il = addMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldfld, eventField));
            il.Append(il.Create(OpCodes.Stloc_0));

            var loopStart = il.Create(OpCodes.Ldloc_0);
            il.Append(loopStart);
            il.Append(il.Create(OpCodes.Stloc_1));
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Ldarg_1));


            il.Append(il.Create(OpCodes.Call, _delegateCombineMethod));
            il.Append(il.Create(OpCodes.Castclass, genericHandlerTypeResolved));

            il.Append(il.Create(OpCodes.Stloc_2));
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldflda, eventField));
            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldloc_1));

            il.Append(il.Create(OpCodes.Call, GenerateGenericInterlockedCompareMethod(genericHandlerTypeResolved)));

            il.Append(il.Create(OpCodes.Stloc_0));
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Bne_Un_S, loopStart));

            il.Append(il.Create(OpCodes.Ret));
            return addMethod;
        }

        private GenericInstanceMethod GenerateGenericInterlockedCompareMethod(TypeReference genericHandlerTypeResolved)
        {
            var genericCompareExchangeMethod = new GenericInstanceMethod(_interlockedCompareExchangeMethod);
            genericCompareExchangeMethod.GenericArguments.Add(genericHandlerTypeResolved);

            return genericCompareExchangeMethod;
        }


        private static CustomAttribute CreateCompilerGeneratedAttibute(ModuleDefinition module)
        {
            var attrConstructor = module.ImportReference(module.ImportReference(typeof(CompilerGeneratedAttribute)).Resolve().Methods
                    .First(m => m.IsConstructor));
            return new CustomAttribute(attrConstructor);
        }

        private class CreateEventFieldResult
        {
            public FieldDefinition FieldDefinition { get; }
            public TypeReference GenericHandlerTypeResolved { get; }

            public CreateEventFieldResult(FieldDefinition fieldDefinition, TypeReference genericHandlerTypeResolved)
            {
                FieldDefinition = fieldDefinition;
                GenericHandlerTypeResolved = genericHandlerTypeResolved;
            }
        }
    }

    public class GenerateEventResult
    {
        public EventDefinition EventDefinition { get; }
        public FieldDefinition FieldDefinition { get; }
        public TypeReference GenericHandlerTypeResolved { get; }
        public TypeReference GenericHandlerParamType { get; }

        public GenerateEventResult(EventDefinition eventDefinition, FieldDefinition fieldDefinition, TypeReference genericHandlerTypeResolved, TypeReference genericHandlerParamType)
        {
            FieldDefinition = fieldDefinition;
            GenericHandlerTypeResolved = genericHandlerTypeResolved;
            GenericHandlerParamType = genericHandlerParamType;
            EventDefinition = eventDefinition;
        }
    }

    [Verb("add-events", HelpText = "Weave IL instructions to library")]
    class AddEventsOptions
    {
        [Option('t',"target-dll-path", Required = true,
            HelpText = @"Location of Unity DLL that will be rewritten, this is usually 
        'C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'")]
        public string TargetDllPath { get; set; }
    }

    [Verb("revert-to-original", HelpText = "Revert to original library.")]
    class RevertToOriginalOptions
    {
        
    }

    public class Options
    {

    }

    class Program
    {
        private static IlEventGenerator _ilEventGenerator;
        private static IlEventHookManager _ilEventManager;

        //TODO: must come from params
        //const string UnityCoreNonDevReleaseDll = @"C:\Program Files\Unity\Hub\Editor\2019.3.0f6\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\mono\Managed\UnityEngine.CoreModule.dll";
        //const string UnityCoreBuildDll = @"F:\_src\!Archive\!Unity\MissingUnityEvents\Build\MissingUnityEvents_Data\Managed\UnityEngine.CoreModule.dll";
        //const string UnityCoreModule = @"C:\Program Files\Unity\Hub\Editor\2019.3.0f6\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll";
        //const string UseDll = UnityCoreModule;

        public static string GenerateDefaultSetPropertyEventName(string propertyName) =>
            $"Set{char.ToUpper(propertyName[0]) + propertyName.Substring(1)}Executing";

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<AddEventsOptions, RevertToOriginalOptions>(args)
                .MapResult(
                    (AddEventsOptions opts) => RunAddEvents(opts),
                    (RevertToOriginalOptions opts) => RunRevertToOriginal(opts),
                    errs => 1);
        }

        private static int RunAddEvents(AddEventsOptions options)
        {
            if (!CreateCleanCopyFromBackup(options.TargetDllPath))
            {
                System.Console.WriteLine($"Unable to {nameof(CreateCleanCopyFromBackup)}, exiting...");
                return 1;
            }

            using (var assembly = AssemblyDefinition.ReadAssembly(options.TargetDllPath, new ReaderParameters { ReadWrite = true }))
            {
                _ilEventGenerator = new IlEventGenerator(assembly);
                _ilEventManager = new IlEventHookManager(assembly);

                CreateEventAndWeaveCallAtSetterStart("Transform", "position", "Vector3");
                CreateEventAndWeaveCallAtSetterStart("Transform", "localScale", "Vector3");
                CreateEventAndWeaveCallAtSetterStart("Transform", "rotation", "Quaternion");

                assembly.Write();
            }

            return 0;
        }

        private static int RunRevertToOriginal(RevertToOriginalOptions options)
        {
            return 0;
        }

        private static void CreateEventAndWeaveCallAtSetterStart(string typeName, string propName, string propTypeName)
        {
            var eventName = GenerateDefaultSetPropertyEventName(propName);
            var generatedEvent = _ilEventGenerator.GenerateEvent(propTypeName, eventName);
            _ilEventManager.HookPropertySet(generatedEvent, typeName, propName);
        }


        private static bool CreateCleanCopyFromBackup(string dllPath)
        {
            bool retry;
            do
            {
                try
                {
                    var backup = $@"{dllPath}.backup";
                    if (!File.Exists(backup))
                    {
                        System.Console.WriteLine("Backup does not exist, creating");
                        File.Copy(dllPath, backup);
                        System.Console.WriteLine($"Backup created: '{backup}'");
                    }

                    if (File.Exists(dllPath)) File.Delete(dllPath);

                    File.Copy(backup, dllPath);

                    retry = false;
                }
                catch (UnauthorizedAccessException e)
                {
                    System.Console.WriteLine("Unable to modify dll, make sure you run the application as administrator and close all applications that use library.");

                    System.Console.Write("\r\nRetry? [y]es, any other key for no\t: ");
                    var key = System.Console.ReadKey().Key;
                    System.Console.WriteLine();
                    if (key == ConsoleKey.Y)
                    {
                        retry = true;
                    }
                    else
                    {
                        return false;
                    }
                }
            } while (retry);

            return true;
        }
    }
}
