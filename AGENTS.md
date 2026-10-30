# Repository Guidelines & Preferences

## Git Synchronization Policy
- **Always Keep Up-to-Date with Origin/Upstream:**
  - At the start of every session or task, check whether the repository and current branch are up-to-date with `origin/master`.
  - If the branch is behind `origin/master`, proactively notify the user and pull/fast-forward the latest changes.
  - Remote definitions:
    - `origin`: The canonical repository (`https://github.com/PlayGrifball/GrifballWebApp.git`).
    - `upstream`: Also points to `https://github.com/PlayGrifball/GrifballWebApp.git`.
  - Keep the local `master` branch aligned with `origin/master`.
