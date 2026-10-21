---
name: Infrastructure Monitor
colors:
  healthy: "#198754"
  degraded: "#ffc107"
  unhealthy: "#dc3545"
  unknown: "#6c757d"
  info: "#0dcaf0"
  navbar: "#212529"
  surface: "#f8f9fa"
typography:
  body:
    fontFamily: "Bootstrap's system sans-serif stack (--bs-font-sans-serif)"
    fontSize: "1rem"
rounded:
  default: "0.375rem"
---

# Design Documentation

## Overview

One authenticated page, the service health dashboard, plus a sign-out action. It is built from stock Bootstrap 5.3.8
classes with no custom CSS: in Development the stylesheet is the copy in `wwwroot/lib/bootstrap`, elsewhere the
jsDelivr CDN with that copy as the fallback. Status is the only thing the colors carry.

## Colors

| Token | Bootstrap class | Meaning |
|---|---|---|
| `healthy` | `border-success`, `bg-success` | the service answered as expected |
| `degraded` | `border-warning`, `bg-warning text-dark` | the check reported degraded |
| `unhealthy` | `border-danger`, `bg-danger` | the check failed |
| `unknown` | `border-secondary`, `bg-secondary` | not yet checked, or an unrecognized status |
| `info` | `text-info`, `list-group-item-info` | the brand mark; a SignalR reconnect in the log |

The navbar is `navbar-dark bg-dark`; the page background is `bg-light`.

## Typography

Bootstrap's defaults throughout. The page title is `h1.h3`; card headers are `fw-semibold`; descriptions, check times
and log entries are `small text-muted`.

## Layout

- A dark top navbar with the brand ("Infrastructure Monitor"), the last-update time and a **Log out** button.
- A `container-fluid` main area: a title row with the connection badge, then a responsive grid of service cards
  (`row-cols-1`, `row-cols-sm-2`, `row-cols-lg-3`, `g-4`), then the Alert Log card.
- A footer line pinned to the bottom (`d-flex flex-column min-vh-100` on `body`, `mt-auto` on the footer).

## Components

### Service card

One per monitored service, created on its first result and updated in place after that: a `card h-100 border-2` whose
border color is the status, a header with the service name (truncated), a large status badge, the check's description
and a footer with the time it was checked. A card's `id` is `svc-` plus the service name lowercased with every
non-alphanumeric character replaced by `-`.

### Connection badge

Shows the SignalR connection: `Connecting…` (secondary), `Connected` (success), `Reconnecting…` (warning), and
`Disconnected` or `Error` (danger).

### Alert log

A card listing status transitions newest first, at most 50 entries, cleared by its **Clear** button: a move to
Unhealthy (danger), a recovery from Unhealthy to Healthy (success), a move to Degraded (warning), and SignalR reconnects
(info) or disconnects (warning). The page only logs transitions it sees after loading.

## Behavior

The page holds no data of its own. It connects to `/hubs/health` with automatic reconnect and renders each
`ReceiveSnapshot` the server pushes; it never polls.

## Do's and Don'ts

- Do use Bootstrap utility and component classes; don't add a stylesheet.
- Do keep status as the only meaning of color, so a glance at the grid reads as health.
- Don't log steady state in the alert log: only transitions, as the server alerts only on transitions.
