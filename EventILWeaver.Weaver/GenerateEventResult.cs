using Mono.Cecil;

namespace EventILWeaver.Weaver
{
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
}