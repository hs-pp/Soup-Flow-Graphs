using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Soup.Editor.Views.Inspector;
using Soup.Editor.Views.Nodes;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.ScriptTemplates
{
    public static class SoupScriptCreator
    {
        private enum SoupScriptKind
        {
            Node,
            NodeView,
            NodeInspector,
        }

        private const string RESOURCES_FOLDER = "Soup/ScriptTemplates/";
        private const string ROOT_NAMESPACE_BEGIN = "#ROOTNAMESPACEBEGIN#";
        private const string ROOT_NAMESPACE_END = "#ROOTNAMESPACEEND#";
        private const string INDENT = "    ";
        private const string SCRIPTS_MENU = "Scripts/";

        [MenuItem("Assets/Create/Soup/Node Script", false, 1)]
        private static void CreateNodeScriptMenuItem()
        {
            string templatePath = Path.GetFullPath(AssetDatabase.GetAssetPath(LoadTemplate(SoupScriptKind.Node)));
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewNode.cs");
        }
        
        /// <summary>
        /// Adds the Scripts submenu to a node's context menu.
        /// </summary>
        internal static void AppendNodeScriptMenuItems(DropdownMenu menu, Type nodeType)
        {
            if (nodeType == null)
            {
                return;
            }

            menu.AppendAction(SCRIPTS_MENU + "Open Node Script", _ => OpenScript(nodeType));
            menu.AppendSeparator(SCRIPTS_MENU);
            AppendEditorScriptItems(menu, nodeType, SoupScriptKind.NodeView, EditorTypesFactory.HasOwnNodeView(nodeType), EditorTypesFactory.GetNodeViewType(nodeType), typeof(DefaultNodeView));
            AppendEditorScriptItems(menu, nodeType, SoupScriptKind.NodeInspector, EditorTypesFactory.HasOwnNodeInspector(nodeType), EditorTypesFactory.GetNodeInspectorType(nodeType), typeof(DefaultNodeInspector));
        }

        private static void AppendEditorScriptItems(DropdownMenu menu, Type nodeType, SoupScriptKind kind, bool hasOwn, Type currentType, Type defaultType)
        {
            string label = GetLabel(kind);
            if (hasOwn)
            {
                menu.AppendAction($"{SCRIPTS_MENU}Open {label} Script", _ => OpenScript(currentType));
                return;
            }

            string text = $"{SCRIPTS_MENU}Create {label} Script";
            if (currentType != defaultType)
            {
                text += $" (currently {currentType.Name})";
            }
            menu.AppendAction(text + "...", _ => CreateEditorScript(kind, nodeType));
        }

        private static void CreateEditorScript(SoupScriptKind kind, Type nodeType)
        {
            string label = GetLabel(kind);
            string path = EditorUtility.SaveFilePanelInProject($"Create Node {label} Script", nodeType.Name + label, "cs", $"Select a folder.", "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string className = Path.GetFileNameWithoutExtension(path).Replace(" ", string.Empty);
            string scriptPath = $"{GetFolder(path)}/{className}.cs";
            if (File.Exists(scriptPath))
            {
                EditorUtility.DisplayDialog($"Can't Create {label} Script", $"{scriptPath} already exists.", "OK");
                return;
            }

            File.WriteAllText(scriptPath, BuildScript(kind, scriptPath, nodeType), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(scriptPath);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            ProjectWindowUtil.ShowCreatedAsset(script);
            AssetDatabase.OpenAsset(script);
        }

        private static string GetLabel(SoupScriptKind kind)
        {
            return kind == SoupScriptKind.NodeView ? "View" : "Inspector";
        }

        private static string GetFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        }

        private static void OpenScript(Type type)
        {
            MonoScript script = FindScript(type);
            if (script == null)
            {
                Debug.LogWarning($"[Soup] Couldn't find the script that defines '{type.Name}'. Unity only finds a class in a file of the same name.");
                return;
            }

            AssetDatabase.OpenAsset(script);
        }

        private static MonoScript FindScript(Type type)
        {
            Type outermost = type;
            while (outermost.DeclaringType != null)
            {
                outermost = outermost.DeclaringType;
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:MonoScript {outermost.Name}"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == outermost)
                {
                    return script;
                }
            }

            return null;
        }
        
        private static TextAsset LoadTemplate(SoupScriptKind kind)
        {
            return Resources.Load<TextAsset>(RESOURCES_FOLDER + GetTemplateName(kind));
        }

        private static string GetTemplateName(SoupScriptKind kind)
        {
            switch (kind)
            {
                case SoupScriptKind.Node:
                    return "NodeTemplate.cs";
                case SoupScriptKind.NodeView:
                    return "NodeViewTemplate.cs";
                case SoupScriptKind.NodeInspector:
                    return "NodeInspectorTemplate.cs";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
        
        private static string BuildScript(SoupScriptKind kind, string scriptPath, Type nodeType)
        {
            string text = LoadTemplate(kind).text
                .Replace("\r\n", "\n")
                .Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(scriptPath))
                .Replace("#NODETYPE#", nodeType.FullName);

            List<string> lines = new(text.Split('\n'));
            WrapInNamespace(lines, CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(scriptPath));
            return string.Join("\n", lines);
        }

        private static void WrapInNamespace(List<string> lines, string rootNamespace)
        {
            int begin = lines.FindIndex(line => line.Trim() == ROOT_NAMESPACE_BEGIN);
            int end = lines.FindIndex(line => line.Trim() == ROOT_NAMESPACE_END);
            if (begin < 0 || end < begin)
            {
                return;
            }

            if (string.IsNullOrEmpty(rootNamespace))
            {
                lines.RemoveAt(end);
                lines.RemoveAt(begin);
                return;
            }

            for (int i = begin + 1; i < end; i++)
            {
                if (lines[i].Length > 0)
                {
                    lines[i] = INDENT + lines[i];
                }
            }

            lines[end] = "}";
            lines[begin] = "{";
            lines.Insert(begin, $"namespace {rootNamespace}");
        }
    }
}
