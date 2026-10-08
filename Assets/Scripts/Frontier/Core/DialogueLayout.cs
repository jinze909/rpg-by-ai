using System;
using System.Collections.Generic;
namespace Starfall
{
    public static class DialogueLayout
    {
        // Conservative weighted line budgets preserve all text and reserve a separate choice area.
        public static List<string> Pages(string text, bool largeText = false)
        {
            var pages = new List<string>(); if (string.IsNullOrEmpty(text)) { pages.Add(""); return pages; }
            int start = 0, i = 0, lines = 1, lastSpace = -1; float units = 0, width = largeText ? 73 : 78; int maxLines = largeText ? 7 : 8;
            while (i < text.Length)
            {
                char c = text[i]; float add = c >= 0x2e80 ? 2 : char.IsUpper(c) ? 1.2f : .93f;
                if (c == '\n') { lines++; units = 0; } else { units += add; if (units > width) { lines++; units = add; } }
                if (char.IsWhiteSpace(c)) lastSpace = i;
                if (lines > maxLines)
                {
                    int end = lastSpace > start && i - lastSpace < 28 ? lastSpace + 1 : i; if (end <= start) end = i + 1;
                    pages.Add(text.Substring(start, end - start)); start = end; i = end; lines = 1; units = 0; lastSpace = -1; continue;
                }
                i++;
            }
            if (start < text.Length) pages.Add(text.Substring(start)); return pages;
        }
    }
}
