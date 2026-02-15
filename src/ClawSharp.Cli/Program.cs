using System.CommandLine.Parsing;
using ClawSharp.Cli;

var rootCommand = CliEntryPoint.CreateRootCommand();
var parseResult = CommandLineParser.Parse(rootCommand, args);
return await parseResult.InvokeAsync();
