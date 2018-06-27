using ScalpelPlugin.Syntax.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace ScalpelPlugin.Syntax
{
    public class FormattedText
    {
        public FormattedTextElement[] Children;
        public int Length { get => Children.Length; }

        public static FormattedText Parse(XmlElement elem)
        {
            var children = new List<FormattedTextElement>();

            if (elem.InnerText == elem.InnerXml)
            {
                children.Add(new Text(elem.InnerText));
            }
            else
            {
                foreach (XmlNode node in elem.ChildNodes)
                {
                    if (node is XmlElement) children.Add(ParseElement(node as XmlElement));
                    else if (node is XmlText) children.Add(new Text((node as XmlText).Value));
                }
            }

            return new FormattedText()
            {
                Children = children.ToArray()
            };
        }

        protected static FormattedTextElement ParseElement(XmlElement elem)
        {
            switch (elem.Name)
            {
                case "para":
                    return new Paragraph()
                    {
                        Content = FormattedText.Parse(elem)
                    };

                case "see":
                    {
                        var cref = elem.GetAttribute("cref");
                        if (cref != null && Scalpel.Interchangeable.Class.ByName.ContainsKey(cref))
                        {
                            return new ClassReference()
                            {
                                Class = Scalpel.Interchangeable.Class.ByName[cref]
                            };
                        }
                        // Todo other types of references
                        else
                        {
                            return new Text(cref);
                        }
                    }

                case "list":
                    {
                        var type = elem.GetAttribute("type") ?? "bullet";
                        var listType = List.ListType.Bullet;
                        switch (type)
                        {
                            case "number": listType = List.ListType.Number; break;
                            case "table":  listType = List.ListType.Table; break;
                        }

                        var items = new List<FormattedText>();
                        foreach (XmlNode cn in elem.GetElementsByTagName("item"))
                            if (cn is XmlElement) items.Add(FormattedText.Parse(cn as XmlElement));

                        return new List()
                        {
                            Type = listType,
                            Items = items.ToArray()
                        };
                    }

                case "c":
                    return new InlineCode(elem.InnerText);

                default:
                    return null;
            }
        }
    }
}
