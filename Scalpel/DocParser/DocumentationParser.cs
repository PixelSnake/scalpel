using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Scalpel.DocParser
{
    /// <summary>
    /// Parses the documentation comments out of the source code
    /// and assigns them to the corresponding classes/functions/etc.
    /// </summary>
    public class DocumentationParser
    {
        protected string InDir;
        protected string[] Filetypes;

        protected static readonly string rxDocComment = @"\/\/\/(.*?)\s*(\n\r?)\s*";
        protected static readonly string rxNamespace = @"namespace\s+(([\w]+)(((\s*\.\s*[\w]+)+)?))\s*";
        protected static readonly string rxClass = @"(public|protected|internal|private)?(\s*(abstract|sealed))?\s+(class)\s+(([\w]+)(\s*<(\s*([\w]+)(\s*,\s*[\w]+)*\s*)>)?)(\s*:((\s*[\w\.]+)(\s*,\s*[\w\.]+)*)?)?\s*";
        protected static readonly string rxFunctionDefinition = @"([\w]+)\s*(<\s*(([\w\.]+)(\s*,\s*[\w\.]+)*)\s*>)?\s*\(";
        protected static readonly string rxLine = @"(.*?)\n\r?";

        public DocumentationParser(string inDir, string[] filetypes)
        {
            InDir = inDir;
            Filetypes = filetypes;
        }

        /// <summary>
        /// Parses the documentation of the input directory specified in the constructor
        /// </summary>
        /// <returns>The parsed documentation in the form of an <see cref="Interchangeable.Documentation"/> instance</returns>
        public Interchangeable.Documentation Parse()
        {
            var namespaces = SearchDirectory(InDir);

            namespaces = MergeNamespaces(namespaces);
            foreach (var ns in namespaces)
                FinalizeDatatypes(ns);
            namespaces = HierarchyzeNamespaces(namespaces);

            return new Interchangeable.Documentation()
            {
                Namespaces = namespaces.ToArray()
            };
        }

        protected List<Interchangeable.Namespace> SearchDirectory(string dirPath)
        {
            var namespaces = new List<Interchangeable.Namespace>();

            var directories = Directory.GetDirectories(dirPath);
            foreach (var d in directories)
                namespaces.AddRange(SearchDirectory(d));

            var files = Directory.GetFiles(dirPath);
            foreach (var f in files)
            {
                var ext = new FileInfo(f).Extension;
                if (ext.Length < 1 || !Filetypes.Contains(ext.Substring(1))) continue;

                var nsInFile = ParseFile(File.ReadAllText(f));
                namespaces.AddRange(nsInFile);
            }

            return namespaces;
        }

        protected List<Interchangeable.Namespace> ParseFile(string content)
        {
            int pos = 0;
            Match m;
            var regexNamespace = new Regex(rxNamespace);
            List<Interchangeable.Namespace> namespaces = new List<Interchangeable.Namespace>();

            while (pos < content.Length)
            {
                if ((m = regexNamespace.Match(content, pos)).Success)
                {
                    var start = m.Index + m.Length;
                    var end = FindClosingScope(content, start);

                    var inner = content.Substring(start + 1, end - start - 1);

                    namespaces.Add(new Interchangeable.Namespace()
                    {
                        Name = m.Groups[1].Value,
                        Datatypes = FindDatatypes(inner).ToArray()
                    });

                    pos = end + 1;
                }
                else
                {
                    break;
                }
            }

            var globalDtypes = FindDatatypes(content.Substring(pos)).ToArray();
            if (globalDtypes.Length > 0)
            {
                namespaces.Add(new Interchangeable.Namespace()
                {
                    Name = "",
                    Datatypes = globalDtypes
                });
            }
            
            return namespaces;
        }

        protected List<Interchangeable.Datatype> FindDatatypes(string content)
        {
            int pos = 0;
            Match m;
            var regexClass = new Regex(rxClass);
            List<Interchangeable.Datatype> dtypes = new List<Interchangeable.Datatype>();

            while (pos < content.Length)
            {
                if ((m = regexClass.Match(content, pos)).Success)
                {
                    var start = m.Index + m.Length;
                    var end = FindClosingScope(content, start);

                    var inner = content.Substring(start + 1, end - start - 1);

                    dtypes.Add(new Interchangeable.Class()
                    {
                        AccessLevel = TrimToNullIfEmpty(m.Groups[1].Value) ?? "private",
                        Modifier = TrimToNullIfEmpty(m.Groups[3].Value),
                        Name = m.Groups[6].Value,
                        TypeParams = MakeEmptyIfFistElementIsZeroLengthString(
                                m.Groups[8].Value.Split(',').Select(p => p.Trim()).ToArray()
                            ),
                        BaseClasses = MakeEmptyIfFistElementIsZeroLengthString(
                                m.Groups[12].Value.Split(',').Select(p => p.Trim()).ToArray()
                            ),

                        Info = ParseDocComment(BackTrackComments(content, m.Index))
                    });

                    pos = end + 1;
                }
                else
                {
                    break;
                }
            }

            return dtypes;
        }

        protected List<Interchangeable.Function> ParseMembers(string content)
        {
            // todo
            return new List<Interchangeable.Function>();
        }

        protected string BackTrackComments(string content, int pos)
        {
            var linesBefore = content.Substring(0, pos).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Reverse();
            var comment = "";

            foreach (var l in linesBefore)
            {
                if (l.StartsWith("[") && l.EndsWith("]")) continue; // Attribute
                if (l.StartsWith("///")) comment = l.Substring(3).Trim() + comment;
                else break;
            }

            return comment;
        }

        protected string TrimToNullIfEmpty(string s)
        {
            return s.Trim().Length < 1 ? null : s.Trim();
        }

        protected string[] MakeEmptyIfFistElementIsZeroLengthString(string[] arr)
        {
            if (arr.Length < 1) return arr;
            if (arr.Length == 1 && TrimToNullIfEmpty(arr[0]) == null) return new string[] { };
            return arr;
        }

        protected int FindClosingScope(string s, int pos)
        {
            var opening = s[pos];
            char closing;
            int level = 0;

            switch (opening)
            {
                case '{': closing = '}'; break;
                default: return -1;
            }

            for (; pos < s.Length; ++pos)
            {
                if (s[pos] == opening) level++;
                else if (s[pos] == closing) level--;
                if (level == 0) return pos;
            }

            return -1;
        }

        protected List<Interchangeable.Namespace> MergeNamespaces(List<Interchangeable.Namespace> namespaces)
        {
            var mergedNamespaces = new Dictionary<string, Interchangeable.Namespace>();

            foreach (var ns in namespaces)
            {
                if (mergedNamespaces.ContainsKey(ns.Name))
                {
                    var _ns = mergedNamespaces[ns.Name];
                    _ns.Datatypes = _ns.Datatypes.Concat(ns.Datatypes).ToArray();
                }
                else
                {
                    mergedNamespaces.Add(ns.Name, ns);
                }
            }

            return mergedNamespaces.Values.ToList();
        }

        protected List<Interchangeable.Namespace> HierarchyzeNamespaces(List<Interchangeable.Namespace> namespaces)
        {
            // todo
            return namespaces;
        }

        protected void FinalizeDatatypes(Interchangeable.Namespace ns)
        {
            foreach (var dt in ns.Datatypes)
            {
                if (dt is Interchangeable.Class)
                {
                    var c = dt as Interchangeable.Class;
                    var absoluteName = $"{ ns.Name }.{ c.Name }";
                    if (Interchangeable.Class.ByName.ContainsKey(absoluteName))
                        throw new ApplicationException($"Duplicate type names: { absoluteName }");
                    Interchangeable.Class.ByName.Add(absoluteName, c);
                    c.Namespace = ns;
                }
            }

            foreach (var c in ns.Datatypes)
            {
                if (c.Info.UnparsedSummary == null) continue;
                c.Info.Summary = ScalpelPlugin.Syntax.FormattedText.Parse(c.Info.UnparsedSummary);
                c.Info.UnparsedSummary = null;

                if (c is Interchangeable.Class)
                {
                    var _class = c as Interchangeable.Class;
                    if (_class.Functions == null) continue;
                    foreach (var f in (c as Interchangeable.Class).Functions)
                    {
                        if (f.Info.UnparsedSummary == null) continue;
                        f.Info.Summary = ScalpelPlugin.Syntax.FormattedText.Parse(f.Info.UnparsedSummary);
                        f.Info.UnparsedSummary = null;
                    }
                }
            }
        }

        protected Interchangeable.DocumentationInfo ParseDocComment(string comment)
        {
            var doc = new XmlDocument();
            doc.LoadXml($"<doc>{ comment }</doc>");
            return ParseDocComment(doc);
        }
        protected Interchangeable.DocumentationInfo ParseDocComment(XmlDocument xml)
        {
            var typeParamDescr = new Dictionary<string, string>();
            foreach (XmlElement tp in xml.GetElementsByTagName("typeparam"))
            {
                var name = tp.GetAttribute("name");
                typeParamDescr.Add(name, tp.InnerText);
            }

            return new Interchangeable.DocumentationInfo()
            {
                UnparsedSummary = xml["doc"]["summary"],
                Author = xml["doc"]["author"]?.InnerText.Trim(),

                TypeParamDescription = typeParamDescr
            };
        }
    }
}

namespace test.hallo
{

}