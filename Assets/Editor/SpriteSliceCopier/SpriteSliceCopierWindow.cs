using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DungeonGuidance.EditorTools
{
    public sealed class SpriteSliceCopierWindow : EditorWindow
    {
        private const string ToolFolder = "Assets/Editor/SpriteSliceCopier";

        private ObjectField sourceField;
        private ObjectField folderField;
        private TextField prefixField;
        private Toggle renameToggle;
        private HelpBox statusBox;
        private ScrollView targetList;

        [MenuItem("Window/Dungeon Guidance/Sprite Slice Copier")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteSliceCopierWindow>();
            window.titleContent = new GUIContent("Sprite Slice Copier");
            window.minSize = new Vector2(460f, 400f);
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                ToolFolder + "/SpriteSliceCopierWindow.uxml");
            if (visualTree == null)
            {
                rootVisualElement.Add(new HelpBox("SpriteSliceCopierWindow.uxml could not be loaded.", HelpBoxMessageType.Error));
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            sourceField = new ObjectField("Source sprite sheet")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false
            };
            folderField = new ObjectField("Target folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };

            rootVisualElement.Q<VisualElement>("sourceFieldHost").Add(sourceField);
            rootVisualElement.Q<VisualElement>("folderFieldHost").Add(folderField);

            prefixField = rootVisualElement.Q<TextField>("prefixField");
            renameToggle = rootVisualElement.Q<Toggle>("renameToggle");
            statusBox = rootVisualElement.Q<HelpBox>("statusBox");
            targetList = rootVisualElement.Q<ScrollView>("targetList");

            rootVisualElement.Q<Button>("previewButton").clicked += PreviewTargets;
            rootVisualElement.Q<Button>("applyButton").clicked += ApplySlices;

            PreviewTargets();
        }

        private void PreviewTargets()
        {
            targetList.Clear();
            try
            {
                CopyPlan plan = BuildPlan();
                statusBox.messageType = HelpBoxMessageType.Info;
                statusBox.text = $"{plan.SourceRects.Length} slices will be copied to {plan.Targets.Count} texture(s).";

                foreach (ProviderContext target in plan.Targets)
                {
                    var targetRow = new Label(target.Path) { name = "targetRow" };
                    targetRow.AddToClassList("target-row");
                    targetList.Add(targetRow);
                }
            }
            catch (Exception exception)
            {
                statusBox.messageType = HelpBoxMessageType.Error;
                statusBox.text = exception.Message;
                targetList.Add(new Label("No valid targets."));
            }
        }

        private void ApplySlices()
        {
            try
            {
                CopyPlan plan = BuildPlan();
                bool confirmed = EditorUtility.DisplayDialog(
                    "Apply sprite slices?",
                    $"Replace slice metadata on {plan.Targets.Count} texture(s) using {plan.SourceRects.Length} slices from:\n{plan.Source.Path}",
                    "Apply",
                    "Cancel");
                if (!confirmed)
                {
                    return;
                }

                foreach (ProviderContext target in plan.Targets)
                {
                    ApplyToTarget(plan, target);
                }

                AssetDatabase.SaveAssets();
                statusBox.messageType = HelpBoxMessageType.Info;
                statusBox.text = $"Applied and verified {plan.SourceRects.Length} slices on {plan.Targets.Count} texture(s).";
                PreviewTargets();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                statusBox.messageType = HelpBoxMessageType.Error;
                statusBox.text = exception.Message;
            }
        }

        private CopyPlan BuildPlan()
        {
            string sourcePath = GetAssetPath(sourceField.value, "Choose a source sprite sheet.");
            string folderPath = GetAssetPath(folderField.value, "Choose a target folder.");
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                throw new InvalidOperationException("The selected target is not a project folder.");
            }

            string prefix = prefixField.value?.Trim() ?? string.Empty;
            if (prefix.Length == 0)
            {
                throw new InvalidOperationException("Filename prefix cannot be empty.");
            }

            ProviderContext source = OpenProvider(sourcePath);
            SpriteRect[] sourceRects = source.Provider.GetSpriteRects();
            if (sourceRects == null || sourceRects.Length == 0)
            {
                throw new InvalidOperationException("The source texture has no slices to copy.");
            }

            string[] targetPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (targetPaths.Length == 0)
            {
                throw new InvalidOperationException($"No target textures beginning with '{prefix}' were found.");
            }

            var targets = new List<ProviderContext>(targetPaths.Length);
            foreach (string targetPath in targetPaths)
            {
                ProviderContext target = OpenProvider(targetPath);
                ValidateDimensions(source.Importer, target.Importer, targetPath);
                targets.Add(target);
            }

            return new CopyPlan(source, sourceRects, targets);
        }

        private void ApplyToTarget(CopyPlan plan, ProviderContext target)
        {
            SpriteRect[] existingRects = target.Provider.GetSpriteRects();
            string targetBaseName = Path.GetFileNameWithoutExtension(target.Path);
            var copiedRects = new SpriteRect[plan.SourceRects.Length];

            for (int index = 0; index < plan.SourceRects.Length; index++)
            {
                SpriteRect sourceRect = plan.SourceRects[index];
                GUID spriteId = index < existingRects.Length ? existingRects[index].spriteID : GUID.Generate();
                copiedRects[index] = new SpriteRect
                {
                    name = renameToggle.value ? BuildTargetName(sourceRect.name, plan.SourceBaseName, targetBaseName, index) : sourceRect.name,
                    rect = sourceRect.rect,
                    pivot = sourceRect.pivot,
                    alignment = sourceRect.alignment,
                    border = sourceRect.border,
                    spriteID = spriteId
                };
            }

            target.Provider.SetSpriteRects(copiedRects);
            var nameFileIdProvider = target.Provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                nameFileIdProvider.SetNameFileIdPairs(
                    copiedRects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)).ToList());
            }

            target.Provider.Apply();
            target.Importer.SaveAndReimport();
            VerifyTarget(plan.SourceRects, OpenProvider(target.Path).Provider.GetSpriteRects(), target.Path);
        }

        private static ProviderContext OpenProvider(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter not found for {assetPath}.");
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                throw new InvalidOperationException($"{assetPath} must use Texture Type Sprite and Sprite Mode Multiple.");
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
            {
                throw new InvalidOperationException($"Sprite data provider not found for {assetPath}.");
            }

            provider.InitSpriteEditorDataProvider();
            ISpriteFrameEditCapability editCapability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (editCapability == null)
            {
                throw new InvalidOperationException($"Sprite editing is unavailable for {assetPath}; operation aborted.");
            }

            var capability = editCapability.GetEditCapability();
            EEditCapability[] required =
            {
                EEditCapability.CreateAndDeleteSprite,
                EEditCapability.EditSpriteName,
                EEditCapability.EditSpriteRect,
                EEditCapability.EditBorder,
                EEditCapability.EditPivot
            };
            foreach (EEditCapability item in required)
            {
                if (!capability.HasCapability(item))
                {
                    throw new InvalidOperationException($"{assetPath} does not support {item}; operation aborted.");
                }
            }

            return new ProviderContext(assetPath, importer, provider);
        }

        private static void ValidateDimensions(TextureImporter source, TextureImporter target, string targetPath)
        {
            source.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            target.GetSourceTextureWidthAndHeight(out int targetWidth, out int targetHeight);
            if (sourceWidth != targetWidth || sourceHeight != targetHeight)
            {
                throw new InvalidOperationException(
                    $"Texture size mismatch for {targetPath}: expected {sourceWidth}x{sourceHeight}, got {targetWidth}x{targetHeight}.");
            }
        }

        private static void VerifyTarget(IReadOnlyList<SpriteRect> source, IReadOnlyList<SpriteRect> target, string targetPath)
        {
            if (source.Count != target.Count)
            {
                throw new InvalidOperationException($"Slice count verification failed for {targetPath}.");
            }

            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].rect != target[index].rect || source[index].pivot != target[index].pivot ||
                    source[index].border != target[index].border || source[index].alignment != target[index].alignment)
                {
                    throw new InvalidOperationException($"Slice verification failed for {targetPath} at index {index}.");
                }
            }
        }

        private static string GetAssetPath(UnityEngine.Object asset, string error)
        {
            if (asset == null)
            {
                throw new InvalidOperationException(error);
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException(error);
            }
            return path;
        }

        private static string BuildTargetName(string sourceName, string sourceBaseName, string targetBaseName, int index)
        {
            return sourceName.StartsWith(sourceBaseName, StringComparison.OrdinalIgnoreCase)
                ? targetBaseName + sourceName.Substring(sourceBaseName.Length)
                : $"{targetBaseName}_{index}";
        }

        private sealed class ProviderContext
        {
            public ProviderContext(string path, TextureImporter importer, ISpriteEditorDataProvider provider)
            {
                Path = path;
                Importer = importer;
                Provider = provider;
            }

            public string Path { get; }
            public TextureImporter Importer { get; }
            public ISpriteEditorDataProvider Provider { get; }
        }

        private sealed class CopyPlan
        {
            public CopyPlan(ProviderContext source, SpriteRect[] sourceRects, List<ProviderContext> targets)
            {
                Source = source;
                SourceRects = sourceRects;
                Targets = targets;
                SourceBaseName = Path.GetFileNameWithoutExtension(source.Path);
            }

            public ProviderContext Source { get; }
            public SpriteRect[] SourceRects { get; }
            public List<ProviderContext> Targets { get; }
            public string SourceBaseName { get; }
        }
    }
}
