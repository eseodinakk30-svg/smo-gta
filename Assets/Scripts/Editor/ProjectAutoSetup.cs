#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SanMonica.EditorTools
{
    /// <summary>
    /// One-shot project configuration. Creates the four URP pipeline assets,
    /// wires them into the quality tiers, configures Android player settings,
    /// registers the gamepad axes, creates the boot scene and adds it to the
    /// build. Runs automatically the first time the project is opened.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectAutoSetup
    {
        private const string MarkerPath = "ProjectSettings/SanMonicaSetup.txt";
        private const string SettingsFolder = "Assets/Settings";
        private const string ScenePath = "Assets/Scenes/Boot.unity";

        static ProjectAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(MarkerPath)) return;
                RunSetup(false);
            };
        }

        [MenuItem("San Monica/Setup Project", priority = 0)]
        public static void SetupMenu() => RunSetup(true);

        public static void RunSetup(bool interactive)
        {
            try
            {
                EnsureFolders();
                var pipelines = CreatePipelineAssets();
                AssignPipelines(pipelines);
                ConfigureQualityLevels(pipelines);
                ConfigureAlwaysIncludedShaders();
                ConfigurePlayerSettings();
                ConfigureInputAxes();
                ConfigurePhysics();
                EnsureBootScene();
                File.WriteAllText(MarkerPath, "San Monica project configured on " + System.DateTime.UtcNow.ToString("u"));
                AssetDatabase.SaveAssets();
                Debug.Log("[San Monica] Project setup complete. Open Assets/Scenes/Boot.unity and press Play.");
                if (interactive) EditorUtility.DisplayDialog("San Monica", "Project configured for URP and Android.\n\nOpen Assets/Scenes/Boot.unity and press Play.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[San Monica] Setup failed: " + e);
            }
        }

        // ------------------------------------------------------------------
        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        }

        private struct Tier
        {
            public string Name;
            public float RenderScale;
            public float ShadowDistance;
            public int Cascades;
            public int Msaa;
            public bool Hdr;
            public bool SoftShadows;
            public int ShadowAtlas;
        }

        private static readonly Tier[] Tiers =
        {
            new Tier { Name = "URP_Low",    RenderScale = 0.62f, ShadowDistance = 45f,  Cascades = 1, Msaa = 1, Hdr = false, SoftShadows = false, ShadowAtlas = 1024 },
            new Tier { Name = "URP_Medium", RenderScale = 0.80f, ShadowDistance = 70f,  Cascades = 2, Msaa = 1, Hdr = false, SoftShadows = false, ShadowAtlas = 1024 },
            new Tier { Name = "URP_High",   RenderScale = 1.00f, ShadowDistance = 110f, Cascades = 3, Msaa = 2, Hdr = true,  SoftShadows = true,  ShadowAtlas = 2048 },
            new Tier { Name = "URP_Ultra",  RenderScale = 1.10f, ShadowDistance = 170f, Cascades = 4, Msaa = 4, Hdr = true,  SoftShadows = true,  ShadowAtlas = 4096 },
        };

        private static UniversalRenderPipelineAsset[] CreatePipelineAssets()
        {
            var results = new UniversalRenderPipelineAsset[Tiers.Length];

            string rendererPath = SettingsFolder + "/SanMonicaRenderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            var rendererSerialized = new SerializedObject(rendererData);
            SetIfPresent(rendererSerialized, "m_RenderingMode", 0);              // Forward
            SetIfPresent(rendererSerialized, "m_DepthPrimingMode", 0);
            SetIfPresent(rendererSerialized, "m_CopyDepthMode", 0);
            SetIfPresent(rendererSerialized, "m_AccurateGbufferNormals", 0);
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < Tiers.Length; i++)
            {
                var tier = Tiers[i];
                string path = SettingsFolder + "/" + tier.Name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                var serialized = new SerializedObject(asset);
                var list = serialized.FindProperty("m_RendererDataList");
                if (list != null)
                {
                    if (list.arraySize == 0) list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                }
                SetIfPresent(serialized, "m_DefaultRendererIndex", 0);
                SetIfPresent(serialized, "m_RequireDepthTexture", 0);
                SetIfPresent(serialized, "m_RequireOpaqueTexture", 0);
                SetIfPresent(serialized, "m_SupportsHDR", tier.Hdr ? 1 : 0);
                SetIfPresent(serialized, "m_MSAA", tier.Msaa);
                SetIfPresentFloat(serialized, "m_RenderScale", tier.RenderScale);
                SetIfPresent(serialized, "m_MainLightRenderingMode", 1);
                SetIfPresent(serialized, "m_MainLightShadowsSupported", 1);
                SetIfPresent(serialized, "m_MainLightShadowmapResolution", tier.ShadowAtlas);
                SetIfPresent(serialized, "m_AdditionalLightsRenderingMode", 1);
                SetIfPresent(serialized, "m_AdditionalLightsPerObjectLimit", tier.Name == "URP_Low" ? 2 : 4);
                SetIfPresent(serialized, "m_AdditionalLightShadowsSupported", 0);
                SetIfPresentFloat(serialized, "m_ShadowDistance", tier.ShadowDistance);
                SetIfPresent(serialized, "m_ShadowCascadeCount", tier.Cascades);
                SetIfPresent(serialized, "m_SoftShadowsSupported", tier.SoftShadows ? 1 : 0);
                SetIfPresent(serialized, "m_UseSRPBatcher", 1);
                SetIfPresent(serialized, "m_SupportsDynamicBatching", 0);
                SetIfPresent(serialized, "m_MixedLightingSupported", 1);
                SetIfPresent(serialized, "m_ColorGradingMode", 0);              // LDR - cheap on mobile
                SetIfPresent(serialized, "m_ColorGradingLutSize", 16);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(asset);
                results[i] = asset;
            }

            AssetDatabase.SaveAssets();
            return results;
        }

        private static void SetIfPresent(SerializedObject serialized, string property, int value)
        {
            var prop = serialized.FindProperty(property);
            if (prop == null) return;
            if (prop.propertyType == SerializedPropertyType.Boolean) prop.boolValue = value != 0;
            else if (prop.propertyType == SerializedPropertyType.Enum) prop.enumValueIndex = value;
            else if (prop.propertyType == SerializedPropertyType.Integer) prop.intValue = value;
            else if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = value;
        }

        private static void SetIfPresentFloat(SerializedObject serialized, string property, float value)
        {
            var prop = serialized.FindProperty(property);
            if (prop == null) return;
            if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = value;
            else if (prop.propertyType == SerializedPropertyType.Integer) prop.intValue = Mathf.RoundToInt(value);
        }

        private static void AssignPipelines(UniversalRenderPipelineAsset[] pipelines)
        {
            if (pipelines == null || pipelines.Length == 0) return;
            GraphicsSettings.defaultRenderPipeline = pipelines[2];
            QualitySettings.renderPipeline = pipelines[2];
        }

        private static void ConfigureQualityLevels(UniversalRenderPipelineAsset[] pipelines)
        {
            var names = QualitySettings.names;
            for (int level = 0; level < names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                int tier = names.Length <= 1 ? 2 : Mathf.Clamp(Mathf.RoundToInt(level / (float)(names.Length - 1) * 3f), 0, 3);
                QualitySettings.renderPipeline = pipelines[tier];
                QualitySettings.skinWeights = SkinWeights.OneBone;
                QualitySettings.vSyncCount = 0;
                QualitySettings.anisotropicFiltering = tier >= 2 ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
                QualitySettings.lodBias = Mathf.Lerp(0.6f, 1.6f, tier / 3f);
                QualitySettings.particleRaycastBudget = 16 + tier * 32;
                QualitySettings.asyncUploadTimeSlice = 4;
                QualitySettings.asyncUploadBufferSize = 16;
            }
            QualitySettings.SetQualityLevel(Mathf.Min(names.Length - 1, 2), true);
        }

        private static void ConfigureAlwaysIncludedShaders()
        {
            // The world builds its materials at runtime through Shader.Find, so the
            // shaders it needs must survive build-time stripping.
            string[] shaderNames =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Skybox/Procedural",
                "Sprites/Default",
                "UI/Default"
            };

            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null || graphicsSettings.Length == 0) return;
            var serialized = new SerializedObject(graphicsSettings[0]);
            var array = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (array == null) return;

            var existing = new HashSet<Object>();
            for (int i = 0; i < array.arraySize; i++)
            {
                var value = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null) existing.Add(value);
            }

            foreach (var name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader == null || existing.Contains(shader)) continue;
                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
                existing.Add(shader);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Outlaw Coast Studio";
            PlayerSettings.productName = "San Monica: Saltwater Debt";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.outlawcoast.sanmonica");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_1);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;

            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);

            // The game builds its own input surface, so the legacy manager is enough.
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        private static void ConfigurePhysics()
        {
            Physics.defaultSolverIterations = 6;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.sleepThreshold = 0.01f;
            Physics.defaultContactOffset = 0.02f;
            Time.fixedDeltaTime = 1f / 50f;
        }

        // ------------------------------------------------------------------
        private static void ConfigureInputAxes()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0) return;
            var serialized = new SerializedObject(assets[0]);
            var axes = serialized.FindProperty("m_Axes");
            if (axes == null) return;

            AddAxis(axes, "RightStickX", 4, false);
            AddAxis(axes, "RightStickY", 5, false);
            AddAxis(axes, "TriggerLeft", 9, true);
            AddAxis(axes, "TriggerRight", 10, true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddAxis(SerializedProperty axes, string name, int axisIndex, bool positiveOnly)
        {
            for (int i = 0; i < axes.arraySize; i++)
            {
                var existing = axes.GetArrayElementAtIndex(i);
                var nameProp = existing.FindPropertyRelative("m_Name");
                if (nameProp != null && nameProp.stringValue == name) return;
            }

            axes.InsertArrayElementAtIndex(axes.arraySize);
            var axis = axes.GetArrayElementAtIndex(axes.arraySize - 1);
            void Set(string field, object value)
            {
                var prop = axis.FindPropertyRelative(field);
                if (prop == null) return;
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.String: prop.stringValue = (string)value; break;
                    case SerializedPropertyType.Float: prop.floatValue = System.Convert.ToSingle(value); break;
                    case SerializedPropertyType.Integer: prop.intValue = System.Convert.ToInt32(value); break;
                    case SerializedPropertyType.Boolean: prop.boolValue = System.Convert.ToBoolean(value); break;
                }
            }

            Set("m_Name", name);
            Set("descriptiveName", "");
            Set("descriptiveNegativeName", "");
            Set("negativeButton", "");
            Set("positiveButton", "");
            Set("altNegativeButton", "");
            Set("altPositiveButton", "");
            Set("gravity", 0f);
            Set("dead", positiveOnly ? 0.05f : 0.19f);
            Set("sensitivity", 1f);
            Set("snap", false);
            Set("invert", false);
            Set("type", 2);          // Joystick axis
            Set("axis", axisIndex);
            Set("joyNum", 0);
        }

        // ------------------------------------------------------------------
        private static void EnsureBootScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var marker = new GameObject("SanMonicaBoot");
                marker.AddComponent<SanMonica.Core.BootMarker>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = buildScenes.Exists(s => s.path == ScenePath);
            if (!present)
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }
    }
}
#endif
