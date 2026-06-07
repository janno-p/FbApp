# AGENTS.md

## Scope

- These instructions apply to the Bun-managed Vite/Elm package in this directory.
- Prefer these local instructions over repo-level web notes when working under `src/FbApp.Web`.
- This package is separate from the .NET services in the repository root.

## Commands

- Install dependencies with `bun install`.
- Run the development server with `bun run dev`.
- Build the production bundle with `bun run build`.
- Preview the production bundle with `bun run preview`.
- Run tests with `bun test` if tests are added.
- Type-check with `bunx tsc --noEmit`.

## Bun Conventions

- Default to Bun instead of Node.js for this package.
- Use `bun <file>` instead of `node <file>` or `ts-node <file>`.
- Use `bun run <script>` instead of `npm run`, `yarn run`, or `pnpm run`.
- Use `bunx <package> <command>` instead of `npx <package> <command>`.
- Keep `bun.lock` in sync when dependencies change.
- Bun loads `.env` automatically for Bun-run commands; do not add `dotenv` for this package.

## Implementation Notes

- Keep TypeScript strict-mode friendly and aligned with `tsconfig.json`.
- Do not add package-manager files for npm, Yarn, or pnpm unless the package is intentionally migrated away from Bun.
