# Code Coverage Auto-Generation

This directory contains a script that automatically generates the `coverage-import.spec.ts` file to ensure all TypeScript files are included in code coverage reports.

## How It Works

The `generate-coverage-imports.js` script:
1. Scans the `src/app` directory recursively for all `.ts` files
2. Excludes spec files (`.spec.ts`), definition files (`.d.ts`), and the generated file itself
3. Generates import statements for all discovered files
4. Creates `src/app/coverage-import.spec.ts` with all the imports

## Usage

### Automatic (Recommended)
The script runs automatically before tests via the `test:ci` npm script:
```bash
npm run test:ci
```

### Manual
You can also run the script manually:
```bash
npm run generate-coverage-imports
```

## Why This Approach?

Angular 20's new build system (esbuild-based) doesn't support `require.context` or `import.meta.glob` patterns that were commonly used in webpack-based setups. This script-based approach provides the same benefits:

- ✅ All files automatically included in coverage
- ✅ No manual maintenance required
- ✅ New files automatically picked up
- ✅ Script runs before each CI test run

## Generated File

The generated `src/app/coverage-import.spec.ts` file:
- Contains import statements for all source files
- Has a simple test that ensures the imports are processed
- Should NOT be manually edited (it will be overwritten)
- Should be committed to version control

This ensures that code coverage reports show **all** files in the codebase, including those without dedicated test files, providing complete visibility into which files lack test coverage.
