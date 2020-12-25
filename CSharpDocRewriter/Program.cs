using System;
using System.IO;
using System.Linq;

namespace CSharpFixes
{
    using Microsoft.CodeAnalysis.CSharp;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    class Program
    {
        private static string BackupPath;

        static IDictionary<string, string> LoadBackup()
        {
            if (!File.Exists(BackupPath))
            {
                return new Dictionary<string, string>();
            }

            var backupContents = File.ReadAllText(BackupPath);
            Console.WriteLine($"Using backup from: { BackupPath }");

            return JsonSerializer.Deserialize<Dictionary<string, string>>(backupContents);
        }

        static void SaveBackup(IDictionary<string, string> backup)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var backupContents = JsonSerializer.Serialize(backup, options);
            File.WriteAllText(BackupPath, backupContents);
            Console.WriteLine($"Wrote backup to: { BackupPath }");
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                throw new ArgumentException("You must provide at least one source file.");
            }

            BackupPath = args.First();
            var rewriter = new CSharpCommentRewriter(LoadBackup());

            foreach (var filePath in args.Skip(1))
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

                Console.WriteLine($"Editing file: { filePath }");
                rewriter.CurrentFilePath = filePath;
                var result = rewriter.Visit(node);

                using (StreamWriter sw = new StreamWriter(filePath, false, inputFileEncoding))
                {
                    sw.Write(result.ToFullString());
                }
            }

            SaveBackup(rewriter.Backup);
        }
    }
}