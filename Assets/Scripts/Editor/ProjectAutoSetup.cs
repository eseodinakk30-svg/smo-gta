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
        private const string ShaderResourceFolder = "Assets/Resources/Shaders";

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
                ConfigureLayers();
                ConfigureShaderInclusion();
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
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ShaderResourceFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Shaders");
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
                    // UniversalRenderPipelineAsset.Create wires up the internal shader
                    // and editor resource references that a bare CreateInstance leaves
                    // empty - without them everything renders pink.
                    asset = UniversalRenderPipelineAsset.Create(rendererData);
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

        /// <summary>
        /// The repository ships a TagManager.asset with the game's layers, but the
        /// names are verified here too so the project still works if that file is
        /// ever regenerated by the editor.
        /// </summary>
        private static void ConfigureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var serialized = new SerializedObject(assets[0]);
            var layers = serialized.FindProperty("layers");
            if (layers == null) return;

            AssignLayer(layers, 8, "Ground");
            AssignLayer(layers, 9, "Building");
            AssignLayer(layers, 10, "Prop");
            AssignLayer(layers, 11, "Player");
            AssignLayer(layers, 12, "Ped");
            AssignLayer(layers, 13, "Vehicle");
            AssignLayer(layers, 14, "VehicleWheel");
            AssignLayer(layers, 15, "Projectile");
            AssignLayer(layers, 16, "Interactable");
            AssignLayer(layers, 17, "Ragdoll");
            AssignLayer(layers, 18, "Foliage");
            AssignLayer(layers, 19, "Terrain");
            AssignLayer(layers, 20, "Road");
            AssignLayer(layers, 21, "MinimapOnly");
            AssignLayer(layers, 22, "Trigger");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignLayer(SerializedProperty layers, int index, string name)
        {
            if (index >= layers.arraySize) return;
            var element = layers.GetArrayElementAtIndex(index);
            if (element == null) return;
            if (string.IsNullOrEmpty(element.stringValue) || element.stringValue != name)
                element.stringValue = name;
        }

        /// <summary>
        /// The world creates every material at runtime, so the shaders it asks
        /// for have to survive build-time stripping. The obvious lever - the
        /// Always Included Shaders list - is a trap: it bypasses URP's variant
        /// stripping and the player build dies outright with "Universal Render
        /// Pipeline/Lit has too many Shader variants (1179648)".
        ///
        /// Instead a handful of keeper materials live in Resources. They pull
        /// the shaders into the build as ordinary asset references, so URP
        /// strips variants the normal way, and each one carries the keywords the
        /// game switches on at runtime so those variants are kept as well.
        /// </summary>
        private static void ConfigureShaderInclusion()
        {
            PruneAlwaysIncludedShaders();

            string[] lit = { "Universal Render Pipeline/Lit", "Standard" };
            string[] simpleLit = { "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Lit" };
            string[] unlit = { "Universal Render Pipeline/Unlit", "Unlit/Color" };
            string[] particle = { "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit" };
            string[] sky = { "Skybox/Procedural", "Skybox/Gradient" };

            string[] surfaceKeywords = { "_NORMALMAP", "_EMISSION" };
            string[] transparentKeywords = { "_SURFACE_TYPE_TRANSPARENT" };

            EnsureKeeperMaterial("Lit", lit, surfaceKeywords, false);
            EnsureKeeperMaterial("LitTransparent", lit, transparentKeywords, true);
            EnsureKeeperMaterial("SimpleLit", simpleLit, surfaceKeywords, false);
            EnsureKeeperMaterial("SimpleLitTransparent", simpleLit, transparentKeywords, true);
            EnsureKeeperMaterial("Unlit", unlit, null, false);
            EnsureKeeperMaterial("ParticleUnlit", particle, null, true);
            EnsureKeeperMaterial("Sky", sky, null, false);
        }

        /// <summary>
        /// Drops the heavyweight shaders from Always Included. Nothing adds them
        /// any more, but a project set up by an earlier version of this script
        /// still carries them, and one of them is enough to fail every build.
        /// </summary>
        private static void PruneAlwaysIncludedShaders()
        {
            var heavy = new HashSet<string>
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Skybox/Procedural",
            };

            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null || graphicsSettings.Length == 0) return;
            var serialized = new SerializedObject(graphicsSettings[0]);
            var array = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (array == null) return;

            bool changed = false;
            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                var shader = array.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == null || !heavy.Contains(shader.name)) continue;
                array.DeleteArrayElementAtIndex(i);
                changed = true;
                Debug.Log("[San Monica] Removed " + shader.name + " from Always Included Shaders.");
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureKeeperMaterial(string assetName, string[] shaderNames,
                                                 string[] keywords, bool transparent)
        {
            Shader shader = null;
            foreach (var name in shaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null) break;
            }
            if (shader == null)
            {
                Debug.LogWarning("[San Monica] No shader found for keeper material " + assetName);
                return;
            }

            string path = ShaderResourceFolder + "/" + assetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(shader);
            else if (material.shader != shader) material.shader = shader;

            material.enableInstancing = true;
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (keywords != null)
                foreach (var keyword in keywords) material.EnableKeyword(keyword);

            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------
        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Outlaw Coast Studio";
            PlayerSettings.productName = "San Monica: Saltwater Debt";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.outlawcoast.sanmonica");
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
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.targetArchitectures = ResolveArchitectures();
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;

            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);

            // The game builds its own input surface, so the legacy manager is enough.
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        /// <summary>
        /// CI picks the architectures through SMO_ANDROID_ARCH ("arm64" or "both").
        /// Building ARM64 only is roughly twice as fast, because IL2CPP compiles
        /// native code separately for each architecture.
        /// </summary>
        private static AndroidArchitecture ResolveArchitectures()
        {
            string requested = System.Environment.GetEnvironmentVariable("SMO_ANDROID_ARCH");
            if (!string.IsNullOrEmpty(requested) && requested.Trim().ToLowerInvariant() == "arm64")
            {
                Debug.Log("[San Monica] Target architectures: ARM64 only (SMO_ANDROID_ARCH=arm64)");
                return AndroidArchitecture.ARM64;
            }
            Debug.Log("[San Monica] Target architectures: ARMv7 + ARM64");
            return AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
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
        // The boot scene is committed to the repository on purpose. It holds no
        // objects at all - GameBootstrap builds the city from a RuntimeInitialize
        // hook - so shipping it costs nothing and spares a batch-mode build from
        // having to create and save a scene before it can build a player.
        private static void EnsureBootScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("[San Monica] " + ScenePath + " is missing; creating an empty one.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var marker = new GameObject("SanMonicaBoot");
                marker.AddComponent<SanMonica.Core.BootMarker>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.ImportAsset(ScenePath);
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
