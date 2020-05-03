using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace EventILWeaver.Weaver
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


        public HookPropertySetResult HookPropertySet(GenerateEventResult generateEventResult, string addToType, string propertyName)
        {
            var addEventToType = _module.Types.Single(t => t.Name == addToType);
            if (addEventToType.Events.Any(ev => ev.Name == generateEventResult.EventDefinition.Name))
            {
                return new HookPropertySetResult($"'{generateEventResult.EventDefinition.FullName}' already existing in type '{addToType}', skipping...");
            }

            addEventToType.Fields.Add(generateEventResult.FieldDefinition);
            addEventToType.Methods.Add(generateEventResult.EventDefinition.AddMethod);
            addEventToType.Methods.Add(generateEventResult.EventDefinition.RemoveMethod);
            addEventToType.Events.Add(generateEventResult.EventDefinition);
            System.Console.WriteLine($"Added event: {generateEventResult.EventDefinition.Name} to '{addEventToType.Name}'");

            InjectEventCallAtPropertySetterStart(propertyName, addEventToType, generateEventResult);
            System.Console.WriteLine($"Event: {generateEventResult.EventDefinition.Name} will be called on start property-setter '{addEventToType.Name}:{propertyName}'");

            return new HookPropertySetResult(null);
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

    public class HookPropertySetResult
    {
        public bool IsSuccess => String.IsNullOrEmpty(ErrorMessage);
        public string ErrorMessage { get; }

        public HookPropertySetResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}