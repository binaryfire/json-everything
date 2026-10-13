using System;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Json.Schema.OpenApi.Tests;

public class SchemaLocationTests
{
	[TestCase(false)]
	[TestCase(true)]
	public void DiscoveredSchemaRetainsItsResourceLocation(bool embeddedId)
	{
		using var document = JsonDocument.Parse($$"""
			{
			  "components": {
			    "schemas": {
			      "Entry": {
			        {{(embeddedId ? "\"$id\": \"child\"," : "")}}
			        "required": ["missing"],
			        "properties": { "value": { "type": "integer" } }
			      }
			    }
			  }
			}
			""");
		var uri = new Uri($"https://example.com/openapi/{embeddedId}/document");
		_ = new OpenApiDoc(uri, document.RootElement);
		var schema = JsonSchema.FromText($$"""{"$ref":"{{uri}}#/components/schemas/Entry"}""");
		using var instance = JsonDocument.Parse("""{"value":"bad"}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var target = result.Details!.Single();
		var expected = embeddedId ? new Uri(uri, "child") + "#" : uri + "#/components/schemas/Entry";
		Assert.That(target.Errors, Does.ContainKey("required"));
		Assert.That(target.SchemaLocation.ToString(), Is.EqualTo(expected));
		var child = target.Details!.Single();
		Assert.That(child.Errors, Does.ContainKey("type"));
		Assert.That(child.SchemaLocation.ToString(), Is.EqualTo(expected + "/properties/value"));
	}
}