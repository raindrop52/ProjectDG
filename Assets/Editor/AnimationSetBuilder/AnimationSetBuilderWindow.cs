using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DungeonGuidance.EditorTools
{
    public sealed class AnimationSetBuilderWindow : EditorWindow
    {
        private const string ToolFolder = "Assets/Editor/AnimationSetBuilder";

        [SerializeField] private List<Sprite> idleSprites = new();
        [SerializeField] private List<Sprite> moveSprites = new();
        [SerializeField] private List<Sprite> attackSprites = new();
        [SerializeField] private List<Sprite> dieSprites = new();
        [SerializeField] private bool idleLoop = true;
        [SerializeField] private bool moveLoop = true;
        [SerializeField] private bool attackLoop;
        [SerializeField] private bool dieLoop;

        private ObjectField folderField;
        private TextField nameField;
        private FloatField frameRateField;
        private HelpBox statusBox;
        private readonly List<AnimationListView> animationViews = new();

        [MenuItem("Window/Dungeon Guidance/Animation Set Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationSetBuilderWindow>();
            window.titleContent = new GUIContent("Animation Set Builder");
            window.minSize = new Vector2(560f, 650f);
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                ToolFolder + "/AnimationSetBuilderWindow.uxml");
            if (visualTree == null)
            {
                rootVisualElement.Add(new HelpBox("AnimationSetBuilderWindow.uxml could not be loaded.", HelpBoxMessageType.Error));
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            folderField = new ObjectField("Output parent folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };
            rootVisualElement.Q<VisualElement>("folderFieldHost").Add(folderField);

            nameField = rootVisualElement.Q<TextField>("nameField");
            frameRateField = rootVisualElement.Q<FloatField>("frameRateField");
            statusBox = rootVisualElement.Q<HelpBox>("statusBox");
            VisualElement listHost = rootVisualElement.Q<VisualElement>("animationLists");

            animationViews.Clear();
            animationViews.Add(CreateAnimationList(listHost, "Idle", idleSprites, idleLoop, value => idleLoop = value));
            animationViews.Add(CreateAnimationList(listHost, "Move", moveSprites, moveLoop, value => moveLoop = value));
            animationViews.Add(CreateAnimationList(listHost, "Attack", attackSprites, attackLoop, value => attackLoop = value));
            animationViews.Add(CreateAnimationList(listHost, "Die", dieSprites, dieLoop, value => dieLoop = value));

            rootVisualElement.Q<Button>("createButton").clicked += CreateAnimationSet;
            SetStatus("Choose an output folder, enter a name, and add Sprites to all four lists.", HelpBoxMessageType.Info);
        }

        private AnimationListView CreateAnimationList(
            VisualElement parent,
            string animationName,
            List<Sprite> sprites,
            bool loop,
            Action<bool> setLoop)
        {
            var section = new VisualElement();
            section.AddToClassList("animation-section");

            var header = new VisualElement();
            header.AddToClassList("section-header");
            var title = new Label(animationName);
            title.AddToClassList("section-title");
            var count = new Label();
            var loopToggle = new Toggle("Loop") { value = loop };
            loopToggle.RegisterValueChangedCallback(evt => setLoop(evt.newValue));
            header.Add(title);
            header.Add(count);
            header.Add(loopToggle);

            var listView = new ListView
            {
                itemsSource = sprites,
                fixedItemHeight = 24f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Multiple,
                showBorder = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            listView.AddToClassList("frame-list");
            listView.makeItem = () =>
            {
                var field = new ObjectField
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false
                };
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData is int index && index >= 0 && index < sprites.Count)
                    {
                        sprites[index] = evt.newValue as Sprite;
                    }
                });
                return field;
            };
            listView.bindItem = (element, index) =>
            {
                var field = (ObjectField)element;
                field.userData = index;
                field.SetValueWithoutNotify(sprites[index]);
            };

            var actions = new VisualElement();
            actions.AddToClassList("list-actions");
            var addButton = new Button(() => AddSelectedSprites(sprites, listView, count)) { text = "Add Selection" };
            var removeButton = new Button(() => RemoveSelectedSprites(sprites, listView, count)) { text = "Remove" };
            var clearButton = new Button(() => ClearSprites(sprites, listView, count)) { text = "Clear" };
            actions.Add(addButton);
            actions.Add(removeButton);
            actions.Add(clearButton);

            section.Add(header);
            section.Add(listView);
            section.Add(actions);
            parent.Add(section);

            var view = new AnimationListView(animationName, sprites, loopToggle, listView, count);
            view.Refresh();
            return view;
        }

        private static void AddSelectedSprites(List<Sprite> sprites, ListView listView, Label count)
        {
            Sprite[] selected = Selection.objects
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name, Comparer<string>.Create(EditorUtility.NaturalCompare))
                .ToArray();

            foreach (Sprite sprite in selected)
            {
                if (!sprites.Contains(sprite))
                {
                    sprites.Add(sprite);
                }
            }
            RefreshList(listView, count, sprites.Count);
        }

        private static void RemoveSelectedSprites(List<Sprite> sprites, ListView listView, Label count)
        {
            int[] selectedIndices = listView.selectedIndices.OrderByDescending(index => index).ToArray();
            foreach (int index in selectedIndices)
            {
                if (index >= 0 && index < sprites.Count)
                {
                    sprites.RemoveAt(index);
                }
            }
            listView.ClearSelection();
            RefreshList(listView, count, sprites.Count);
        }

        private static void ClearSprites(List<Sprite> sprites, ListView listView, Label count)
        {
            sprites.Clear();
            listView.ClearSelection();
            RefreshList(listView, count, 0);
        }

        private static void RefreshList(ListView listView, Label count, int itemCount)
        {
            listView.Rebuild();
            count.text = $"{itemCount} frame(s)";
        }

        private void CreateAnimationSet()
        {
            try
            {
                string parentFolder = folderField.value == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(folderField.value);
                var definitions = new[]
                {
                    new AnimationSetGenerator.ClipDefinition("Idle", idleSprites, idleLoop),
                    new AnimationSetGenerator.ClipDefinition("Move", moveSprites, moveLoop),
                    new AnimationSetGenerator.ClipDefinition("Attack", attackSprites, attackLoop),
                    new AnimationSetGenerator.ClipDefinition("Die", dieSprites, dieLoop)
                };

                AnimationSetGenerator.Result result = AnimationSetGenerator.Create(
                    parentFolder,
                    nameField.value?.Trim(),
                    frameRateField.value,
                    definitions);

                SetStatus($"Created animation set at {result.FolderPath}.", HelpBoxMessageType.Info);
                Selection.activeObject = result.Controller;
                EditorGUIUtility.PingObject(result.Controller);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus(exception.Message, HelpBoxMessageType.Error);
            }
        }

        private void SetStatus(string message, HelpBoxMessageType messageType)
        {
            statusBox.text = message;
            statusBox.messageType = messageType;
        }

        private sealed class AnimationListView
        {
            public AnimationListView(
                string name,
                List<Sprite> sprites,
                Toggle loopToggle,
                ListView listView,
                Label count)
            {
                Name = name;
                Sprites = sprites;
                LoopToggle = loopToggle;
                ListView = listView;
                Count = count;
            }

            public string Name { get; }
            public List<Sprite> Sprites { get; }
            public Toggle LoopToggle { get; }
            public ListView ListView { get; }
            public Label Count { get; }

            public void Refresh()
            {
                RefreshList(ListView, Count, Sprites.Count);
            }
        }
    }
}
