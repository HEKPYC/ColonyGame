# Application shell

## Editor setup

Use Unity 6000.6.4f1. Let the new scripts and theme import, then choose **ColonyGame > Create Main Menu Scene** once. This command:

- creates `Assets/ColonyGame/MenuPanelSettings.asset`;
- creates `Assets/Scenes/MainMenu.unity` with a Menu Shell GameObject;
- adds a UI Document, MenuShell, LobbyAudio, AudioSource, and AudioListener;
- assigns the theme and resolution scaling;
- creates a Menu Background Camera that clears Display 1 behind the screen-space UI, renders no scene layers, and has no additional AudioListener;
- places MainMenu first in the build scene list while preserving existing scene entries.

The command refuses to overwrite an existing MainMenu scene. It asks to save modified scenes before proceeding in an interactive Editor. Open MainMenu and enter Play Mode to use the shell. No UXML or prefabs are needed: MenuShell creates the UI Toolkit visual tree at runtime. Unity generates metadata for newly created assets.

The theme imports Unity's built-in runtime controls using the documented `unity-theme://default` theme URL: https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-uss/uie-tss

## Responsibilities

- `Scripts/Presentation/MenuShell.cs`: screen navigation and UI callbacks.
- `Scripts/Profiles/ProfileStore.cs`: profile metadata and filesystem operations; it has no scene or lifecycle dependency. It uses Unity JsonUtility only for serialization.
- `Scripts/Settings/AudioPreferences.cs`: global preferences, using PlayerPrefs and namespaced keys. Separate music enablement and volume fields can coexist with future audio categories without adding those categories now.
- `Scripts/Presentation/LobbyAudio.cs`: one looping, non-spatial AudioSource. Screen navigation does not recreate or restart it.
- `Editor/ShellSetup.cs`: scene setup and isolated storage/settings validation commands.

Main Menu routes to Singleplayer, Multiplayer, and Settings. Singleplayer routes to profile creation/listing, a separate delete confirmation screen, or the selected profile's placeholder screen. Every screen has a return or cancel route. All screens live within MainMenu; no simulation or networking is present.

## Profile storage

Each profile has its own directory:

`Path.Combine(Application.persistentDataPath, "Saves", "Profiles", profileId, "profile.json")`

With the current DefaultCompany / ColonyGame configuration, the expected Windows path is:

`%USERPROFILE%/AppData/LocalLow/DefaultCompany/ColonyGame/Saves/Profiles/<id>/profile.json`

Use `Application.persistentDataPath` as the runtime authority rather than hardcoding that Windows path. Company or product name changes can change the root location.

Metadata contains a GUID ID (N format), trimmed custom name, ISO 8601 UTC creation and last-played timestamps, and saveFormatVersion = 1. New profiles have an empty last-played value until Play is selected. Names may repeat; IDs distinguish profiles. Names are limited to 80 characters for usability, but profile count has no fixed cap and the list scrolls.

Writes use a temporary file and replace the destination. Unreadable, invalid, or unsupported-version metadata is skipped with a visible warning and left intact. IO failures display an error instead of navigating away. Future world saves belong inside the profile directory; no world data is implemented now. Confirmed deletion removes the entire selected profile directory, including any future world files.

## Settings and audio

PlayerPrefs keys:

- `ColonyGame.Audio.LobbyMusicEnabled` (integer boolean; default on)
- `ColonyGame.Audio.MusicVolume` (float 0–1; default 0.6)

Changes are saved immediately. The slider displays the rounded 0–100 percentage immediately. Off sets AudioSource.mute without changing MusicVolume; On unmutes at the saved volume. No Master/SFX/Ambience/Voice controls are implemented yet.

To add music, select **Menu Shell** in MainMenu and assign an authorized AudioClip to **Lobby Audio > Lobby Music**. Save the scene. No clip is supplied; silence is expected and is handled safely. The source stays alive across screen changes, including the placeholder profile screen. This is a single-scene foundation; persistence across future gameplay scene loads will need to be added when those scenes exist.

## Validation checklist

Automated Editor commands (temporary profile directories and isolated test preference keys):

1. **ColonyGame > Validation > Validate Profile Storage** verifies distinct IDs, timestamps/version, reload, last-played persistence, selected deletion, corrupt-data preservation, and blank-name rejection.
2. **ColonyGame > Validation > Validate Audio Settings** verifies preferences reload, 63% retained while muted, 63% restored on unmute, and safe missing-clip handling.

Manual Play Mode and standalone checks:

1. Launch MainMenu: verify Singleplayer, Multiplayer, Settings, Quit; inspect the Console for errors. Confirm Display 1 has no "No cameras rendering" overlay and there is only one enabled AudioListener.
2. Create two named profiles; add enough to require scrolling. Verify blank names show an error, and identical names still represent separate profiles.
3. Stop and restart Play Mode/application: verify the profiles remain.
4. Play each profile: verify the correct name and placeholder message, then return to Main Menu. Verify the last-played timestamp changes only on Play.
5. Delete a profile: Cancel must preserve it; Delete permanently must remove only the chosen profile. Restart and verify deletion persists.
6. Verify Multiplayer displays Multiplayer / Coming Soon and returns to Main Menu.
7. Set volume to 63%; verify the displayed percentage updates while dragging. Switch Lobby Music off, restart, then on: verify it remains 63%.
8. Assign a test clip you have permission to use: verify audible volume/mute behavior and uninterrupted music through navigation. Remove the clip and verify no errors.
9. Check mouse and keyboard interaction (Tab, arrows, Enter/Space), scrolling, and layout at 1280x720, 1920x1080, 2560x1440, ultrawide, and 4:3 resolutions.
10. Verify Quit stops Play Mode in Editor and closes a standalone player. Confirm a standalone build starts at MainMenu.

## Current environment validation

The source was compiled against the installed Unity 6000.6.4f1 assemblies using the bundled C# compiler. This does not replace Unity's import, player build, or Play Mode validation.

The sandboxed Editor could generate the new source/theme metadata but repeatedly failed to connect to its licensing client. Scene generation and the Editor validation commands did not complete. Run the setup command and validation checklist above in your normal Unity Editor before treating this slice as fully validated.

## Menu background camera and Panel Renderer warning

MainMenu contains a scene-local, enabled orthographic Menu Background Camera targeting Display 1 (targetDisplay = 0). It clears to the same dark color as the menu backdrop, uses Culling Mask Nothing, and has no render texture or camera stack. HDR, MSAA, occlusion culling, URP shadows, post-processing, and depth/color texture requests are disabled. The screen-space UI Document continues rendering independently above the camera. The existing AudioListener stays on Menu Shell.

The scene setup command creates the same configuration for future new MainMenu scenes. It still refuses to overwrite an existing scene.

Unity 6.6 marks UI Document obsolete but explicitly supports existing components. Panel Renderer offers improved lifecycle handling, performance, and world-space integration. Migration is not required to fix the Game View camera overlay or keep this small, single-screen-space-panel prototype working. Keep the warning visible and revisit migration when UI work needs those capabilities or an approved Unity upgrade changes compatibility; no migration has been performed here.

Reference: https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-create-ui-document-component

Camera-fix validation: the setup code was compiled against the installed Unity 6000.6.4f1 and URP assemblies; serialized scene camera/reference checks were performed. Sandboxed Editor execution again stalled on licensing, so Play Mode rendering still needs the manual check above. The current scene was updated by adding camera records based on the project's Unity-generated SampleScene camera serialization; the existing menu records and asset GUIDs were preserved.
