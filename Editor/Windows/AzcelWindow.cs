using System.IO;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.EnvInstaller.Editor.UI;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Azcel.Editor
{
    /// <summary>
    /// Azcel 编辑器窗口
    /// </summary>
    public class AzcelWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private string[] _envIds = new[] {"ExcelDataReader.DataSet", "ExcelDataReader"};

        [MenuItem("Azathrix/Azcel/配置窗口")]
        public static void ShowWindow()
        {
            var window = GetWindow<AzcelWindow>("Azcel");
            window.minSize = new Vector2(400, 300);
        }

        [MenuItem("Azathrix/Azcel/转换配置 &`")]
        public static void ConvertConfig()
        {
            ConvertConfig(true).Forget();
        }

        public static async UniTask ConvertConfig(bool autoRefresh)
        {
            if (!CheckEnvironment(out var message))
            {
                EditorUtility.DisplayDialog("Azcel", message, "确定");
                return;
            }

            await RunConvert(autoRefresh);
        }

        private static async UniTask RunConvert(bool autoRefresh)
        {
            var converter = PipelineFactory.Get<ConfigConverter>();
            if (converter == null)
            {
                EditorUtility.DisplayDialog("Azcel", "未找到 Azcel.Converter 管线，请检查注册表。", "确定");
                return;
            }
            var context = new ConvertContext
            {
                SkipAssetRefresh = !autoRefresh
            };
            await converter.ExecuteAsync(context);
        }

        private void OnGUI()
        {
            if (!EnvDependencyUI.DrawDependencyCheck(_envIds))
                return;

            DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawSettings();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("转换配置", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ConvertConfig();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("设置", EditorStyles.toolbarButton, GUILayout.Width(50)))
                Selection.activeObject = AzcelSettings.Instance;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettings()
        {
            var settings = AzcelSettings.Instance;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Excel目录", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // 多目录列表
            for (int i = 0; i < settings.excelPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                settings.excelPaths[i] = EditorGUILayout.TextField(settings.excelPaths[i]);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var path = EditorUtility.OpenFolderPanel("选择Excel目录", settings.excelPaths[i], "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // 转换为相对路径
                        if (path.StartsWith(Application.dataPath))
                            path = "Assets" + path.Substring(Application.dataPath.Length);
                        settings.excelPaths[i] = path;
                    }
                }

                if (GUILayout.Button("打开", GUILayout.Width(40)))
                {
                    if (Directory.Exists(settings.excelPaths[i]))
                        EditorUtility.RevealInFinder(settings.excelPaths[i]);
                }

                if (settings.excelPaths.Count > 1 && GUILayout.Button("-", GUILayout.Width(25)))
                {
                    settings.excelPaths.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("添加目录", GUILayout.Width(80)))
                settings.excelPaths.Add("Assets/Excel");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输出配置", EditorStyles.boldLabel);

            DrawPathField("代码输出目录", ref settings.codeOutputPath);
            DrawPathField("数据输出目录", ref settings.dataOutputPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("代码生成", EditorStyles.boldLabel);

            settings.codeNamespace = EditorGUILayout.TextField("命名空间", settings.codeNamespace);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("数据格式", EditorStyles.boldLabel);

            var formatIds = ConfigFormatPluginRegistry.GetFormatIdArray();
            if (formatIds.Length == 0)
            {
                EditorGUILayout.HelpBox("未发现可用格式，请确保实现 IConfigFormatPlugin 并可被扫描到。", MessageType.Warning);
            }
            else
            {
                var selectedIndex = 0;
                for (int i = 0; i < formatIds.Length; i++)
                {
                    if (string.Equals(formatIds[i], settings.dataFormatId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                var newIndex = EditorGUILayout.Popup("格式选择", selectedIndex, formatIds);
                settings.dataFormatId = formatIds[newIndex];
            }
            settings.arraySeparator = EditorGUILayout.TextField("数组分隔符", settings.arraySeparator);
            settings.objectSeparator = EditorGUILayout.TextField("对象分隔符", settings.objectSeparator);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("解析默认值", EditorStyles.boldLabel);

            settings.defaultKeyField = EditorGUILayout.TextField("默认主键字段", settings.defaultKeyField);
            settings.defaultKeyType = EditorGUILayout.TextField("默认主键类型", settings.defaultKeyType);
            settings.defaultFieldRow = EditorGUILayout.IntField("默认字段行", settings.defaultFieldRow);
            settings.defaultTypeRow = EditorGUILayout.IntField("默认类型行", settings.defaultTypeRow);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(settings);
        }

        private void DrawPathField(string label, ref string path)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var selected = EditorUtility.OpenFolderPanel(label, path, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                        selected = "Assets" + selected.Substring(Application.dataPath.Length);
                    path = selected;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static bool CheckEnvironment(out string message)
        {
#if AZCEL_EXCEL_READER
            message = string.Empty;
            return true;
#else
            message = "ExcelDataReader 未安装。\n请通过 Azathrix/环境管理器 安装依赖。";
            return false;
#endif
        }
    }
}
