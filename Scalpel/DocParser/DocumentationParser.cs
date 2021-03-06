﻿using System;
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

            foreach (var ns in namespaces)
            {
                Console.WriteLine(ns.Name);
                foreach (var dt in ns.Datatypes)
                {
                    Console.WriteLine("\t" + dt);
                    if (dt is Interchangeable.Class)
                    {
                        var c = dt as Interchangeable.Class;
                        foreach (var f in c.Functions)
                            Console.WriteLine("\t\t" + f);
                    }
                }
            }

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
                        Functions = ParseMembers(inner, m.Groups[6].Value)
                            .Where(mem => mem is Interchangeable.Function)
                            .Cast<Interchangeable.Function>()
                            .ToArray()
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

        protected List<Interchangeable.ClassMember> ParseMembers(string content, string dtypeName)
        {
            var pos = 0;
            var members = new List<Interchangeable.ClassMember>();

            while (true)
            {
                var member = ParseNextMember(content, dtypeName, pos, out pos);
                if (member == null) break;

                var followingChar = content.Substring(pos).TrimStart()[0];
                if (followingChar == ';')
                {
                    // this member is a variable
                    // TODO
                    continue;
                }
                else if (followingChar == '=')
                {
                    var endOfDeclaration = content.IndexOf(';', pos);
                    pos = endOfDeclaration > 0 ? endOfDeclaration : pos;
                }
                else if (followingChar == ',')
                {
                    var endOfDeclaration = content.IndexOf(';', pos);
                    pos = endOfDeclaration > 0 ? endOfDeclaration : pos;
                }
                else if (member != null && followingChar == '(')
                {
                    var function = new Interchangeable.Function()
                    {
                        Name = member.Name,
                        AccessLevel = member.AccessLevel,
                        Modifiers = member.Modifiers.Select(m => m.Trim()).ToArray(),
                        TypeUnparsed = member.TypeUnparsed.Trim()
                    };

                    pos = content.IndexOf('(', pos);
                    var paramListEnd = FindClosingScope(content, pos);
                    var paramList = content.Substring(pos + 1, paramListEnd - pos - 1);

                    function.Params = ParseParamList(paramList).ToArray();
                    function.Info = ParseDocComment(BackTrackComments(content, content.LastIndexOf('\n', pos)));

                    members.Add(function);
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

            return members;
        }

        /// <summary>
        /// Parses the next class member from the class body
        /// </summary>
        /// <param name="content">The body of the class</param>
        /// <param name="dtypeName">The name of the class type or null</param>
        /// <param name="inPos">The positin where the search starts</param>
        /// <param name="outPos">The position where the search ended</param>
        /// <param name="baseMember">
        ///     If a member is declared as part of a multi-declaration, this contains all information about the type except for the name.
        ///     If a name is specified, it is ignored.
        /// </param>
        /// <returns></returns>
        protected Interchangeable.ClassMember ParseNextMember(
            string content,
            string dtypeName,
            int inPos,
            out int outPos,
            Interchangeable.ClassMember baseMember = null)
        {
            var member = new Interchangeable.ClassMember();
            var modifiers = new List<string>();

            if (baseMember != null)
            {
                baseMember.Name = null;
                member = baseMember;
            }

            while (true)
            {
                var nextWord = getNextWord(inPos, out inPos);
                if (nextWord != null)
                {
                    switch (nextWord)
                    {
                        case "private":
                        case "protected":
                        case "internal":
                        case "public":
                            member.AccessLevel = nextWord;
                            break;

                        case "sealed":
                        case "abstract":
                        case "override":
                        case "virtual":
                        case "new":
                        case "readonly":
                        case "static":
                        case "out":
                        case "params":
                        case "ref":
                            modifiers.Add(nextWord);
                            break;

                        default:
                            /* if nextWord is the constructor */
                            if (nextWord == dtypeName || nextWord == "Main")
                            {
                                member.TypeUnparsed = "this";
                                member.Name = nextWord;
                            }
                            else
                            {
                                /* nextWord is not a keyword. This means, it is eather the return type or the name of the member */
                                if (member.TypeUnparsed == null)
                                {
                                    member.TypeUnparsed = nextWord;
                                }
                                else
                                {
                                    member.Name = nextWord;
                                }
                            }
                            break;
                    }

                    if (member.TypeUnparsed != null && member.Name != null)
                    {
                        if (member.AccessLevel == null) member.AccessLevel = "private";
                        member.Modifiers = modifiers.ToArray();

                        outPos = inPos;
                        return member;
                    }
                }
                else
                {
                    break;
                }
            }

            outPos = inPos;
            return null;


            /* --------- Local Helper Functions -------- */
            string getNextWord(int _inPos, out int _outPos)
            {
                Match m;

                while (_inPos < content.Length)
                {
                    var s = content.Substring(_inPos);
                    m = new Regex(@"(\s|[^<>\w.\[\]])").Match(s);
                    string word;

                    if (m.Success)
                        word = s.Substring(0, m.Index);
                    else
                        word = s;

                    if (word.Length == 0)
                    {
                        _inPos = SkipNoCode(content, _inPos + 1);
                        continue;
                    }

                    if (m.Success)
                    {
                        _inPos += m.Index;
                        _inPos = SkipNoCode(content, _inPos);
                    }
                    else
                    {
                        _inPos = content.Length;
                    }

                    _outPos = _inPos;
                    return word;
                }

                _outPos = _inPos;
                return null;
            }
        }

        protected List<Interchangeable.FunctionParameter> ParseParamList(string s)
        {
            var paramList = new List<Interchangeable.FunctionParameter>();
            var pos = 0;

            for (; pos < s.Length; ++pos)
            {
                var c = s[pos];

                switch (c)
                {
                    case '(':
                    case '[':
                    case '<':
                    case '{':
                        pos = FindClosingScope(s, pos);
                        break;

                    default:
                        if (c == ',' || pos == s.Length - 1)
                        {
                            if (pos == s.Length - 1) pos++;

                            var param = ParseNextMember(s.Substring(0, pos), null, 0, out pos);
                            if (param == null) break;

                            paramList.Add(new Interchangeable.FunctionParameter()
                            {
                                TypeUnparsed = param.TypeUnparsed,
                                Name = param.Name,
                                Modifiers = param.Modifiers
                            });

                            if (pos < s.Length - 1) s = s.Substring(pos + 1).Trim();
                            else s = "";
                            pos = -1;
                        }
                        break;
                }
            }
            return paramList;
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
                case '[': closing = ']'; break;
                case '<': closing = '>'; break;
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

            while (pos < s.Length)
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
