using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CSharpFixes
{
    public class CSharpCommentRewriter : CSharpSyntaxRewriter
    {
        public static string[] DefaultTagOrdering = {
            "summary",
            "typeparam",
            "param",
            "returns",
            "exception",
            "remarks",
            "example"
        };

        private readonly IDictionary<string, string> state;
        private readonly string authorFilter;

        public IDictionary<string, string> SavedState => state;

        public bool IsStopping { get; private set; } = false;
        public bool HasEditErrors { get; private set; } = false;

        public bool SkipEditor { get; set; } = false;

        public IEnumerable<string> TagOrdering = null;

        // TODO: this is a kludge..Would be better to encapsulate file access
        // in the same class so we know we have the right file.
        public string CurrentFilePath { get; set; }

        public CSharpCommentRewriter(IDictionary<string, string> savedState, string authorFilter = null): base(true)
        {
            if (savedState == null)
            {
                throw new ArgumentException($"{nameof(savedState)} must not be null.");
            }

            this.state = savedState;
            this.authorFilter = authorFilter;
        }

        bool ShouldEdit(int lineNumber, int commentLength)
        {
            if (string.IsNullOrWhiteSpace(authorFilter))
            {
                return true;
            }

            var start = lineNumber;
            var end = lineNumber + commentLength - 1;

            // Get author list for this comment with git blame
            var authorsCommand = $"file_path=`wslpath \"{CurrentFilePath}\"`; cd `dirname $file_path` ; git blame --line-porcelain -L {start},{end} $file_path " +
                $"| sed -n 's/^author //p' " +
                $"| sort " +
                $"| uniq";

            var authors = authorsCommand.Bash().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return authors.Contains(authorFilter);
        }

        bool TryPerformEdits(string comment, int lineNumber, out string edited)
        {
            if (CurrentFilePath == null)
            {
                throw new InvalidOperationException("File path must be set externally.");
            }

            edited = SkipEditor ? comment : Edits.EditInVim(CurrentFilePath, comment, lineNumber);
            if (TagOrdering != null)
            {
                return Edits.TryEditReorderTags(TagOrdering, edited, out edited);
            }

            return true;
        }

        static IEnumerable<string> ToLines(string content)
        {
            using (var reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        static IEnumerable<(string, string)> GetPadLineTuples(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    yield return (line, string.Empty);

                var padRegex = "^\\s*/// ?";
                var match = Regex.Match(line, padRegex);

                if (!match.Success)
                    throw new ArgumentException("Malformed C# doc comment.");

                var pad = match.Value;
                var rest = Regex.Replace(line, padRegex, string.Empty);

                yield return (pad, rest);
            }
        }

        static string LinesToString(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (this.IsStopping || trivia.Kind() != SyntaxKind.SingleLineDocumentationCommentTrivia)
            {
                return base.VisitTrivia(trivia);
            }

            string rawComment = trivia.ToFullString();

            // Check if we've seen this comment before.
            if (state.ContainsKey(rawComment))
            {
                // This is a comment we've edited.
                return SyntaxFactory.SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, rawComment);
            }

            var padLineTuples = GetPadLineTuples(ToLines(rawComment));
            var lines = padLineTuples.Select(s => s.Item2);

            var commentLineNumber = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (!ShouldEdit(commentLineNumber, lines.Count()))
            {
                // This comment didn't match the current filter, so we'll skip it.
                return base.VisitTrivia(trivia);
            }

            var elementLineNumber = trivia.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

            var xml = LinesToString(lines);

            string rewritten;
            if (!TryPerformEdits(xml, elementLineNumber, out rewritten))
            {
                // One of the edits failed.
                HasEditErrors = true;
                return base.VisitTrivia(trivia);
            }

            // If the user deletes everything, this signals they want to stop.
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                Console.WriteLine("\nEmpty contents. What would you like to do?");

                var validResponses = new int[] { 'q', 'c' };
                while (true)
                {
                    Console.WriteLine("(q) Save progress and exit.");
                    Console.WriteLine("(c) Continue, but skip to the next comment (no modification).");
                    Console.Write("Answer: ");

                    var response = Console.ReadKey().KeyChar;

                    // Write a blank so the next output starts on the next line from
                    // the user's answer.
                    Console.WriteLine();

                    if (validResponses.Contains(response))
                    {
                        if (response == 'q')
                        {
                            this.IsStopping = true;
                        }

                        return base.VisitTrivia(trivia);
                    }

                    Console.WriteLine("\nEhem...");
                }
            }

            var rewrittenLines = ToLines(rewritten);

            var paddings = padLineTuples.Select(s => s.Item1);

            // The first line won't have any indentation, since it's the start
            // of the doc comment token.
            var firstLinePadding = paddings.FirstOrDefault() ?? string.Empty;

            // The second line will be arbitrarily indented. We need to preserve it
            // and apply the same indent to the rest of the comment after rewriting.
            var secondLinePadding = paddings.Skip(1).FirstOrDefault() ?? string.Empty;

            var rewrittenLinesWithPadHead = rewrittenLines.Take(1).Select(s => $"{ firstLinePadding }{ s }");
            var rewrittenLinesWithPadTail = rewrittenLines.Skip(1).Select(s => $"{ secondLinePadding }{ s }");
            var rewrittenLinesWithPad = rewrittenLinesWithPadHead.Concat(rewrittenLinesWithPadTail);

            var rewrittenComment = LinesToString(rewrittenLinesWithPad);
            var expression = SyntaxFactory.SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, rewrittenComment);

            // Save this rewrite so we won't visit it again.
            // Note: we don't really need rawComment, but JSON doesn't have sets.
            state.TryAdd(rewrittenComment, rawComment);

            return expression;
        }
    }
}