using System.Diagnostics;

namespace SuperRender.Browser;

/// <summary>
/// Cross-platform clipboard access using platform-native commands.
/// </summary>
internal static class ClipboardHelper
{
    public static string GetText()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                return RunProcess("pbpaste");

            if (OperatingSystem.IsLinux())
                return RunProcess("xclip", "-selection clipboard -o");

            if (OperatingSystem.IsWindows())
                return RunProcess("powershell.exe", "-command Get-Clipboard");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clipboard] Read failed: {ex.Message}");
        }

        return "";
    }

    public static void SetText(string text)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                WriteToProcess("pbcopy", "", text);
            else if (OperatingSystem.IsLinux())
                WriteToProcess("xclip", "-selection clipboard", text);
            else if (OperatingSystem.IsWindows())
                WriteToProcess("powershell.exe", "-command $input | Set-Clipboard", text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clipboard] Write failed: {ex.Message}");
        }
    }

    private static string RunProcess(string fileName, string arguments = "")
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(2000);
        return output;
    }

    private static void WriteToProcess(string fileName, string arguments, string input)
    {
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        proc.Start();
        proc.StandardInput.Write(input);
        proc.StandardInput.Close();
        proc.WaitForExit(2000);
    }
}
