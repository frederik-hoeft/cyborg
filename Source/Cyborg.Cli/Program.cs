using ConsoleAppFramework;
using Cyborg.Cli;

ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);