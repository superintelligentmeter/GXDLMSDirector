# Set 02 — Soft Duo

An alternate original family for the same 16 Gurux commands: rounded silhouettes, mint fills (#c5e2db), jade accents (#0f9488), and deep petrol details (#1e494e).

Includes editable 24-unit SVGs and transparent PNGs at 16, 20, 24, 32, and 48 pixels. The contact sheet shows 36px and 20px examples. Read/write arrows retain their outward/inward direction relative to the meter.

This is a retained alternate family. Director now uses Studio Outline. Icons render at the current DPI; Windows Forms renders disabled commands in grayscale. Windows high-contrast mode uses the system-color outline family to preserve readability.

The reproducible geometry and exporter are in GXDLMSDirector/Development/SoftIcons.cs. Regenerate from the workspace root:

```powershell
dotnet run --project migration/IconSetTwo/IconSetTwo.csproj -c Release -- GXDLMSDirector/Development/Assets/SoftDuoIcons
```
