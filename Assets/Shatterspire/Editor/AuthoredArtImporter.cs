using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Applies deterministic mobile-friendly import settings to the authored art
    /// slice. This runs automatically when the update is copied into a project.
    /// </summary>
    public sealed class AuthoredArtImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Shatterspire/Resources/Art3D/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            if (assetPath.Contains("/Art3D/KayKit/Animations/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.optimizeGameObjects = false;
                importer.bakeAxisConversion = true;
                var clips = importer.defaultClipAnimations;
                for (var i = 0; i < clips.Length; i++)
                {
                    var name = clips[i].name;
                    var loop = name.Contains("Idle") || name.Contains("Walking") || name.Contains("Running");
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                }
                if (clips.Length > 0) importer.clipAnimations = clips;
                return;
            }

            if (assetPath.EndsWith("Male_Ranger.fbx") || assetPath.Contains("/Art3D/Heroes/") ||
                assetPath.Contains("/Art3D/KayKit/Characters/") ||
                assetPath.Contains("/Art3D/KayKit/Skeletons/Characters/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.optimizeGameObjects = false;
                importer.bakeAxisConversion = true;
                return;
            }

            if (assetPath.EndsWith("UAL2_Standard.fbx"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.optimizeGameObjects = false;
                importer.bakeAxisConversion = true;
                var clips = importer.defaultClipAnimations;
                for (var i = 0; i < clips.Length; i++)
                {
                    var loop = clips[i].name.Contains("_Loop");
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                }
                if (clips.Length > 0) importer.clipAnimations = clips;
                return;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.bakeAxisConversion = true;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = assetPath.Contains("/Rex/") || assetPath.Contains("/Heroes/") ||
                                      assetPath.Contains("/KayKit/") ? 1024 : 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            if (assetPath.Contains("/KayKit/"))
            {
                importer.filterMode = FilterMode.Point;
                importer.anisoLevel = 0;
            }

            if (assetPath.Contains("_Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (assetPath.Contains("_ORM") || assetPath.Contains("_DetailMask"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
            }

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 60;
            importer.SetPlatformTextureSettings(android);

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = importer.maxTextureSize;
            ios.format = TextureImporterFormat.ASTC_6x6;
            ios.compressionQuality = 60;
            importer.SetPlatformTextureSettings(ios);
        }
    }

    /// <summary>
    /// The first project import may discover FBX files before this postprocessor
    /// has compiled. This one-shot repair pass guarantees the two humanoid assets
    /// are configured after the editor domain reload as well.
    /// </summary>
    [InitializeOnLoad]
    internal static class AuthoredArtImportRepair
    {
        private const string RexPath = "Assets/Shatterspire/Resources/Art3D/KayKit/Characters/Ranger.fbx";
        private const string AnimationPath = "Assets/Shatterspire/Resources/Art3D/Animations/UAL2_Standard.fbx";
        private static readonly string[] CompanionPaths =
        {
            "Assets/Shatterspire/Resources/Art3D/KayKit/Characters/Barbarian.fbx",
            "Assets/Shatterspire/Resources/Art3D/KayKit/Characters/Mage.fbx"
        };
        private static readonly string[] KayAnimationPaths =
        {
            "Assets/Shatterspire/Resources/Art3D/KayKit/Animations/Rig_Medium_General.fbx",
            "Assets/Shatterspire/Resources/Art3D/KayKit/Animations/Rig_Medium_MovementBasic.fbx"
        };
        private static readonly string[] SkeletonPaths =
        {
            "Assets/Shatterspire/Resources/Art3D/KayKit/Skeletons/Characters/Skeleton_Mage.fbx",
            "Assets/Shatterspire/Resources/Art3D/KayKit/Skeletons/Characters/Skeleton_Minion.fbx",
            "Assets/Shatterspire/Resources/Art3D/KayKit/Skeletons/Characters/Skeleton_Rogue.fbx",
            "Assets/Shatterspire/Resources/Art3D/KayKit/Skeletons/Characters/Skeleton_Warrior.fbx"
        };
        private static bool queued;

        static AuthoredArtImportRepair() => Queue();

        [MenuItem("SHATTERSPIRE/Repair Authored Art Import")]
        private static void RepairFromMenu() => Queue();

        private static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += Repair;
        }

        private static void Repair()
        {
            queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Queue();
                return;
            }

            var rexChanged = ConfigureHumanoid(RexPath, false);
            var animationChanged = ConfigureHumanoid(AnimationPath, true);
            var companionsChanged = false;
            foreach (var path in CompanionPaths)
                companionsChanged |= ConfigureHumanoid(path, false);
            var kayAnimationsChanged = false;
            foreach (var path in KayAnimationPaths)
                kayAnimationsChanged |= ConfigureHumanoid(path, true);
            var skeletonsChanged = false;
            foreach (var path in SkeletonPaths)
                skeletonsChanged |= ConfigureHumanoid(path, false);
            if (rexChanged || animationChanged || companionsChanged || kayAnimationsChanged || skeletonsChanged)
                Debug.Log("SHATTERSPIRE: Authored heroes and humanoid animation imports configured.");
        }

        private static bool ConfigureHumanoid(string path, bool importAnimations)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return false;
            var changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (importer.importAnimation != importAnimations) { importer.importAnimation = importAnimations; changed = true; }
            if (importer.optimizeGameObjects) { importer.optimizeGameObjects = false; changed = true; }
            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; changed = true; }

            if (importAnimations)
            {
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                var clipsChanged = false;
                for (var i = 0; i < clips.Length; i++)
                {
                    var shouldLoop = clips[i].name.Contains("_Loop") || clips[i].name.Contains("Idle") ||
                                     clips[i].name.Contains("Walking") || clips[i].name.Contains("Running");
                    if (clips[i].loopTime == shouldLoop && clips[i].loopPose == shouldLoop) continue;
                    clips[i].loopTime = shouldLoop;
                    clips[i].loopPose = shouldLoop;
                    clipsChanged = true;
                }
                if (clipsChanged)
                {
                    importer.clipAnimations = clips;
                    changed = true;
                }
            }

            if (changed) importer.SaveAndReimport();
            return changed;
        }
    }
}
