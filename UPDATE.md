Guidelines:
- Add a new line for each update with a date and short summary
- Example: 2026-01-01 — Fixed plant lifecycle timing
- Avoid sensitive data

Updates:
2026-01-03 — Restored Laboratory display animation lifecycle; preloaded display images; MachineLight cross-fade
2026-01-03 — Integrated MachineDisplayButton animation timeline; slide distance adjusted; ExtractButton enabled only after extension
2026-01-03 — Implemented extraction action: consume selected Resource, add resulting Liquids, and persist materials
2026-01-03 — Prevented full resource panel rebuild on extraction; update cached panel items to avoid flicker
2026-01-03 — Cleared cached Machine UI references on page cleanup
2026-01-03 — Added defensive gating to station click handlers to respect CanInteract and block premature clicks
2026-01-03 — Ensured player receives 1 grass_seed at startup if none are present
2026-01-03 — Removed Greenhouse auto-planting of a test grass plant in pot 3 when no saved plants exist

2026-01-04 — Implemented pixel-art 9-slice panels and panel items
2026-01-04 — Added CreateNineSlicePanel/CreateNineSlicePanelWithScroll/CreateSliceView to Services/UserInterfaceCreator.cs
2026-01-04 — Reworked per-item UI: `CreatePanelItem` uses two layered 9-slice backgrounds; selection toggles via cross-fade
2026-01-04 — Switched `LaboratoryPage` resource panel to the 9-slice panel with inner `ScrollView`
2026-01-04 — Switched `GreenhousePage` all panels to reuse the 9-slice panel API
2026-01-04 — Fixed layout: reserved corner space, equalized padding, centered icon/content

2026-01-05 — Positioned MovePanel directly to the right of BottomPanel using a shared bottom-row Grid
2026-01-05 — Fixed Windows layout skew and ensured bottom panel buttons receive input (ZIndex/InputTransparent)
2026-01-05 — Fixed recursion bug in `GreenhousePage` that caused repeated UpdateLiquidsPanel calls
2026-01-05 — Added `UserInterfaceCreator.CheckUiElementsExist` and assigned `AutomationId`s to dynamic UI elements
2026-01-05 — Optimized `Update*` methods in `GreenhousePage` and `LaboratoryPage` to create only missing UI elements (prevents duplicates)
