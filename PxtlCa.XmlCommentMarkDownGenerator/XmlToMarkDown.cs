using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PxtlCa.XmlCommentMarkDownGenerator
{
    public static class XmlToMarkdown
    {
        public static string ToMarkDown(this string s)
        {
            return s.ToMarkDown(new ConversionContext {
                UnexpectedTagAction = UnexpectedTagActionEnum.Error
                , WarningLogger = new TextWriterWarningLogger(Console.Error)
            });
        }

        public static string ToMarkDown(this string s, ConversionContext context)
        {
            var xdoc = XDocument.Parse(s);
            return xdoc
                .ToMarkDown(context)
                .RemoveRedundantLineBreaks();
        }

        public static string ToMarkDown(this Stream s)
        {
            var xdoc = XDocument.Load(s);
            return xdoc
                .ToMarkDown(new ConversionContext { UnexpectedTagAction = UnexpectedTagActionEnum.Error, WarningLogger = new TextWriterWarningLogger(Console.Error) })
                .RemoveRedundantLineBreaks();
        }

        // Handle types from https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/processing-the-xml-file
        private static Dictionary<string, string> _MemberNamePrefixDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["N:"] = "Namespace",
            ["T:"] = "Type",
            ["F:"] = "Field",
            ["P:"] = "Property",
            ["M:"] = "Method",
            ["E:"] = "Event",
            ["!:"] = "Error",
        };

        /// <summary>
        /// Convert header text to an anchor link.
        /// </summary>
        /// <param name="anchor">The header to reference.</param>
        /// <returns>The corrected anchor link.</returns>
        public static string ToAnchor(this string anchor)
        {
            // Remove any characters disallowed for use in an anchor fragment, as well as parentheses and '.'
            Regex pattern = new Regex(@"[""#%<>[\\\]^`{|}().]");
            // Replace spaces with '-'
            return "#" + pattern.Replace(anchor, "").Replace(' ', '-').ToLowerInvariant();
        }

        /// <summary>
        /// Convert header text to a MSDN link.
        /// </summary>
        /// <param name="header">The header to reference.</param>
        /// <returns>The corrected MSDN link.</returns>
        public static string ToMsdnLink(this string header)
        {
            // Remove any characters disallowed for use in an anchor fragment, as well as parentheses and '.'
            Regex pattern = new Regex(@"((^(([A-Z]\:)|(\w+\s)))|(\(.+\)))");
            return "https://msdn.microsoft.com/en-us/library/" + pattern.Replace(header, "");
        }

        /// <summary>
        /// Convert header text to a link.
        /// </summary>
        /// <param name="header">The header to reference.</param>
        /// <returns>The corrected link.</returns>
        public static string ToLink(this string header)
        {
            // Check to see if we should link to MSDN:
            // FIXME: Currently only checks for System namespace AFTER the assembly namespace is removed.
            //        This could result in false positives, if the assembly has a System sub-namespace.
            Regex isMsdn = new Regex(@"^(([A-Z]\:)|(\w+\s))System\.");
            return isMsdn.IsMatch(header) ? ToMsdnLink(header) : ToAnchor(header);
        }

        /// <summary>
        /// Write out the given XML Node as Markdown. Recursive function used internally.
        /// </summary>
        /// <param name="node">The xml node to write out.</param>
        /// <param name="ConversionContext">The Conversion Context that will be passed around and manipulated over the course of the translation.</param>
        /// <returns>The converted markdown text.</returns>
        public static string ToMarkDown(this XNode node, ConversionContext context)
        {
            if(node is XDocument)
            {
                node = ((XDocument)node).Root;
            }

            string name;
            if (node.NodeType == XmlNodeType.Element)
            {
                var el = (XElement)node;
                name = el.Name.LocalName;
                if (name == "member")
                {
                    string expandedName = null;
                    if(!_MemberNamePrefixDict.TryGetValue(el.Attribute("name").Value.Substring(0,2), out expandedName))
                    {
                        expandedName = "none";
                    }
                    //name = expandedName.ToLowerInvariant();
                }
                if (name == "see")
                {
                    var anchor = el.Attribute("cref") != null && el.Attribute("cref").Value.StartsWith("!:#");
                    name = anchor ? "seeAnchor" : "seePage";
                }
                //treat first Param element separately to add table headers.
                if (name.EndsWith("param")
                    && node
                        .ElementsBeforeSelf()
                        .LastOrDefault()
                        ?.Name
                        ?.LocalName != "param")
                {
                    name = "firstparam";
                }

                try { 
                    var vals = TagRenderer.Dict[name].ValueExtractor(el, context).ToArray();
                    return string.Format(TagRenderer.Dict[name].FormatString, args: vals);
                }
                catch(KeyNotFoundException ex)
                {
                    var lineInfo = (IXmlLineInfo)node;
                    switch(context.UnexpectedTagAction)
                    {
                        case UnexpectedTagActionEnum.Error:
                            throw new XmlException($@"Unknown element type ""{ name }""", ex, lineInfo.LineNumber, lineInfo.LinePosition);
                        case UnexpectedTagActionEnum.Warn:
                            context.WarningLogger.LogWarning($@"Unknown element type ""{ name }"" on line {lineInfo.LineNumber}, pos {lineInfo.LinePosition}");
                            break;
                        case UnexpectedTagActionEnum.Accept:
                            //do nothing;
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected {nameof(UnexpectedTagActionEnum)}");
                    }
                }
            }


            if (node.NodeType == XmlNodeType.Text)
                return WebUtility.HtmlEncode(Regex.Replace(((XText)node).Value.Replace('\n', ' '), @"\s+", " "));

            return "";
        }

        private static readonly Regex _PrefixReplacerRegex = new Regex(@"(^[A-Z]\:)");

        internal static string[] ExtractNameAndBodyFromMember(string att, XElement node, ConversionContext context)
        {
            var name = node.Attribute(att)?.Value;
            if (name == null)
            {
                return new[]
                   {
                    null,
                    node.Nodes().ToMarkDown(context)
                };
            }
            var newName = Regex.Replace(name, $@":{Regex.Escape(context.AssemblyName)}\.", ":"); //remove leading namespace if it matches the assembly name
            //TODO: do same for function parameters
            newName = _PrefixReplacerRegex.Replace(newName, match => _MemberNamePrefixDict[match.Value] + " "); //expand prefixes into more verbose words for member.
            return new[]
               {
                    newName,
                    node.Nodes().ToMarkDown(context)
                };
        }

        internal static string[] ExtractNameAndBodyFromMember(XElement node, ConversionContext context)
        {
            return ExtractNameAndBodyFromMember("name", node, context);
        }

        internal static string[] ExtractNameAndBody(string att, XElement node, ConversionContext context)
        {
            return new[]
               {
                    node.Attribute(att)?.Value,
                    node.Nodes().ToMarkDown(context)
                };
        }

        internal static string ToMarkDown(this IEnumerable<XNode> es, ConversionContext context)
        {
            return es.Aggregate("", (current, x) => current + x.ToMarkDown(context));
        }

        internal static string ToCodeBlock(this string s)
        {
            var lines = s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
            return string.Join("\n", lines.Select(x => new string(x.SkipWhile((y, i) => i < blank).ToArray()))).TrimEnd();
        }

        static string RemoveRedundantLineBreaks(this string s)
        {
            return Regex.Replace(s, @"\n\n\n+", "\n\n");
        }
    }
}
