using BenchmarkDotNet.Running;
using AsyncEvents.Benchmarks;

// Switcher rather than an explicit type list so command-line arguments reach BenchmarkDotNet
// and every benchmark class in the assembly is discovered. The previous list named two of the
// three classes, leaving CancelableEventHandlerBenchmarks unrunnable.
BenchmarkSwitcher.FromAssembly(typeof(AsyncEventHandlerBenchmarks).Assembly).Run(args);
