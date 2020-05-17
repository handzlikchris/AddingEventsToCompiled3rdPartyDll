# Adding events to compiled C# library

Sometimes when using 3rd party library I found it'd be great to have additional events that original creator did not think about. Be it for debug purposes or to promptly respond to change. 

This command line tool will allow you to add standard C# events to compiled DLLs (at this points property setters are supported).

## Approach
Tool uses `IL Weaving` and will add specified `Events` directly to specified DLLs. These are standard events, nothing different from writing them out by hand. Usage and performance will be in line with what you'd expect from manually created events. 

> **Since DLL IL code is modified you want to check with dll license if that's allowed. Tool and docs are provided for educational purposes.**

# General help
Run the tool from command line and reveiw help notes there

# Example commands
``` 
add-events 
    -t "(filePath)\UnityEngine.CoreModule.dll;(path)\UnityEngine.PhysicsModule.dll; 
    --target-definitions Transform-position-UnityEngine.CoreModule;BoxCollider-size-UnityEngine.PhysicsModule` 
```



