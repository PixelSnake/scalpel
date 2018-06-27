using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scalpel.Interchangeable;
using ScalpelPlugin.Syntax.Elements;

public class Plugin : ScalpelPlugin.Plugins.Plugin
{
    public void Convert(Documentation documentation, string outPath)
    {
        var classes = new List<Class>();
        foreach (var f in documentation.Files) classes.AddRange(f.Classes);
        classes = classes.OrderBy(c => c.Name).ToList();

        var tex = DocumentHeader() + TexifyClasses(classes) + DocumentFooter();

        System.IO.Directory.CreateDirectory(outPath);

        var texPath = System.IO.Path.Combine(outPath, "out.tex");
        System.IO.File.WriteAllText(texPath, tex);
        System.Diagnostics.Process.Start("pdflatex", texPath);

        Console.WriteLine($"Saved PDF at { outPath }");
    }

    internal string DocumentHeader()
    {
        return @"
            \documentclass[a4paper, 11pt, xcolor=dvipsnames]{article}
            \usepackage[ngerman]{babel}
            \usepackage[utf8]{inputenc}
            \usepackage{amssymb}
            \usepackage{lmodern}
            \usepackage{mathtools, nccmath}
            \usepackage{xparse}
            \usepackage{graphicx}
            \usepackage{tabularx}
            \usepackage[dvipsnames]{xcolor}
            \usepackage{tikz}
            \usepackage{gensymb}
            \usepackage{hyperref}
            \usetikzlibrary{automata,positioning}

            \def\code#1{\texttt{\colorbox{LightLightGray}{#1}}}
            
            \definecolor{LightLightGray}{rgb}{0.9,0.9,0.9}

            \begin{document}

            \begin{center}
	            {\huge Project Name \\
	            Code Documentation} \\
            \end{center}

            \tableofcontents
            \newpage
        ";
    }

    internal string DocumentFooter()
    {
        return @"\end{document}";
    }

    internal string TexifyClasses(IList<Class> classes)
    {
        var tex = "\\section{Classes}\n";

        foreach (var c in classes)
        {
            tex += @"
                \subsection{" + TexEscape(c.Name) + @"}
                \label{class-id:" + c.Id + @"}
                    " + texAttributes(c) + @"
                    " + texBasicInfo(c) + @"
		
		            " + texSummary(c) + @"
                    " + texTypeParameters(c) + @"
                \vspace{1cm}
            ";
        }
        return tex;

        #region Local Functions
        string texBasicInfo(Class c)
        {
            var basicTex = @"
                \mbox{} \\
                \colorbox{LightLightGray}
                {
		            \begin{tabularx}{\textwidth}{ Xr}            
            ";
            var content = "";

            if (c.Info.Author != null)
                content += c.Info.Author != null ? @"\textbf{Author} & " + c.Info.Author + @" \\ \hline" : "";
            content += texBaseClasses();

            if (content.Length < 1) return "";
            return basicTex + content + @"\end{tabularx}}" + "\n";

            string texBaseClasses()
            {
                if (c.BaseClasses == null || c.BaseClasses.Length == 0) return "";

                var first = true;
                var deriveTex = "";

                foreach (var bc in c.BaseClasses)
                {
                    var bcRef = Class.ByName.ContainsKey(bc) ? Class.ByName[bc] : null;

                    deriveTex += (first ? @"\textbf{Derives from}" : "") +
                        (bcRef == null ? @"& " + bc + @" \\" : @"& \hyperref[class-id:" + bcRef.Id + @"]{\color{MidnightBlue}" + bc + @"} \\");
                    first = false;
                }
                return deriveTex;
            }
        }

        string texAttributes(Class c)
        {
            var attrTex = "";

            if (c.IsGeneric) attrTex += @"\colorbox{RedOrange}{\color{white}\textbf{\strut generic}}" + "\n";
            if (c.AccessLevel != "") attrTex += @"\colorbox{" + GetAccessLevelColor(c.AccessLevel) + @"}{\color{white}\textbf{\strut " + c.AccessLevel + @"}}" + "\n";
            if (c.Modifier != "") attrTex += @"\colorbox{MidnightBlue}{\color{white}\textbf{\strut " + c.Modifier + @"}}" + "\n";

            return attrTex;

            string GetAccessLevelColor(string al)
            {
                switch (al)
                {
                    default:
                    case "public":
                        return "ForestGreen";
                    case "internal":
                        return "Emerald";
                    case "protected":
                        return "MidnightBlue";
                    case "private":
                        return "Gray";
                }
            }
        }

        string texSummary(Class c)
        {
            if (c.Info.Summary.Length == 0) return "";
            return @"\subsubsection{Summary}" + TexifyFormattedText(c.Info.Summary);
        }

        string texTypeParameters(Class c)
        {
            if (!c.IsGeneric) return "";

            var typeParamTex = @"\subsubsection{Type Parameters}" + "\n" + @"\begin{itemize}";
            foreach (var tp in c.TypeParams)
            {
                var descr = c.Info.TypeParamDescription.ContainsKey(tp) ? c.Info.TypeParamDescription[tp] : "";
                typeParamTex += @"\item \code{" + tp + @"} - " + descr + "\n";
            }

            return typeParamTex + @"\end{itemize}";
        }
        #endregion
    }

    string TexEscape(string s)
    {
        return s
            .Replace("_", @"\_")
            .Replace(@"\", @"\\")
            .Replace(@"%", @"\%")
            .Replace("<", @"\textless ")
            .Replace(">", @"\textgreater ");
    }

    string TexifyFormattedText(ScalpelPlugin.Syntax.FormattedText ft)
    {
        var tex = "";

        foreach (var elem in ft.Children)
        {
            if (elem == null) continue;

            if (elem is Text) tex += (elem as Text).Value;
            else if (elem is List)
            {
                var list = elem as List;
                if (list.Items.Length < 1) continue;

                var type = "itemize";
                switch (list.Type)
                {
                    case List.ListType.Number: type = "enumerate"; break;
                    case List.ListType.Table: type = "tabular"; break;
                }

                tex += @"\begin{" + type + @"}" + (list.Type == List.ListType.Table ? "{l}" : "") + "\n";
                tex += String.Join("\n", list.Items.Select(i => (list.Type != List.ListType.Table ? @"\item " : "") + TexifyFormattedText(i) + (list.Type == List.ListType.Table ? @"\\ \hline" : "")));
                tex += @"\end{" + type + @"}" + "\n";
            }
            else if (elem is CodeReference)
            {
                if (elem is ClassReference)
                {
                    var cr = elem as ClassReference;
                    tex += @"\hyperref[class-id:" + cr.Class.Id + @"]{\color{MidnightBlue}" + cr.Class.Name + @"}";
                }
            }
            else if (elem is InlineCode)
            {
                var code = elem as InlineCode;
                tex += @"\code{" + TexEscape(code.Value) + @"}";
            }
        }

        return tex;
    }
}
