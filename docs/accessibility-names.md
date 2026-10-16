# Accessible names in the main workspace (#143)

Composite button content does not reliably become a WPF UI Automation Name.
Main navigation, shortcut actions, capture commands/media/modes and import controls
therefore have explicit localized `AutomationProperties.Name` values. The delay
and history-filter selectors describe their purpose independently of their value.
Existing catalog entries are reused; user content, commands and appearance remain
unchanged.

The capture post-action command binds its accessible name to the existing displayed
mode text, so changing between direct/editor presentation cannot leave a stale name.
The preview Expander has a localized name which its actual header ToggleButton
forwards through the template. This labels the operable header without adding a
tab stop or assigning names to unrelated internal controls.

`--accessibility-names-smoke <output>` runs before normal startup, uses a synthetic
pilot profile and presents the actual MainWindow without activation outside the
screen. It retains the original Window namescope, so ElementName bindings are tested
as used in production. It does not start hooks, tray, recording, network updates or
access the personal clipboard. Preferences and sample user content are checked for
preservation.

Run `scripts/accessibility-names-smoke.ps1 -Executable <exe> -OutputDirectory <folder>`
against the compiled or published executable. The test covers pt-BR/en/es,
Light/Dark/System, reference/minimum sizes and top/bottom navigation through all six
views. It checks exact localized purposes for 23 named targets, samples the enabled,
focusable tab stops exposed in the actual provider control view, rejects empty names,
checks the dynamic label and keeps internal combo toggles non-focusable. It does not
alter names to make the assertions pass. Build Windows runs this regression and
retains its output with the About evidence.

These are provider/layout and programmatic interaction checks, not a certification
of Narrator/NVDA speech, physical keyboard use, high contrast, mixed monitor DPI or
hardware. The integrated candidate review in #70 retains those checks. Contrast
(#144) and GIF preview translation (#145) remain separate changes.
