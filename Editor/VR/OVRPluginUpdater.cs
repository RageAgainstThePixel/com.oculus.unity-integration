/************************************************************************************

Copyright   :   Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus SDK License Version 3.4.1 (the "License");
you may not use the Oculus SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.4.1

Unless required by applicable law or agreed to in writing, the Oculus SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS
#define USING_XR_SDK
#endif

#if UNITY_2020_1_OR_NEWER
#define REQUIRES_XR_SDK
#endif

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
internal class OVRPluginUpdater
{
    private enum PluginPlatform
    {
        Android,
        AndroidUniversal,
        AndroidOpenXR,
        OSXUniversal,
        Win,
        Win64,
        Win64OpenXR,
    }

    private class PluginPackage
    {
        public string RootPath;
        public Version Version;
        public Dictionary<PluginPlatform, string> Plugins = new Dictionary<PluginPlatform, string>();

        public bool IsEnabled()
        {
            return Plugins.Any(pair =>
            {
                GetPluginPaths(pair.Value, out _, out var enabledPath);
                return File.Exists(enabledPath);
            });
        }

        public bool IsPlatformPresent(PluginPlatform platform)
        {
            if (Plugins.TryGetValue(platform, out var path))
            {
                GetPluginPaths(path, out var disabledPath, out var enabledPath);

                if (File.Exists(enabledPath) ||
                    File.Exists(disabledPath))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPlatformEnabled(PluginPlatform platform)
        {
            if (Plugins.TryGetValue(platform, out var path))
            {
                GetPluginPaths(path, out _, out var relPath);

                if (File.Exists(relPath))
                {
                    PluginImporter pi = AssetImporter.GetAtPath(relPath) as PluginImporter;

                    if (pi != null)
                    {
                        return pi.GetCompatibleWithPlatform(GetBuildTarget(platform)) && pi.GetCompatibleWithEditor();
                    }
                }
            }

            return false;
        }

        private BuildTarget GetBuildTarget(PluginPlatform platform)
        {
            switch (platform)
            {
                case PluginPlatform.Android:
                case PluginPlatform.AndroidUniversal:
                case PluginPlatform.AndroidOpenXR:
                    return BuildTarget.Android;
                case PluginPlatform.OSXUniversal:
                    return BuildTarget.StandaloneOSX;
                case PluginPlatform.Win:
                    return BuildTarget.StandaloneWindows;
                case PluginPlatform.Win64:
                case PluginPlatform.Win64OpenXR:
                    return BuildTarget.StandaloneWindows64;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }
    }

    private static bool restartPending = false;
    private static bool unityRunningInBatchmode = false;
    private static bool unityVersionSupportsAndroidUniversal = true;
    private static bool enableAndroidUniversalSupport = true;

    private static Version invalidVersion = new Version("0.0.0");
    private static Version minimalProductionVersionForOpenXR = new Version(1, 63, 0);


    static OVRPluginUpdater()
    {
        EditorApplication.delayCall += OnDelayCall;
    }

    private static void OnDelayCall()
    {
        if (Environment.CommandLine.Contains("-batchmode"))
        {
            unityRunningInBatchmode = true;
        }

        if (enableAndroidUniversalSupport)
        {
            unityVersionSupportsAndroidUniversal = true;
        }

        if (ShouldAttemptPluginUpdate())
        {
            AttemptPluginUpdate(true);
        }
    }

    private static PluginPackage GetPluginPackage(string rootPath)
    {
        return new PluginPackage
        {
            RootPath = rootPath,
            Version = GetPluginVersion(rootPath),
            Plugins = new Dictionary<PluginPlatform, string>
            {
                { PluginPlatform.Android,          $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.Android)         }"},
                { PluginPlatform.AndroidUniversal, $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.AndroidUniversal)}"},
                { PluginPlatform.AndroidOpenXR,    $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.AndroidOpenXR)   }"},
                { PluginPlatform.OSXUniversal,     $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.OSXUniversal)    }"},
                { PluginPlatform.Win,              $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.Win)             }"},
                { PluginPlatform.Win64,            $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.Win64)           }"},
                { PluginPlatform.Win64OpenXR,      $"{rootPath}{GetPluginBuildTargetSubPath(PluginPlatform.Win64OpenXR)     }"},
            }
        };
    }

    private static List<PluginPackage> GetAllUtilitiesPluginPackages()
    {
        var pluginRootPath = GetUtilitiesPluginRootPath();
        List<PluginPackage> packages = new List<PluginPackage>();

        if (Directory.Exists(pluginRootPath))
        {
            var dirs = Directory.GetDirectories(pluginRootPath);
            packages.AddRange(dirs.Select(GetPluginPackage));
        }

        return packages;
    }

    private static string GetCurrentProjectPath()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static string GetUtilitiesPluginRootPath()
    {
        return GetUtilitiesRootPath() + @"/Runtime/VR/Plugins";
    }

    private static string GetUtilitiesRootPath()
    {
        var so = ScriptableObject.CreateInstance(typeof(OVRPluginUpdaterStub));
        var script = MonoScript.FromScriptableObject(so);
        string assetPath = AssetDatabase.GetAssetPath(script);
        string editorVrPath = Directory.GetParent(assetPath).FullName;
        string editorPath = Directory.GetParent(editorVrPath).FullName;
        return Directory.GetParent(editorPath).FullName;
    }

    private static string GetPluginBuildTargetSubPath(PluginPlatform target)
    {
        string path;

        switch (target)
        {
            case PluginPlatform.Android:
                path = @"/Android/OVRPlugin.aar";
                break;
            case PluginPlatform.AndroidUniversal:
                path = @"/AndroidUniversal/OVRPlugin.aar";
                break;
            case PluginPlatform.AndroidOpenXR:
                path = @"/AndroidOpenXR/OVRPlugin.aar";
                break;
            case PluginPlatform.OSXUniversal:
                path = @"/OSXUniversal/OVRPlugin.bundle";
                break;
            case PluginPlatform.Win:
                path = @"/Win/OVRPlugin.dll";
                break;
            case PluginPlatform.Win64:
                path = @"/Win64/OVRPlugin.dll";
                break;
            case PluginPlatform.Win64OpenXR:
                path = @"/Win64OpenXR/OVRPlugin.dll";
                break;
            default:
                throw new ArgumentException("Attempted GetPluginBuildTargetSubPath() for unsupported BuildTarget: " + target);
        }

        return path;
    }

    private static string GetDisabledPluginSuffix()
    {
        return @".disabled";
    }

    private static Version GetPluginVersion(string path)
    {
        Version pluginVersion;

        try
        {
            pluginVersion = new Version(Path.GetFileName(path));
        }
        catch
        {
            pluginVersion = invalidVersion;
        }

        if (pluginVersion == invalidVersion)
        {
            //Unable to determine version from path, fallback to Win64 DLL meta data
            path += GetPluginBuildTargetSubPath(PluginPlatform.Win64);
            if (!File.Exists(path))
            {
                path += GetDisabledPluginSuffix();
                if (!File.Exists(path))
                {
                    return invalidVersion;
                }
            }

            FileVersionInfo pluginVersionInfo = FileVersionInfo.GetVersionInfo(path);

            if (string.IsNullOrEmpty(pluginVersionInfo.ProductVersion))
            {
                return invalidVersion;
            }

            pluginVersion = new Version(pluginVersionInfo.ProductVersion);
        }

        return pluginVersion;
    }

    public static string GetVersionDescription(Version version)
    {
        bool isVersionValid = version != invalidVersion;
        return isVersionValid ? version.ToString() : "(Unknown)";
    }

    private static bool ShouldAttemptPluginUpdate()
    {
        if (unityRunningInBatchmode)
        {
            return false;
        }

        return !UnitySupportsEnabledAndroidPlugin() || autoUpdateEnabled && !restartPending && !Application.isPlaying;
    }

    private static void DisableAllUtilitiesPluginPackages()
    {
        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            foreach (var path in pluginPkg.Plugins.Values)
            {
                GetPluginPaths(path, out _, out var pluginPath);

                if (Directory.Exists(pluginPath) ||
                    File.Exists(pluginPath))
                {
                    AssetDatabase.DeleteAsset(pluginPath);
                }
            }
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
    }

    private static void GetPluginPaths(string path, out string disabledPath, out string enabledPath)
    {
        string basePath = GetCurrentProjectPath();
        string relPath = path.Substring(basePath.Length + 1).Replace("\\", "/");
        disabledPath = relPath + GetDisabledPluginSuffix();
        relPath = relPath.Replace("Packages/com.oculus.unity-integration/Runtime/VR/Plugins/", "");
        enabledPath = $"Assets/Plugins/OVR/{relPath}";
    }

    private static void EnablePluginPackage(PluginPackage pluginPkg)
    {
#if UNITY_2020_1_OR_NEWER
		bool activateOpenXRPlugin = pluginPkg.Version >= minimalProductionVersionForOpenXR;
		if (activateOpenXRPlugin && !unityRunningInBatchmode)
		{
			while(true)
			{
				// display a dialog to prompt developer to confirm if they want to proceed with OpenXR backend
				int result = EditorUtility.DisplayDialogComplex("OpenXR Backend",
					"OpenXR is now fully supported by Oculus. However, some of the functionalities are not supported in the baseline OpenXR spec, which would be provided in our future releases.\n\nIf you depend on the following features in your project, please click Cancel to continue using the legacy backend:\n\n  1. Advanced hand tracking features (collision capsule, input metadata, Thumb0, default handmesh)\n  2. Mixed Reality Capture on Rift\n\nNew features, such as Passthrough API, are only supported through the OpenXR backend.\n\nPlease check our release notes for more details.\n\nReminder: you can switch the legacy and OpenXR backends at any time from Oculus > Tools > OpenXR menu options.", "Use OpenXR", "Cancel", "Release Notes");
				if (result == 0)
                {
                    break;
                }
                else if (result == 1)
				{
					activateOpenXRPlugin = false;
					break;
				}
				else if (result == 2)
				{
					Application.OpenURL("https://developer.oculus.com/downloads/package/unity-integration/");
				}
				else
				{
					UnityEngine.Debug.LogWarningFormat("Unrecognized result from DisplayDialogComplex: {0}", result);
					break;
				}
			}
		}
#else
        bool activateOpenXRPlugin = false;
#endif
        if (activateOpenXRPlugin)
        {
            UnityEngine.Debug.Log("OVRPlugin with OpenXR backend is activated by default");
            if (!unityRunningInBatchmode)
            {
                EditorUtility.DisplayDialog("OVRPlugin", "OVRPlugin with OpenXR backend will be activated by default", "Ok");
            }
        }
        else
        {
            UnityEngine.Debug.Log("OVRPlugin with LibOVR/VRAPI backend is activated by default");
            if (!unityRunningInBatchmode)
            {
                EditorUtility.DisplayDialog("OVRPlugin", "OVRPlugin with LibOVR/VRAPI backend will be activated by default", "Ok");
            }
        }

        foreach (var kvp in pluginPkg.Plugins)
        {
            PluginPlatform platform = kvp.Key;
            string path = kvp.Value;

            if (Directory.Exists($"{path}{GetDisabledPluginSuffix()}") ||
                File.Exists($"{path}{GetDisabledPluginSuffix()}"))
            {
                GetPluginPaths(path, out var relDisabledPath, out var pluginPath);
                string pluginDirectory = Directory.GetParent($"{GetCurrentProjectPath()}/{pluginPath}").FullName;

                if (!Directory.Exists(pluginDirectory))
                {
                    Directory.CreateDirectory(pluginDirectory);
                }

                File.Copy(Path.GetFullPath(relDisabledPath), Path.GetFullPath(pluginPath));
                AssetDatabase.ImportAsset(pluginPath, ImportAssetOptions.ForceUpdate);

                PluginImporter pi = AssetImporter.GetAtPath(pluginPath) as PluginImporter;

                if (pi == null)
                {
                    continue;
                }

                // Disable support for all platforms, then conditionally enable desired support below
                pi.SetCompatibleWithEditor(false);
                pi.SetCompatibleWithAnyPlatform(false);
                pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
                pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);

                switch (platform)
                {
                    case PluginPlatform.Android:
                        pi.SetCompatibleWithPlatform(BuildTarget.Android, !unityVersionSupportsAndroidUniversal);
                        if (!unityVersionSupportsAndroidUniversal)
                        {
                            pi.SetPlatformData(BuildTarget.Android, "CPU", "ARMv7");
                        }
                        break;
                    case PluginPlatform.AndroidUniversal:
                        if (!activateOpenXRPlugin)
                        {
                            pi.SetCompatibleWithPlatform(BuildTarget.Android, unityVersionSupportsAndroidUniversal);
                        }
                        break;
                    case PluginPlatform.AndroidOpenXR:
                        if (activateOpenXRPlugin)
                        {
                            pi.SetCompatibleWithPlatform(BuildTarget.Android, unityVersionSupportsAndroidUniversal);
                        }
                        break;
                    case PluginPlatform.OSXUniversal:
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                        pi.SetCompatibleWithEditor(true);
                        pi.SetEditorData("CPU", "AnyCPU");
                        pi.SetEditorData("OS", "OSX");
                        pi.SetPlatformData("Editor", "CPU", "AnyCPU");
                        pi.SetPlatformData("Editor", "OS", "OSX");
                        break;
                    case PluginPlatform.Win:
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, true);
                        pi.SetCompatibleWithEditor(true);
                        pi.SetEditorData("CPU", "X86");
                        pi.SetEditorData("OS", "Windows");
                        break;
                    case PluginPlatform.Win64:
                        if (!activateOpenXRPlugin)
                        {
                            pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                            pi.SetCompatibleWithEditor(true);
                            pi.SetEditorData("CPU", "X86_64");
                            pi.SetEditorData("OS", "Windows");
                            pi.SetPlatformData("Editor", "CPU", "X86_64");
                            pi.SetPlatformData("Editor", "OS", "Windows");
                        }
                        break;
                    case PluginPlatform.Win64OpenXR:
                        if (activateOpenXRPlugin)
                        {
                            pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                            pi.SetCompatibleWithEditor(true);
                            pi.SetEditorData("CPU", "X86_64");
                            pi.SetEditorData("OS", "Windows");
                            pi.SetPlatformData("Editor", "CPU", "X86_64");
                            pi.SetPlatformData("Editor", "OS", "Windows");
                        }
                        break;
                    default:
                        throw new ArgumentException("Attempted EnablePluginPackage() for unsupported BuildTarget: " + platform);
                }

                AssetDatabase.ImportAsset(pluginPath, ImportAssetOptions.ForceUpdate);
            }
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

    }

    private static readonly string autoUpdateEnabledKey = "Oculus_Utilities_OVRPluginUpdater_AutoUpdate_" + OVRManager.utilitiesVersion;
    private static bool autoUpdateEnabled
    {
        get => PlayerPrefs.GetInt(autoUpdateEnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(autoUpdateEnabledKey, value ? 1 : 0);
    }

    [MenuItem("Oculus/Tools/Disable OVR Utilities Plugin")]
    private static void AttemptPluginDisable()
    {
        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        PluginPackage enabledUtilsPluginPkg = null;

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (pluginPkg.IsEnabled())
            {
                if (enabledUtilsPluginPkg == null || pluginPkg.Version > enabledUtilsPluginPkg.Version)
                {
                    enabledUtilsPluginPkg = pluginPkg;
                }
            }
        }

        if (enabledUtilsPluginPkg == null)
        {
            if (unityRunningInBatchmode
                || EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
                    "The OVRPlugin included with Oculus Utilities is already disabled."
                        + " The OVRPlugin installed through the Package Manager will continue to be used.\n",
                    "Ok",
                    ""))
            {
                return;
            }
        }
        else
        {
            if (unityRunningInBatchmode
                || EditorUtility.DisplayDialog("Disable Oculus Utilities Plugin",
                    "Do you want to disable the OVRPlugin included with Oculus Utilities and revert to the OVRPlugin installed through the Package Manager?\n\n"
                        + "Current version: " + GetVersionDescription(enabledUtilsPluginPkg.Version),
                    "Yes",
                    "No"))
            {
                DisableAllUtilitiesPluginPackages();

                if (unityRunningInBatchmode
                    || EditorUtility.DisplayDialog("Restart Unity",
                        "Now you will be using the OVRPlugin installed through Package Manager."
                            + "\n\nPlease restart the Unity Editor to complete the update process.",
                        "Restart",
                        "Not Now"))
                {
                    RestartUnityEditor();
                }
            }
        }
    }

    [MenuItem("Oculus/Tools/Update OVR Utilities Plugin")]
    private static void RunPluginUpdate()
    {
        autoUpdateEnabled = true;
        AttemptPluginUpdate(false);
    }

    private static void BatchmodeActivateOVRPluginOpenXR()
    {
        OnDelayCall(); // manually invoke when running editor in batchmode
        ActivateOVRPluginOpenXR();
    }

    [MenuItem("Oculus/Tools/OpenXR/Switch to OVRPlugin with OpenXR backend", true)]
    private static bool IsActivateOVRPluginOpenXRMenuEnabled()
    {
#if !USING_XR_SDK && !REQUIRES_XR_SDK
		return false;
#else
        return true;
#endif
    }

    [MenuItem("Oculus/Tools/OpenXR/Switch to OVRPlugin with OpenXR backend")]
    private static void ActivateOVRPluginOpenXR()
    {
        if (!unityVersionSupportsAndroidUniversal)
        {
            UnityEngine.Debug.LogError("Unexpected error: Unity must support AndroidUniversal version of Oculus Utilities Plugin for accessing OpenXR");
            return;
        }

#if !USING_XR_SDK && !REQUIRES_XR_SDK
		UnityEngine.Debug.LogError("Oculus Utilities Plugin with OpenXR only supports XR Plug-in Managmenent with Oculus XR Plugin");
		return;
#else

        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        PluginPackage enabledUtilsPluginPkg = null;

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (pluginPkg.IsEnabled())
            {
                enabledUtilsPluginPkg = pluginPkg;
                break;
            }
        }

        if (enabledUtilsPluginPkg == null)
        {
            UnityEngine.Debug.LogError("Unable to Activate OVRPlugin with OpenXR: Oculus Utilities Plugin package not activated");
            return;
        }

        if (!enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.AndroidOpenXR) &&
            !enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.Win64OpenXR))
        {
            UnityEngine.Debug.LogError("Unable to Activate OVRPlugin with OpenXR: Both AndroidOpenXR/OVRPlugin.aar or Win64OpenXR/OVRPlugin.dll does not exist");
            return;
        }

        if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR) &&
            enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.Win64OpenXR))
        {
            if (!unityRunningInBatchmode)
            {
                EditorUtility.DisplayDialog("Unable to Activate OVRPlugin with OpenXR", "Both AndroidOpenXR/OVRPlugin.aar and Win64OpenXR/OVRPlugin.dll already enabled", "Ok");
            }
            return;
        }

        if (enabledUtilsPluginPkg.Version < minimalProductionVersionForOpenXR)
        {
            if (!unityRunningInBatchmode)
            {
                bool accepted = EditorUtility.DisplayDialog("Warning",
                    "OVRPlugin with OpenXR backend is experimental before v31. You may expect to encounter stability issues and/or missing functionalities, " +
                    "including but not limited to, fixed foveated rendering / composition layer / display refresh rates / etc." +
                    "\n\n" +
                    "Also, OVRPlugin with OpenXR backend only supports XR Plug-in Managmenent with Oculus XR Plugin on Quest",
                    "Continue", "Cancel");

                if (!accepted)
                {
                    return;
                }
            }
        }

        if (enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.AndroidOpenXR) &&
            !enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR))
        {
            if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidUniversal))
            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.AndroidUniversal], out _, out var androidUniversalPath);
                PluginImporter pi = AssetImporter.GetAtPath(androidUniversalPath) as PluginImporter;

                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
                    AssetDatabase.ImportAsset(androidUniversalPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + androidUniversalPath);
                }
            }

            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.AndroidOpenXR], out _, out var androidOpenXRPluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(androidOpenXRPluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.Android, true);
                    AssetDatabase.ImportAsset(androidOpenXRPluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + androidOpenXRPluginRelPath);
                }
            }
        }


        bool win64PluginUpdated = false;

        if (enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.Win64OpenXR) &&
            !enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.Win64OpenXR))
        {
            if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.Win64))
            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.Win64], out _, out var win64PluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(win64PluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.ClearSettings();
                    pi.SetCompatibleWithEditor(false);
                    pi.SetCompatibleWithAnyPlatform(false);
                    AssetDatabase.ImportAsset(win64PluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + win64PluginRelPath);
                }
            }

            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.Win64OpenXR], out _, out var win64OpenXRPluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(win64OpenXRPluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                    pi.SetCompatibleWithEditor(true);
                    pi.SetEditorData("CPU", "X86_64");
                    pi.SetEditorData("OS", "Windows");
                    pi.SetPlatformData("Editor", "CPU", "X86_64");
                    pi.SetPlatformData("Editor", "OS", "Windows");
                    AssetDatabase.ImportAsset(win64OpenXRPluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + win64OpenXRPluginRelPath);
                }
            }

            win64PluginUpdated = true;
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        if (!unityRunningInBatchmode)
        {
            EditorUtility.DisplayDialog("Activate OVRPlugin with OpenXR", "Oculus Utilities Plugin with OpenXR has been enabled on Android", "Ok");
            if (win64PluginUpdated && EditorUtility.DisplayDialog("Restart Unity",
                                    "Win64 plugin updated. Do you want to restart Unity editor?",
                                    "Restart",
                                    "Not Now"))
            {
                RestartUnityEditor();
            }
        }
#endif // !USING_XR_SDK
    }

    [MenuItem("Oculus/Tools/OpenXR/Switch to Legacy OVRPlugin (with LibOVR and VRAPI backends)")]
    private static void RestoreStandardOVRPlugin()
    {
        if (!unityVersionSupportsAndroidUniversal) // sanity check
        {
            UnityEngine.Debug.LogError("Unexpected error: Unity must support AndroidUniversal version of Oculus Utilities Plugin for accessing OpenXR");
            return;
        }

        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        PluginPackage enabledUtilsPluginPkg = null;

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (pluginPkg.IsEnabled())
            {
                enabledUtilsPluginPkg = pluginPkg;
                break;
            }
        }

        if (enabledUtilsPluginPkg == null)
        {
            UnityEngine.Debug.LogError("Unable to Restore Standard Oculus Utilities Plugin: Oculus Utilities Plugin package not activated");
            return;
        }

        if (!enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.AndroidUniversal) &&
            !enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.Win64))
        {
            UnityEngine.Debug.LogError("Unable to Restore Standard Oculus Utilities Plugin: Both AndroidOpenXR/OVRPlugin.aar and Win64/OVRPlugin.dll does not exist");
            return;
        }

        if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidUniversal) &&
            enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.Win64))
        {
            if (!unityRunningInBatchmode)
            {
                EditorUtility.DisplayDialog("Unable to Restore Standard Oculus Utilities Plugin", "Both AndroidUniversal/OVRPlugin.aar and Win64/OVRPlugin.dll already enabled", "Ok");
            }
            return;
        }

        if (enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.AndroidUniversal) &&
            !enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidUniversal))
        {
            if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR))
            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.AndroidOpenXR], out _, out var androidOpenXRPluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(androidOpenXRPluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
                    AssetDatabase.ImportAsset(androidOpenXRPluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + androidOpenXRPluginRelPath);
                }
            }

            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.AndroidUniversal], out _, out var androidUniversalPath);
                PluginImporter pi = AssetImporter.GetAtPath(androidUniversalPath) as PluginImporter;
                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.Android, true);
                    AssetDatabase.ImportAsset(androidUniversalPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + androidUniversalPath);
                }
            }

        }

        bool win64PluginUpdated = false;
        if (enabledUtilsPluginPkg.IsPlatformPresent(PluginPlatform.Win64) &&
            !enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.Win64))
        {
            if (enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR))
            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.Win64OpenXR], out _, out var win64OpenXRPluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(win64OpenXRPluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.ClearSettings();
                    pi.SetCompatibleWithEditor(false);
                    pi.SetCompatibleWithAnyPlatform(false);
                    AssetDatabase.ImportAsset(win64OpenXRPluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + win64OpenXRPluginRelPath);
                }
            }

            {
                GetPluginPaths(enabledUtilsPluginPkg.Plugins[PluginPlatform.Win64], out _, out var win64PluginRelPath);
                PluginImporter pi = AssetImporter.GetAtPath(win64PluginRelPath) as PluginImporter;

                if (pi != null)
                {
                    pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                    pi.SetCompatibleWithEditor(true);
                    pi.SetEditorData("CPU", "X86_64");
                    pi.SetEditorData("OS", "Windows");
                    pi.SetPlatformData("Editor", "CPU", "X86_64");
                    pi.SetPlatformData("Editor", "OS", "Windows");
                    AssetDatabase.ImportAsset(win64PluginRelPath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Unable to find PluginImporter: " + win64PluginRelPath);
                }
            }

            win64PluginUpdated = true;
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        if (!unityRunningInBatchmode)
        {
            EditorUtility.DisplayDialog("Restore Standard OVRPlugin", "Standard version of Oculus Utilities Plugin has been enabled on Android", "Ok");
            if (win64PluginUpdated && EditorUtility.DisplayDialog("Restart Unity",
                                    "Win64 plugin updated. Do you want to restart Unity editor?",
                                    "Restart",
                                    "Not Now"))
            {
                RestartUnityEditor();
            }
        }
    }

    // Test if the OVRPlugin/OpenXR plugin is currently activated, used by other editor utilities
    public static bool IsOVRPluginOpenXRActivated()
    {
        if (!unityVersionSupportsAndroidUniversal) // sanity check
        {
            return false;
        }

        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        PluginPackage enabledUtilsPluginPkg = null;

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (pluginPkg.IsEnabled())
            {
                enabledUtilsPluginPkg = pluginPkg;
                break;
            }
        }

        if (enabledUtilsPluginPkg == null)
        {
            return false;
        }

        return enabledUtilsPluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR);
    }

    // Separate entry point needed since "-executeMethod" does not support parameters or default parameter values
    private static void BatchmodePluginUpdate()
    {
        OnDelayCall(); // manually invoke when running editor in batchmode
        AttemptPluginUpdate(false);
    }

    private static void AttemptPluginUpdate(bool triggeredByAutoUpdate)
    {
        OVRPlugin.SendEvent("attempt_plugin_update_auto", triggeredByAutoUpdate.ToString());

        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        PluginPackage enabledUtilsPluginPkg = null;
        PluginPackage newestUtilsPluginPkg = null;

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (newestUtilsPluginPkg == null || pluginPkg.Version > newestUtilsPluginPkg.Version)
            {
                newestUtilsPluginPkg = pluginPkg;
            }

            if (pluginPkg.IsEnabled())
            {
                if (enabledUtilsPluginPkg == null || pluginPkg.Version > enabledUtilsPluginPkg.Version)
                {
                    enabledUtilsPluginPkg = pluginPkg;
                }
            }
        }

        bool reenableCurrentPluginPkg = false;
        PluginPackage targetPluginPkg = null;

        if (newestUtilsPluginPkg != null)
        {
            if (enabledUtilsPluginPkg == null || enabledUtilsPluginPkg.Version != newestUtilsPluginPkg.Version)
            {
                targetPluginPkg = newestUtilsPluginPkg;
            }
        }

        PluginPackage currentPluginPkg = enabledUtilsPluginPkg;

        if ((targetPluginPkg == null) && !UnitySupportsEnabledAndroidPlugin())
        {
            // Force reenabling the current package to configure the correct android plugin for this unity version.
            reenableCurrentPluginPkg = true;
            targetPluginPkg = currentPluginPkg;
        }

        if (currentPluginPkg != null && targetPluginPkg == null)
        {
            if (!triggeredByAutoUpdate && !unityRunningInBatchmode)
            {
                EditorUtility.DisplayDialog("Update Oculus Utilities Plugin",
                    "OVRPlugin is already up to date.\n\nCurrent version: "
                        + GetVersionDescription(currentPluginPkg.Version),
                    "Ok",
                    "");
            }

            return; // No update necessary.
        }


        bool userAcceptsUpdate = false;

        if (unityRunningInBatchmode || currentPluginPkg == null)
        {
            userAcceptsUpdate = true;
        }
        else
        {
            Version targetVersion = targetPluginPkg.Version;
            string dialogBody = "Oculus Utilities has detected that a newer OVRPlugin is available."
                + " Using the newest version is recommended. Do you want to enable it?\n\n"
                + "Current version: "
                + GetVersionDescription(currentPluginPkg.Version)
                + "\nAvailable version: "
                + targetVersion;

            if (reenableCurrentPluginPkg)
            {
                dialogBody = "Oculus Utilities has detected a configuration change that requires re-enabling the current OVRPlugin."
                    + " Do you want to proceed?\n\nCurrent version: "
                    + GetVersionDescription(currentPluginPkg.Version);
            }

            int dialogResult = EditorUtility.DisplayDialogComplex("Update Oculus Utilities Plugin", dialogBody, "Yes", "No, Don't Ask Again", "No");

            switch (dialogResult)
            {
                case 0: // "Yes"
                    userAcceptsUpdate = true;
                    break;
                case 1: // "No, Don't Ask Again"
                    autoUpdateEnabled = false;

                    EditorUtility.DisplayDialog("Oculus Utilities OVRPlugin",
                        "To manually update in the future, use the following menu option:\n\n"
                            + "[Oculus -> Tools -> Update OVR Utilities Plugin]",
                        "Ok",
                        "");
                    return;
                case 2: // "No"
                    return;
            }
        }

        if (userAcceptsUpdate)
        {
            DisableAllUtilitiesPluginPackages();
            EnablePluginPackage(targetPluginPkg);

            if (unityRunningInBatchmode
                || EditorUtility.DisplayDialog("Restart Unity",
                    "OVRPlugin has been updated to "
                        + GetVersionDescription(targetPluginPkg.Version)
                        + ".\n\nPlease restart the Unity Editor to complete the update process."
                        ,
                    "Restart",
                    "Not Now"))
            {
                RestartUnityEditor();
            }
        }
    }

    private static bool UnitySupportsEnabledAndroidPlugin()
    {
        List<PluginPackage> allUtilsPluginPkgs = GetAllUtilitiesPluginPackages();

        foreach (PluginPackage pluginPkg in allUtilsPluginPkgs)
        {
            if (pluginPkg.IsEnabled())
            {
                if ((pluginPkg.IsPlatformEnabled(PluginPlatform.AndroidUniversal) ||
                     pluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR)) &&
                    !unityVersionSupportsAndroidUniversal)
                {
                    // Android Universal should only be enabled on supported Unity versions since it can prevent app launch.
                    return false;
                }
                else if (!pluginPkg.IsPlatformEnabled(PluginPlatform.AndroidUniversal) &&
                         pluginPkg.IsPlatformPresent(PluginPlatform.AndroidUniversal) &&
                         !pluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR) &&
                         pluginPkg.IsPlatformEnabled(PluginPlatform.AndroidOpenXR) &&
                    unityVersionSupportsAndroidUniversal)
                {
                    // Android Universal is present and should be enabled on supported Unity versions since ARM64 config will fail otherwise.
                    return false;
                }
            }
        }

        return true;
    }

    private static void RestartUnityEditor()
    {
        if (unityRunningInBatchmode)
        {
            EditorApplication.Exit(0);
        }
        else
        {
            restartPending = true;
            EditorApplication.OpenProject(GetCurrentProjectPath());
        }
    }
}
