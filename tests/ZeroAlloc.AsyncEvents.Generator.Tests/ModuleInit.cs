using System.Runtime.CompilerServices;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
