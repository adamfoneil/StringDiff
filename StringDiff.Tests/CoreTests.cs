using StringDiff.Library;

namespace StringDiff.Tests;

public class StringDiffTests
{
    [Fact]
    public void Diff_BothStringsEmpty_ReturnsEmptySequence()
    {
        var result = Library.StringDiff.ByCharacter("", "");

        Assert.Empty(result);
    }

    [Fact]
    public void Diff_BothStringsNull_ReturnsEmptySequence()
    {
        var result = Library.StringDiff.ByCharacter(null, null);

        Assert.Empty(result);
    }

    [Fact]
    public void Diff_OriginalEmpty_ReturnsInserted()
    {
        var result = Library.StringDiff.ByCharacter("", "hello").ToList();

        Assert.Single(result);
        Assert.IsType<InsertedText>(result[0]);
        Assert.Equal("hello", result[0].Content);
    }

    [Fact]
    public void Diff_ModifiedEmpty_ReturnsDeleted()
    {
        var result = Library.StringDiff.ByCharacter("hello", "").ToList();

        Assert.Single(result);
        Assert.IsType<DeletedText>(result[0]);
        Assert.Equal("hello", result[0].Content);
    }

    [Fact]
    public void Diff_IdenticalStrings_ReturnsUnchanged()
    {
        var result = Library.StringDiff.ByCharacter("hello", "hello").ToList();

        Assert.Single(result);
        Assert.IsType<UnchangedText>(result[0]);
        Assert.Equal("hello", result[0].Content);
    }

    [Fact]
    public void Diff_SimpleInsertion_ReturnsCorrectOperations()
    {
        var result = Library.StringDiff.ByCharacter("helo", "hello").ToList();

        Assert.NotEmpty(result);
        Assert.Contains(result, op => op is InsertedText && op.Content == "l");
    }

    [Fact]
    public void Diff_SimpleDeletion_ReturnsCorrectOperations()
    {
        var result = Library.StringDiff.ByCharacter("hello", "helo").ToList();

        Assert.NotEmpty(result);
        Assert.Contains(result, op => op is DeletedText && op.Content == "l");
    }

    [Fact]
    public void Diff_CompleteReplacement_ReturnsDeletedAndInserted()
    {
        var result = Library.StringDiff.ByCharacter("abc", "xyz").ToList();

        Assert.Equal(2, result.Count);
        Assert.IsType<DeletedText>(result[0]);
        Assert.Equal("abc", result[0].Content);
        Assert.IsType<InsertedText>(result[1]);
        Assert.Equal("xyz", result[1].Content);
    }

    [Fact]
    public void Diff_MixedOperations_ReturnsCorrectSequence()
    {
        var result = Library.StringDiff.ByCharacter("kitten", "sitting").ToList();

        Assert.NotEmpty(result);
        // Should have a mix of unchanged, deleted, and inserted operations
        Assert.Contains(result, op => op is UnchangedText);
        Assert.Contains(result, op => op is DeletedText);
        Assert.Contains(result, op => op is InsertedText);
    }

    [Fact]
    public void Diff_PreservesUnchangedSequences()
    {
        var result = Library.StringDiff.ByCharacter("hello world", "hello universe").ToList();

        // "hello " should be preserved as unchanged
        Assert.Contains(result, op => op is UnchangedText && op.Content.Contains("hello"));
    }

    [Theory]
    [InlineData("a", "b")]
    [InlineData("test", "text")]
    [InlineData("saturday", "sunday")]
    public void Diff_VariousInputs_ReturnsNonEmptyResult(string original, string modified)
    {
        var result = Library.StringDiff.ByCharacter(original, modified);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void DiffOperation_CanBePatternMatched()
    {
        var operations = new DiffText[]
        {
            new UnchangedText("hello"),
            new DeletedText("world"),
            new InsertedText("there")
        };

        var results = operations.Select(op => op switch
        {
            UnchangedText => "unchanged",
            DeletedText => "deleted",
            InsertedText => "inserted",
            _ => "unknown"
        }).ToList();

        Assert.Equal(["unchanged", "deleted", "inserted"], results);
    }

    [Fact]
    public void DiffOperation_ContentProperty_IsAccessible()
    {
        DiffText op = new UnchangedText("test");

        Assert.Equal("test", op.Content);
    }

    [Fact]
    public void Diff_LongStrings_CompletesSuccessfully()
    {
        var original = new string('a', 1000) + new string('b', 1000);
        var modified = new string('a', 1000) + new string('c', 1000);

        var result = Library.StringDiff.ByCharacter(original, modified).ToList();

        Assert.NotEmpty(result);
        // Should have unchanged 'a's at the start
        Assert.Contains(result, op => op is UnchangedText);
    }

    [Theory]
    [InlineData("this or that", "this and that", "this [or|and] that")]
    [InlineData("the quick brown fox had antlers", "the quick red fox had butternut squash", "the quick [brown|red] fox had [antlers|butternut squash]")]
    [InlineData("49580071456", "4958003456", "495800[71|3]456")]
    [InlineData("2487220047782274109", "4872200477182274109", "[2|]4872200477[|1]82274109")]
    public void DiffExpressions(string original, string modified, string diffExpression)
    {
        var diffs = Library.StringDiff.ByWords(original, modified);
        var expression = Library.StringDiff.Expression(diffs);

        Assert.Equal(diffExpression, expression);
    }

    [Theory] // segments are 0-based indexed
    [InlineData("466:232:932:185", "466:232:932:185", "")] // no diffs
    [InlineData("466:232:185", "466:232:932:185", "+2:932")] // inserted second segment
    [InlineData("466:232:932:185", "466:232:185", "-2:932")] // deleted second segment
    [InlineData("847:117:722:362:002", "847:117:362:002:956", "-2:722;+4:956")] // deleted second segment, inserted fourth segment
    [InlineData("847:117:722:362:002", "847:117:723:362:002", "-2:722;+2:723")] // replaced second segment with new value
    public void DiffSegments(string original, string modified, string diffExpression)
    {
        var originalSegments = original.Split(':');
        var modifiedSegments = modified.Split(':');
        var diffSegments = Library.StringDiff.BySegment(originalSegments, modifiedSegments);
        var expression = Library.StringDiff.SegmentsExpression(diffSegments);
        Assert.Equal(diffExpression, expression);
    }
}
