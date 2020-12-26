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
        private static string BackupPath =>
            Environment.GetEnvironmentVariable("REWRITER_BACKUP_PATH")
            ?? ".\\CommentBackup.json";

        static IDictionary<string, string> LoadBackup()
        {
            if (!File.Exists(BackupPath))
            {
                return new Dictionary<string, string>();
            }

            var backupContents = File.ReadAllText(BackupPath);
            Console.WriteLine($"Using backup from: { Path.GetFullPath(BackupPath) } ");

            return JsonSerializer.Deserialize<Dictionary<string, string>>(backupContents);
        }

        static void SaveBackup(IDictionary<string, string> backup)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var backupContents = JsonSerializer.Serialize(backup, options);
            File.WriteAllText(BackupPath, backupContents);
            Console.WriteLine($"Wrote backup to: { Path.GetFullPath(BackupPath) }");
        }

        static void DoPreamble()
        {
            Console.WriteLine($@"
Hello.
We're about to visit every XML doc comment from the input files in Vim, one at a time.

Edit the current comment buffer only (changes made directly to the source file will be discarded).

If you need to take a break, delete everything inside the current XML comment buffer
to signal that you'd like to save and exit.

Your progress will be saved to: { Path.GetFullPath(BackupPath) }

To make editing easier, the following Vim macros are available:

    @t  Make the current word into a <paramref> tag.
    @y  Make the current word into a <see> tag.
    @n  Insert a blank <returns> tag at the end of the comment.
    @m  Insert a blank <remarks> tag at the end of the comment.

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

            var rewriter = new CSharpCommentRewriter(LoadBackup());

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

            SaveBackup(rewriter.Backup);

            if (!rewriter.IsStopping)
            {
                Console.WriteLine("\nNothing left to do! Exiting...");
            }
        }
    }
}