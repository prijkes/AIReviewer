using Quaally.Infrastructure.Utils;

namespace Quaally.Tests.Utils;

public class JsonHelperTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public List<string>? Tags { get; set; }
    }

    [Fact]
    public void Serialize_WithSimpleObject_ShouldReturnCamelCaseJson()
    {
        // Arrange
        var obj = new TestModel { Name = "John", Age = 30 };

        // Act
        var json = JsonHelpers.Serialize(obj);

        // Assert
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"age\"");
        json.Should().Contain("\"John\"");
        json.Should().Contain("30");
    }

    [Fact]
    public void Serialize_WithNullProperties_ShouldOmitNullValues()
    {
        // Arrange
        var obj = new TestModel { Name = "John", Age = 30, Tags = null };

        // Act
        var json = JsonHelpers.Serialize(obj);

        // Assert
        json.Should().NotContain("tags");
    }

    [Fact]
    public void Serialize_WithNonNullProperties_ShouldIncludeAllValues()
    {
        // Arrange
        var obj = new TestModel
        {
            Name = "John",
            Age = 30,
            Tags = ["developer", "tester"]
        };

        // Act
        var json = JsonHelpers.Serialize(obj);

        // Assert
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"age\"");
        json.Should().Contain("\"tags\"");
        json.Should().Contain("developer");
        json.Should().Contain("tester");
    }

    [Fact]
    public void DeserializeStrict_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void DeserializeStrict_WithCamelCase_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"Alice\",\"age\":25}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("Alice");
        result.Age.Should().Be(25);
    }

    [Fact]
    public void DeserializeStrict_WithPascalCase_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = "{\"Name\":\"Bob\",\"Age\":35}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("Bob");
        result.Age.Should().Be(35);
    }

    [Fact]
    public void DeserializeStrict_WithComments_ShouldSkipComments()
    {
        // Arrange
        var json = @"{
            // This is a comment
            ""name"": ""John"",
            /* Multi-line
               comment */
            ""age"": 30
        }";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void DeserializeStrict_WithTrailingCommas_ShouldHandleCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30,}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void DeserializeStrict_WithInvalidJson_ShouldThrowInvalidDataException()
    {
        // Arrange
        var json = "{invalid json}";

        // Act
        var act = () => JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*JSON parse failed*");
    }

    [Fact]
    public void DeserializeStrict_WithMalformedJson_ShouldThrowInvalidDataException()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":}"; // Missing value

        // Act
        var act = () => JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*JSON parse failed*");
    }

    [Fact]
    public void DeserializeStrict_WithNullJson_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var json = "null";

        // Act
        var act = () => JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Deserialized null JSON*");
    }

    [Fact]
    public void DeserializeStrict_WithEmptyObject_ShouldReturnDefaultValues()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().BeEmpty();
        result.Age.Should().Be(0);
        result.Tags.Should().BeNull();
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_ShouldMaintainData()
    {
        // Arrange
        var original = new TestModel
        {
            Name = "Test User",
            Age = 42,
            Tags = ["tag1", "tag2", "tag3"]
        };

        // Act
        var json = JsonHelpers.Serialize(original);
        var deserialized = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        deserialized.Name.Should().Be(original.Name);
        deserialized.Age.Should().Be(original.Age);
        deserialized.Tags.Should().BeEquivalentTo(original.Tags);
    }

    [Fact]
    public void DeserializeStrict_WithExtraProperties_ShouldIgnoreExtraProperties()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30,\"extraField\":\"ignored\"}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void DeserializeStrict_WithMissingProperties_ShouldUseDefaults()
    {
        // Arrange
        var json = "{\"name\":\"John\"}";

        // Act
        var result = JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        result.Name.Should().Be("John");
        result.Age.Should().Be(0); // Default int value
    }

    [Fact]
    public void DeserializeStrict_WithWrongType_ShouldThrowInvalidDataException()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":\"not a number\"}";

        // Act
        var act = () => JsonHelpers.DeserializeStrict<TestModel>(json);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Serialize_WithComplexNestedObject_ShouldSerializeCorrectly()
    {
        // Arrange
        var obj = new
        {
            User = new { Name = "John", Age = 30 },
            Items = new[] { "item1", "item2" },
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        // Act
        var json = JsonHelpers.Serialize(obj);

        // Assert
        json.Should().Contain("\"user\"");
        json.Should().Contain("\"items\"");
        json.Should().Contain("\"metadata\"");
        json.Should().Contain("item1");
        json.Should().Contain("key1");
    }

    [Fact]
    public void DeserializeStrict_WithArray_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = "[{\"name\":\"John\",\"age\":30},{\"name\":\"Jane\",\"age\":25}]";

        // Act
        var result = JsonHelpers.DeserializeStrict<List<TestModel>>(json);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("John");
        result[1].Name.Should().Be("Jane");
    }

    [Fact]
    public void Serialize_WithEmptyArray_ShouldReturnEmptyArray()
    {
        // Arrange
        var obj = new List<TestModel>();

        // Act
        var json = JsonHelpers.Serialize(obj);

        // Assert
        json.Should().Be("[]");
    }
}
