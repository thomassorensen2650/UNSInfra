using FluentAssertions;
using UNSInfra.Models.Hierarchy;
using Xunit;

namespace UNSInfra.Core.Tests.Models.Hierarchy;

public class HierarchicalPathTests
{
    [Fact]
    public void Constructor_ShouldInitializeEmptyValues()
    {
        // Act
        var hierarchicalPath = new HierarchicalPath();

        // Assert
        hierarchicalPath.Values.Should().NotBeNull();
        hierarchicalPath.Values.Should().BeEmpty();
    }

    [Fact]
    public void SetValue_ShouldAddValueToValues()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        const string levelName = "Enterprise";
        const string value = "TestEnterprise";

        // Act
        hierarchicalPath.SetValue(levelName, value);

        // Assert
        hierarchicalPath.Values.Should().ContainKey(levelName);
        hierarchicalPath.Values[levelName].Should().Be(value);
    }

    [Fact]
    public void SetValue_ShouldUpdateExistingValue()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        const string levelName = "Enterprise";
        const string oldValue = "OldEnterprise";
        const string newValue = "NewEnterprise";

        // Act
        hierarchicalPath.SetValue(levelName, oldValue);
        hierarchicalPath.SetValue(levelName, newValue);

        // Assert
        hierarchicalPath.Values.Should().ContainKey(levelName);
        hierarchicalPath.Values[levelName].Should().Be(newValue);
    }

    [Fact]
    public void GetValue_ShouldReturnValueForExistingLevel()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        const string levelName = "Site";
        const string expectedValue = "TestSite";
        hierarchicalPath.SetValue(levelName, expectedValue);

        // Act
        var result = hierarchicalPath.GetValue(levelName);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public void GetValue_ShouldReturnEmptyStringForNonExistentLevel()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        const string levelName = "NonExistent";

        // Act
        var result = hierarchicalPath.GetValue(levelName);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GetFullPath_ShouldReturnEmptyStringForEmptyPath()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();

        // Act
        var result = hierarchicalPath.GetFullPath();

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GetFullPath_ShouldReturnSlashSeparatedPath()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        hierarchicalPath.SetValue("Enterprise", "TestEnterprise");
        hierarchicalPath.SetValue("Site", "TestSite");
        hierarchicalPath.SetValue("Area", "TestArea");

        // Act
        var result = hierarchicalPath.GetFullPath();

        // Assert
        result.Should().Be("TestEnterprise/TestSite/TestArea");
    }

    [Fact]
    public void GetFullPath_ShouldSkipEmptyValues()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        hierarchicalPath.SetValue("Enterprise", "TestEnterprise");
        hierarchicalPath.SetValue("Site", "");
        hierarchicalPath.SetValue("Area", "TestArea");

        // Act
        var result = hierarchicalPath.GetFullPath();

        // Assert
        result.Should().Be("TestEnterprise/TestArea");
    }

    [Fact]
    public void GetFullPath_ShouldSkipNullValues()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        hierarchicalPath.Values["Enterprise"] = "TestEnterprise";
        hierarchicalPath.Values["Site"] = null!;
        hierarchicalPath.Values["Area"] = "TestArea";

        // Act
        var result = hierarchicalPath.GetFullPath();

        // Assert
        result.Should().Be("TestEnterprise/TestArea");
    }

    [Fact]
    public void GetAllValues_ShouldReturnEmptyDictionaryForEmptyPath()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();

        // Act
        var result = hierarchicalPath.GetAllValues();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllValues_ShouldReturnOnlyNonEmptyValues()
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        hierarchicalPath.SetValue("Enterprise", "TestEnterprise");
        hierarchicalPath.SetValue("Site", "");
        hierarchicalPath.SetValue("Area", "TestArea");
        hierarchicalPath.Values["WorkCenter"] = null!;

        // Act
        var result = hierarchicalPath.GetAllValues();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKeys("Enterprise", "Area");
        result["Enterprise"].Should().Be("TestEnterprise");
        result["Area"].Should().Be("TestArea");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("TestValue", "TestValue")]
    [InlineData("   ", "   ")]
    public void SetValue_GetValue_RoundTrip_ShouldPreserveValue(string inputValue, string expectedValue)
    {
        // Arrange
        var hierarchicalPath = new HierarchicalPath();
        const string levelName = "TestLevel";

        // Act
        hierarchicalPath.SetValue(levelName, inputValue);
        var result = hierarchicalPath.GetValue(levelName);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public void Values_ShouldBeInitializedDictionary()
    {
        // Arrange & Act
        var hierarchicalPath = new HierarchicalPath();

        // Assert
        hierarchicalPath.Values.Should().NotBeNull();
        hierarchicalPath.Values.Should().BeAssignableTo<Dictionary<string, string>>();
    }
}