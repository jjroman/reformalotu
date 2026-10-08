---
name: reformalotu-site-style
description: Create or update any ReformaloTu website page or component while preserving its calm, warm, editorial visual identity. Use across the home page, authentication, project flows, forms, navigation, dashboards, and shared site styles.
---

# ReformaloTu site style

Use this skill for visual or interaction changes anywhere in the ReformaloTu website—not only the marketing landing page. This includes shared navigation and layout, Identity pages, project creation and editing, project history and detail, recommendation and inventory views, metrics, empty/error states, forms, and responsive behavior. Extend the existing design rather than introducing a second visual system.

## Source of truth

- The current web application is the Razor Pages project in `login/`. Inspect the relevant page and its PageModel, plus the shared shell [`login/Pages/Shared/_Layout.cshtml`](../../../login/Pages/Shared/_Layout.cshtml) and navigation partial [`login/Pages/Shared/_LoginPartial.cshtml`](../../../login/Pages/Shared/_LoginPartial.cshtml), before changing a site surface.
- The shared stylesheet is [`login/wwwroot/css/site.css`](../../../login/wwwroot/css/site.css). The shared layout loads Bootstrap first, then `site.css`, then generated page styles. Keep this load order and avoid adding a second global stylesheet, inline `<style>` blocks, or duplicated component rules.
- The landing page [`login/Pages/Index.cshtml`](../../../login/Pages/Index.cshtml) is the visual reference, not the only page this skill covers. Reuse its palette, type, whitespace, surface, control, and interaction language in appropriate forms across the product.
- Before editing, locate existing components and CSS for the page, reuse them, and keep selectors scoped to the smallest relevant surface. Use `landing-` only for elements that are truly specific to the marketing landing. For shared site components use clear shared names; for product-only features use a feature/page prefix such as `project-` or `metrics-`.
- Do not apply marketing-hero layouts, oversized editorial headings, decorative preview illustrations, or conversion CTA treatments indiscriminately to operational screens. Product pages should share the brand tone while keeping information dense enough to complete tasks efficiently.
- Keep existing application behavior, Razor routes, validation, Identity security, and accessibility intact while styling. Never hide authorization or validation messages as a visual shortcut.

## Visual identity

The complete site should feel calm, considered, welcoming, and editorial—not like a generic SaaS dashboard, a default Bootstrap template, or a loud construction marketplace.

- Use generous but purposeful whitespace, a clear hierarchy, concise readable copy, soft natural surfaces, and restrained decoration. On product pages prioritize scannability, task completion, and clear status feedback.
- Use Georgia or a compatible serif for prominent editorial headings, with medium weight, tight line-height, and slightly negative tracking. Use the existing system sans-serif stack for body text, forms, navigation, buttons, tables, and dense data.
- Save two-column compositions, oversized typography, and illustrative project previews for marketing or genuinely explanatory content. Use simpler, compact layouts for authentication, editing, histories, metrics, and other utility screens.
- Round large feature surfaces generously; keep cards, fields, and controls less rounded. Use subtle borders and diffuse shadows rather than heavy outlines or strong elevation. Do not wrap every item in a card.
- Reuse the established surfaces instead of introducing a competing palette: use the sage hero gradient for broad supporting panels and focused white surfaces for forms or content. Match the homepage preview card’s subtle border, diffuse shadow, and 17px radius; keep fields and buttons less rounded and avoid an overall beige/brown cast.
- Keep scale proportional to purpose: reserve oversized headings for editorial landing content. Utility and form screens should use compact prominent headings (roughly 1.75–2.25rem), system sans-serif labels and body copy, fields around 54px high, and primary actions around 48–52px. Treat these as fluid starting points, not fixed dimensions, and retain comfortable touch targets.
- Use simple inline SVG or restrained typographic symbols for decoration. Product icons should be consistent, secondary to content, and accessible.
- Keep the experience Spanish-first and consistent with the existing `lang="es"` shell. Preserve functional content and avoid mixing languages in new UI. Do not rewrite generated Identity content or localization as an unrelated styling change.

## Color palette

Use the existing CSS custom properties in `:root` as the canonical brand palette throughout the site:

| Token | Current value | Role |
| --- | --- | --- |
| `--landing-ink` | `#192a25` | Primary headings and important text |
| `--landing-muted` | `#64716b` | Supporting text |
| `--landing-green` | `#28745a` | Brand emphasis, links, and primary actions |
| `--landing-green-dark` | `#1c5b45` | Hover and pressed primary-action state |
| `--landing-cream` | `#f6f7f1` | Warm neutral surface |

Current supporting colors include white `#fff`, sage/cream hero stops `#f5f7f1`, `#f2f5eb`, `#e8eee1`, and muted ochre accent `#d28d49`. Secondary sage, sand, and lilac benefit icons use `#eaf0e7`, `#f5eee3`, and `#efedf3`.

- Although these existing tokens currently have a `--landing-` prefix, treat them as the current site palette and reuse them on other pages. Do not make a broad token rename just to change a page; if a future task calls for a shared-token refactor, update every usage together and preserve visual parity.
- Prefer the canonical variables over retyping brand colors. For new shared site colors, introduce a small named token in `:root` rather than scattering raw values. Keep neutral, semantic, and interactive roles distinguishable.
- For new shades, choose muted tints of the existing green, warm cream, and ochre families. Avoid introducing another saturated accent or using color as the only indicator of meaning.
- Use brand green for primary actions and selected/emphasized states, not for every link, badge, and surface. Keep secondary actions neutral. Use explicit accessible colors for success, warning, and error states that still harmonize with the palette.
- Use readable foreground/background contrast. Do not make muted text so pale that it becomes difficult to read.
- Do not change the palette tokens casually. If a brand-level adjustment is required, update the shared tokens and review the landing, authentication, navigation, forms, project pages, and status states together.

## Layout and responsive behavior

- Match the existing Bootstrap container and centered max-width. Do not add a competing full-width layout around a page. Respect the existing shared header, main content, and footer shell.
- Use the current breakpoints (`991.98px` tablet and `767.98px` mobile) unless the page's actual content requires a justified adjustment. Keep responsive behavior consistent across the site.
- On mobile, stack multi-column content and forms, allow actions to wrap, make controls comfortably tappable, preserve readable type, and prevent long IDs/descriptions from forcing overflow.
- Check representative desktop, tablet, and mobile sizes after layout changes. There should be no horizontal scrolling, clipped buttons, overlapping navigation, inaccessible dialogs, or unreadably narrow content.
- Avoid fixed heights for content that may grow. Prefer responsive grid/flex layouts, `min-height`, and fluid `clamp()` sizing.
- Operational pages should keep related fields and information grouped, labels visible, error/help text close to their controls, and primary/secondary actions in predictable positions.

## Shared components and page patterns

- Primary actions use brand green, bold readable text, and a clear hover/focus state. Secondary actions remain visually quiet and neutral. Use button patterns consistently in forms and navigation; reserve the marketing arrow treatment for marketing CTAs when appropriate.
- Forms use persistent labels, clear required/optional indication, inline validation and error messages, and a visible submit action. Preserve server-side validation and validation-summary behavior.
- Group secondary links by intent: place recovery or help links beside the related preference or field, and keep account creation or confirmation links together in a quieter row after primary or external sign-in actions. Let link rows wrap on small screens. When rearranging interactive links, keep DOM, screen-reader, and keyboard order aligned with the visual order; avoid CSS-only reordering or absolute positioning.
- Project lists and recommendation results should have clear hierarchy, consistent spacing, and useful empty/loading/error states. Keep metadata secondary to the main project/video information.
- Status, category, and count badges should be restrained and meaningful. Do not use a decorative badge for information that matters to completing the task.
- Keep one clear `<h1>` per page, meaningful section headings in order, descriptive link labels, semantic landmarks, and visible keyboard focus.
- Use real Razor Pages routes and preserve authorization-sensitive navigation and ownership behavior. The interface must not be the only access-control layer.
- Only claim capabilities that actually exist. Do not invent testimonials, usage statistics, prices, guarantees, integrations, or finished project imagery. Any sample preview must be clearly illustrative and consistent with the real product.
- Describe materials and tools as items *mentioned or detected in transcript text*. Do not imply that keyword matches guarantee a complete shopping list, exact quantities, safety suitability, or professional advice.

## Interaction and accessibility

- Keep hover states subtle and consistent across the site; do not rely on hover alone to communicate clickability.
- Retain clear `:focus-visible` behavior and keyboard access. Do not remove global focus styles to achieve a visual match.
- Honor `prefers-reduced-motion: reduce`; keep transitions short and disable nonessential movement for users who request reduced motion.
- Mark decorative icons/illustrations `aria-hidden="true"`; give meaningful imagery and previews an accessible label or equivalent text.
- Do not encode essential copy solely in an image, pseudo-element, or color.
- Keep text contrast, form error associations, heading order, link purpose, and accessible names intact when changing markup or styling. Do not make keyboard focus invisible.

## Editing and verification checklist

1. Identify the affected page, its PageModel, shared layout, partials, and existing CSS. Read those before editing; do not assume all pages use the same content pattern.
2. Reuse the shared palette, typography, spacing, controls, and page shell. Use `landing-` only for landing-only markup; scope new page-specific rules and avoid unrelated global restyling.
3. Preserve functionality, authorization, validation, real route destinations, and truthful product copy. Confirm that unauthenticated and authenticated navigation remain appropriate.
4. Build the app with `dotnet build .\login\login.csproj`.
5. If a browser is available, inspect the affected page at desktop, tablet, and mobile widths. Check overflow, navigation, CTA destinations, form errors/empty states, keyboard focus, and the final rendered typography.
6. Check the final change for duplicate CSS, unused selectors, broken Razor markup, inaccessible states, and accidental changes to shared styles.
