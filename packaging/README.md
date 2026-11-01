# Windows x64 Release packaging

Added 2026-11-01. GPL-2.0-only; upstream notices remain applicable.

Clone the Director and companion UI forks into sibling directories named
`GXDLMSDirector` and `Gurux.DLMS.UI.Net`. Use Windows, PowerShell 7, Git and the
.NET 10 SDK. Both source checkouts must be clean so BUILD-MANIFEST.json identifies
the exact source commits. Run from the Director repository:

```powershell
./packaging/Publish-Windows.ps1
# Optional feature-free variant:
./packaging/Publish-Windows.ps1 -DisableDashboards
```

The profile publishes the application and companion UI in optimized Release mode
for win-x64, including the .NET runtime. The official Gurux.DLMS NuGet dependency
is unchanged. Trimming and single-file bundling are disabled to preserve WinForms
resources, reflection and external content. No Copilot, Node.js or WebView2 is
introduced into this fork.

Each invocation creates an ignored packaging/artifacts subdirectory containing:

- The complete app folder and a portable application ZIP.
- A separate private PDB archive for crash diagnosis.
- SHA256SUMS.txt and a build manifest recording both commits, SDK, runtime versions
  and the publish command.

Extract the entire application ZIP before launching GXDLMSDirector.exe. This is
a portable Release candidate, not a Windows installer. Installation shortcuts,
uninstall support and code signing require a separate installer workflow.

The packaging script retains existing application notices and copies licenses
from the exact .NET runtime packages selected by restore. It does not retrieve
or certify complete corresponding source for all third-party dependencies.
Follow RELEASE-COMPLIANCE.txt before publishing binaries: supply matching source
for both forks and every covered dependency, verify notices and source builds,
and test the exact package on a clean Windows x64 machine. Preserve private
symbols internally. Committing these build scripts does not publish binaries.
