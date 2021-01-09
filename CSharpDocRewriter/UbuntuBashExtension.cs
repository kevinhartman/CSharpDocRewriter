/*
 * https://loune.net/2017/06/running-shell-bash-commands-in-net-core/
 */

namespace CSharpFixes
{
    using System.Diagnostics;

    public static class UbuntuBashExtension
    {
        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
                
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ubuntu",
                    Arguments = $"run \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                }
            };

            process.Start();

            string result = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
            {
                System.Console.Error.WriteLine("\n ** Warning! Script command wrote to stderr:\n");
                System.Console.Error.WriteLine(error);
            }

            return result;
        }
    }
}