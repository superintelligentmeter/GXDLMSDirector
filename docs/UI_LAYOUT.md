# Property editor layout

This rule applies to the application's object views, supporting dialogs, Settings and dashboard editors. Preserve the current fonts, colors, native editor behavior, DLMS bindings and Gurux.DLMS package.

## Standard property sections

- Use an upper-left aligned property region with a maximum width of **800 logical pixels at 96 DPI**. Do not stretch simple settings across a large monitor.
- Use one **144 logical-pixel label column**, including the label-to-field gap. It may shrink to **100** in narrow containers. Wrap long labels and measure their height rather than cutting them off or moving individual inputs to unrelated columns.
- Align peer input left and right edges. The field or field-plus-action group has a **640 logical-pixel maximum**. The region reserves up to 16 pixels at its right edge. On smaller windows, use the available space; these are maximums, not minimum window sizes.
- Keep standard object rows on the existing 40-pixel rhythm, with native preferred editor heights and 4-pixel vertical insets. Dialog rows use their native preferred heights and measured margins. Do not stretch native comboboxes vertically.
- Labels and controls remain in reading/tab order. Keep the original bound instances and handlers when moving them into a layout table.
- Implement standard rows through `EditorLayoutPolicy.ConfigurePropertyTable`. Use `ConstrainSection` only for explicitly compound property regions. Recompute sizes from constants and the current `DeviceDpi`; never multiply previously scaled values or fonts.

## Content-specific exceptions

- Tables, object lists, trees, charts, documents, multiline values and structured DLMS values may fill the available width. In mixed screens, ordinary scalar rows stay bounded while data areas expand. A value changing from scalar to structured must lift its scalar cap.
- Compact address, range, date/time, identifier, radio and repeated tariff groups retain widths appropriate to their content. Their containing property region stays bounded; related controls stay together.
- Profile Generic keeps paired metadata/read-range columns and its full-width wrapping sort-object reference. Activity Calendar keeps full-width profile lists beneath bounded property headers. Device Properties retains its deliberate two-column connection layout.
- Dashboard cards/charts retain user-saved widths. The Add OBIS pane remains bounded by the dashboard sidebar; its fields share edges, with top labels normally and the existing 76-pixel label rail at short heights. Do not add an outer scrollbar.
- External Gurux media property panels keep their vendor-owned internal layout inside a bounded host. About/version information and document/list dialogs use their content-driven layout. Do not rewrite an external package to change its label rail.
- Every exception must be named in the screen coverage ledger with its reason. "Legacy designer width" is not a reason.

## Verification

Enumerate all compiled screens, not only the screens with recent screenshots. Render each affected screen and relevant tabs with empty, representative and long content. Check normal, narrow and restored-wide states; compare input bounds and typography. Exercise available real monitor scales and minimize/maximize. Explicitly report unavailable physical monitor tests; synthetic scaling does not count as a real monitor round trip. Inspect captures as well as assertions. Use offline fixtures and temporary storage, preserving user meter files/preferences.

## Basis

Microsoft's [Windows layout guidance](https://learn.microsoft.com/en-us/windows/win32/uxguide/vis-layout) recommends capped form widths, consistent peer sizing/alignment and dimensions suited to expected content. [GOV.UK text input guidance](https://design-system.service.gov.uk/components/text-input/) likewise relates input width to expected content and available space. The 800/144/640 dimensions are this application's convention, chosen to match its existing compact editor style; they are not prescribed by those sources.

## Recorded review

[Application screen coverage](UI_LAYOUT_DIRECTOR_COVERAGE.json): all 21 inventoried host screens reviewed on 2026-10-30, with 317 retained rendered states and focused confirmations after corrections. Dashboard checks also passed. The companion UI repository records its 73 object views and 43 supporting dialogs separately. Physical coverage was 120 DPI (125%); current 96 DPI and cross-monitor verification remains pending because the second display was disconnected.
