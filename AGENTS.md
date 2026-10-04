# Agent development rules

## Scope and communication

- All communication, source code, comments, documentation, identifiers, and agent-created project content must be written in English.
- Work within the user's requested scope. Do not implement gameplay or add speculative systems without a task requesting them.
- Inspect existing code and applicable instructions before editing. Keep changes small and focused; preserve unrelated user changes.
- Do not commit, push, rewrite Git history, or perform destructive cleanup unless the user explicitly authorizes it.

## Project vision and architectural direction

ColonyGame is intended to become a colony/settlement simulation inspired in part by games and systems such as MineColonies.

Development begins with a deliberately simple top-down prototype using primitive visuals. The long-term goal is a fully 3D game where the player can explore their settlement directly, potentially switching between first-person and third-person perspectives.

- Treat the simulation layer and presentation layer as separate concerns.
- Core simulation systems should not depend unnecessarily on specific cameras, character models, animations, UI, or other presentation details.
- Design colonists, needs, skills, jobs, tasks, resources, inventories, buildings, production, economy, quests, progression, and world state so their simulation logic can survive the transition from the early top-down prototype to the later full 3D presentation.
- Avoid coupling core simulation logic directly to MonoBehaviour unless Unity lifecycle or scene integration genuinely requires it. Prefer plain C# domain logic where practical.
- Do not overengineer speculative systems before they are needed. Apply this direction to systems as they are introduced; it is not a requirement to implement them in advance.
- Build incrementally through small playable vertical slices.
- Preserve compatibility with Unity 6.6 / 6000.6.4f1 unless an upgrade is explicitly approved by the user.
- All source code, comments, documentation, identifiers, and agent-created project content must be written in English.

## Unity project integrity

- Use Unity 6.6 editor version 6000.6.4f1, as recorded in ProjectSettings/ProjectVersion.txt. Do not upgrade the editor without explicit authorization.
- Preserve asset GUIDs. Keep every asset or folder change paired with its corresponding .meta file. Prefer Unity Editor operations for moving or renaming assets.
- Do not hand-author or regenerate .meta files for existing assets. Let Unity generate metadata for new assets and include it with the asset.
- Track Assets/, Packages/manifest.json, Packages/packages-lock.json, and ProjectSettings/.
- Never add Library/, Temp/, Logs/, UserSettings/, IDE caches, generated project files, or build outputs to Git.
- Put generated builds in the root Build/ or Builds/ directory.
- Change ProjectSettings, packages, scenes, or rendering/input configuration only when required by the current task; explain the reason and resulting changes.
- Do not edit generated files or manually rewrite serialized Unity assets when a safer Editor workflow is available.

## Implementation and validation

- Follow existing project conventions. Prefer clear, maintainable C# and avoid unnecessary dependencies or abstractions.
- Never include credentials, tokens, private keys, or machine-specific absolute paths in committed content.
- Validate in proportion to the change. For code changes, check compilation and run relevant available Edit Mode or Play Mode tests. For scene or runtime behavior changes, verify the affected behavior in Unity when available.
- Do not claim tests, imports, or builds passed unless they actually ran. If the required tooling is unavailable, report the limitation and the checks still needed.
- Before handing work back, inspect the diff and Git status for accidental asset, metadata, settings, or package changes.
- Report what changed, what was checked, and anything the user should review.
