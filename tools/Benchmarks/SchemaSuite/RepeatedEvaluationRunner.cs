using System.Linq;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Json.Schema;

namespace Json.Benchmarks.SchemaSuite;

[MemoryDiagnoser]
public class RepeatedEvaluationRunner
{
	private JsonSchema _schema = null!;
	private JsonDocument _instance = null!;
	private EvaluationOptions _options = null!;

	[Params(OutputFormat.Flag, OutputFormat.Hierarchical)]
	public OutputFormat OutputFormat { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_schema = JsonSchema.FromText("""
			{
			  "type": "array",
			  "items": { "$ref": "#/$defs/entry" },
			  "$defs": {
			    "entry": {
			      "additionalProperties": false,
			      "required": ["name", "quantity", "tags"],
			      "properties": {
			        "name": { "type": "string", "minLength": 1 },
			        "quantity": { "type": "integer", "minimum": 0 },
			        "tags": { "type": "array", "items": { "type": "string" } }
			      },
			      "type": "object"
			    }
			  }
			}
			""", new BuildOptions { SchemaRegistry = new() });
		_instance = JsonDocument.Parse("[" + string.Join(",", Enumerable.Repeat(
			"""{"name":"item","quantity":3,"tags":["first","second"]}""", 32)) + "]");
		_options = new EvaluationOptions { OutputFormat = OutputFormat };
	}

	[Benchmark]
	public bool Evaluate() => _schema.Evaluate(_instance.RootElement, _options).IsValid;

	[GlobalCleanup]
	public void Cleanup() => _instance.Dispose();
}
