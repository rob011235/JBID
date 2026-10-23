## JBIDS local extraction helper

Requires the .NET 10 runtime and the official OpenAI Codex CLI. Install Codex using the instructions at https://developers.openai.com/codex/cli and sign in with your ChatGPT account by running Sign-in.cmd. Your subscription's Codex usage limits apply. This helper requires ChatGPT authentication, removes API-key environment variables from child processes and forces ChatGPT login mode. There is no paid API fallback. JBIDS never receives your account credentials.

1. Unzip the entire download into a folder.
2. Run Sign-in.cmd once. Complete sign-in in your browser.
3. Download an extraction JSON file from your project's Extract candidate objectives page.
4. Drag that JSON file onto Extract.cmd.
5. Upload the adjacent .results.json file back to the same JBIDS project while signed in to the same JBIDS account that downloaded it.

The helper sends the supplied announcement text and existing objective text to OpenAI through Codex. Only use sources you intend to process there. It does not run a background service or accept inbound network connections. No JBIDS server address or credentials are embedded in the package, so the same helper works with local and hosted sites.

A successful announcement is checkpointed after extraction, including an empty objective list. Running the same input resumes its saved results and retries failed announcements. Upload any partial results to save successful work. If a request becomes stale because announcement text or reused objectives changed, export a fresh request. Interrupt with Ctrl+C to stop the current Codex process.

On macOS/Linux use `dotnet JBIDS.LocalHelper.dll --login` and `dotnet JBIDS.LocalHelper.dll /path/to/request.json` from a terminal. If Codex cannot be found, set JBIDS_CODEX_PATH to the full native executable path (codex.exe on Windows), not a .cmd or .ps1 wrapper.

Build the downloadable archive from the repository with:

    powershell -File scripts/Package-LocalHelper.ps1

Run the packaging script before starting/publishing JBIDS.Server so the static download is included. The download is framework-dependent; it does not bundle .NET or Codex.
