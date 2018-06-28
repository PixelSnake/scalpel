using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scalpel.Interchangeable;
using ScalpelPlugin.Plugins;
using ScalpelPlugin.Syntax.Elements;

public class Plugin : ScalpelPlugin.Plugins.Plugin
{
    bool OpenFile = false;

    public void Convert(PluginParams p)
    {
        var documentation = p.Documentation;
        ParseArguments(p.Arguments);

        documentation.Namespaces = documentation.Namespaces.OrderBy(ns => ns.Name).ToArray();
        foreach (var ns in documentation.Namespaces)
            ns.Datatypes = ns.Datatypes.OrderBy(d => d.Name).ToArray();

        var tex = DocumentHeader() + TexifyDocumentation(documentation) + DocumentFooter();

        System.IO.Directory.CreateDirectory(p.TargetDirectory);

        var texPath = System.IO.Path.Combine(p.TargetDirectory, "out.tex");
        System.IO.File.WriteAllText(texPath, tex);

        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.Arguments = "out.tex";
        startInfo.FileName = "pdflatex";
        startInfo.WorkingDirectory = p.TargetDirectory;

        /* twice for robustness */
        var pdflatex = System.Diagnostics.Process.Start(startInfo);
        pdflatex.WaitForExit();
        pdflatex = System.Diagnostics.Process.Start(startInfo);
        pdflatex.WaitForExit();

        Console.WriteLine($"Saved PDF at { p.TargetDirectory }");

        if (OpenFile)
        {
            var pdfPath = System.IO.Path.Combine(p.TargetDirectory, "out.pdf");
            Console.WriteLine("Opening PDF file at " + pdfPath);
            System.Diagnostics.Process.Start(pdfPath);
        }
    }

    internal void ParseArguments(string[] args)
    {
        for (var i = 0; i < args.Length; ++i)
        {
            var p = args[i];

            switch (p)
            {
                default:
                    Console.WriteLine($"Unrecongnized flag \"{ p }\"");
                    PrintUsage();
                    break;

                case "-o":
                case "--open":
                    OpenFile = true;
                    break;
            }
        }
    }

    internal void PrintUsage()
    {

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

    internal string TexifyDocumentation(Documentation doc)
    {
        var tex = "";

        foreach (var ns in doc.Namespaces)
        {
            if (ns.Name.Length > 0) tex += @"\section{namespace " + ns.Name + "}\n";
            else tex += @"\section{Global}" + "\n";
            tex += TexifyDatatypes(ns.Datatypes, ns);
        }

        return tex;
    }

    internal string TexifyDatatypes(Datatype[] dtypes, Namespace ns)
    {
        var tex = "";

        foreach (var dt in dtypes)
        {
            if (dt is Class)
            {
                var c = dt as Class;
                tex += @"
                    \subsection{class " + TexEscape(c.Name) + (c.TypeParams.Length > 0 ? $"\\textless { String.Join(", ", c.TypeParams) }\\textgreater " : "") + @"}
                    \label{type:" + c.ToString() + @"}
                        " + texAttributes(c) + @"
                        " + texBasicInfo(c) + @"
		
		                " + texSummary(c) + @"
                        " + texTypeParameters(c) + @"
                    \vspace{1cm}
                ";
            }
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
                    var absoluteName = (ns.Name.Length > 0 ? ns.Name + "." : "") + bc;
                    var bcRef = Class.ByName.ContainsKey(absoluteName) ? Class.ByName[absoluteName] : null;

                    deriveTex += (first ? @"\textbf{Derives from}" : "") +
                        (bcRef == null ? @"& " + bc + @" \\" : @"& " + CodeRef(bcRef, ns) + @"\\");
                    first = false;
                }
                return deriveTex;
            }
        }

        string texAttributes(Class c)
        {
            var attrTex = "";

            if (c.IsGeneric) attrTex += @"\colorbox{RedOrange}{\color{white}\textbf{\strut generic}}" + "\n";
            if (c.AccessLevel != null) attrTex += @"\colorbox{" + GetAccessLevelColor(c.AccessLevel) + @"}{\color{white}\textbf{\strut " + c.AccessLevel + @"}}" + "\n";
            if (c.Modifier != null) attrTex += @"\colorbox{MidnightBlue}{\color{white}\textbf{\strut " + c.Modifier + @"}}" + "\n";

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
            if (c.Info?.Summary == null) return "";
            if (c.Info.Summary.Length == 0) return "";
            return @"\subsubsection{Summary}" + TexifyFormattedText(c.Info.Summary, ns);
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

    internal string CodeRef(Datatype dtype, Namespace currentNamespace = null)
    {
        var cr = Hyperref("type:" + dtype.ToString(), (dtype.Namespace == currentNamespace ? "" : dtype.Namespace.Name) + dtype.Name);
        return cr;
    }
    internal string Hyperref(string label, string text)
    {
        var hr = @"\hyperref[" + label + @"]{\color{MidnightBlue}" + text + "}";
        return hr;
    }

    string TexEscape(string s)
    {
        return s
            .Replace("_", @"\_")
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("<", @"\textless ")
            .Replace(">", @"\textgreater ");
    }

    string TexifyFormattedText(ScalpelPlugin.Syntax.FormattedText ft, Namespace currentNamespace)
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
                tex += String.Join("\n", list.Items.Select(i => (list.Type != List.ListType.Table ? @"\item " : "") + TexifyFormattedText(i, currentNamespace) + (list.Type == List.ListType.Table ? @"\\ \hline" : "")));
                tex += @"\end{" + type + @"}" + "\n";
            }
            else if (elem is CodeReference)
            {
                if (elem is ClassReference)
                {
                    var cr = elem as ClassReference;
                    tex += CodeRef(cr.Class, currentNamespace);
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
