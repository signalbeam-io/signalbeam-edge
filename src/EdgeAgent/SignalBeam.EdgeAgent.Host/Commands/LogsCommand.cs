using System.CommandLine;
using Microsoft.Extensions.Configuration;
using SignalBeam.EdgeAgent.Host.Configuration;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class LogsCommand
{
    public static Command Create()
    {
        var command = new Command("logs", "Show the SignalBeam agent logs");

        var followOption = new Option<bool>("--follow")
        {
            Description = "Follow log output",
            DefaultValueFactory = _ => false
        };

        var linesOption = new Option<int>("--lines")
        {
            Description = "Number of lines to show",
            DefaultValueFactory = _ => 50
        };

        command.Options.Add(followOption);
        command.Options.Add(linesOption);

        command.SetAction((parseResult, _) => ExecuteAsync(
            parseResult.GetValue(followOption),
            parseResult.GetValue(linesOption)));

        return command;
    }

    private static async Task<int> ExecuteAsync(bool follow, int lines)
    {
        try
        {
            // Load configuration to get log file path
            var configuration = HostBuilder.BuildConfiguration();

            var logPath = configuration["Agent:LogFilePath"] ?? AgentPaths.DefaultLogFilePath;

            if (!File.Exists(logPath))
            {
                // Try to find the most recent log file
                var logDirectory = Path.GetDirectoryName(logPath);
                if (logDirectory != null && Directory.Exists(logDirectory))
                {
                    var logFiles = Directory.GetFiles(logDirectory, "agent*.log")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .ToList();

                    if (logFiles.Any())
                    {
                        logPath = logFiles.First();
                    }
                    else
                    {
                        Console.WriteLine($"❌ No log files found in {logDirectory}");
                        return 1;
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Log file not found: {logPath}");
                    Console.WriteLine("The agent may not have been run yet.");
                    return 1;
                }
            }

            Console.WriteLine($"📋 Showing logs from: {logPath}");
            Console.WriteLine();

            if (follow)
            {
                // Follow mode - tail the file
                await TailFileAsync(logPath);
            }
            else
            {
                // Show last N lines
                var allLines = await File.ReadAllLinesAsync(logPath);
                var linesToShow = allLines.TakeLast(lines);

                foreach (var line in linesToShow)
                {
                    Console.WriteLine(line);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to read logs: {ex.Message}");
            return 1;
        }
    }

    private static async Task TailFileAsync(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);

        // Go to end of file
        reader.BaseStream.Seek(0, SeekOrigin.End);

        Console.WriteLine("Following logs (press Ctrl+C to stop)...");
        Console.WriteLine();

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line != null)
            {
                Console.WriteLine(line);
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }
}
