using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Json.Benchmarks.LogicSuite;
using Json.Benchmarks.Pointer;
using Json.Benchmarks.SchemaSuite;
using Json.Schema;

namespace Json.Benchmarks;

class Program
{
	static void Main(string[] args)
	{
#if DEBUG
		//IConfig config = new DebugBuildConfig();
		//config.WithOptions(ConfigOptions.DisableOptimizationsValidator);
		//var summary = BenchmarkRunner.Run<TestSuiteRunner>(config);

		var runner = new SingleSchemaRunner();
		//while (true)
		{
			runner.JsonSchemaNetEvaluateOnly(1000);
		}
#else
		if (args.Length == 0)
			BenchmarkRunner.Run<SingleSchemaRunner>();
		else
			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
#endif
	}
}
