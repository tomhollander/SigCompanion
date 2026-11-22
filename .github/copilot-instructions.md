# Copilot Instructions

## Project Guidelines
- For Sigen integration in this repo, build a direct OpenAPI REST wrapper for calling endpoints, not an MCP tool client.
- Use strict Sigen payload contracts only (`systemId` and `systemName` for system list); do not add flexible fallback parsing for unexpected payload shapes. Ask the user if payload format is uncertain.
- Sig Companion’s current Sigenergy tariff threshold defaults are deliberately 20/40 for buy and 6/20 for sell; keep corresponding test expectations aligned with those defaults.
- For SigCompanion's Sigenergy Modbus integration, read EV charging registers 31502 (DC) and 32003 (AC) as holding registers (function 0x03), matching the other plant reads. The user believes the device uses holding registers despite the documentation labeling them input registers; hardware confirmation is pending.

## Dashboard Instructions
- The dashboard gauge should use the battery icon from Assets and show `grid_disconnected.png` above SOC when the grid is not active.
- Enabled energy prices refresh in the existing two-minute dashboard polling loop. Tariff level names are independent of numeric levels, with numeric levels 1–6 mapped to magenta, blue, green, yellow, orange, and red, respectively.
- Sig Companion energy price features should also work in demo mode through DemoDataProvider, without live API requests or provider credentials.