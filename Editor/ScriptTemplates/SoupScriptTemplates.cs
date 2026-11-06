using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// file name), #NAME# its display name, #NODETYPE# the node class a view or inspector registers for. The lines between
    /// #NAMESPACEBEGIN# and #NAMESPACEEND# go in the root namespace of the assembly the file lands in, and the node's
    /// namespace joins the using block when the file can't already see it.
    /// </summary>
    public static class SoupScriptTemplates
    {
        private const string RESOURCES_FOLDER = "Soup/ScriptTemplates/";
        private const string NAMESPACE_BEGIN = "#NAMESPACEBEGIN#";
        private const string NAMESPACE_END = "#NAMESPACEEND#";
        private const string INDENT = "    ";

        private static readonly Regex s_identifier = new("^[A-Za-z_][A-Za-z0-9_]*$");

        public static bool IsValidClassName(string name)
        {
            return !string.IsNullOrEmpty(name) && s_identifier.IsMatch(name);
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
            string rootNamespace = CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(scriptPath) ?? string.Empty;

            string text = template.text.Replace("\r\n", "\n")
                .Replace("#SCRIPTNAME#", className)
                .Replace("#NAME#", ObjectNames.NicifyVariableName(className))
                .Replace("#NODETYPE#", nodeType != null ? GetTypeName(nodeType) : string.Empty);

            List<string> lines = new(text.Split('\n'));
            if (kind != SoupScriptKind.Node)
            {
                AddUsing(lines, nodeType.Namespace, rootNamespace);
            }
            WrapInNamespace(lines, rootNamespace);

            content = string.Join(GetLineEnding(), lines);
            error = null;
            return true;
        }

        public static void WriteScript(string scriptPath, string content)
        {
            File.WriteAllText(scriptPath, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(scriptPath);
        }

        private static string GetTemplateName(SoupScriptKind kind)
        {
            return kind switch
            {
                SoupScriptKind.Node => "NodeTemplate.cs",
                SoupScriptKind.NodeView => "NodeViewTemplate.cs",
                SoupScriptKind.NodeInspector => "NodeInspectorTemplate.cs",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // Nested types are named through their declaring types, as code in the node's namespace would name them.
        private static string GetTypeName(Type type)
        {
            return type.IsNested ? $"{GetTypeName(type.DeclaringType)}.{type.Name}" : type.Name;
        }

        // The template's using block is the run of using lines at its top.
        private static void AddUsing(List<string> lines, string nodeNamespace, string rootNamespace)
        {
            if (string.IsNullOrEmpty(nodeNamespace))
            {
                return;
            }
            // Code in a namespace already sees the types of every namespace enclosing it.
            if (rootNamespace == nodeNamespace || rootNamespace.StartsWith(nodeNamespace + ".", StringComparison.Ordinal))
            {
                return;
            }

            int count = 0;
            while (count < lines.Count && lines[count].StartsWith("using ", StringComparison.Ordinal))
            {
                count++;
            }

            List<string> usings = lines.GetRange(0, count);
            string directive = $"using {nodeNamespace};";
            if (usings.Contains(directive))
            {
                return;
            }

            usings.Add(directive);
            usings.Sort(CompareUsings);
            lines.RemoveRange(0, count);
            lines.InsertRange(0, usings);
            if (count == 0)
            {
                lines.Insert(1, string.Empty);
            }
        }

        // System namespaces first, then the rest in ordinal order, the way the existing Soup files are sorted.
        private static int CompareUsings(string a, string b)
        {
            bool aIsSystem = IsSystemUsing(a);
            bool bIsSystem = IsSystemUsing(b);
            if (aIsSystem != bIsSystem)
            {
                return aIsSystem ? -1 : 1;
            }

            return string.CompareOrdinal(a, b);
        }

        private static bool IsSystemUsing(string directive)
        {
            return directive == "using System;" || directive.StartsWith("using System.", StringComparison.Ordinal);
        }

        private static void WrapInNamespace(List<string> lines, string rootNamespace)
        {
            int begin = lines.FindIndex(line => line.Trim() == NAMESPACE_BEGIN);
            int end = lines.FindIndex(line => line.Trim() == NAMESPACE_END);
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
            return EditorSettings.lineEndingsForNewScripts switch
            {
                LineEndingsMode.Windows => "\r\n",
                LineEndingsMode.Unix => "\n",
                _ => Environment.NewLine,
            };
        }
    }
}
