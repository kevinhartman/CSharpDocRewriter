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
        public CSharpCommentRewriter(): base(true)
        {
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

        static IEnumerable<(string, string)> LinePaddings(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    yield return (line, string.Empty);

                var padRegex = "^\\s*///";
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
            if(trivia.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia)
            {
                string rawComment = trivia.ToFullString();

                var linePaddings = LinePaddings(ToLines(rawComment));
                var paddings = linePaddings.Select(s => s.Item1);
                var lines = linePaddings.Select(s => s.Item2);
                var xml = LinesToString(lines);

                string rewritten = EditCommentInVim(xml);

                var rewrittenLines = ToLines(rewritten);

                var zipped = rewrittenLines.Zip(paddings);
                var rewrittenLinesWithPad = zipped.Select(t => $"{ t.Second }{ t.First }");
 
                var expression = SyntaxFactory.SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, LinesToString(rewrittenLinesWithPad));
 
                return expression;
            }

            return base.VisitTrivia(trivia);
        }
    }
}