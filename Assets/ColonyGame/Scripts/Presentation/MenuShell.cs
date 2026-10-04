using System;
using System.IO;
using ColonyGame.Profiles;
using ColonyGame.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColonyGame.Presentation
{
    [RequireComponent(typeof(UIDocument), typeof(LobbyAudio))]
    public sealed class MenuShell : MonoBehaviour
    {
        private VisualElement root;
        private VisualElement card;
        private Label message;
        private ProfileStore profiles;
        private AudioPreferences preferences;
        private LobbyAudio audioManager;
        private void OnEnable()
        {
            profiles = new ProfileStore(Path.Combine(Application.persistentDataPath, "Saves", "Profiles"));
            preferences = new AudioPreferences();
            audioManager = GetComponent<LobbyAudio>();
            audioManager.Initialize(preferences);
            root = GetComponent<UIDocument>().rootVisualElement;
            root.style.backgroundColor = new Color(0.045f, 0.065f, 0.09f);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.paddingTop = 24;
            root.style.paddingBottom = 24;
            MainMenu();
        }
        private void Screen(string title, string subtitle)
        {
            root.Clear();
            card = new VisualElement();
            card.style.width = Length.Percent(90);
            card.style.maxWidth = 760;
            card.style.maxHeight = Length.Percent(100);
            card.style.backgroundColor = new Color(0.09f, 0.12f, 0.16f);
            card.style.paddingTop = card.style.paddingBottom = 28;
            card.style.paddingLeft = card.style.paddingRight = 32;
            root.Add(card);
            var heading = Text(title, 32);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(subtitle, 16);
            message = Text("", 14);
            message.style.color = new Color(1f, 0.76f, 0.4f);
        }
        private Label Text(string value, int size, VisualElement parent = null)
        {
            var label = new Label(value) { enableRichText = false };
            label.style.fontSize = size;
            label.style.color = new Color(0.9f, 0.94f, 0.98f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 12;
            (parent ?? card).Add(label);
            return label;
        }
        private Button Action(string title, System.Action action, VisualElement parent = null)
        {
            var button = new Button(action) { text = title };
            button.style.height = 42;
            button.style.minWidth = 90;
            button.style.marginBottom = 8;
            button.style.fontSize = 16;
            button.style.backgroundColor = new Color(0.16f, 0.23f, 0.3f);
            button.style.color = Color.white;
            (parent ?? card).Add(button);
            return button;
        }
        private void Try(System.Action operation)
        {
            try { operation(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                message.text = ex is ArgumentException ? ex.Message : "Could not access save data. Check available disk space and folder permissions, then try again.";
                Debug.LogWarning("Profile operation failed: " + ex.Message);
            }
        }
        private void MainMenu()
        {
            Screen("ColonyGame", "Build a settlement. Shape its future.");
            Action("Singleplayer", Profiles);
            Action("Multiplayer", () =>
            {
                Screen("Multiplayer", "Coming Soon");
                Action("Return to Main Menu", MainMenu);
            });
            Action("Settings", Settings);
            Action("Quit", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
        private void Profiles()
        {
            Screen("Singleplayer", "Choose a profile or start a new one.");
            var name = new TextField("Profile name") { maxLength = 80 };
            name.labelElement.style.color = Color.white;
            name.style.marginBottom = 12;
            card.Add(name);
            Action("Create Profile", () => Try(() => { profiles.Create(name.value); Profiles(); }));
            var list = new ScrollView();
            list.style.maxHeight = 350;
            list.style.flexShrink = 1;
            list.style.minHeight = 60;
            card.Add(list);
            Try(() =>
            {
                var all = profiles.Load(out var skipped);
                if (all.Count == 0) Text("No profiles yet. Create one to begin.", 16, list);
                if (skipped > 0) message.text = skipped + " unreadable or unsupported profile(s) were left untouched.";
                foreach (var profile in all)
                {
                    var row = new VisualElement();
                    row.style.paddingTop = 12;
                    row.style.paddingBottom = 12;
                    list.Add(row);
                    Text(profile.name, 20, row);
                    Text("Created: " + FriendlyDate(profile.createdUtc) + " | Last played: " + FriendlyDate(profile.lastPlayedUtc), 12, row);
                    var buttons = new VisualElement();
                    buttons.style.flexDirection = FlexDirection.Row;
                    row.Add(buttons);
                    Action("Play", () => Try(() => { profiles.MarkPlayed(profile); Play(profile); }), buttons);
                    Action("Delete", () => ConfirmDelete(profile), buttons);
                }
            });
            Action("Return to Main Menu", MainMenu);
        }
        private static string FriendlyDate(string timestamp)
        {
            return DateTimeOffset.TryParse(timestamp, out var time) ? time.ToLocalTime().ToString("g") : "Never";
        }
        private void ConfirmDelete(Profile profile)
        {
            Screen("Delete profile?", "Delete \"" + profile.name + "\" and all its save data? This cannot be undone.");
            Action("Cancel", Profiles);
            Action("Delete permanently", () => Try(() => { profiles.Delete(profile); Profiles(); }));
        }
        private void Play(Profile profile)
        {
            Screen(profile.name, "Colony gameplay will be implemented later.");
            Action("Return to Main Menu", MainMenu);
        }
        private void Settings()
        {
            Screen("Settings", "Audio");
            var enabled = new Toggle("Lobby Music") { value = preferences.LobbyMusicEnabled };
            enabled.style.color = Color.white;
            enabled.labelElement.style.color = Color.white;
            enabled.style.marginBottom = 20;
            card.Add(enabled);
            enabled.RegisterValueChangedCallback(evt => { preferences.SetLobbyMusic(evt.newValue); audioManager.Apply(preferences); });
            var percentage = Text("Music Volume: " + Mathf.RoundToInt(preferences.MusicVolume * 100) + "%", 18);
            var slider = new Slider(0, 100) { value = preferences.MusicVolume * 100 };
            slider.style.marginBottom = 24;
            card.Add(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                percentage.text = "Music Volume: " + Mathf.RoundToInt(evt.newValue) + "%";
                preferences.SetMusicVolume(evt.newValue / 100);
                audioManager.Apply(preferences);
            });
            Action("Return to Main Menu", MainMenu);
        }
    }
}


