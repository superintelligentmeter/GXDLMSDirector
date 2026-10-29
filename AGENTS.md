# Repository instructions

## UI quality is part of completion

Treat every UI change as a review of the complete affected screen and its related dialogs. Follow the application's existing WinForms style. Inspect rendered results before calling the work finished; a successful build or passing behavior tests alone do not establish UI quality.

- Check spacing, alignment, typography, contrast, control sizes, visible labels, keyboard focus/order, disabled/loading/error states, resizing and scrolling.
- Exercise realistic content: many objects, multiple meters, long names/values/errors, empty results, disconnected or missing meters, and saved layouts.
- Review both normal and smaller supported windows. Check DPI-aware sizing without introducing global font overrides or repeated scaling. Clearly identify physical multi-monitor behavior that has not been verified.
- Render the relevant screens together, fix observed issues in a bounded pass, then confirm the result. Include the main workspace, picker, edit state, charts/tables, Settings, and affected dialogs when changing dashboards.
- Preserve earlier decisions and existing functionality. Fix concrete UI defects without replacing the established visual style.

## Dashboard decisions to preserve

- Dashboards are embedded in the main application, with their own screen and a tree root alongside Devices. No extra Objects/Dashboards tab, toolbar Dashboards button, or View > Dashboards item.
- A dashboard can contain measurements from multiple loaded meters. The OBIS picker references the same objects as Devices, lists all objects for its selected meter, labels Meter clearly, and explains unavailable selections.
- Follow the user-provided Electrical overview layout reference for rounded cards/picker, full-width monitoring and notice bars, three-card rows where space permits, and wide charts below. Keep existing application fonts/colors and the removed workspace subtitle.
- Keep top chrome compact: use reduced title, monitoring and notice padding so cards retain more vertical room. Inputs and dropdowns use rounded containers around native editors; preserve fonts/colors and keyboard behavior.
- The Add OBIS pane has no outer scrollbar. Let the object list scroll, adapt labels beside fields at short heights, and use the full workspace on small windows; keep all fields, Close and Add visible.
- Dashboards autosave; do not reintroduce a manual Save button. Done only exits editing. Preserve debouncing, completed-resize writes, close flushing, failed-load protection and visible retry on save failure.
- Keep Edit layout and Done at the same width so toggling edit mode does not move header buttons.
- The button reads Add OBIS, without a plus sign. Dashboard buttons share the existing rounded style and solid focus treatment; avoid dotted custom button outlines.
- Drag tiles or chart/table headers to reorder. Resize at the bottom-right corner and save both order and size. Do not restore Up/Down/Remove buttons. Show delete at the top-right on hover, with keyboard access through Edit layout.
- Keep the workspace subtitle removed. Align refresh controls, monitoring commands and header actions; keep menus on the owning monitor.
- For DPI-sensitive dashboard changes, use the available real 100%/125% displays and exercise round trips plus minimize/maximize with the picker open and closed. Compare dashboard body text with the application/Devices tree at its CURRENT scale, preserving heading/value proportions to that body size. Do not force fixed startup point sizes after a monitor move. Check current-DPI button bounds, saved logical tile sizes, picker fields and cards rebuilt after moving. Apply font roles after controls finish parenting; do not multiply previously scaled fonts or override global typography.
- When checking Add OBIS typography, inspect every nested label/editor/button immediately after first opening and reopening the hidden pane after monitor moves, before maximize can mask deferred DPI changes. Verify the heading label and header actions do not overlap, not just that each button fits its own panel.
- Native dialogs must belong to the actual main window. Preserve the existing application typography and DPI fixes.
- Enable dashboards through Tools > Settings > Dashboards; save on OK and apply after restart. Preserve saved definitions and meter identities when disabled.
- Keep implementation in Development/Features/Dashboards behind its adapter. Retain enabled and feature-free builds. Keep the existing Gurux.DLMS NuGet package unchanged.

## Validation and delivery

Use offline fixtures and temporary storage for automated UI checks; do not alter a user's meter files, preferences or dashboard library. Report what was rendered and tested, and distinguish remaining hardware checks. Keep changes local unless pushing is explicitly authorized.
