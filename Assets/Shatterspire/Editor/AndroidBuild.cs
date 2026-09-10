using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shatterspire.Editor
{
    internal static class AndroidBuild
    {
        private const string OutputDirectory = "Builds/Android";
        private const string OutputPath = OutputDirectory + "/Shatterspire-Prototype.apk";

        [MenuItem("SHATTERSPIRE/Prepare Android Project")]
        public static void PrepareAndroidProject()
        {
            PlayerSettings.companyName = "Shatterspire Studio";
            PlayerSettings.productName = "SHATTERSPIRE";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.shatterspire.prototype");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.preserveFramebufferAlpha = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = true;

            EditorUserBuildSettings.buildAppBundle = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            AssetDatabase.SaveAssets();
            Debug.Log("SHATTERSPIRE: Android settings prepared (Landscape, ARM64, min SDK 26, APK output)." );
        }

        [MenuItem("SHATTERSPIRE/Build Android APK")]
        public static void BuildApk()
        {
            PrepareAndroidProject();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is not installed or the platform switch failed.");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scene found in Build Settings.");

            Directory.CreateDirectory(OutputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)." );

            Debug.Log($"SHATTERSPIRE APK created: {Path.GetFullPath(OutputPath)} ({report.summary.totalSize / 1048576f:0.0} MB)" );
            EditorUtility.RevealInFinder(OutputPath);
        }
    }
}
