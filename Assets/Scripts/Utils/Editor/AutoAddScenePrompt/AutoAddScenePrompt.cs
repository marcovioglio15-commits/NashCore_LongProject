using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that detects new scene assets and prompts the user
/// to add them to the Build Settings list.
/// </summary>
[InitializeOnLoad] 
public static class AutoAddScenePrompt
{
    #region Variables And Properties
    // Keeps track of all known scene paths to detect newly created ones
    private static HashSet<string> knownScenes = new HashSet<string>();
    #endregion

    #region Constructors
    // Static constructor runs once when the editor domain loads
    static AutoAddScenePrompt()
    {
        // Collect all current scenes in the project at startup
        string[] allScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);

        // Store them in the hash set for reference
        foreach (string path in allScenes)
            knownScenes.Add(path);

        // Register callback when the asset database changes (new, deleted, renamed files)
        EditorApplication.projectChanged += OnProjectChanged;
    }
    #endregion

    #region Methods
    // Called automatically when something changes in the project (files added, removed, etc.)
    private static void OnProjectChanged()
    {
        // Retrieve current list of all .unity scene files in the project
        string[] allScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);

        // Iterate through every scene found in Assets
        foreach (string scenePath in allScenes)
        {
            // If this path wasn't previously known, it's a newly created scene
            if (!knownScenes.Contains(scenePath))
            {
                // Ask user whether to add this scene to the build list
                bool addScene = EditorUtility.DisplayDialog(
                    "Add Scene to Build Settings?",                                 // Dialog title
                    $"A new scene has been detected:\n\n{scenePath}\n\nDo you want to add it to the Build Settings?", // Message text
                    "Yes, add it",                                                  // Confirm button label
                    "No, skip it"                                                   // Cancel button label
                );

                // If user confirms, proceed to add it to the build list
                if (addScene)
                    AddSceneToBuild(scenePath);

                // Register this path as known, so it won't prompt again
                knownScenes.Add(scenePath);
            }
        }

        // Remove paths that no longer exist (e.g., scenes deleted)
        knownScenes.RemoveWhere(p => !File.Exists(p));
    }

    // Adds a given scene path to the Build Settings, avoiding duplicates
    private static void AddSceneToBuild(string scenePath)
    {
        // Get current list of build settings scenes
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Check if this scene already exists in the build list
        bool alreadyPresent = scenes.Any(s => s.path == scenePath);

        // If not present, append it
        if (!alreadyPresent)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true)); // true = enabled for build
            EditorBuildSettings.scenes = scenes.ToArray();             // Apply updated list
            Debug.Log($"[AutoAddScenePrompt] Added new scene to build settings: {scenePath}");
        }
        else
        {
            Debug.Log($"[AutoAddScenePrompt] Scene already exists in build settings: {scenePath}");
        }
    }
    #endregion
}
