# Boki

Unity project: `Source/Game/`. Open it with Unity **6000.6.7f1**, as pinned in `Source/Game/ProjectSettings/ProjectVersion.txt`. The project uses the Universal Render Pipeline and Input System.

## Repository structure

- `Build/`: Windows player (`.exe`, data folder, DLLs, and runtime files). See [packaging instructions](Build/README.md). No player binary is included in this checkout yet.
- `Documentation/`: team design documents, implementation plans, and verification notes.
- `Source/Game/`: Unity project; select this folder in Unity Hub.
- `Source/music/`, `Source/sfx/`, `Source/output/`, and `Source/tools/`: audio sources, art sources, previews, and generation tools. Run documented generator commands from the repository root.

## Development setup

The target development machine runs Windows 11 Education with 16 GB RAM and a Ryzen 5600G. Check gameplay performance on the Ryzen 5600G. Validate Windows-specific behavior in the Windows Editor when developing on Linux.

## Contributor guidance

Read [AGENTS.md](AGENTS.md) for branch policy, scene ownership, Unity asset handling, naming, and task-specific documentation references. All documentation prose is English; established Slovenian quotations, character prompts, and references retain their original wording.

## Design references

Read the relevant sections for your task rather than all documents upfront:

- [Gameplay](Documentation/Gameplay.md): mechanics, canonical route matrix, state, and acceptance checks.
- [Story](Documentation/Story.md): narrative, character dialogue, atmosphere, and visual direction.
- [Architecture](Documentation/Architecture.md): planned Unity modules, scene integration, platform checks, and milestones.

These documents describe the intended design; they do not imply that the systems are already implemented.

## Player effects preview

The first boost trails and damage flash have an isolated [preview and integration guide](Documentation/PlayerEffects.md). Open `Source/Game/Assets/Scenes/BokiEffectsPreview.unity` in the pinned Editor and press Play; hold Left Shift for trails and press H for a damage flash.
