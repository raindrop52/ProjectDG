using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DungeonGuidance.EditorTools
{
    public static class AnimationSetGenerator
    {
        public sealed class ClipDefinition
        {
            public ClipDefinition(string name, IReadOnlyList<Sprite> sprites, bool loop)
            {
                Name = name;
                Sprites = sprites;
                Loop = loop;
            }

            public string Name { get; }
            public IReadOnlyList<Sprite> Sprites { get; }
            public bool Loop { get; }
        }

        public sealed class Result
        {
            public Result(string folderPath, AnimatorController controller)
            {
                FolderPath = folderPath;
                Controller = controller;
            }

            public string FolderPath { get; }
            public AnimatorController Controller { get; }
        }

        public static Result Create(
            string parentFolder,
            string setName,
            float frameRate,
            IReadOnlyList<ClipDefinition> definitions)
        {
            Validate(parentFolder, setName, frameRate, definitions);

            string outputFolder = parentFolder.TrimEnd('/') + "/" + setName;
            if (AssetDatabase.IsValidFolder(outputFolder))
            {
                throw new InvalidOperationException($"A folder already exists at {outputFolder}.");
            }

            string createdGuid = AssetDatabase.CreateFolder(parentFolder, setName);
            if (string.IsNullOrEmpty(createdGuid))
            {
                throw new InvalidOperationException($"Could not create {outputFolder}.");
            }

            try
            {
                var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
                foreach (ClipDefinition definition in definitions)
                {
                    AnimationClip clip = CreateClip(outputFolder, frameRate, definition);
                    clips.Add(definition.Name, clip);
                }

                string controllerPath = $"{outputFolder}/{setName}.controller";
                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                ConfigureController(controller, clips);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new Result(outputFolder, controller);
            }
            catch
            {
                AssetDatabase.DeleteAsset(outputFolder);
                AssetDatabase.Refresh();
                throw;
            }
        }

        private static AnimationClip CreateClip(string outputFolder, float frameRate, ClipDefinition definition)
        {
            var clip = new AnimationClip
            {
                name = definition.Name,
                frameRate = frameRate
            };

            var keyframes = new ObjectReferenceKeyframe[definition.Sprites.Count];
            for (int index = 0; index < definition.Sprites.Count; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = index / frameRate,
                    value = definition.Sprites[index]
                };
            }

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = definition.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{outputFolder}/{definition.Name}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static void ConfigureController(
            AnimatorController controller,
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            var positions = new Dictionary<string, Vector3>(StringComparer.Ordinal)
            {
                ["Idle"] = new Vector3(220f, 80f),
                ["Move"] = new Vector3(440f, 80f),
                ["Attack"] = new Vector3(440f, 190f),
                ["Die"] = new Vector3(440f, 300f)
            };

            foreach (KeyValuePair<string, AnimationClip> entry in clips)
            {
                AnimatorState state = stateMachine.AddState(entry.Key, positions[entry.Key]);
                state.motion = entry.Value;
                if (entry.Key == "Idle")
                {
                    stateMachine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(controller);
        }

        private static void Validate(
            string parentFolder,
            string setName,
            float frameRate,
            IReadOnlyList<ClipDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder))
            {
                throw new InvalidOperationException("Choose a valid output folder inside Assets.");
            }
            if (!parentFolder.Equals("Assets", StringComparison.Ordinal) &&
                !parentFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The output folder must be inside Assets.");
            }
            if (string.IsNullOrWhiteSpace(setName))
            {
                throw new InvalidOperationException("Enter a folder and controller name.");
            }
            if (setName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || setName.Contains("/") || setName.Contains("\\"))
            {
                throw new InvalidOperationException("The name contains characters that cannot be used in a folder or asset name.");
            }
            if (frameRate <= 0f)
            {
                throw new InvalidOperationException("Frame rate must be greater than zero.");
            }
            if (definitions == null || definitions.Count != 4)
            {
                throw new InvalidOperationException("Idle, Move, Attack, and Die definitions are required.");
            }

            var expectedNames = new HashSet<string>(new[] { "Idle", "Move", "Attack", "Die" }, StringComparer.Ordinal);
            foreach (ClipDefinition definition in definitions)
            {
                if (!expectedNames.Remove(definition.Name))
                {
                    throw new InvalidOperationException($"Unexpected or duplicate animation name: {definition.Name}.");
                }
                if (definition.Sprites == null || definition.Sprites.Count == 0)
                {
                    throw new InvalidOperationException($"Add at least one Sprite to {definition.Name}.");
                }
                for (int index = 0; index < definition.Sprites.Count; index++)
                {
                    if (definition.Sprites[index] == null)
                    {
                        throw new InvalidOperationException($"{definition.Name} contains an empty Sprite at index {index}.");
                    }
                }
            }
        }
    }
}
