using System;
using System.IO;

namespace CSharpFixes
{
    using Microsoft.CodeAnalysis.CSharp;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    class Program
    {
        private static string SaveFileLocation =>
            Environment.GetEnvironmentVariable("REWRITER_SAVE_LOCATION")
            ?? ".\\RewriterSavedState.json";

        private static string AuthorFilter =>
            Environment.GetEnvironmentVariable("REWRITER_GIT_AUTHOR_NAME");

        static IDictionary<string, string> LoadSavedState()
        {
            if (!File.Exists(SaveFileLocation))
            {
                return new Dictionary<string, string>();
            }

            var savedContents = File.ReadAllText(SaveFileLocation);
            Console.WriteLine($"Using save from: { Path.GetFullPath(SaveFileLocation) } ");

            return JsonSerializer.Deserialize<Dictionary<string, string>>(savedContents);
        }

        static void WriteSavedState(IDictionary<string, string> savedState)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var savedContents = JsonSerializer.Serialize(savedState, options);
            File.WriteAllText(SaveFileLocation, savedContents);
            Console.WriteLine($"Wrote save to: { Path.GetFullPath(SaveFileLocation) }");
        }

        static void DoPreamble()
        {
            var authorMessage = !string.IsNullOrWhiteSpace(AuthorFilter)
                ? $"Only visiting comments (touched) by: {AuthorFilter}"
                : "To visit only comments by a specific Git author, provide the author name\n" +
                  "as REWRITER_GIT_AUTHOR_NAME env var.";

            Console.WriteLine($@"
It's focus time!
We're about to visit every XML doc comment from the input files in Vim, one at a time.

Edit the current comment buffer only (changes made directly to the source file will be discarded).

If you need to take a break, delete everything inside the current XML comment buffer
to signal that you'd like to save and exit.

Your progress will be saved to: { Path.GetFullPath(SaveFileLocation) }

To make editing easier, the following Vim macros are available:

    @t  Make the current word into a <paramref> tag.
    @y  Make the current word into a <see> tag.
    @n  Insert a blank <returns> tag at the end of the comment.
    @m  Insert a blank <remarks> tag at the end of the comment.
    @p  Insert a <param> tag for the current word at the end of the comment.

{authorMessage}

Press any key to start.
");
            Console.ReadKey();
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("You must provide at least one source file.");
            }

            DoPreamble();

            var rewriter = new CSharpCommentRewriter(LoadSavedState(), AuthorFilter);

            foreach (var filePath in args)
            {
                if (rewriter.IsStopping)
                {
                    break;
                }

                string code = "";
                Encoding inputFileEncoding;
                using (StreamReader sr = new StreamReader(filePath))
                {
                    code = sr.ReadToEnd();
                    inputFileEncoding = sr.CurrentEncoding;
                }

                var tree = CSharpSyntaxTree.ParseText(code);
                var node = tree.GetRoot();

                Console.WriteLine($"Current file: { filePath }");
                rewriter.CurrentFilePath = filePath;
                var result = rewriter.Visit(node);

                using (StreamWriter sw = new StreamWriter(filePath, false, inputFileEncoding))
                {
                    sw.Write(result.ToFullString());
                }
            }

            WriteSavedState(rewriter.SavedState);

            if (!rewriter.IsStopping)
            {
                Console.WriteLine("\nNothing left to do! Exiting...");
            }
        }
    }
}