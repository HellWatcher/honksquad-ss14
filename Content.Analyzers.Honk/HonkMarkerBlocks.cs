using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Content.Analyzers.Honk;

/// <summary>
/// Finds `// HONK START` / `// HONK END` blocks in a source file and
/// answers whether a given span sits inside one. Matches both the
/// spaced (`// HONK START`) and unspaced (`//HONK START`) variants
/// that exist in the fork today.
///
/// Unbalanced markers are ignored here, the dedicated HONK0004
/// analyzer reports those.
/// </summary>
internal static class HonkMarkerBlocks
{
    private const string StartMarker = "HONK START";
    private const string EndMarker = "HONK END";

    public static IReadOnlyList<TextSpan> Find(SourceText text)
    {
        var blocks = new List<TextSpan>();
        int? openStart = null;

        foreach (var line in text.Lines)
        {
            var lineText = text.ToString(line.Span);
            var trimmed = lineText.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            var payload = trimmed.Substring(2).TrimStart();

            if (payload.StartsWith(StartMarker, StringComparison.Ordinal))
            {
                openStart ??= line.Start;
            }
            else if (payload.StartsWith(EndMarker, StringComparison.Ordinal))
            {
                if (openStart is { } s)
                {
                    blocks.Add(TextSpan.FromBounds(s, line.End));
                    openStart = null;
                }
            }
        }

        return blocks;
    }

    public static bool Contains(IReadOnlyList<TextSpan> blocks, TextSpan span)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Contains(span))
                return true;
        }
        return false;
    }
}
