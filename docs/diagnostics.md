# Diagnostics

The source generator reports these diagnostics. Each one means that no events are generated for
the class it points at. Every other class in the project is still generated.

| ID | Severity | Title |
|----|----------|-------|
| [ZAAE001](#zaae001) | Warning | Containing type of a class with async events is not partial |
| [ZAAE002](#zaae002) | Error | File-local class with async events is not generated |
| [ZAAE003](#zaae003) | Error | Class name differs only in case from another class with async events |

## ZAAE001

**Containing type of a class with async events is not partial.**

A class nested in another type gets its events generated into the real nested class. To do that,
the generated file reopens every containing type as `partial`, so each containing type has to be
declared `partial`. When one is not, the generator reports ZAAE001 on the class, naming the
outermost containing type that is not partial, and generates nothing for it.

```csharp
public class Orders                       // ZAAE001: Orders is not partial
{
    public partial class Service
    {
        [AsyncEvent] private AsyncEventHandler<OrderPlacedArgs> _orderPlaced;
    }
}
```

Fix it by declaring every containing type `partial`:

```csharp
public partial class Orders
{
    public partial class Service
    {
        [AsyncEvent] private AsyncEventHandler<OrderPlacedArgs> _orderPlaced;
    }
}
```

## ZAAE002

**File-local class with async events is not generated.**

A `file` type is visible only in the source file that declares it, so the generated file cannot
extend it. The generator reports the error ZAAE002 on a class that is declared `file`, or that is
nested in a `file` type, and generates nothing for it.

```csharp
file partial class Service                // ZAAE002
{
    [AsyncEvent] private AsyncEventHandler<OrderPlacedArgs> _orderPlaced;
}
```

Fix it by removing the `file` modifier, for example by making the class `internal`. To keep the
class file-local, remove `[AsyncEvent]` and write the `add` and `remove` accessors by hand.

## ZAAE003

**Class name differs only in case from another class with async events.**

Each class gets a generated file named after its namespace, its containing types and its name, such
as `App.Service.AsyncEvents.g.cs`. The compiler compares these file names ignoring case, so two
classes whose names differ only in case, such as `App.Service` and `App.service`, cannot both get a
file. The class declared first, by file path and then position, is generated. Every later one gets
the error ZAAE003 and is not generated.

```csharp
namespace App;

public partial class Service { [AsyncEvent] private AsyncEventHandler<string> _a; }
public partial class service { [AsyncEvent] private AsyncEventHandler<string> _b; }   // ZAAE003
```

Fix it by renaming one of the classes, or by moving it to another namespace or containing type.
