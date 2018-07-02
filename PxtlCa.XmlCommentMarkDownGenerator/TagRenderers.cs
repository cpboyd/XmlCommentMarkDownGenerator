using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PxtlCa.XmlCommentMarkDownGenerator
{
    public class TagRenderer
    {
        public TagRenderer(string formatString, Func<XElement, ConversionContext, IEnumerable<string>> valueExtractor)
        {
            FormatString = formatString;
            ValueExtractor = valueExtractor;
        }

        public string FormatString { get; } = "";

        public Func<
            XElement, //xml Element to extract from 
            ConversionContext, //context
            IEnumerable<string> //resultant list of values that will get used with formatString
        > ValueExtractor;

        public static Dictionary<string, TagRenderer> Dict { get; } = new Dictionary<String, TagRenderer>()
        {
            ["doc"] = new TagRenderer(
                "# {0} #\n\n{1}\n\n",
                (x, context) => new[]{
                        x.Element("assembly").Element("name").Value,
                        x.Element("members").Elements("member").ToMarkDown(context.MutateAssemblyName(x.Element("assembly").Element("name").Value))
                }
            ),
            ["a"] = new TagRenderer(
                "[{0}]({1})",
                (x, context) => new[] { x.Nodes().ToMarkDown(context), x.Attribute("href")?.Value ?? "" }
            ),
            ["i"] = new TagRenderer(
                "*{0}*",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["b"] = new TagRenderer(
                "**{0}**",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            // No text underline in Markdown, so just bold instead:
            ["u"] = new TagRenderer(
                "**{0}**",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["p"] = new TagRenderer(
                "{0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["list"] = new TagRenderer(
                "{0}\n",
                (x, context) =>
                {
                    Func<string, string> tableRows = (key) => x.Elements(key).Aggregate("", (current, y) => current + y.Elements("term").Aggregate("| ", (current2, z) => current2 + z.Nodes().ToMarkDown(context) + " | ") + "\n");
                    Func<XElement, string> describe = (el) =>
                    {
                        var description = el.Element("description");
                        if (description == null)
                        {
                            return el.Nodes().ToMarkDown(context);
                        }
                        var term = el.Element("term");
                        return (term == null ? "" : term.Nodes().ToMarkDown(context) + ": ") + description.Nodes().ToMarkDown(context);
                    };
                    var firstHeader = x.Element("listheader");
                    switch (x.Attribute("type").Value)
                    {
                        case "table":
                            return new[] { ((firstHeader != null) ? (tableRows("listheader") + firstHeader.Elements("term").Aggregate("\n| ", (current, y) => current + "--- | ") + "\n") : "\n| Name | Description |\n|-----|------|\n") + tableRows("item") };
                        case "number":
                            return new[] { x.Elements("item").Aggregate("", (current, y) => current + "1. " + describe(y) + "\n") };
                        case "bullet":
                        default:
                            return new[] { x.Elements("item").Aggregate("", (current, y) => current + "* " + describe(y) + "\n") };
                    }
                }
            ),
            ["preliminary"] = new TagRenderer(
                "**{0}**",
                (x, context) =>
                {
                    var description = x.Nodes().ToMarkDown(context);
                    return new[] { string.IsNullOrEmpty(description) ? "[This is preliminary documentation and subject to change.]" : description };
                }
            ),
            ["br"] = new TagRenderer(
                "\n  ",
                (x, context) => new string[0]
            ),
            ["summary"] = new TagRenderer(
                "{0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["value"] = new TagRenderer(
                "**Value**: {0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["blockquote"] = new TagRenderer(
                "\n\n{0}\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context).Split('\n').Aggregate("", (current, y) => current + "> " + y + "\n") }
            ),
            ["remarks"] = new TagRenderer(
                "\n\n{0}\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context).Split('\n').Aggregate("", (current, y) => current + "> " + y + "\n") }
            ),
            ["example"] = new TagRenderer(
                "##### Example: {0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["para"] = new TagRenderer(
                "{0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["code"] = new TagRenderer(
                "\n\n###### {0} code\n\n```\n{1}\n```\n\n",
                (x, context) => new[] { x.Attribute("lang")?.Value ?? "", x.Value.ToCodeBlock() }
            ),
            ["seealso"] = new TagRenderer(
                "##### See also: [{1}]({0})\n",
                (x, context) =>
                {
                    var xx = XmlToMarkdown.ExtractNameAndBodyFromMember("cref", x, context);
                    if (string.IsNullOrEmpty(xx[1]))
                    {
                        xx[1] = xx[0];
                    }
                    xx[0] = xx[0].ToAnchor();
                    return xx;
                }
            ),
            ["seeAnchor"] = new TagRenderer(
                "[{1}]({0})",
                (x, context) => { var xx = XmlToMarkdown.ExtractNameAndBody("cref", x, context); xx[0] = xx[0].ToAnchor(); return xx; }
            ),
            ["seePage"] = new TagRenderer(
                "[{1}]({0})",
                (x, context) =>
                {
                    var xx = XmlToMarkdown.ExtractNameAndBodyFromMember("cref", x, context);
                    if (string.IsNullOrEmpty(xx[1]))
                    {
                        xx[1] = xx[0];
                    }
                    xx[0] = xx[0].ToAnchor();
                    return xx;
                }
            ),
            ["firstparam"] = new TagRenderer(
                "\n| Name | Description |\n|-----|------|\n|{0}: |{1}|\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)
            ),
            ["typeparam"] = new TagRenderer(
                "|{0}: |{1}|\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)
            ),
            ["param"] = new TagRenderer(
                "|{0}: |{1}|\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)
            ),
            ["paramref"] = new TagRenderer(
                "`{0}`",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)
            ),
            ["exception"] = new TagRenderer(
                "[[{0}|{0}]]: {1}\n\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("cref", x, context)
            ),
            ["returns"] = new TagRenderer(
                "**Returns**: {0}\n\n",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["c"] = new TagRenderer(
                " `{0}` ",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["member"] = new TagRenderer(
                "## {0}\n\n{1}\n\n---\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBodyFromMember(x, context)
            ),
            ["none"] = new TagRenderer(
                "",
                (x, context) => new string[0]
            ),
            // Custom:
            ["version"] = new TagRenderer(
                "*Added in {0}*",
                (x, context) => new[] { x.Nodes().ToMarkDown(context) }
            ),
            ["platform"] = new TagRenderer(
                "*Available on {0}*",
                (x, context) =>
                {
                    return new[] { x.Elements().Aggregate("", (current, y) => (y.Name == "frameworks" ? (y.Element("compact").Value == "true" ? "Compact Framework" : ".NET Framework") : y.Nodes().ToMarkDown(context)) + ", ").TrimEnd(',', ' ') };
                }
            ),
        };
    }

    
}
