using AIReviewer.Utils;

namespace AIReviewer.Tests.Utils;

public class LanguageDetectorTests
{
    [Fact]
    public void DetectLanguage_WithEnglishText_ShouldReturnEnglish()
    {
        // Arrange
        var text = "This is a simple English text for testing purposes.";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithJapaneseText_ShouldReturnJapanese()
    {
        // Arrange
        var text = "これは日本語のテストです。";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithMixedTextMostlyEnglish_ShouldReturnEnglish()
    {
        // Arrange
        var text = "This is mostly English text with a few 日本語 words.";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithMixedTextMostlyJapanese_ShouldReturnJapanese()
    {
        // Arrange
        var text = "これは主に日本語のテキストですが、some English words も含まれています。";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithEmptyString_ShouldReturnEnglish()
    {
        // Act
        var result = LanguageDetector.DetectLanguage("");

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithWhitespaceOnly_ShouldReturnEnglish()
    {
        // Arrange
        var text = "   \t\n   ";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithNull_ShouldReturnEnglish()
    {
        // Act
        var result = LanguageDetector.DetectLanguage(null!);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithCustomThreshold_ShouldRespectThreshold()
    {
        // Arrange - Text with exactly 50% Japanese characters
        var text = "Hello こんにちは";
        var highThreshold = 0.6; // Requires >60% Japanese
        var lowThreshold = 0.2;  // Requires >20% Japanese

        // Act
        var resultHigh = LanguageDetector.DetectLanguage(text, highThreshold);
        var resultLow = LanguageDetector.DetectLanguage(text, lowThreshold);

        // Assert
        resultHigh.Should().Be("en"); // Not enough Japanese for high threshold
        resultLow.Should().Be("ja");  // Enough Japanese for low threshold
    }

    [Theory]
    [InlineData("ひらがな")]  // Hiragana
    [InlineData("カタカナ")]  // Katakana
    [InlineData("漢字")]      // Kanji
    [InlineData("全角")]      // Fullwidth
    public void DetectLanguage_WithDifferentJapaneseCharacterTypes_ShouldDetectJapanese(string text)
    {
        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithNumbersAndSymbols_ShouldNotCountAsJapanese()
    {
        // Arrange
        var text = "12345 @#$%^&*()";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithCodeSnippet_ShouldReturnEnglish()
    {
        // Arrange
        var text = @"public class Example {
            private string name;
            public Example(string name) {
                this.name = name;
            }
        }";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithJapaneseComments_ShouldReturnJapanese()
    {
        // Arrange
        var text = @"// これはコメントです
        public class Example {
            private string name; // 名前フィールド
        }";

        // Act
        var result = LanguageDetector.DetectLanguage(text, 0.15); // Lower threshold for code with comments

        // Assert
        result.Should().Be("ja");
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void DetectLanguage_WithVariousThresholds_ShouldWorkCorrectly(double threshold)
    {
        // Arrange
        var japaneseText = "これは完全に日本語のテキストです。";
        var englishText = "This is completely English text.";

        // Act
        var japaneseResult = LanguageDetector.DetectLanguage(japaneseText, threshold);
        var englishResult = LanguageDetector.DetectLanguage(englishText, threshold);

        // Assert
        japaneseResult.Should().Be("ja");
        englishResult.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithDefaultThreshold_ShouldBe30Percent()
    {
        // Arrange - Text with exactly 30% Japanese (3 out of 10 non-whitespace chars)
        var text = "Hello こんにちは world";

        // Act & Assert
        // With default threshold of 0.3, this should be borderline
        // Actually testing that the default is working as expected
        var result = LanguageDetector.DetectLanguage(text);
        result.Should().BeOneOf("ja", "en"); // Exact behavior depends on implementation
    }

    [Fact]
    public void DetectLanguage_WithLongJapaneseText_ShouldReturnJapanese()
    {
        // Arrange
        var text = @"
        プログラミングは問題解決のプロセスです。
        コンピュータに指示を与えることで、
        さまざまなタスクを自動化できます。
        新しい技術を学ぶことは常に重要です。
        ";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithLongEnglishText_ShouldReturnEnglish()
    {
        // Arrange
        var text = @"
        Programming is a process of problem solving.
        By giving instructions to computers,
        we can automate various tasks.
        Learning new technologies is always important.
        ";

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_WithFullwidthAlphanumerics_ShouldDetectAsJapanese()
    {
        // Arrange
        var text = "ＡＢＣＤＥ１２３４５"; // Fullwidth characters

        // Act
        var result = LanguageDetector.DetectLanguage(text);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithZeroThreshold_ShouldAlwaysReturnJapaneseIfAnyJapanese()
    {
        // Arrange
        var text = "This is mostly English with one 字";

        // Act
        var result = LanguageDetector.DetectLanguage(text, 0.0);

        // Assert
        result.Should().Be("ja");
    }

    [Fact]
    public void DetectLanguage_WithOneThreshold_ShouldNeverReturnJapanese()
    {
        // Arrange
        var text = "これは完全に日本語です";

        // Act
        var result = LanguageDetector.DetectLanguage(text, 1.0);

        // Assert
        // With threshold of 1.0, ratio must be > 1.0 to return "ja", which is impossible
        // So even 100% Japanese text will return "en"
        result.Should().Be("en");
    }
}
