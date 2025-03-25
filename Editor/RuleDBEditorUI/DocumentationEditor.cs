using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(FMDocumentation))]
public class DocumentationEditor : Editor 
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Parse Documentation"))
        {
            FMDocumentation fmDoc = (FMDocumentation)target;
            if (fmDoc != null)
            {
                ProblemReporting problem = fmDoc.CreateDocumentations();
                if (problem != null && problem.ContainsError() || problem.ContainsWarning())
                {
                    string printProblem = problem.ToString();
                    EditorUtility.DisplayDialog("Warning", printProblem, "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("OK", "Documentation Parsed without issues", "OK");
                }
                EditorUtility.SetDirty(fmDoc);
                AssetDatabase.SaveAssetIfDirty(fmDoc);
            }
        }
    }
}
