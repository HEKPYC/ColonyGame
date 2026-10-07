# ColonyGame

A Unity Universal 3D project. The project includes the initial application-shell source: main menu, profiles, settings, and lobby audio. The standalone settlement prototype includes economy, Town Hall/building progression, production, Builder jobs, housing, Tavern recruitment and day/night. See [settlement implementation and Play Mode checklist](Docs/SettlementPrototype.md). See [application shell setup and validation](Docs/MenuShell.md) for the required scene setup and test checklist.

## Requirements

- Unity 6.6, editor version **6000.6.4f1** (see `ProjectSettings/ProjectVersion.txt`).
- Unity Hub is recommended for installing and opening the matching editor.
- Git for version control.

## Getting started

1. Clone the private repository using an account with access.
2. In Unity Hub, add this repository's root folder as an existing project.
3. Open it with Unity **6000.6.4f1** and allow package restoration and asset import to finish.
4. Follow [application shell setup](Docs/MenuShell.md) to create and open `Assets/Scenes/MainMenu.unity`.

The project uses the Universal Render Pipeline (URP). Package dependencies are defined in `Packages/manifest.json` and resolved in `Packages/packages-lock.json`.

## Repository layout

- `Assets/`: project assets, scenes, scripts, and their `.meta` files.
- `Packages/`: package manifest and lock file.
- `ProjectSettings/`: shared Unity project configuration.
- `AGENTS.md`: development rules for AI agents.

Unity caches, local editor preferences, generated IDE files, and build outputs are excluded by `.gitignore`. Place generated builds under `Build/` or `Builds/` at the repository root.

## Development

Use the pinned editor version. Keep asset changes together with their `.meta` files and avoid unrelated scene, settings, or package changes. Never commit credentials or generated caches. No automated build or CI workflow is configured yet.
