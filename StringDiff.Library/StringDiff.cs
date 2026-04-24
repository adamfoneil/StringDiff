namespace StringDiff.Library;

/// <summary>
/// Base abstraction for diff operations that can be projected into different presentations
/// </summary>
public record DiffText(string Content);

/// <summary>
/// Represents text that is unchanged between the two strings
/// </summary>
public record UnchangedText(string Content) : DiffText(Content);

/// <summary>
/// Represents text that was deleted from the original string
/// </summary>
public record DeletedText(string Content) : DiffText(Content);

/// <summary>
/// Represents text that was inserted into the new string
/// </summary>
public record InsertedText(string Content) : DiffText(Content);

/// <summary>
/// Base abstraction for segment diff operations that include index information
/// </summary>
public record DiffSegment(int Index, string Content);

/// <summary>
/// Represents a segment that is unchanged between the two arrays
/// </summary>
public record UnchangedSegment(int Index, string Content) : DiffSegment(Index, Content);

/// <summary>
/// Represents a segment that was deleted from the original array
/// </summary>
public record DeletedSegment(int Index, string Content) : DiffSegment(Index, Content);

/// <summary>
/// Represents a segment that was inserted into the new array
/// </summary>
public record InsertedSegment(int Index, string Content) : DiffSegment(Index, Content);

public static class StringDiff
{
    /// <summary>
    /// Computes the difference between two strings and returns a sequence of diff operations
    /// </summary>
    /// <param name="original">The original string</param>
    /// <param name="modified">The modified string</param>
    /// <returns>A sequence of diff operations representing the changes</returns>
    public static IEnumerable<DiffText> ByCharacter(string original, string modified)
    {
        if (string.IsNullOrEmpty(original) && string.IsNullOrEmpty(modified))
        {
            return [];
        }

        if (string.IsNullOrEmpty(original))
        {
            return [new InsertedText(modified)];
        }

        if (string.IsNullOrEmpty(modified))
        {
            return [new DeletedText(original)];
        }

        var operations = new List<DiffText>();
        var lcsTable = BuildLCSTable(original, modified);
        BuildDiffFromLCS(original, modified, lcsTable, operations);

        return operations;
    }

    /// <summary>
    /// Computes word-level differences between two strings
    /// </summary>
    /// <param name="original">The original string</param>
    /// <param name="modified">The modified string</param>
    /// <returns>A sequence of diff operations at the word level</returns>
    public static IEnumerable<DiffText> ByWords(string original, string modified)
    {
        if (string.IsNullOrEmpty(original) && string.IsNullOrEmpty(modified))
        {
            return [];
        }

        if (string.IsNullOrEmpty(original))
        {
            return [new InsertedText(modified)];
        }

        if (string.IsNullOrEmpty(modified))
        {
            return [new DeletedText(original)];
        }

        var originalWords = TokenizeWords(original);
        var modifiedWords = TokenizeWords(modified);

        // If tokenization results in just one token each, fall back to character-level diff
        if (originalWords.Count == 1 && modifiedWords.Count == 1)
        {
            return ByCharacter(original, modified);
        }

        var operations = new List<DiffText>();
        var lcsTable = BuildLCSTableForWords(originalWords, modifiedWords);
        BuildDiffFromLCSForWords(originalWords, modifiedWords, lcsTable, operations);

        return operations;
    }

    public static IEnumerable<DiffSegment> BySegment(IEnumerable<string> original, IEnumerable<string> modified)
    {
        var originalArray = original.ToArray();
        var modifiedArray = modified.ToArray();

        if (originalArray.Length == 0 && modifiedArray.Length == 0)
        {
            return [];
        }

        if (originalArray.Length == 0)
        {
            return modifiedArray.Select((seg, idx) => new InsertedSegment(idx, seg));
        }

        if (modifiedArray.Length == 0)
        {
            return originalArray.Select((seg, idx) => new DeletedSegment(idx, seg));
        }

        var operations = new List<DiffSegment>();
        var lcsTable = BuildLCSTableForSegments(originalArray, modifiedArray);
        BuildDiffFromLCSForSegments(originalArray, modifiedArray, lcsTable, operations);

        return operations;
    }

    /// <summary>
    /// Tokenizes a string into words and separators
    /// </summary>
    private static List<string> TokenizeWords(string text)
    {
        var tokens = new List<string>();
        var currentToken = new System.Text.StringBuilder();
        bool inWord = false;

        foreach (char c in text)
        {
            bool isWordChar = char.IsLetterOrDigit(c);

            if (isWordChar)
            {
                if (!inWord && currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                currentToken.Append(c);
                inWord = true;
            }
            else
            {
                if (inWord && currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                currentToken.Append(c);
                inWord = false;
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Builds the LCS table for word arrays
    /// </summary>
    private static int[,] BuildLCSTableForWords(List<string> words1, List<string> words2)
    {
        int m = words1.Count;
        int n = words2.Count;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (words1[i - 1] == words2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        return dp;
    }

    /// <summary>
    /// Iteratively builds the diff operations from the LCS table for words
    /// </summary>
    private static void BuildDiffFromLCSForWords(List<string> original, List<string> modified, 
                                                  int[,] lcsTable, List<DiffText> operations)
    {
        int i = original.Count;
        int j = modified.Count;
        var stack = new Stack<DiffText>();

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && original[i - 1] == modified[j - 1])
            {
                // Words match - part of LCS
                stack.Push(new UnchangedText(original[i - 1]));
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcsTable[i, j - 1] >= lcsTable[i - 1, j]))
            {
                // Insertion in modified
                stack.Push(new InsertedText(modified[j - 1]));
                j--;
            }
            else if (i > 0)
            {
                // Deletion from original
                stack.Push(new DeletedText(original[i - 1]));
                i--;
            }
        }

        // Merge consecutive operations of the same type
        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (operations.Count > 0 && 
                operations[^1].GetType() == current.GetType())
            {
                // Merge with previous operation of same type
                operations[^1] = current switch
                {
                    UnchangedText => new UnchangedText(operations[^1].Content + current.Content),
                    InsertedText => new InsertedText(operations[^1].Content + current.Content),
                    DeletedText => new DeletedText(operations[^1].Content + current.Content),
                    _ => current
                };
            }
            else
            {
                operations.Add(current);
            }
        }
    }

    /// <summary>
    /// Builds the LCS table for segment arrays
    /// </summary>
    private static int[,] BuildLCSTableForSegments(string[] segments1, string[] segments2)
    {
        int m = segments1.Length;
        int n = segments2.Length;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (segments1[i - 1] == segments2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        return dp;
    }

    /// <summary>
    /// Iteratively builds the diff operations from the LCS table for segments
    /// </summary>
    private static void BuildDiffFromLCSForSegments(string[] original, string[] modified, 
                                                     int[,] lcsTable, List<DiffSegment> operations)
    {
        int i = original.Length;
        int j = modified.Length;
        var stack = new Stack<DiffSegment>();

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && original[i - 1] == modified[j - 1])
            {
                // Segments match - part of LCS
                stack.Push(new UnchangedSegment(i - 1, original[i - 1]));
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcsTable[i, j - 1] >= lcsTable[i - 1, j]))
            {
                // Insertion in modified
                stack.Push(new InsertedSegment(j - 1, modified[j - 1]));
                j--;
            }
            else if (i > 0)
            {
                // Deletion from original
                stack.Push(new DeletedSegment(i - 1, original[i - 1]));
                i--;
            }
        }

        // Add operations in correct order (no merging needed for segments)
        while (stack.Count > 0)
        {
            operations.Add(stack.Pop());
        }
    }

    /// <summary>
    /// Builds the LCS table using dynamic programming
    /// </summary>
    private static int[,] BuildLCSTable(string s1, string s2)
    {
        int m = s1.Length;
        int n = s2.Length;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (s1[i - 1] == s2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        return dp;
    }

    /// <summary>
    /// Iteratively builds the diff operations from the LCS table
    /// </summary>
    private static void BuildDiffFromLCS(string original, string modified, int[,] lcsTable, 
                                         List<DiffText> operations)
    {
        int i = original.Length;
        int j = modified.Length;
        var stack = new Stack<DiffText>();

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && original[i - 1] == modified[j - 1])
            {
                // Characters match - part of LCS
                stack.Push(new UnchangedText(original[i - 1].ToString()));
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcsTable[i, j - 1] >= lcsTable[i - 1, j]))
            {
                // Insertion in modified
                stack.Push(new InsertedText(modified[j - 1].ToString()));
                j--;
            }
            else if (i > 0)
            {
                // Deletion from original
                stack.Push(new DeletedText(original[i - 1].ToString()));
                i--;
            }
        }

        // Merge consecutive operations of the same type
        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (operations.Count > 0 && 
                operations[^1].GetType() == current.GetType())
            {
                // Merge with previous operation of same type
                operations[^1] = current switch
                {
                    UnchangedText => new UnchangedText(operations[^1].Content + current.Content),
                    InsertedText => new InsertedText(operations[^1].Content + current.Content),
                    DeletedText => new DeletedText(operations[^1].Content + current.Content),
                    _ => current
                };
            }
            else
            {
                operations.Add(current);
            }
        }
    }

    /// <summary>
    /// Converts a sequence of diff operations into an expression string
    /// where unchanged text appears as-is and changes are shown as [deleted|inserted]
    /// </summary>
    /// <param name="diffs">The sequence of diff operations</param>
    /// <returns>A string expression representing the diff</returns>
    public static string Expression(IEnumerable<DiffText> diffs)
    {
        var result = new System.Text.StringBuilder();
        var operations = diffs.ToList();

        for (int i = 0; i < operations.Count; i++)
        {
            var current = operations[i];

            switch (current)
            {
                case UnchangedText unchanged:
                    result.Append(unchanged.Content);
                    break;

                case DeletedText deleted:
                    // Look ahead to see if there's a corresponding insertion
                    if (i + 1 < operations.Count && operations[i + 1] is InsertedText nextInserted)
                    {
                        result.Append($"[{deleted.Content}|{nextInserted.Content}]");
                        i++; // Skip the next operation since we've already processed it
                    }
                    else
                    {
                        result.Append($"[{deleted.Content}|]");
                    }
                    break;

                case InsertedText inserted:
                    // If we get here, it's an insertion without a preceding deletion
                    result.Append($"[|{inserted.Content}]");
                    break;
            }
        }

        return result.ToString();
    }

    public static string SegmentsExpression(IEnumerable<DiffSegment> diffs)
    {
        var operations = diffs
            .Where(d => d is DeletedSegment or InsertedSegment)
            .ToList();

        if (operations.Count == 0)
        {
            return string.Empty;
        }

        var result = new List<string>();

        foreach (var op in operations)
        {
            var prefix = op switch
            {
                DeletedSegment => "-",
                InsertedSegment => "+",
                _ => ""
            };
            result.Add($"{prefix}{op.Index}:{op.Content}");
        }

        return string.Join(";", result);
    }
}
