using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class BatchProcessorWindow : EditorWindow
{
    [SerializeField]
    private List<GameObject> sourceObjects = new List<GameObject>();
    [SerializeField]
    private List<GameObject> targetObjects = new List<GameObject>();

    private SerializedObject serializedObject;
    private SerializedProperty sourceObjectsProperty;
    private SerializedProperty targetObjectsProperty;

    private Vector2 scrollPosition;

    // --- 多语言支持 ---
    private enum DisplayLanguage { Chinese, English, Japanese, Korean }
    private DisplayLanguage currentLanguage;
    private static Dictionary<DisplayLanguage, UITexts> allTexts;
    private UITexts currentTexts;

    private struct UITexts
    {
        public string WindowTitle;
        public string SourceObjectHeader;
        public string SourceObjectLabel;
        public string TargetObjectHeader;
        public string TargetObjectLabel;
        public string ApplyButton;
        public string HelpHint;
        public string SuccessTitle;
        public string SuccessMessage;
        public string ErrorTitle;
        public string ErrorSourceMissing;
        public string ErrorTargetMissing;
        public string UndoActionName;
    }
    // --- 结束多语言支持 ---

    [MenuItem("Tools/Batch Processor")]
    public static void ShowWindow()
    {
        BatchProcessorWindow window = GetWindow<BatchProcessorWindow>();
        window.titleContent = new GUIContent("Batch Processor");
    }

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        sourceObjectsProperty = serializedObject.FindProperty("sourceObjects");
        targetObjectsProperty = serializedObject.FindProperty("targetObjects");

        InitializeLanguages();
        currentLanguage = (DisplayLanguage)EditorPrefs.GetInt("BatchProcessor_Language", (int)DisplayLanguage.English);
        LoadLanguageTexts(currentLanguage);
    }

    private void OnGUI()
    {
        serializedObject.Update();

        DrawLanguageButtons();

        EditorGUILayout.Space(10);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField(currentTexts.SourceObjectLabel, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sourceObjectsProperty, new GUIContent(currentTexts.SourceObjectHeader), true);

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField(currentTexts.TargetObjectLabel, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetObjectsProperty, new GUIContent(currentTexts.TargetObjectHeader), true);

        EditorGUILayout.Space(40);

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button(currentTexts.ApplyButton, GUILayout.Height(40)))
        {
            ApplyChanges();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(currentTexts.HelpHint, MessageType.Info);
        
        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    private void ApplyChanges()
    {
        if (sourceObjects.Count == 0 || sourceObjects.Find(s => s != null) == null)
        {
            EditorUtility.DisplayDialog(currentTexts.ErrorTitle, currentTexts.ErrorSourceMissing, "OK");
            return;
        }

        if (targetObjects.Count == 0 || targetObjects.Find(t => t != null) == null)
        {
            EditorUtility.DisplayDialog(currentTexts.ErrorTitle, currentTexts.ErrorTargetMissing, "OK");
            return;
        }

        int sourceCount = 0;
        int targetCount = 0;

        Undo.SetCurrentGroupName(currentTexts.UndoActionName);
        int group = Undo.GetCurrentGroup();

        foreach (var target in targetObjects)
        {
            if (target == null) continue;
            targetCount++;
            Undo.RecordObject(target.transform, "Modify Parent");

            sourceCount = 0;
            foreach (var source in sourceObjects)
            {
                if (source == null) continue;
                sourceCount++;

                // [重要改动] 开始智能实例化逻辑
                GameObject prefabAsset = null;

                // 检查源物体是否是一个预制件资产（从项目窗口拖入）或实例（从场景拖入）
                if (PrefabUtility.IsPartOfPrefabAsset(source))
                {
                    // 情况1: 源是项目文件夹中的预制件资产
                    prefabAsset = source;
                }
                else if (PrefabUtility.IsPartOfAnyPrefab(source))
                {
                    // 情况2: 源是场景中的一个预制件实例，我们需要找到它对应的原始资产
                    prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(source);
                }

                GameObject newInstance;
                if (prefabAsset != null)
                {
                    // 如果我们成功找到了预制件资产，就用它来实例化，以保持链接
                    newInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, target.transform);
                }
                else
                {
                    // 情况3: 源是一个普通的、非预制件的游戏物体，直接克隆它
                    newInstance = Instantiate(source, target.transform);
                }
                // [结束重要改动]
                
                newInstance.name = source.name;
                Undo.RegisterCreatedObjectUndo(newInstance, "Create " + newInstance.name);
            }
        }
        
        Undo.CollapseUndoOperations(group);

        string successMsg = string.Format(currentTexts.SuccessMessage, sourceCount, targetCount);
        EditorUtility.DisplayDialog(currentTexts.SuccessTitle, successMsg, "OK");

        Debug.Log(successMsg);
    }

    #region Language Methods

    private void DrawLanguageButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("中文")) SwitchLanguage(DisplayLanguage.Chinese);
        if (GUILayout.Button("English")) SwitchLanguage(DisplayLanguage.English);
        if (GUILayout.Button("日本語")) SwitchLanguage(DisplayLanguage.Japanese);
        if (GUILayout.Button("한국어")) SwitchLanguage(DisplayLanguage.Korean);
        EditorGUILayout.EndHorizontal();
    }
    
    private void SwitchLanguage(DisplayLanguage lang)
    {
        if (currentLanguage != lang)
        {
            LoadLanguageTexts(lang);
            EditorPrefs.SetInt("BatchProcessor_Language", (int)lang);
        }
    }

    private void LoadLanguageTexts(DisplayLanguage lang)
    {
        currentLanguage = lang;
        currentTexts = allTexts[lang];
        this.titleContent = new GUIContent(currentTexts.WindowTitle);
    }

    private static void InitializeLanguages()
    {
        if (allTexts != null) return;
        allTexts = new Dictionary<DisplayLanguage, UITexts>();
        // ... (语言文本部分未变，保持原样) ...
        // English
        allTexts[DisplayLanguage.English] = new UITexts
        {
            WindowTitle = "Batch Processor",
            SourceObjectHeader = "Source Objects",
            SourceObjectLabel = "1. Add objects to be copied (Prefabs or scene objects) to the list below:",
            TargetObjectHeader = "Target Objects",
            TargetObjectLabel = "2. Add target objects to the list below. Source objects will be copied as their children:",
            ApplyButton = "Apply",
            HelpHint = "Tip: All operations support standard Undo (Ctrl+Z or Cmd+Z).",
            SuccessTitle = "Success",
            SuccessMessage = "Successfully added {0} source object(s) to {1} target object(s).",
            ErrorTitle = "Error",
            ErrorSourceMissing = "Please specify at least one source object!",
            ErrorTargetMissing = "Please specify at least one target object!",
            UndoActionName = "Batch Add Children"
        };
        // Chinese
        allTexts[DisplayLanguage.Chinese] = new UITexts
        {
            WindowTitle = "批量处理工具",
            SourceObjectHeader = "源物体",
            SourceObjectLabel = "1. 将需要被复制的物体 (预制件或场景物体) 添加到下方列表：",
            TargetObjectHeader = "目标物体",
            TargetObjectLabel = "2. 将目标物体添加到下方列表，源物体将被复制并成为其子物体：",
            ApplyButton = "应用",
            HelpHint = "提示：所有操作都支持标准的撤销功能 (Ctrl+Z 或 Cmd+Z)。",
            SuccessTitle = "成功",
            SuccessMessage = "成功为 {1} 个目标物体分别添加了 {0} 个源物体。",
            ErrorTitle = "错误",
            ErrorSourceMissing = "请至少指定一个源物体！",
            ErrorTargetMissing = "请至少指定一个目标物体！",
            UndoActionName = "批量添加子物体"
        };
        // Japanese
        allTexts[DisplayLanguage.Japanese] = new UITexts
        {
            WindowTitle = "バッチ処理ツール",
            SourceObjectHeader = "ソースオブジェクト",
            SourceObjectLabel = "1. コピーするオブジェクト（プレハブまたはシーンオブジェクト）を下のリストに追加します：",
            TargetObjectHeader = "ターゲットオブジェクト",
            TargetObjectLabel = "2. ターゲットオブジェクトを下のリストに追加します。ソースオブジェクトがその子としてコピーされます：",
            ApplyButton = "適用",
            HelpHint = "ヒント：すべての操作は標準の元に戻す（Ctrl+ZまたはCmd+Z）をサポートしています。",
            SuccessTitle = "成功",
            SuccessMessage = "{1}個のターゲットオブジェクトに{0}個のソースオブジェクトを正常に追加しました。",
            ErrorTitle = "エラー",
            ErrorSourceMissing = "少なくとも1つのソースオブジェクトを指定してください！",
            ErrorTargetMissing = "少なくとも1つのターゲットオブジェクトを指定してください！",
            UndoActionName = "子オブジェクトのバッチ追加"
        };
        // Korean
        allTexts[DisplayLanguage.Korean] = new UITexts
        {
            WindowTitle = "일괄 처리 도구",
            SourceObjectHeader = "소스 오브젝트",
            SourceObjectLabel = "1. 복사할 오브젝트(프리팹 또는 씬 오브젝트)를 아래 목록에 추가하십시오:",
            TargetObjectHeader = "타겟 오브젝트",
            TargetObjectLabel = "2. 타겟 오브젝트를 아래 목록에 추가하십시오. 소스 오브젝트가 자식으로 복사됩니다:",
            ApplyButton = "적용",
            HelpHint = "팁: 모든 작업은 표준 실행 취소(Ctrl+Z 또는 Cmd+Z)를 지원합니다.",
            SuccessTitle = "성공",
            SuccessMessage = "{1}개의 타겟 오브젝트에 {0}개의 소스 오브젝트를 성공적으로 추가했습니다.",
            ErrorTitle = "오류",
            ErrorSourceMissing = "하나 이상의 소스 오브젝트를 지정하십시오!",
            ErrorTargetMissing = "하나 이상의 타겟 오브젝트를 지정하십시오!",
            UndoActionName = "자식 오브젝트 일괄 추가"
        };
    }
    #endregion
}
