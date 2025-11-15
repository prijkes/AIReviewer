using Quaally.Infrastructure.Utils;

namespace Quaally.Tests.Utils;

public sealed class ProgrammingLanguageDetectorTests
{
    [Theory]
    [InlineData("Program.cs", ProgrammingLanguageDetector.ProgrammingLanguage.CSharp)]
    [InlineData("src/Services/UserService.cs", ProgrammingLanguageDetector.ProgrammingLanguage.CSharp)]
    [InlineData("test.CS", ProgrammingLanguageDetector.ProgrammingLanguage.CSharp)]
    public void DetectLanguage_CSharpFiles_ReturnsCSharp(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("main.cpp", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData("src/utils.cxx", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData("lib/core.cc", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData("engine.CPP", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    public void DetectLanguage_CppFiles_ReturnsCpp(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("utils.c", ProgrammingLanguageDetector.ProgrammingLanguage.C)]
    [InlineData("src/core.c", ProgrammingLanguageDetector.ProgrammingLanguage.C)]
    public void DetectLanguage_CFiles_ReturnsC(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Header.hpp", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData("src/utils.hxx", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData("include/core.h", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    public void DetectLanguage_HeaderFiles_ReturnsCpp(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("wrapper.cli", ProgrammingLanguageDetector.ProgrammingLanguage.Cli)]
    [InlineData("src/interop.CLI", ProgrammingLanguageDetector.ProgrammingLanguage.Cli)]
    public void DetectLanguage_CliFiles_ReturnsCli(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("config.json")]
    [InlineData("styles.css")]
    [InlineData("script.js")]
    [InlineData("data.xml")]
    public void DetectLanguage_UnknownFiles_ReturnsUnknown(string filePath)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DetectLanguage_EmptyOrNullPath_ReturnsUnknown(string filePath)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, result);
    }

    [Theory]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.CSharp, "C#/.NET")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Cpp, "C++")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.C, "C")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Cli, "C++/CLI")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, "Unknown")]
    public void GetDisplayName_ReturnsCorrectName(ProgrammingLanguageDetector.ProgrammingLanguage language, string expectedName)
    {
        // Act
        var result = ProgrammingLanguageDetector.GetDisplayName(language);

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.CSharp, "csharp")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Cpp, "cpp")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.C, "c")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Cli, "cli")]
    [InlineData(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, "general")]
    public void GetPolicySuffix_ReturnsCorrectSuffix(ProgrammingLanguageDetector.ProgrammingLanguage language, string expectedSuffix)
    {
        // Act
        var result = ProgrammingLanguageDetector.GetPolicySuffix(language);

        // Assert
        Assert.Equal(expectedSuffix, result);
    }

    [Fact]
    public void DetectLanguage_PathWithMultipleDots_DetectsCorrectly()
    {
        // Arrange
        var filePath = "src/my.library.service.cs";

        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(ProgrammingLanguageDetector.ProgrammingLanguage.CSharp, result);
    }

    [Fact]
    public void DetectLanguage_PathWithNoExtension_ReturnsUnknown()
    {
        // Arrange
        var filePath = "Makefile";

        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, result);
    }

    [Theory]
    [InlineData(@"C:\Projects\MyApp\Program.cs", ProgrammingLanguageDetector.ProgrammingLanguage.CSharp)]
    [InlineData("/home/user/project/main.cpp", ProgrammingLanguageDetector.ProgrammingLanguage.Cpp)]
    [InlineData(@"..\..\src\utils.c", ProgrammingLanguageDetector.ProgrammingLanguage.C)]
    public void DetectLanguage_FullPaths_DetectsCorrectly(string filePath, ProgrammingLanguageDetector.ProgrammingLanguage expected)
    {
        // Act
        var result = ProgrammingLanguageDetector.DetectLanguage(filePath);

        // Assert
        Assert.Equal(expected, result);
    }
}
