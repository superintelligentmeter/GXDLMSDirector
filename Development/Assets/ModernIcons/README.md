# Gurux command icons

Original vector family for GXDLMSDirector: a 24-unit grid, 1.65-unit rounded strokes, slate `#334155` and blue `#3056d3`. Read arrows point out of the meter; write arrows point into it. The twelve toolbar commands and corresponding menu items use the same glyphs. Additional glyphs cover disconnect, add device, cancel, and refresh.

`ModernIcons.cs` is the geometry source of truth. It renders at the current toolbar DPI and exports these SVGs and transparent PNGs at 16, 20, 24, 32, and 48 pixels. High-contrast rendering uses the system foreground color. WinForms retains command enabled/checked state, keyboard behavior, event handlers, and tooltips.

Run `migration/Verify.ps1` from the workspace root to regenerate exports and verify the toolbar mapping. `icon-family.png` shows the complete family; `migration/results/modern-toolbar.png` shows the actual rendered toolbar, including disabled commands.

License: GPL-2.0-only. Modified for the Intelmeter fork, 2026-10-16. See the repository LICENSE and ASSET-PROVENANCE.txt. Historical migration harness paths below/above are not included in this fork; use the included C# geometry and ExportAssets methods.
