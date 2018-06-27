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
    public class DocumentationParser
    {
        protected string InDir;
        protected string[] Filetypes;

        protected string rxDocComment = @"\/\/\/(.*?)\s*(\n\r?)\s*";
        protected string rxClassDefinition = @"(public|protected|internal|private)?(\s*abstract|sealed)?\s+(class)\s+(([\w]+)(\s*<\s*([\w]+,?)+\s*>)?)(\s*:((\s*[\w]+)(\s*,\s*[\w]+)*)?)?\s*";
        protected string rxLine = @"(.*?)\n\r?";

        public DocumentationParser(string inDir, string[] filetypes)
        {
            InDir = inDir;
            Filetypes = filetypes;
        }

        public Interchangeable.Documentation Parse()
        {
            var files = SearchDirectory(InDir);

            foreach (var file in files)
                FinalizeClasses(file.Classes);

            return new Interchangeable.Documentation()
            {
                Files = files.ToArray()
            };
        }

        protected IList<Interchangeable.File> SearchDirectory(string dirPath)
        {
            var documentedFiles = new List<Interchangeable.File>();

            var directories = Directory.GetDirectories(dirPath);
            foreach (var d in directories)
                documentedFiles.AddRange(SearchDirectory(d));

            var files = Directory.GetFiles(dirPath);
            foreach (var f in files)
            {
                var ext = new FileInfo(f).Extension;
                if (ext.Length < 1 || !Filetypes.Contains(ext.Substring(1))) continue;

                var file = ParseFile(File.ReadAllText(f), f);
                documentedFiles.Add(file);
            }

            return documentedFiles;
        }

        protected Interchangeable.File ParseFile(string content, string path)
        {
            var regexComment = new Regex(rxDocComment);
            var regexLine = new Regex(rxLine);

            var classes = new List<Interchangeable.Class>();

            Match m;
            int pos = 0;
            string currentComment = "";
            var lastTimeWasSuccessful = false;

            while ((m = regexComment.Match(content, pos)).Success || lastTimeWasSuccessful)
            {
                lastTimeWasSuccessful = m.Success;

                if (!lastTimeWasSuccessful || (pos < m.Index && pos != 0))
                {
                    // now comes what is documented by currentComment. Maybe a class, declaration, etc.
                    var nextLineMatch = regexLine.Match(content, pos);

                    Interchangeable.IInterchangeable next;
                    if ((next = ParseNextClass(nextLineMatch.Value, currentComment)) != null)
                        classes.Add(next as Interchangeable.Class);

                    currentComment = "";
                }

                if (lastTimeWasSuccessful)
                {
                    currentComment += "\n" + m.Groups[1];
                    pos = m.Index + m.Length;
                }
            }

            return new Interchangeable.File()
            {
                Path = path,
                Classes = classes.ToArray(),
                
            };
        }

        protected Interchangeable.Class ParseNextClass(string content, string docComment)
        {
            var regex = new Regex(rxClassDefinition);
            Match m;

            XmlDocument xmlDoc = null;
            docComment = $"<doc>{docComment}</doc>";

            try
            {
                xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(docComment);
            }
            catch (XmlException) { }

            if ((m = regex.Match(content)).Success)
            {
                var typeParams = m.Groups[7].Value.Split(',').Select(c => c.Trim()).ToArray();
                var baseClasses = m.Groups[10].Value.Split(',').Select(c => c.Trim()).ToArray();

                var _class = new Interchangeable.Class()
                {
                    AccessLevel = m.Groups[1].Value.Trim(),
                    Modifier = m.Groups[2].Value.Trim(),
                    Name = m.Groups[4].Value.Trim(),
                    BaseClasses = baseClasses == null || baseClasses.Length < 1 || baseClasses[0] == "" ? null : baseClasses,
                    TypeParams = typeParams == null || typeParams.Length < 1 || typeParams[0] == "" ? null : typeParams,

                    Info = xmlDoc != null ? ParseDocComment(xmlDoc) : null
                };
                Interchangeable.Class.ByName.Add(_class.Name, _class);

                return _class;
            }

            return null;
        }

        protected void FinalizeClasses(IEnumerable<Scalpel.Interchangeable.Class> classes)
        {
            foreach (var c in classes)
            {
                if (c.Info.UnparsedSummary == null) continue;
                c.Info.Summary = ScalpelPlugin.Syntax.FormattedText.Parse(c.Info?.UnparsedSummary);
                c.Info.UnparsedSummary = null;
            }
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
