using ConsoleAppFramework;
using Cyborg.Cli;

var app = ConsoleApp.Create();
app.Add<BackupCommands>();
await app.RunAsync(args);
