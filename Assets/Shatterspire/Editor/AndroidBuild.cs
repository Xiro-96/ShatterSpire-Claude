using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// APK fuers Telefon. Im Editor ueber SHATTERSPIRE › Build Android APK, ohne Editor per
    /// Unity.exe -batchmode -quit -buildTarget Android -executeMethod Shatterspire.Editor.AndroidBuild.BuildApk
    /// </summary>
    public static class AndroidBuild
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
            // ARM64 gibt es auf Android nur mit IL2CPP. Mit dem Standard Mono brach der Build ab.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
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

        /// <summary>
        /// Windows-Build nur fuer automatische Bildkontrollen mit CaptureDemo, nicht zum Verteilen.
        /// Aufruf: Shatterspire.exe -shatterspire-capture Ordner
        /// </summary>
        public static void BuildWindowsCapture()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scene found in Build Settings.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/WindowsCapture/Shatterspire.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows capture build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
            Debug.Log("SHATTERSPIRE Windows capture build ready.");
        }
    }
}
