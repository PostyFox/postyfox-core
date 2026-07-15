using PostyFox.Application.Connectors;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConfigSchemaValidatorTests
{
    private const string BlueSkySchema = """
        { "Handle": { "label": "Handle", "required": true, "pattern": "^[^@]",
                      "message": "No leading @." } }
        """;

    [Fact]
    public void Null_or_blank_schema_imposes_no_rules() =>
        Assert.Null(ConfigSchemaValidator.Validate(null, "{\"anything\":\"@x\"}"));

    [Fact]
    public void Legacy_placeholder_values_impose_no_rules() =>
        Assert.Null(ConfigSchemaValidator.Validate("{\"Handle\":\"\"}", "{\"Handle\":\"@x\"}"));

    [Fact]
    public void Handle_with_leading_at_is_rejected_with_custom_message()
    {
        var error = ConfigSchemaValidator.Validate(BlueSkySchema, "{\"Handle\":\"@me.bsky.social\"}");
        Assert.Equal("No leading @.", error);
    }

    [Fact]
    public void Handle_without_leading_at_passes() =>
        Assert.Null(ConfigSchemaValidator.Validate(BlueSkySchema, "{\"Handle\":\"me.bsky.social\"}"));

    [Fact]
    public void Missing_required_field_is_rejected()
    {
        var error = ConfigSchemaValidator.Validate(BlueSkySchema, "{}");
        Assert.Equal("Handle is required.", error);
    }

    [Fact]
    public void Whitespace_only_required_field_is_rejected() =>
        Assert.Equal("Handle is required.",
            ConfigSchemaValidator.Validate(BlueSkySchema, "{\"Handle\":\"   \"}"));

    [Fact]
    public void Length_bounds_are_enforced()
    {
        const string schema = """{ "Pin": { "label": "PIN", "minLength": 4, "maxLength": 6 } }""";
        Assert.Equal("PIN must be at least 4 characters.",
            ConfigSchemaValidator.Validate(schema, "{\"Pin\":\"12\"}"));
        Assert.Equal("PIN must be at most 6 characters.",
            ConfigSchemaValidator.Validate(schema, "{\"Pin\":\"1234567\"}"));
        Assert.Null(ConfigSchemaValidator.Validate(schema, "{\"Pin\":\"1234\"}"));
    }

    [Fact]
    public void Invalid_regex_in_schema_does_not_block()
    {
        const string schema = """{ "F": { "label": "F", "pattern": "(" } }""";
        Assert.Null(ConfigSchemaValidator.Validate(schema, "{\"F\":\"anything\"}"));
    }
}
