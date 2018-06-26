using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scalpel.Interchangeable;

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
            \usetikzlibrary{automata,positioning}

            \def\code#1{\texttt{#1}}

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
                    \colorbox{lightgray}{
		            \begin{tabularx}{\textwidth}{Xr}
			            " + (c.Info.Author != null ? @"\textbf{Author} & \code{Thomas Neumüller} \\ \hline" : "") + @"
			            " + texDerivesFrom(c) + @"
		            \end{tabularx}
                    }
		
		            " + texSummary(c) + @"
                    " + texTypeParameters(c) + @"
";
        }
        return tex;

        #region Local Functions
        string texDerivesFrom(Class c)
        {
            if (c.BaseClasses == null || c.BaseClasses.Length == 0) return "";

            var first = true;
            var deriveTex = "";

            foreach (var bc in c.BaseClasses)
            {
                deriveTex += (first ? @"\textbf{Derives from}" : "") + @"& \code{" + bc + @"} \\";
                first = false;
            }
            return deriveTex;
        }

        string texAttributes(Class c)
        {
            var attrTex = "";

            if (c.IsGeneric) attrTex += @"\colorbox{RedOrange}{\color{white}\textbf{\strut generic}}" + "\n";
            if (c.AccessLevel != "") attrTex += @"\colorbox{" + GetAccessLevelColor(c.AccessLevel) + @"}{\color{white}\textbf{\strut " + c.AccessLevel + @"}}" + "\n";
            if (c.Modifier != "") attrTex += @"\colorbox{MidnightBlue}{\color{white}\textbf{\strut " + c.Modifier + @"}}" + "\n";

            return attrTex + (attrTex.Length > 0 ? @"\\\\" : "");

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
            return @"\subsubsection{Summary}" + c.Info.Summary;
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
}
