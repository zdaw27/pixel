using UnityEngine;
using UnityEditor;

public class PixelLevelEditorWindow : EditorWindow
{
    private string levelName = "NewLevel";
    private PixelType selectedType = PixelType.Sand;
    private bool isStartPointsMode = false;
    private bool isGoalPointsMode = false;

    [MenuItem("Pixel Sim/Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<PixelLevelEditorWindow>("Pixel Level Editor");
    }

    void OnGUI()
    {
        GUILayout.Label("Pixel Simulation Level Editor", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Editor features work in Play Mode.", MessageType.Info);
            return;
        }

        PixelInput input = FindObjectOfType<PixelInput>();
        LevelManager levelManager = FindObjectOfType<LevelManager>();

        if (input == null || levelManager == null)
        {
            EditorGUILayout.HelpBox("PixelInput or LevelManager not found in scene.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        GUILayout.Label("Tools", EditorStyles.boldLabel);

        // Brush Selection
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sand")) SetBrush(input, PixelType.Sand);
        if (GUILayout.Button("Stone")) SetBrush(input, PixelType.Stone);
        if (GUILayout.Button("Water")) SetBrush(input, PixelType.Water);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fire")) SetBrush(input, PixelType.Fire);
        if (GUILayout.Button("Gas")) SetBrush(input, PixelType.Gas);
        if (GUILayout.Button("Erase (Empty)")) SetBrush(input, PixelType.Empty);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.Label("Game Objects", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = isStartPointsMode ? Color.green : Color.white;
        if (GUILayout.Button("Set Start Point"))
        {
            SetTool(input, true, false);
        }

        GUI.backgroundColor = isGoalPointsMode ? Color.magenta : Color.white;
        if (GUILayout.Button("Set Goal Point"))
        {
             SetTool(input, false, true);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.Label("Storage", EditorStyles.boldLabel);
        
        levelName = EditorGUILayout.TextField("Level Name", levelName);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Level"))
        {
            levelManager.SaveLevel(levelName);
        }
        if (GUILayout.Button("Load Level"))
        {
            levelManager.LoadLevel(levelName);
        }
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("Clear Grid (R)"))
        {
            PixelSimulation.Instance.ClearGrid();
        }
    }

    void SetBrush(PixelInput input, PixelType type)
    {
        input.SetToolType(type);
        isStartPointsMode = false;
        isGoalPointsMode = false;
        Repaint();
    }

    void SetTool(PixelInput input, bool start, bool goal)
    {
        isStartPointsMode = start;
        isGoalPointsMode = goal;
        
        if (start) input.SetToolStartPoint();
        else if (goal) input.SetToolGoalPoint();
        
        Repaint();
    }
}
