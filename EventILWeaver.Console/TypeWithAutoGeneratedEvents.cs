﻿using System.Collections.Generic;
using Mono.Cecil;

namespace EventILWeaver.Console
{
    public class TypeWithAutoGeneratedEvents
    {
        public TypeDefinition Type { get; }
        public IEnumerable<EventDefinition> EventsWithAutoGeneratedAttribute { get; }

        public TypeWithAutoGeneratedEvents(TypeDefinition type, IEnumerable<EventDefinition> eventsWithAutoGeneratedAttribute)
        {
            Type = type;
            EventsWithAutoGeneratedAttribute = eventsWithAutoGeneratedAttribute;
        }
    }
}