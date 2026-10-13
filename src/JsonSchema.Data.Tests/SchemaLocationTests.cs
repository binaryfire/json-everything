using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Json.Schema.Data.Tests;

public class SchemaLocationTests
{
	[TestCase("data", false)]
	[TestCase("data", true)]
	[TestCase("optionalData", false)]
	[TestCase("optionalData", true)]
	public void ReconstructedSchemasRetainTheirOwningLocation(string keyword, bool embeddedId)
	{
		var options = new BuildOptions { SchemaRegistry = new(), Dialect = Dialect.Data_202012 };
		var schema = JsonSchema.FromText($$"""
			{
			  "$id": "https://example.com/data-root",
			  "properties": {
			    "outer": {
			      {{(embeddedId ? "\"$id\": \"child\"," : "")}}
			      "{{keyword}}": { "required": "/requiredRule", "properties": "/propertyRules" }
			    }
			  }
			}
			""", options);
		using var instance = JsonDocument.Parse("""
			{
			  "requiredRule": ["missing"],
			  "propertyRules": { "value": { "type": "integer" } },
			  "outer": { "value": "bad" }
			}
			""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var rebuilt = result.Details!.Single().Details!.Single();
		var expected = embeddedId ? "https://example.com/child#" : "https://example.com/data-root#/properties/outer";
		Assert.That(rebuilt.Errors, Does.ContainKey("required"));
		Assert.That(rebuilt.SchemaLocation.ToString(), Is.EqualTo(expected));
		var child = rebuilt.Details!.Single();
		Assert.That(child.Errors, Does.ContainKey("type"));
		Assert.That(child.SchemaLocation.ToString(), Is.EqualTo(expected + "/properties/value"));
	}
}