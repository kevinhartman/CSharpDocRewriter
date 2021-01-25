using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CSharpFixes
{
    public class Edits
    {
        public static string EditInVim(string filePath, string comment, int lineNumber)
        {
            return $"cat <<EOM | ./editor.sh `wslpath \"{filePath}\"` {lineNumber} | cat\n{comment}EOM".Bash();
        }

        public static bool TryEditReorderTags(IEnumerable<string> tagOrdering, string comment, out string editedComment)
        {
            // We first need to create a root XML element (this is dirty)
            var withRoot = "<root>\r\n" + comment + "\r\n</root>";
            XDocument xml;
            try
            {
                xml = XDocument.Parse(withRoot, LoadOptions.PreserveWhitespace);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to parse XML when attempting to reorder elements: " + e.Message);
                Console.Error.WriteLine(comment);
                editedComment = null;
                return false;
            }

            var rootElement = xml.Element("root");
            var orderedXml = rootElement.Elements().OrderBy(xElement => xElement,
                Comparer<XElement>.Create((n1, n2) =>
                {
                    // First, check if tags have a known ordering based on their names alone.
                    // Unknown tags will all have the same and lowest precedence.
                    var n1Pos = tagOrdering.TakeWhile(tag => tag != n1.Name.LocalName).Count();
                    var n2Pos = tagOrdering.TakeWhile(tag => tag != n2.Name.LocalName).Count();

                    if (n1Pos != n2Pos)
                    {
                        return n1Pos - n2Pos;
                    }

                    // Tags are the same, or they're both unknown.
                    // Use their original document ordering.
                    var n1PosOrig = rootElement.Elements().TakeWhile(xElement => !xElement.Equals(n1)).Count();
                    var n2PosOrig = rootElement.Elements().TakeWhile(xElement => !xElement.Equals(n2)).Count();

                    return n1PosOrig - n2PosOrig;
                })
            );

            IEnumerable<XNode> ElementsAndComments()
            {
                return rootElement.Nodes()
                    .Where(node => node.NodeType == System.Xml.XmlNodeType.Element || node.NodeType == System.Xml.XmlNodeType.Comment);
            }

            IEnumerable<XNode> GetComments(XNode element)
            {
                var reverseSearchResult = ElementsAndComments().Reverse().SkipWhile(n => n != element);
                var commentsReversed = reverseSearchResult.Skip(1).TakeWhile(n => n.NodeType == System.Xml.XmlNodeType.Comment);

                return commentsReversed.Reverse();
            }

            // Manually write each element fragment, since we cannot
            // easily write individual fragments without a root.
            var stringBuilder = new StringBuilder();
            foreach (var xElement in orderedXml)
            {
                foreach (var xComment in GetComments(xElement))
                {
                    stringBuilder.AppendLine(xComment.ToString(SaveOptions.DisableFormatting));
                }

                stringBuilder.AppendLine(xElement.ToString(SaveOptions.DisableFormatting));
            }

            // There might be extra comments not attached to any element.
            var trailingComments = ElementsAndComments()
                .Reverse()
                .TakeWhile(n => n.NodeType == System.Xml.XmlNodeType.Comment)
                .Reverse();

            foreach (var xComment in trailingComments)
            {
                stringBuilder.AppendLine(xComment.ToString(SaveOptions.DisableFormatting));
            }

            editedComment = stringBuilder.ToString().TrimEnd();
            return true;
        }
    }
}
