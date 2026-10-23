// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.Diagnostics;
using System.Text.Json;
using JBIDS.Extraction;

/// <summary>Exercises the real helper process against a fake native Codex CLI.</summary>
internal static class LocalHelperChecks
{
    /// <summary>Implements the fake CLI inside the checks executable.</summary>
    /// <param name="args">The simulated Codex arguments.</param>
    /// <returns>The fake exit code.</returns>
    public static async Task<int> FakeAsync(string[] args)
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null || Environment.GetEnvironmentVariable("CODEX_API_KEY") is not null)
        {
            return 91;
        }

        if (args.Contains("status"))
        {
            Console.Error.WriteLine(Environment.GetEnvironmentVariable("JBIDS_FAKE_API") == "1" ? "Logged in using an API key" : "Logged in using ChatGPT");
            return 0;
        }

        if (!args.Contains("forced_login_method=\"chatgpt\"") || !args.Contains("--sandbox") || !args.Contains("read-only") || !args.Contains("--output-schema"))
        {
            return 92;
        }

        var prompt = await Console.In.ReadToEndAsync();
        var marker = prompt.IndexOf("SOURCE DATA:", StringComparison.Ordinal);
        var data = prompt.Substring(marker + "SOURCE DATA:".Length);
        using var document = JsonDocument.Parse(data);
        var source = document.RootElement.GetProperty("source");
        var title = source.GetProperty("title").GetString();
        await File.AppendAllTextAsync(Environment.GetEnvironmentVariable("JBIDS_FAKE_LOG") ?? throw new InvalidOperationException("Missing fake log."), title + "\n");
        if (title == "Fail once" && Environment.GetEnvironmentVariable("JBIDS_FAKE_FAIL") == "1")
        {
            return 1;
        }

        var index = Array.IndexOf(args, "--output-last-message");
        var result = new { objectives = new[] { new CandidateObjective("one", null, "Implement CRUD operations.", "Apply", null, "Implement CRUD operations.") } };
        await File.WriteAllTextAsync(args[index + 1], JsonSerializer.Serialize(result, ExtractionJson.Options));
        return 0;
    }

    /// <summary>Checks subscription authentication, structured output and resuming partial results.</summary>
    /// <returns>The asynchronous checks.</returns>
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "JBIDS.Checks", "bin", "helper check " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "request with spaces.json");
            var sources = new[]
            {
                new SourceInput(1, Guid.NewGuid(), "First", "Implement CRUD operations."),
                new SourceInput(2, Guid.NewGuid(), "Fail once", "Implement CRUD operations."),
            };
            var request = new ExtractionRequest(1, Guid.NewGuid(), sources, Array.Empty<ExistingObjective>());
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(request, ExtractionJson.Options));
            var log = Path.Combine(directory, "calls.txt");
            var resultPath = Path.Combine(directory, "request with spaces.results.json");
            Assert(await Run(true, true) == 1 && !File.Exists(resultPath), "API-key authentication is refused.");
            Assert(await Run(false, true) == 1, "Failed source produces a partial result.");
            var partial = JsonSerializer.Deserialize<ExtractionResponse>(await File.ReadAllTextAsync(resultPath), ExtractionJson.Options) ?? throw new InvalidOperationException("Missing results.");
            Assert(partial.Sources.Length == 1, "Only a successful source is checkpointed.");
            Assert(await Run(false, false) == 0, "A retry resumes and completes the request.");
            var complete = JsonSerializer.Deserialize<ExtractionResponse>(await File.ReadAllTextAsync(resultPath), ExtractionJson.Options) ?? throw new InvalidOperationException("Missing results.");
            Assert(complete.Sources.Length == 2 && (await File.ReadAllLinesAsync(log)).Count(l => l == "First") == 1, "Completed sources are not submitted again on resume.");
            Console.WriteLine("PASS: real local-helper process, ChatGPT-only authentication, removed API keys, structured output, paths with spaces, partial failure and resume (fake Codex; no account usage).");

            async Task<int> Run(bool api, bool fail)
            {
                var helper = Path.Combine(AppContext.BaseDirectory, "JBIDS.LocalHelper.dll");
                var fake = Path.ChangeExtension(typeof(Program).Assembly.Location, OperatingSystem.IsWindows() ? ".exe" : null);
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(helper);
                start.ArgumentList.Add(input);
                start.Environment["TEMP"] = directory;
                start.Environment["TMP"] = directory;
                start.Environment["JBIDS_CODEX_PATH"] = fake;
                start.Environment["JBIDS_FAKE_CODEX"] = "1";
                start.Environment["JBIDS_FAKE_API"] = api ? "1" : "0";
                start.Environment["JBIDS_FAKE_FAIL"] = fail ? "1" : "0";
                start.Environment["JBIDS_FAKE_LOG"] = log;
                start.Environment["OPENAI_API_KEY"] = "synthetic-api-key-must-be-removed";
                start.Environment["CODEX_API_KEY"] = "synthetic-api-key-must-be-removed";
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Helper did not start.");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch
                {
                    process.Kill(entireProcessTree: true);
                    throw;
                }

                var output = await stdout + await stderr;
                if ((!api && !File.Exists(resultPath)) || process.ExitCode != (api || fail ? 1 : 0))
                {
                    throw new InvalidOperationException(output);
                }

                return process.ExitCode;
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
