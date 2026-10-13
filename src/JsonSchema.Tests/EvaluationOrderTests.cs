using System.Text.Json;
using NUnit.Framework;

namespace Json.Schema.Tests;

public class EvaluationOrderTests
{
	[TestCase(OutputFormat.Flag)]
	[TestCase(OutputFormat.List)]
	[TestCase(OutputFormat.Hierarchical)]
	public void DependentKeywordsUseBuildOrderOnRepeatedEvaluations(OutputFormat outputFormat)
	{
		var schema = JsonSchema.FromText("""
			{
			  "unevaluatedProperties": false,
			  "additionalProperties": { "type": "boolean" },
			  "properties": {
			    "name": { "type": "string" },
			    "values": {
			      "items": { "type": "boolean" },
			      "prefixItems": [{ "type": "string" }],
			      "type": "array"
			    }
			  },
			  "type": "object"
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var valid = JsonDocument.Parse("""{"name":"item","values":["first",true],"enabled":false}""");
		using var invalid = JsonDocument.Parse("""{"name":"item","values":["first",1]}""");
		var options = new EvaluationOptions { OutputFormat = outputFormat };

		for (var i = 0; i < 3; i++)
		{
			Assert.That(schema.Evaluate(valid.RootElement, options).IsValid, Is.True);
			Assert.That(schema.Evaluate(invalid.RootElement, options).IsValid, Is.False);
		}
	}

	[TestCase(OutputFormat.Flag)]
	[TestCase(OutputFormat.List)]
	[TestCase(OutputFormat.Hierarchical)]
	public void RecursiveSchemaCanEvaluateDifferentInstanceLocations(OutputFormat outputFormat)
	{
		var schema = JsonSchema.FromText("""
			{
			  "type": "object",
			  "properties": {
			    "value": { "type": "integer" },
			    "next": { "$ref": "#" }
			  }
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("""{"value":1,"next":{"value":2}}""");
		var options = new EvaluationOptions { OutputFormat = outputFormat };

		Assert.That(schema.Evaluate(instance.RootElement, options).IsValid, Is.True);
	}

	[TestCase(OutputFormat.Flag)]
	[TestCase(OutputFormat.List)]
	[TestCase(OutputFormat.Hierarchical)]
	public void ReenteringSameNodeAndInstanceLocationStillRejectsCycle(OutputFormat outputFormat)
	{
		var schema = JsonSchema.FromText("""{"not":{"$ref":"#"}}""",
			new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("{}");
		var options = new EvaluationOptions { OutputFormat = outputFormat };

		Assert.Throws<JsonSchemaException>(() => schema.Evaluate(instance.RootElement, options));
	}
}
