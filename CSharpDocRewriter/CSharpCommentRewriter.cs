using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text;

namespace CSharpFixes
{
    public class CSharpCommentRewriter : CSharpSyntaxRewriter
    {
        private readonly IDictionary<string, string> commentBackup;

        public IDictionary<string, string> Backup => commentBackup;

        public bool IsStopping { get; private set; } = false;

        public CSharpCommentRewriter(IDictionary<string, string> commentBackup): base(true)
        {
            this.commentBackup = commentBackup;
        }

        static string EditCommentInVim(string comment)
        {
            return $"cat <<EOM | EDITOR=vim vipe | cat\n{comment}EOM".Bash();
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
            string rewrittenBackup;
            if (commentBackup.TryGetValue(rawComment, out rewrittenBackup))
            {
                return SyntaxFactory.SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, rewrittenBackup);
            }

            var padLineTuples = GetPadLineTuples(ToLines(rawComment));
            var lines = padLineTuples.Select(s => s.Item2);

            var xml = LinesToString(lines);
            var rewritten = EditCommentInVim(xml);

            // If the user deletes everything, this signals they want to stop.
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                Console.WriteLine("Empty contents. Would you like to stop? (Y)");
                Console.WriteLine("Your progress will be saved.\n");
                Console.WriteLine("If not, the original comment will be left as it was.");
                var response = Console.ReadLine();

                if (response.Trim().ToLowerInvariant() == "y")
                {
                    this.IsStopping = true;
                }

                return base.VisitTrivia(trivia);
            }

            var rewrittenLines = ToLines(rewritten);

            var paddings = padLineTuples.Select(s => s.Item1);

            // The first line won't have any indentation, since it's the start
            // of the doc comment token.
            var firstLinePadding = paddings.FirstOrDefault() ?? string.Empty;

            // The second line will be arbitrarily indented. We need to preserve it
            // and apply it the same indent to the rest of the comment after rewriting.
            var secondLinePadding = paddings.Skip(1).FirstOrDefault() ?? string.Empty;

            var rewrittenLinesWithPadHead = rewrittenLines.Take(1).Select(s => $"{ firstLinePadding }{ s }");
            var rewrittenLinesWithPadTail = rewrittenLines.Skip(1).Select(s => $"{ secondLinePadding }{ s }");
            var rewrittenLinesWithPad = rewrittenLinesWithPadHead.Concat(rewrittenLinesWithPadTail);

            var rewrittenComment = LinesToString(rewrittenLinesWithPad);
            var expression = SyntaxFactory.SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, rewrittenComment);

            // Back up this rewrite in case of restart.
            commentBackup.Add(rawComment, rewrittenComment);

            return expression;
        }
    }
}