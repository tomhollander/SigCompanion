# Copilot Instructions

## Project Guidelines
- For Sigen integration in this repo, build a direct OpenAPI REST wrapper for calling endpoints, not an MCP tool client.
- Use strict Sigen payload contracts only (`systemId` and `systemName` for system list); do not add flexible fallback parsing for unexpected payload shapes. Ask the user if payload format is uncertain.
- For Sig Companion’s Amber integration, always use the first site returned by /sites and cache its ID for the app session per configured key. Map Amber descriptors explicitly: extremelyLow → Ext Low/1, veryLow → Very Low/2, low → Low/3, neutral → Neutral/4, high → High/5, spike → Spike/6; show any other descriptor verbatim at level 0 (gray).

## Dashboard Instructions
- The dashboard gauge should use the battery icon from Assets and show `grid_disconnected.png` above SOC when the grid is not active.
- Enabled energy prices refresh in the existing two-minute dashboard polling loop. Tariff level names are independent of numeric levels, with numeric levels 1–6 mapped to magenta, blue, green, yellow, orange, and red, respectively.
- Sig Companion energy price features should also work in demo mode through DemoDataProvider, without live API requests or provider credentials.