using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Soup.Editor.ScriptTemplates
{
    public enum SoupScriptKind
    {
        Node,
        NodeView,
        NodeInspector,
    }

    /// <summary>
    /// Fills in the .cs.txt templates under Resources/Soup/ScriptTemplates. Tokens: #SCRIPTNAME# is the class name (the
    /// file name), #DISPLAYNAME# its display name, #NODETYPE# the full name of the node class a view or inspector
    /// registers for. The lines between #ROOTNAMESPACEBEGIN# and #ROOTNAMESPACEEND# go in the root namespace of the
    /// assembly the file lands in.
    /// </summary>
    public static class SoupScriptTemplates
    {
        private const string RESOURCES_FOLDER = "Soup/ScriptTemplates/";
        private const string ROOT_NAMESPACE_BEGIN = "#ROOTNAMESPACEBEGIN#";
        private const string ROOT_NAMESPACE_END = "#ROOTNAMESPACEEND#";
        private const string INDENT = "    ";

        private static readonly Regex IDENTIFIER_REGEX = new("^[A-Za-z_][A-Za-z0-9_]*$");

        public static bool IsValidClassName(string name)
        {
            return !string.IsNullOrEmpty(name) && IDENTIFIER_REGEX.IsMatch(name);
        }

        /// <summary>Null when generated code can name the node type in a typeof(...), otherwise why it can't.</summary>
        public static string GetUnsupportedNodeTypeReason(Type nodeType)
        {
            if (nodeType == null)
            {
                return "unknown node type";
            }
            if (nodeType.IsGenericType)
            {
                return "generic node type";
            }
            if (!nodeType.IsVisible)
            {
                return "node type isn't public";
            }

            return null;
        }

        public static bool TryRender(SoupScriptKind kind, string scriptPath, Type nodeType, out string content, out string error)
        {
            content = null;
            string templateName = GetTemplateName(kind);
            TextAsset template = Resources.Load<TextAsset>(RESOURCES_FOLDER + templateName);
            if (template == null)
            {
                error = $"The script template '{RESOURCES_FOLDER}{templateName}' is missing.";
                return false;
            }

            if (kind != SoupScriptKind.Node)
            {
                string reason = GetUnsupportedNodeTypeReason(nodeType);
                if (reason != null)
                {
                    error = $"Can't generate a script for '{nodeType?.Name}': {reason}.";
                    return false;
                }
            }

            string className = Path.GetFileNameWithoutExtension(scriptPath);
            string text = template.text.Replace("\r\n", "\n")
                .Replace("#SCRIPTNAME#", className)
                .Replace("#DISPLAYNAME#", ObjectNames.NicifyVariableName(className))
                .Replace("#NODETYPE#", nodeType != null ? GetFullTypeName(nodeType) : string.Empty);

            List<string> lines = new(text.Split('\n'));
            WrapInNamespace(lines, CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(scriptPath));

            content = string.Join(GetLineEnding(), lines);
            error = null;
            return true;
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

        /// <summary>
        /// The node type's full name, so it can't clash with a type that the template's usings bring in, such as a node
        /// named Image and UnityEngine.UIElements.Image. Nested types are joined with '.', as C# names them, rather than
        /// the '+' in Type.FullName.
        /// </summary>
        private static string GetFullTypeName(Type type)
        {
            return type.FullName.Replace('+', '.');
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

        private static string GetLineEnding()
        {
            switch (EditorSettings.lineEndingsForNewScripts)
            {
                case LineEndingsMode.Windows:
                    return "\r\n";
                case LineEndingsMode.Unix:
                    return "\n";
                default:
                    return Environment.NewLine;
            }
        }
    }
}
