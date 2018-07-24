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
                    var lineBeginPos = content.LastIndexOf('\n', m.Index);
                    if (lineBeginPos >= 0 && content.Substring(lineBeginPos, m.Index - lineBeginPos).Trim().StartsWith("//"))
                    {
                        pos = m.Index + m.Length;
                        continue;
                    }

                    var start = content.IndexOf('{', m.Index + m.Length);
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

                        Info = ParseDocComment(BackTrackComments(content, m.Index)),
                        Functions = ParseMembers(inner, m.Groups[6].Value).ToArray()
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

        protected List<Interchangeable.Function> ParseMembers(string content, string dtypeName)
        {
            var pos = 0;
            var members = new List<Interchangeable.Function>();

            var func = new Interchangeable.Function();
            var modifiers = new List<string>();

            while (true)
            {
                var nextWord = getNextWord();
                if (nextWord != null)
                {
                    Console.WriteLine("nextWord: " + nextWord);

                    switch (nextWord)
                    {
                        case "private":
                        case "protected":
                        case "internal":
                        case "public":
                            func.AccessLevel = nextWord;
                            break;

                        case "sealed":
                        case "abstract":
                        case "override":
                        case "new":
                        case "readonly":
                        case "static":
                            modifiers.Add(nextWord);
                            break;

                        default:
                            /* if nextWord is the constructor */
                            if (nextWord == dtypeName)
                            {
                                func.ReturnTypeUnparsed = "this";
                                func.Name = nextWord;
                            }
                            else
                            {
                                /* nextWord is not a keyword. This means, it is eather the return type or the name of the member */
                                if (func.ReturnTypeUnparsed == null)
                                {
                                    func.ReturnTypeUnparsed = nextWord;
                                }
                                else
                                {
                                    func.Name = nextWord;
                                }
                            }
                            break;
                    }

                    if (func.ReturnTypeUnparsed != null && func.Name != null)
                    {
                        if (func.AccessLevel == null) func.AccessLevel = "private";
                        func.Modifiers = modifiers.ToArray();

                        var followingChar = content.Substring(pos).Trim()[0];
                        if (followingChar == ';' || followingChar == '=')
                        {
                            // this is a private variable
                            // TODO
                            clear();
                            continue;
                        }
                        else if (followingChar == '(')
                        {
                            var paramListEnd = FindClosingScope(content, pos);
                            var paramList = content.Substring(pos + 1, paramListEnd - pos - 1);
                            func.Parameters = ParseParamList(paramList).ToArray();
                            
                            members.Add(func);
                            clear();
                            pos = paramListEnd + 1;

                            var body = content.Substring(pos);
                            if (body.Trim()[0] == '{')
                            {
                                var bodyBegin = content.IndexOf('{', pos);
                                var bodyEnd = FindClosingScope(content, bodyBegin);
                                pos = bodyEnd + 1;
                            }
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            return members;


            /* --------- Local Helper Functions -------- */
            string getNextWord()
            {
                Match m;

                while (true)
                {
                    var s = content.Substring(pos);
                    m = new Regex(@"(\s|[^<>\w.\[\]])").Match(s);
                    if (m.Success)
                    {
                        var word = s.Substring(0, m.Index);

                        if (word.Length == 0)
                        {
                            pos = SkipNoCode(content, pos + 1);
                            continue;
                        }

                        pos += m.Index;
                        pos = SkipNoCode(content, pos);
                        return word;
                    }
                    return null;
                }
            }

            void clear()
            {
                func = new Interchangeable.Function();
                modifiers.Clear();
            }
        }

        protected List<Interchangeable.FunctionParameter> ParseParamList(string s)
        {
            return new List<Interchangeable.FunctionParameter>();
        }

        protected string BackTrackComments(string content, int pos)
        {
            var linesBefore = content.Substring(0, pos).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Reverse();
            var comment = "";

            foreach (var l in linesBefore)
            {
                if (l.StartsWith("[") && l.EndsWith("]")) continue; // Attribute
                if (l.StartsWith("///")) comment = l.Substring(3).Trim() + "\n" + comment;
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

        /// <summary>
        /// Finds the closing bracket for the opening bracket at the given parameter <see cref="pos"/>
        /// </summary>
        /// <param name="s">The search string</param>
        /// <param name="pos">The position of the opening bracket</param>
        /// <returns>The position of the closing bracket in the given string <see cref="s"/> or -1 if no closing bracket is found or there is no opening bracket at the given position</returns>
        /// 
        /// TODO: not yet able to scan @ and $ prefixed strings properly
        protected int FindClosingScope(string s, int pos)
        {
            var opening = s[pos];
            char closing;
            int level = 0;

            switch (opening)
            {
                case '{': closing = '}'; break;
                case '(': closing = ')'; break;
                default: return -1;
            }

            for (; pos < s.Length; ++pos)
            {
                pos = SkipNoCode(s, pos);
                if (pos >= s.Length) break;

                if (s[pos] == opening) level++;
                else if (s[pos] == closing) level--;
                if (level == 0) return pos;
            }

            return -1;
        }

        protected int SkipNoCode(string s, int pos)
        {
            var codeModeStack = new Stack<CodeMode>();
            codeModeStack.Push(CodeMode.Code);

            pos--;

            while (true)
            {
                pos++;
                var subPos = s.Substring(pos);

                /* single line comment */
                if (isInCode() && subPos.StartsWith("//"))
                {
                    codeModeStack.Push(CodeMode.CommentSingleLine);
                    pos++;
                }
                else if (codeModeStack.Peek() == CodeMode.CommentSingleLine && subPos.StartsWith("\n"))
                    codeModeStack.Pop();
                /* multi line comment */
                else if (isInCode() && subPos.StartsWith("/*"))
                {
                    codeModeStack.Push(CodeMode.CommentMultiLine);
                    pos++;
                }
                else if (codeModeStack.Peek() == CodeMode.CommentMultiLine && subPos.StartsWith("*/"))
                {
                    codeModeStack.Pop();
                    pos++;
                }
                /* string literal */
                else if (!isInComment() && subPos.StartsWith("\""))
                {
                    /* if we are currently NOT inside a string or character literal */
                    if (codeModeStack.Peek() != CodeMode.StringLiteral && codeModeStack.Peek() != CodeMode.CharacterLiteral)
                        codeModeStack.Push(CodeMode.StringLiteral);
                    /* if we are currently inside a string literal */
                    else if (codeModeStack.Peek() == CodeMode.StringLiteral && pos > 1)
                    {
                        /* if there is no escape symbol before the string opening, close the string literal */
                        if (s[pos - 1] != '\\' || s[pos - 2] == '\\') codeModeStack.Pop();
                    }
                }
                else if (!isInComment() && subPos.StartsWith("'"))
                {
                    /* if we are currently NOT inside a string or character literal */
                    if (codeModeStack.Peek() != CodeMode.StringLiteral && codeModeStack.Peek() != CodeMode.CharacterLiteral)
                        codeModeStack.Push(CodeMode.CharacterLiteral);
                    /* if we are currently inside a character literal */
                    else if (codeModeStack.Peek() == CodeMode.CharacterLiteral && pos > 1)
                    {
                        /* if there is no escape symbol before the character opening, close the character literal */
                        if (s[pos - 1] != '\\' || s[pos - 2] == '\\') codeModeStack.Pop();
                    }
                }

                if (isInCode())
                    break;
            }

            return pos;

            /*-------------- Code Mode Helper Functions -------------*/
            bool isInComment()
            {
                return codeModeStack.Peek() == CodeMode.CommentMultiLine || codeModeStack.Peek() == CodeMode.CommentSingleLine;
            }
            bool isInCode()
            {
                return codeModeStack.Peek() == CodeMode.Code;
            }
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
                    c.Namespace = ns;
                    if (Interchangeable.Class.ByName.ContainsKey(dt.ToString()))
                        throw new ApplicationException($"Duplicate type names: { dt.ToString() }");
                    Interchangeable.Class.ByName.Add(dt.ToString(), c);
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

        private enum CodeMode
        {
            Code,
            CommentSingleLine,
            CommentMultiLine,
            StringLiteral,
            CharacterLiteral
        }
    }
}
