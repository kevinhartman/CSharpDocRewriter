/*
 * https://loune.net/2017/06/running-shell-bash-commands-in-net-core/
 */

namespace CSharpFixes
{
    using System;
    using System.Diagnostics;
    using System.Threading;

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
                    UseShellExecute = false,
                    CreateNoWindow = false,
                }
            };

            process.Start();

            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return result;
        }
    }
}