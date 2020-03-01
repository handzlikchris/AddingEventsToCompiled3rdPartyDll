using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Console
{
    class Program
    {
        //TODO: must come from params
        const string UnityCoreNonDevReleaseDll = @"C:\Program Files\Unity\Hub\Editor\2019.3.0f6\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\mono\Managed\UnityEngine.CoreModule.dll";
        const string UnityCoreBuildDll = @"F:\_src\!Archive\!Unity\MissingUnityEvents\Build\MissingUnityEvents_Data\Managed\UnityEngine.CoreModule.dll";
        const string UnityCoreModule = @"C:\Program Files\Unity\Hub\Editor\2019.3.0f6\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll";
        const string UseDll = UnityCoreBuildDll;

        static void Main(string[] args)
        {
            CopyOriginal();

            var eventName = "SetPositionExecuting";

            using (var unityCoreAssy = AssemblyDefinition.ReadAssembly(UseDll, new ReaderParameters { ReadWrite = true }))
            {
                var module = unityCoreAssy.MainModule;

                var transformType = module.Types.Single(t => t.Name == "Transform");
                //		transformType.Dump();

                //Add event Field
                var handlerType = module.ImportReference(typeof(EventHandler<>));
                var handlerGenericParamType = module.Types.Single(t => t.Name == "Vector3");

                var genericHandlerType = new GenericInstanceType(handlerType);
                genericHandlerType.GenericArguments.Add(handlerGenericParamType);
                var genericHandlerTypeResolved = module.ImportReference(genericHandlerType);

                var eventField = new FieldDefinition(eventName, Mono.Cecil.FieldAttributes.Private, genericHandlerTypeResolved);
                var compilerGeneratedAttributeType = module.ImportReference(typeof(CompilerGeneratedAttribute));

                var attrConstructor = module.ImportReference(compilerGeneratedAttributeType.Resolve().Methods.First(m => m.IsConstructor));
                var compilerGeneratedAttribute = new CustomAttribute(attrConstructor);
                eventField.CustomAttributes.Add(compilerGeneratedAttribute);

                transformType.Fields.Add(eventField);

                //Add add method
                var addMethod = new MethodDefinition($"add_{eventName}", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName, module.TypeSystem.Void);
                var addMethodParameter = new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, genericHandlerTypeResolved);
                addMethod.Parameters.Add(addMethodParameter);
                addMethod.CustomAttributes.Add(compilerGeneratedAttribute);

                addMethod.Body.MaxStackSize = 3;
                addMethod.Body.InitLocals = true;

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

                var delegateType = module.ImportReference(typeof(Delegate));
                var delegateCombineMethod = module.ImportReference(delegateType.Resolve().Methods.First(m => m.Name == nameof(Delegate.Combine) && m.Parameters.Count == 2));
                il.Append(il.Create(OpCodes.Call, delegateCombineMethod));
                il.Append(il.Create(OpCodes.Castclass, genericHandlerTypeResolved));

                il.Append(il.Create(OpCodes.Stloc_2));
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldflda, eventField));
                il.Append(il.Create(OpCodes.Ldloc_2));
                il.Append(il.Create(OpCodes.Ldloc_1));

                var interlockedClass = module.ImportReference(typeof(Interlocked));
                var compareExchangeMethod = module.ImportReference(interlockedClass.Resolve().Methods.First(m => m.Name == nameof(Interlocked.CompareExchange) && m.GenericParameters.Count == 1 && m.Parameters.Count == 3));
                var genericCompareExchangeMethod = new GenericInstanceMethod(compareExchangeMethod);
                genericCompareExchangeMethod.GenericArguments.Add(genericHandlerTypeResolved);
                il.Append(il.Create(OpCodes.Call, genericCompareExchangeMethod));

                il.Append(il.Create(OpCodes.Stloc_0));
                il.Append(il.Create(OpCodes.Ldloc_0));
                il.Append(il.Create(OpCodes.Ldloc_1));
                il.Append(il.Create(OpCodes.Bne_Un_S, loopStart));

                il.Append(il.Create(OpCodes.Ret));

                transformType.Methods.Add(addMethod);

                //add remove method
                var removeMethod = new MethodDefinition($"remove_{eventName}", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName, module.TypeSystem.Void);
                var removeMethodParameter = new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, genericHandlerTypeResolved);
                removeMethod.Parameters.Add(removeMethodParameter);
                removeMethod.CustomAttributes.Add(compilerGeneratedAttribute);

                removeMethod.Body.MaxStackSize = 3;
                removeMethod.Body.InitLocals = true;

                removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
                removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));
                removeMethod.Body.Variables.Add(new VariableDefinition(genericHandlerTypeResolved));

                il = removeMethod.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldfld, eventField));
                il.Append(il.Create(OpCodes.Stloc_0));

                loopStart = il.Create(OpCodes.Ldloc_0);
                il.Append(loopStart);
                il.Append(il.Create(OpCodes.Stloc_1));
                il.Append(il.Create(OpCodes.Ldloc_1));
                il.Append(il.Create(OpCodes.Ldarg_1));

                var delegateRemoveMethod = module.ImportReference(delegateType.Resolve().Methods.First(m => m.Name == nameof(Delegate.Remove) && m.Parameters.Count == 2));
                il.Append(il.Create(OpCodes.Call, delegateRemoveMethod));
                il.Append(il.Create(OpCodes.Castclass, genericHandlerTypeResolved));

                il.Append(il.Create(OpCodes.Stloc_2));
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldflda, eventField));
                il.Append(il.Create(OpCodes.Ldloc_2));
                il.Append(il.Create(OpCodes.Ldloc_1));
                il.Append(il.Create(OpCodes.Call, genericCompareExchangeMethod));
                il.Append(il.Create(OpCodes.Stloc_0));
                il.Append(il.Create(OpCodes.Ldloc_0));
                il.Append(il.Create(OpCodes.Ldloc_1));
                il.Append(il.Create(OpCodes.Bne_Un_S, loopStart));

                il.Append(il.Create(OpCodes.Ret));

                transformType.Methods.Add(removeMethod);

                //add event
                var ev = new EventDefinition(eventName, Mono.Cecil.EventAttributes.None, genericHandlerTypeResolved);
                ev.AddMethod = addMethod;
                ev.RemoveMethod = removeMethod;
                transformType.Events.Add(ev);

                //change existing method to call into event
                var setPositionMethod = transformType.Methods.Single(m => m.Name == "set_position");
                il = setPositionMethod.Body.GetILProcessor();
                var firstInstruction = setPositionMethod.Body.Instructions.First();

                //		il.InsertBefore(firstInstruction, il.Create(OpCodes.Nop));
                //
                var loadThisArgForEventCall = il.Create(OpCodes.Ldarg_0);

                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldfld, eventField));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Dup));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Brtrue, loadThisArgForEventCall));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Pop));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Br, firstInstruction));

                il.InsertBefore(firstInstruction, loadThisArgForEventCall);
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_1));

                var invokeMethod = module.ImportReference(eventField.FieldType.Resolve().Methods.Single(m => m.Name == nameof(EventHandler.Invoke)));
                var genericInvoke = MakeHostInstanceGeneric(invokeMethod, handlerGenericParamType);
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, genericInvoke));

                //save
                unityCoreAssy.Write();
            }
        }

        private static void CopyOriginal()
        {
            var original = $@"{UseDll}.original";

            if (File.Exists(UseDll)) File.Delete(UseDll);

            File.Copy(original, UseDll);
        }

        public static MethodReference MakeHostInstanceGeneric(MethodReference self, params TypeReference[] arguments)
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
}
