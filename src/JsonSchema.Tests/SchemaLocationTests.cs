using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Json.Schema.Tests;

public class SchemaLocationTests
{
	[TestCase("if", """{"if":{"type":"integer"},"then":false}""", "1")]
	[TestCase("then", """{"if":true,"then":false}""", "1")]
	[TestCase("else", """{"if":false,"else":false}""", "1")]
	[TestCase("not", """{"not":{"type":"integer"}}""", "1")]
	[TestCase("items", """{"items":{"type":"integer"}}""", "[\"bad\"]")]
	[TestCase("contains", """{"contains":{"type":"integer"}}""", "[\"bad\"]")]
	[TestCase("additionalProperties", """{"additionalProperties":{"type":"integer"}}""", "{\"x\":\"bad\"}")]
	[TestCase("unevaluatedProperties", """{"unevaluatedProperties":{"type":"integer"}}""", "{\"x\":\"bad\"}")]
	[TestCase("unevaluatedItems", """{"unevaluatedItems":{"type":"integer"}}""", "[\"bad\"]")]
	[TestCase("propertyNames", """{"propertyNames":{"pattern":"^good$"}}""", "{\"bad\":1}")]
	public void SingleSubschemaDoesNotInheritParentPropertyPath(string keyword, string body, string value)
	{
		var schema = JsonSchema.FromText($$$"""
			{
			  "$schema": "https://json-schema.org/draft/2020-12/schema",
			  "$id": "https://example.com/nested",
			  "properties": { "outer": {{{body}}} }
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse($$"""{"outer":{{value}}}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var path = "/properties/outer/" + keyword;
		var detail = Flatten(result).Single(x => x.EvaluationPath.ToString() == path || x.EvaluationPath.ToString().StartsWith(path + "/", StringComparison.Ordinal));
		Assert.That(detail.IsValid, Is.EqualTo(keyword is "if" or "not"));
		Assert.That(detail.SchemaLocation.ToString(), Is.EqualTo("https://example.com/nested#" + path));
	}

	[TestCase("""{"$defs":{"entry":{"if":true,"then":{"properties":{"blocked":false}}}},"$ref":"#/$defs/entry"}""", "https://example.com/composed#/$defs/entry/then/properties/blocked")]
	[TestCase("""{"allOf":[{"if":true,"then":{"properties":{"blocked":false}}}]}""", "https://example.com/composed#/allOf/0/then/properties/blocked")]
	public void ConditionalFalsePropertyHasExactLocation(string body, string expected)
	{
		var schema = JsonSchema.FromText("{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\",\"$id\":\"https://example.com/composed\"," + body[1..], new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("""{"blocked":true}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical, IncludeApplicatorErrors = false });
		Assert.That(result.IsValid, Is.False);
		var failure = Flatten(result).Single(x => x.Errors?.ContainsKey("") == true);
		Assert.That(failure.SchemaLocation.ToString(), Is.EqualTo(expected));
	}

	[TestCase("child")]
	[TestCase("#/$defs/entry")]
	public void EmbeddedResourceLocationsAreRelativeToTheirOwnId(string reference)
	{
		var schema = JsonSchema.FromText($$"""
			{
			  "$schema": "https://json-schema.org/draft/2020-12/schema",
			  "$id": "https://example.com/root",
			  "$defs": {
			    "entry": {
			      "$id": "child",
			      "type": "object",
			      "properties": { "value": { "type": "integer" } }
			    }
			  },
			  "$ref": "{{reference}}"
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("""{"value":"bad"}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical, IncludeApplicatorErrors = false });
		var failure = Flatten(result).Single(x => x.Errors?.ContainsKey("type") == true);
		Assert.That(result.IsValid, Is.False);
		Assert.That(result.Details![0].SchemaLocation.ToString(), Is.EqualTo("https://example.com/child#"));
		Assert.That(failure.SchemaLocation.ToString(), Is.EqualTo("https://example.com/child#/properties/value"));
	}

	[TestCase("http://json-schema.org/draft-06/schema#", "#named")]
	[TestCase("http://json-schema.org/draft-06/schema#", "#/definitions/entry")]
	[TestCase("http://json-schema.org/draft-07/schema#", "#named")]
	[TestCase("http://json-schema.org/draft-07/schema#", "#/definitions/entry")]
	public void FragmentAnchorsRetainDocumentLocations(string dialect, string reference)
	{
		var schema = JsonSchema.FromText($$"""
			{
			  "$schema": "{{dialect}}",
			  "$id": "https://example.com/anchor-root",
			  "definitions": {
			    "entry": {
			      "$id": "#named",
			      "required": ["missing"],
			      "properties": { "value": { "type": "integer" } }
			    }
			  },
			  "$ref": "{{reference}}"
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("""{"value":"bad"}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var owner = Flatten(result).Single(x => x.Errors?.ContainsKey("required") == true);
		var child = Flatten(result).Single(x => x.Errors?.ContainsKey("type") == true);
		Assert.That(owner.SchemaLocation.ToString(), Is.EqualTo("https://example.com/anchor-root#/definitions/entry"));
		Assert.That(child.SchemaLocation.ToString(), Is.EqualTo("https://example.com/anchor-root#/definitions/entry/properties/value"));
	}

	[TestCase("http://json-schema.org/draft-06/schema#", false)]
	[TestCase("http://json-schema.org/draft-07/schema#", false)]
	[TestCase("https://json-schema.org/draft/2019-09/schema", false)]
	[TestCase("http://json-schema.org/draft-06/schema#", true)]
	[TestCase("http://json-schema.org/draft-07/schema#", true)]
	[TestCase("https://json-schema.org/draft/2019-09/schema", true)]
	public void OlderDraftItemLocationsDoNotInheritParentPath(string dialect, bool additional)
	{
		var body = additional
			? "\"items\": [], \"additionalItems\": { \"type\": \"integer\" }"
			: "\"items\": { \"type\": \"integer\" }";
		var schema = JsonSchema.FromText($$"""
			{
			  "$schema": "{{dialect}}",
			  "$id": "https://example.com/items",
			  "properties": { "outer": { {{body}} } }
			}
			""", new BuildOptions { SchemaRegistry = new() });
		using var instance = JsonDocument.Parse("""{"outer":["bad"]}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var failure = Flatten(result).Single(x => x.Errors?.ContainsKey("type") == true);
		Assert.That(failure.SchemaLocation.ToString(), Is.EqualTo("https://example.com/items#/properties/outer/" + (additional ? "additionalItems" : "items")));
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void DiscoveredSchemasOwnTheirResourceLocations(bool baseDocument, bool embeddedId)
	{
		using var document = JsonDocument.Parse($$"""
			{
			  "schemas": {
			    "entry": {
			      {{(embeddedId ? "\"$id\": \"child\"," : "")}}
			      "required": ["missing"],
			      "properties": { "blocked": false }
			    }
			  }
			}
			""");
		var options = new BuildOptions { SchemaRegistry = new(), Dialect = Dialect.Draft202012.With([], allowUnknownKeywords: true) };
		var uri = new Uri("https://example.com/document");
		if (baseDocument)
			options.SchemaRegistry.Register(new JsonElementBaseDocument(document.RootElement, uri));
		else
			JsonSchema.Build(document.RootElement, options, uri);
		var schema = JsonSchema.FromText("""{"$ref":"https://example.com/document#/schemas/entry"}""", options);
		using var instance = JsonDocument.Parse("""{"blocked":true}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var expected = embeddedId ? "https://example.com/child#" : "https://example.com/document#/schemas/entry";
		Assert.That(Flatten(result).Single(x => x.Errors?.ContainsKey("required") == true).SchemaLocation.ToString(), Is.EqualTo(expected));
		Assert.That(Flatten(result).Single(x => x.Errors?.ContainsKey("") == true).SchemaLocation.ToString(), Is.EqualTo(expected + "/properties/blocked"));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ReferencedBooleanUsesItsDefiningResource(bool value)
	{
		var options = new BuildOptions { SchemaRegistry = new(), Dialect = Dialect.Draft202012 };
		JsonSchema.FromText($$"""
			{
			  "$id": "https://example.com/definitions",
			  "$defs": { "target": {{(value ? "true" : "false")}} }
			}
			""", options);
		var schema = JsonSchema.FromText("""
			{
			  "$id": "https://example.com/caller",
			  "$ref": "https://example.com/definitions#/$defs/target"
			}
			""", options);
		using var instance = JsonDocument.Parse("1");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.EqualTo(value));
		var target = result.Details!.Single();
		Assert.That(target.IsValid, Is.EqualTo(value));
		Assert.That(target.SchemaLocation.ToString(), Is.EqualTo("https://example.com/definitions#/$defs/target"));
	}

	[Test]
	public void CustomScalarKeywordReceivesItsOwnPath()
	{
		var options = new BuildOptions { SchemaRegistry = new(), Dialect = Dialect.Draft202012.With([new ApplyKeyword()]) };
		var schema = JsonSchema.FromText("""
			{
			  "$id": "https://example.com/custom",
			  "properties": { "outer": { "apply": false } }
			}
			""", options);
		using var instance = JsonDocument.Parse("""{"outer":1}""");
		var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
		Assert.That(result.IsValid, Is.False);
		var failure = Flatten(result).Single(x => x.Errors?.ContainsKey("") == true);
		Assert.That(failure.SchemaLocation.ToString(), Is.EqualTo("https://example.com/custom#/properties/outer/apply"));
	}

	private class ApplyKeyword : IKeywordHandler
	{
		public string Name => "apply";

		public object? ValidateKeywordValue(JsonElement value) => null;

		public void BuildSubschemas(KeywordData keyword, BuildContext context)
		{
			keyword.Subschemas = [JsonSchema.BuildNode(context with { LocalSchema = keyword.RawValue })];
		}

		public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
		{
			var result = keyword.Subschemas[0].Evaluate(context with { EvaluationPath = context.EvaluationPath.Combine(Name) });
			return new KeywordEvaluation { Keyword = Name, IsValid = result.IsValid, Details = [result] };
		}
	}

	private static IEnumerable<EvaluationResults> Flatten(EvaluationResults result)
	{
		yield return result;
		if (result.Details == null) yield break;
		foreach (var child in result.Details)
			foreach (var nested in Flatten(child))
				yield return nested;
	}
}