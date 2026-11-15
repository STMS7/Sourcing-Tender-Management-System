# Zaytouna design guide (one look for every cycle)

Use this when you build or restyle a page, by hand or with an AI assistant. The goal is that the Admin, Sourcing officer,
Supplier, Evaluation, Manager, Top Committee and Department pages all look like one product.

Stack reminder: ASP.NET Core MVC, Razor, Bootstrap 5, hand-written CSS, small vanilla JS. No Node, no Tailwind, no React.
Do not change controllers, models, routes, permissions or form field names when you restyle. Changes are markup and CSS.

## 1. Palette (Bustan Al-Zaytoun)

| Token | Hex | Use |
|---|---|---|
| Deep olive | `#3D4A2A` | headings, sidebar, primary buttons, selected tabs |
| Darkest olive | `#2E3920` | sidebar background, button hover |
| Olive | `#6B7B3A` | decorative fills, icons, bullets |
| Olive (text-safe) | `#56632E` | small labels and links on cream (contrast 4.5:1) |
| Button olive | `#5A6A30` | green buttons |
| Cream | `#F5F0E1` | page background |
| Warm white | `#FFFDF7` | cards, inputs |
| Sand | `#E4DCC8` | warm tints, borders |
| Light sage | `#E8EBD9` | icon circles, hover, quiet fills |
| Gold | `#C9A84C` | accent only: one highlighted item, active icon, a "waiting for you" dot |
| Ink | `#3F4733` | body text |
| Muted ink | `#5C6149` | secondary text |
| Success / Danger | `#2F6B2A` / `#B4411E` | status only, always with text |

Proportion: about 70% cream and warm white, 20% green, 10% gold. Gold never fills a large area.
The tokens exist as CSS variables on `main.zt-heritage` (`--o800`, `--o600`, `--cream100`, `--sage100`...) in `wwwroot/css/heritage.css`.
In your own CSS use `var(--o800, #3D4A2A)` so the page still works if the variable is missing.

## 2. Type and spacing

- Headings: Bricolage Grotesque 600. Body and buttons: DM Sans. Small labels and numbers in tables: DM Mono (uppercase, letter-spaced).
- Page title `clamp(1.9rem, 1.4rem + 1.8vw, 2.75rem)`, section title about 1.1 to 1.6rem, body 1rem / 1.6.
- Page width: content max 1280px. Mobile gutter 16px. One title per page.
- Admin rhythm for lists: compact header, table head 48px, rows 61px, 16px cell padding, pager 12px below.

## 3. The card (glass)

```css
.card-like {
  background: linear-gradient(150deg, rgba(255,253,247,.84), rgba(255,253,247,.58));
  border: 1px solid rgba(255,255,255,.8);
  border-radius: 6px 28px 6px 28px;              /* leaf corners: two opposite corners round */
  box-shadow: 0 1px 0 rgba(255,255,255,.9) inset, 0 0 0 1px rgba(61,74,42,.07), 0 20px 36px -26px rgba(46,57,32,.5);
  -webkit-backdrop-filter: blur(14px) saturate(1.3); backdrop-filter: blur(14px) saturate(1.3);
}
@supports not ((backdrop-filter: blur(1px)) or (-webkit-backdrop-filter: blur(1px))) { .card-like { background: #FFFDF7; } }
```

Plain Bootstrap `.card`, `.table-responsive` and dashboard tiles already get this on inner pages through `heritage.css`.
Admin pages get it through `admin-palette.css` (colours and card look only). Do not stack a glass card inside a glass card:
inside a panel, reset the inner table wrapper (see `.ow-panel .table-responsive`).

Dark card: deep olive gradient with cream text, used once per screen at most.
Status chip: small, leaf-shaped (`border-radius: 2px 10px 2px 10px`), tinted background, status text always visible.

## 4. Page arrangement

1. Header: small mono kicker, title, one line of description, primary action on the right.
2. Summary cards (KPIs) with icon, number, label and one short note. Only show numbers that exist in the data.
3. Quick actions (2 to 4) that all lead somewhere real. If an action is not available, show it as a dashed, dimmed card with the reason.
4. Body in one or two columns. Two columns only from 1400px wide; below that stack them so tables keep their full width.
5. Long record pages use tabs (Overview first) and keep every section reachable. Without JavaScript all sections stay visible.
6. Navigation lives in the sidebar (`_Layout.cshtml`, `links` list). No second tab bar above the page except real in-page tabs.
7. Messaging pages: inbox, conversation, details. New message opens in a dialog, not a form above the list.

## 5. Behaviour rules

- Server-side authorization stays exactly as it is. Hiding a button is never protection.
- Every list has an empty state; every async action has loading and error text; destructive actions ask first.
- Animation: only `transform` and `opacity`, 150 to 300 ms, and always inside `prefers-reduced-motion`.
- Focus rings visible, labels tied to inputs, contrast 4.5:1, status never by colour alone.
- Tables: inside `.table-responsive`, header in DM Mono, no inner scrollbar on desktop.

## 6. Check before you push

Open the page at 375, 768 and 1280 px. There must be no horizontal page scroll, no clipped text and no overlapping cards.
Use each role that can see the page. Run the real actions (create, save, send, delete) and then remove the test rows.

## 7. Prompt for an AI assistant

Copy this, then add the page name and the file paths:

```
Restyle <PAGE> for the Zaytouna procurement system. Follow docs/DESIGN_GUIDE.md exactly.
Palette and tokens, fonts, glass card recipe, page arrangement and rules are defined there; do not invent new colours.
Rules: change markup and CSS only. Keep every controller, model, route, permission, form field name, id and data- attribute.
Reuse existing partials and the classes in wwwroot/css/heritage.css, officer-workspace.css and officer-tender.css before writing new ones.
Put new styles in a new css file for this page, not in site.css, layout.css or content.css.
Do not use dummy data: show only what exists in the model. If a requested element has no data, leave it out and say so.
Build to a temporary folder, test as every role that can open the page at 375/768/1280 px, run the real actions, delete test data,
and list what you could not verify. Do not commit or push unless asked.
```

## 8. Where things live

- `wwwroot/css/heritage.css`: the shared skin for inner pages and dashboards (tokens, glass cards, tables, forms, buttons). Edit it directly.
- `wwwroot/css/admin-palette.css`: palette and card look for the admin area only (loaded when `body.zt-admin`).
- `wwwroot/css/officer-workspace.css`: officer dashboard and Messages components (`ow-*`).
- `wwwroot/css/officer-tender.css`: tender details components (`od-*`).
- `wwwroot/css/cycle.css`: hides the old officer tab bar (navigation is in the sidebar).
