using System;
using System.IO;
using System.Linq;

namespace CSharpFixes
{
    using Microsoft.CodeAnalysis.CSharp;

    class Program
    {
        static void Main(string[] args)
        {            
            if (args.Length == 0)
            {
                throw new ArgumentException("You must provide at least one source file.");
            }

            var myRewriter = new CSharpCommentRewriter();

            foreach (var filePath in args)
            {
                string code = "";
                using (StreamReader sr = new StreamReader(filePath))
                {
                    code = sr.ReadToEnd();
                }

                var tree = CSharpSyntaxTree.ParseText(code);
                var node = tree.GetRoot();

                using (StreamWriter sw = new StreamWriter(filePath))
                {
                    var result = myRewriter.Visit(node);
                    sw.Write(result);
                }
            }
        }
    }
}