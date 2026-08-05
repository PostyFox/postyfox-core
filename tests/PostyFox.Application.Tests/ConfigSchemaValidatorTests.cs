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

    // Fields with a fixed set of choices (FurAffinity's category/species/…) declare `options`; the
    // client renders them as a dropdown, and this is the gate for anything bypassing the client.
    private const string ChoiceSchema = """
        { "Category": { "label": "Category", "options": [
            { "value": "1", "label": "All", "group": "Visual Art" },
            { "value": "13", "label": "Story", "group": "Readable Art" } ] } }
        """;

    [Fact]
    public void A_declared_option_is_accepted() =>
        Assert.Null(ConfigSchemaValidator.Validate(ChoiceSchema, """{"Category":"13"}"""));

    [Fact]
    public void A_value_outside_the_options_is_rejected() =>
        Assert.Equal(
            "Category is not one of the available choices.",
            ConfigSchemaValidator.Validate(ChoiceSchema, """{"Category":"999"}"""));

    [Fact]
    public void An_option_field_left_blank_is_still_optional() =>
        Assert.Null(ConfigSchemaValidator.Validate(ChoiceSchema, """{"Category":""}"""));

    [Fact]
    public void A_required_option_field_still_demands_a_choice()
    {
        const string schema = """
            { "C": { "label": "C", "required": true, "options": [ { "value": "1", "label": "One" } ] } }
            """;
        Assert.Equal("C is required.", ConfigSchemaValidator.Validate(schema, "{}"));
    }

    [Fact]
    public void An_options_field_can_override_the_rejection_message()
    {
        const string schema = """
            { "C": { "label": "C", "message": "Pick a real category.",
                     "options": [ { "value": "1", "label": "One" } ] } }
            """;
        Assert.Equal("Pick a real category.", ConfigSchemaValidator.Validate(schema, """{"C":"2"}"""));
    }

    [Fact]
    public void An_empty_options_array_imposes_no_choice_rule() =>
        Assert.Null(ConfigSchemaValidator.Validate(
            """{ "C": { "label": "C", "options": [] } }""", """{"C":"anything"}"""));

    [Fact]
    public void Dollar_prefixed_keys_are_schema_metadata_not_fields() =>
        Assert.Null(ConfigSchemaValidator.Validate(
            """{ "$comment": { "required": true }, "F": { "label": "F" } }""", "{}"));
}
