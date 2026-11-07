# Copilot Instructions

## Project Guidelines
- For Sigen integration in this repo, build a direct OpenAPI REST wrapper for calling endpoints, not an MCP tool client.
- Use strict Sigen payload contracts only (`systemId` and `systemName` for system list); do not add flexible fallback parsing for unexpected payload shapes. Ask the user if payload format is uncertain.