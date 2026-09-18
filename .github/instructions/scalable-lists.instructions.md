---
description: "Use when building or changing list endpoints or list UI (events, goods, services, chat, admin) that can grow in size. Ensures search, filtering, and paging are considered from the start."
applyTo: "api/Functions/**/*.cs, frontend/src/**/*.tsx"
---
# Scalable Lists

Community content (events, goods, services, users, messages) grows over time, so lists must not assume a small, fixed dataset.

- Don't rely on a plain `.Take(N)` cap as the only limit (see existing `.Take(200)` calls in `api/Functions/*.cs`) — when adding or touching a list endpoint, prefer real paging (e.g. `skip`/`take` or cursor-based query params) over a hardcoded cap.
- New list endpoints should accept optional query parameters for search (text match on the relevant field) and filtering (status, category, date range, etc.) rather than returning everything and filtering client-side.
- On the frontend, list views should be built assuming pagination/infinite scroll and a search/filter input will be added, even if the first version only wires up the basic list — don't hardcode "load everything at once" in a way that's hard to extend.
- Keep it simple: don't build a generic pagination/filtering framework speculatively — implement it for the list at hand, following the same shape as other paged lists once one exists.
