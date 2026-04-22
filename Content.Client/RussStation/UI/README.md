# Fork UI controls

Shared client-side controls that exist to replace or extend Robust's UI
primitives. Reach for these before introducing a new variant.

## Controls

- **`HonkFilterPanel<TKey>`** — inline collapsible multi-select filter
  tray. Fork standard for any window that pairs a search field with
  categorical filters. Same surface as Robust's
  `MultiselectOptionButton<TKey>` (`AddItem`, `SelectedKeys`,
  `SelectedLabels`, `DeselectAll`, `OnItemSelected`) so existing callers
  drop in with a type swap. The header mounts inline with the search row;
  the body drops under the row when expanded. Right-click the header to
  reset filters.

- **`RightClickClearTextBoxController`** — global right-click-to-clear on
  any focused, editable `LineEdit`. Installed once, fires the
  `OnTextChanged` event so downstream search handlers see the clear. Use
  it instead of adding a dedicated "clear" button next to a search bar.

## When to add here

Drop a control in this directory when it is:

- A style or UX choice the fork wants applied everywhere (e.g. filter trays,
  text-input shortcuts), or
- A generic primitive that doesn't belong to any one feature.

Feature-local controls stay next to the feature (`Content.Client/<feature>/UI/`).
