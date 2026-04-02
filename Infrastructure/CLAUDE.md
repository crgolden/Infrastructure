# Infrastructure CLAUDE.md

## Project Overview
Blazor Server application targeting .NET 10.

## JavaScript / TypeScript

### Configuration
A `tsconfig.json` exists at the project root (`Infrastructure/`) with `"target": "ESNext"` and `"allowJs": true`. This is the correct configuration — always use `ESNext`, never pin an older version without a documented reason.

### Visual Studio: False Positive Squiggles on Valid Modern JS Syntax
If Visual Studio shows errors like `( expected` on valid modern JavaScript syntax (e.g. optional catch binding `catch {}`), this is a Visual Studio IDE issue — **not a code or config file problem**.

**Fix:** Tools → Options → Text Editor → Languages → JavaScript/TypeScript → Language Service → disable "Dedicated syntax process" → restart Visual Studio.

Note: JS/TS settings are under **Text Editor → Languages**, not directly under Text Editor root.

The `tsconfig.json` is still correct to have but will not suppress these squiggles — the setting above must be changed per machine.
