using FluentAssertions;
using UNSInfra.Models.Hierarchy;
using UNSInfra.Services;
using Xunit;

namespace UNSInfra.Core.Tests.Services;

public class HierarchicalPathComparerTests
{
    private readonly HierarchicalPathComparer _comparer = new();

    [Fact]
    public void Equals_WithIdenticalPaths_ShouldReturnTrue()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "TestEnterprise");
        path1.SetValue("Site", "TestSite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TestEnterprise");
        path2.SetValue("Site", "TestSite");

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithSameReference_ShouldReturnTrue()
    {
        // Arrange
        var path = new HierarchicalPath();
        path.SetValue("Enterprise", "TestEnterprise");

        // Act
        var result = _comparer.Equals(path, path);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "Enterprise1");
        path1.SetValue("Site", "Site1");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "Enterprise2");
        path2.SetValue("Site", "Site1");

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentKeys_ShouldReturnFalse()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "TestEnterprise");
        path1.SetValue("Site", "TestSite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TestEnterprise");
        path2.SetValue("Area", "TestArea");

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentCounts_ShouldReturnFalse()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "TestEnterprise");
        path1.SetValue("Site", "TestSite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TestEnterprise");

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithCaseInsensitiveValues_ShouldReturnTrue()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "testenterprise");
        path1.SetValue("Site", "testsite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TESTENTERPRISE");
        path2.SetValue("Site", "TESTSITE");

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullPaths_ShouldHandleGracefully()
    {
        // Act & Assert
        _comparer.Equals(null, null).Should().BeTrue();
        _comparer.Equals(new HierarchicalPath(), null).Should().BeFalse();
        _comparer.Equals(null, new HierarchicalPath()).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithEmptyPaths_ShouldReturnTrue()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        var path2 = new HierarchicalPath();

        // Act
        var result = _comparer.Equals(path1, path2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithIdenticalPaths_ShouldReturnSameHash()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "TestEnterprise");
        path1.SetValue("Site", "TestSite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TestEnterprise");
        path2.SetValue("Site", "TestSite");

        // Act
        var hash1 = _comparer.GetHashCode(path1);
        var hash2 = _comparer.GetHashCode(path2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithCaseInsensitiveValues_ShouldReturnSameHash()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "testenterprise");
        path1.SetValue("Site", "testsite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "TESTENTERPRISE");
        path2.SetValue("Site", "TESTSITE");

        // Act
        var hash1 = _comparer.GetHashCode(path1);
        var hash2 = _comparer.GetHashCode(path2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentPaths_ShouldReturnDifferentHashes()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "Enterprise1");
        path1.SetValue("Site", "Site1");

        var path2 = new HierarchicalPath();
        path2.SetValue("Enterprise", "Enterprise2");
        path2.SetValue("Site", "Site2");

        // Act
        var hash1 = _comparer.GetHashCode(path1);
        var hash2 = _comparer.GetHashCode(path2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GetHashCode_WithEmptyPath_ShouldNotThrow()
    {
        // Arrange
        var path = new HierarchicalPath();

        // Act
        var act = () => _comparer.GetHashCode(path);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetHashCode_WithDifferentKeyOrder_ShouldReturnSameHash()
    {
        // Arrange
        var path1 = new HierarchicalPath();
        path1.SetValue("Enterprise", "TestEnterprise");
        path1.SetValue("Site", "TestSite");

        var path2 = new HierarchicalPath();
        path2.SetValue("Site", "TestSite");
        path2.SetValue("Enterprise", "TestEnterprise");

        // Act
        var hash1 = _comparer.GetHashCode(path1);
        var hash2 = _comparer.GetHashCode(path2);

        // Assert
        hash1.Should().Be(hash2);
    }
}