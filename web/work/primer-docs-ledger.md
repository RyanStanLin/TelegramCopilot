# Primer documentation ledger

- Project: TelegramCopilot
- Date: 2026-11-07 Asia/Taipei
- Configured MCP: primerdoc / npx @primer/mcp; native tool namespace absent from this chat.
- Direct standard stdio MCP client: tools/primer_mcp.py, @primer/mcp 1.1.2 (npm registry verified).
- Raw official responses: work/primer/ (local, ignored).
- Init, all patterns, coding guidelines and foundations read before the implementation plan below.
- Full component catalog requested; invalid responses were retried and retained as failures. Official-source fallback was explicitly authorized by the user.
- Selected component API, demos, usage and accessibility evidence are the implementation basis; invalid/unselected catalog helpers are not claimed as verified components.

## 2026-11-07 recovery and implementation scope

- init read; all 19 patterns enumerated, fetched and read. Exact live catalog: Copy, Create, Customize, Delegate, Delete, Edit, Filter, Search, View; Data Visualization, Degraded Experiences, Empty States, Feature Onboarding, Forms, Loading, Navigation, Notification message, Progressive disclosure, Saving.
- Coding guidelines, specs, token usage, color and typography read. Specs examples contain raw CSS and inconsistent motion naming; installed semantic variables take precedence.
- Full de-duplicated component catalog fetched with API/examples/usage/accessibility requests. 97 responses invalid (empty or Primer landing page), all 97 retried, all remain invalid. User explicitly authorized official primer/react sources/examples and relevant patterns/accessibility as fallback.
- Full catalog and raw responses preserved locally under work/primer; no invalid response is counted as a read guide. Catalog contains unsupported/retired helper names; Header, Flash, FeatureFlags, Hidden, Octicon, Pagehead, AriaAlert/AriaStatus, FilteredSearch, IssueLabel are not selected.
- Implementation components: ThemeProvider/BaseStyles (init), Stack/Item, PageLayout/Header/Pane/Content, PageHeader/TitleArea/Title/Actions, NavList/Item, Heading, Button (default/primary/danger), FormControl/Label/Caption/Validation, TextInput, Textarea, Select/Option, Checkbox, Spinner, Banner, Link, Table compound components (experimental).
- APIs and in-API demos read for selected components. Dedicated guides are used where valid; Forms, Saving, Navigation, Loading and Degraded Experiences supply the interaction contracts where dedicated usage pages are absent. Official source/types were checked against installed 38.45.0. The release build validates actual exports and props.

## UI Implementation Plan

- `/login`: creation/task surface, one global passkey registration or authentication. No account switch or invented identity. Standard form feedback.
- `/targets`, `/accounts`, `/schedules`, `/destinations`, `/rules`, `/settings`: data-list/settings parent with named resources, search, exact count, bounded pagination, row actions; full-page creation/edit forms with one explicit save/cancel. Dependencies are chosen by name, not hand-entered IDs.
- `/presence`: detail/overview of one selected target. Shareable date/timezone/state/period filters; observed totals and coverage, contribution calendar plus equivalent data table, interval table and CSV. Unknown stays visibly distinct. No fictitious data on empty installation.
- `/events`, `/deliveries`: data-list history, time/entity filters and pagination.
- Shell: neutral global header, PageLayout standard container/pane widths and spacing; NavList changes URL. Pane reflows on narrow viewport; primary action belongs to PageHeader. No custom breakpoint or speculative pixel measurements.
- Component mapping: page structure → PageLayout/PageHeader; flow → Stack; controls → Primer form controls; records → Table; errors → Banner; asynchronous work → Spinner/native Button loading. Form labels persist, errors focus the first field or form-level message. Unsaved edits have cancellation protection.
- Custom extension: contribution calendar is actual data visualization, no Primer contribution React component exists in the catalog. Semantic contribution tokens encode observed online frequency, hatch/outline marks unknown days, each date has a textual accessible name and equivalent table. Standard buttons remain Primer; no custom native controls.
- Styling: CSS Modules only; semantic bg/fg/border/text/stack/control/contribution tokens. No Box/sx, decorative gradients, emojis, shadows, arbitrary metrics or icon substitutes. Source files below 200 physical lines. zh-CN/en dictionaries, Intl dates/numbers, preference persistence without input loss.
- Validation: TypeScript, production build, live Chromium/virtual WebAuthn, config CRUD, statistics/CSV, narrow viewport, keyboard, locales; final CSS via MCP lint_css. No claim of pixel equality without a reference screenshot.

## Final implementation evidence

- Versions: @primer/react 38.45.0, @primer/primitives 11.10.0, @primer/octicons-react 19.42.0. ThemeProvider uses the official `@primer/react/next` entry point; BaseStyles and the documented html theme attributes are installed at the root.
- Official package sources: `dist/DataTable/Table.d.ts` and `Table.js` supply `gridTemplateColumns`, absent from the MCP Table props summary. Table uses this public prop; no internal selector is overridden.
- Primer's theme stylesheet defines colors; explicit official base/functional size, border, radius and typography styles supply the remaining variables. Runtime geometry checks verify that table columns and calendar cells actually resolve.
- ConfirmationDialog hard-codes its English close label without a public override in 38.45.0. The implementation uses Dialog's documented renderHeader, Header, Title and footerButtons APIs, composed with Stack and a localized Primer Button. Dialog retains focus trapping, Escape and return focus.
- Actual icons: SearchIcon and PlusIcon. MCP icon requests returned the icon index; official installed Octicons exports/types and Search/Create examples supply the fallback evidence.
- Calendar extension: seven weekday rows, a leading partial week, real date range, semantic contribution levels, dashed unknown coverage, accessible daily links, and numeric statistics. Cell size is the official medium control token; tiny decorative hit targets were avoided. Legend keys use the verified base-size-12 token.
- Final CSS modules: Root.module.css, Shell.module.css, Contribution.module.css. All passed the official MCP lint_css and local @primer/stylelint-config checks. Raw lint responses stay in work/primer/lint-final-*.
- Browser checks cover zh-CN/en, persistence after reload, retaining a dirty form across language changes, raw user names that match translation keys, all six configuration workflows, validation focus, confirmation/Escape/focus return, CSV and error retry. Responsive checks use 1280/768/375/320 CSS px, dark mode and 200% text scaling.
- Pixel equality: Unverified. There is no task-specific GitHub reference screenshot with known viewport/DPR/zoom. Screenshots were inspected for structure, grouping, clipping and actual token resolution, without claiming a 1:1 reproduction or a full screen-reader audit.

## Official source pointers

- https://primer.style/product/ui-patterns/
- https://primer.style/product/getting-started/react/
- https://primer.style/product/components/data-table/
- https://primer.style/product/components/dialog/
- https://github.com/primer/react/tree/main/packages/react/src
- https://github.com/primer/primitives
- https://github.com/primer/octicons
