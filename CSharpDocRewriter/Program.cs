namespace CSharpFixes
{
    using Microsoft.CodeAnalysis.CSharp;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;

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

        static int Main(string[] args)
        {
            // Create a root command with some options
            var rootCommand = new RootCommand
            {
                new Option<bool>(
                    "--automatic",
                    getDefaultValue: () => false,
                    description: "Don't open Vim. Instead, apply automatic changes, only."),
                new Option<bool>(
                    "--reorder-tags",
                    getDefaultValue: () => false,
                    description: "Reorder comment XML tags after edits."),
                new Argument<IEnumerable<string>>("files", "The list of files to visit.")
            };

            rootCommand.Description = "Iterates over all XML doc comments in the " +
                "provided source file(s) one-by-one, opening each for manual editing in Vim, " +
                "alongside the corresponding source-code element.";

            // Note that the parameters of the handler method are matched according
            // to the names of the options!
            rootCommand.Handler = CommandHandler.Create<bool, bool, IEnumerable<string>>(Visit);

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        // IMPORTANT!
        // The parameter names of this method must match those of 'rootCommand' above!
        static void Visit(bool automatic, bool reorderTags, IEnumerable<string> files)
        {
            if (files.Count() < 1)
            {
                throw new ArgumentException("You must provide at least one source file.");
            }

            if (!automatic)
            {
                DoPreamble();
            }

            var rewriter = new CSharpCommentRewriter(LoadSavedState(), AuthorFilter)
            {
                SkipEditor = automatic,
                TagOrdering = reorderTags ? CSharpCommentRewriter.DefaultTagOrdering : null
            };

            foreach (var filePath in files)
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

            if (rewriter.HasEditErrors)
            {
                Console.Error.WriteLine("\nErrors encounted during at least one edit.");
                Console.Error.WriteLine("Run the program again with the same files to retry only the failed edits.");
                
                if (automatic)
                {
                    Console.Error.WriteLine("Suggestion: remove --automatic and manually fix any invalid XML.");
                }
            }

            if (!rewriter.IsStopping)
            {
                Console.WriteLine("\nNothing left to do! Exiting...");
            }
        }
    }
}