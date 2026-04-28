using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Simply.JobApplication.Tests.Helpers;

// Builds minimal valid DOCX byte arrays from XML-like instructions for unit testing.
public static class TestDocxBuilder
{
    private static readonly XNamespace W    = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R    = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static XElement Para(string style, params XElement[] runs)
    {
        var pPr = string.IsNullOrEmpty(style)
            ? null
            : new XElement(W + "pPr",
                new XElement(W + "pStyle", new XAttribute(W + "val", style)));
        var children = pPr != null
            ? new XObject[] { pPr }.Concat(runs).ToArray()
            : runs.Cast<XObject>().ToArray();
        return new XElement(W + "p", children);
    }

    private static XElement Run(string text, bool bold = false, bool italic = false)
    {
        var rPr = (bold || italic)
            ? new XElement(W + "rPr",
                bold   ? new XElement(W + "b")   : null!,
                italic ? new XElement(W + "i")   : null!)
            : null;
        var t = new XElement(W + "t",
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            text);
        return rPr != null
            ? new XElement(W + "r", rPr, t)
            : new XElement(W + "r", t);
    }

    private static XElement BulletPara(string text, bool ordered = false, int ilvl = 0)
    {
        var numId = ordered ? "2" : "1";
        return new XElement(W + "p",
            new XElement(W + "pPr",
                new XElement(W + "numPr",
                    new XElement(W + "ilvl", new XAttribute(W + "val", ilvl.ToString())),
                    new XElement(W + "numId", new XAttribute(W + "val", numId)))),
            Run(text));
    }

    // bodyContent may be an XElement (single paragraph), XElement[] (multiple paragraphs),
    // or any other content accepted by the XElement constructor.
    private static byte[] Package(object bodyContent, XElement? numbering = null, XElement? rels = null)
    {
        var ms  = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // [Content_Types].xml (minimal)
            WriteEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                (numbering != null ? "<Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>" : "") +
                "</Types>");

            // _rels/.rels
            WriteEntry(zip, "_rels/.rels",
                "<?xml version=\"1.0\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                "</Relationships>");

            // word/document.xml
            var doc = new XDocument(
                new XElement(W + "document",
                    new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
                    new XElement(W + "body", bodyContent)));
            WriteEntry(zip, "word/document.xml", doc.ToString());

            // word/_rels/document.xml.rels
            var relsXml = rels ?? new XElement(Rels + "Relationships",
                new XAttribute("xmlns", Rels.NamespaceName));
            WriteEntry(zip, "word/_rels/document.xml.rels", relsXml.ToString());

            // word/numbering.xml (if needed)
            if (numbering != null)
                WriteEntry(zip, "word/numbering.xml", numbering.ToString());
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    // ── Public factory methods ────────────────────────────────────────────────

    public static byte[] WithHeading1(string text)
        => Package(Para("Heading1", Run(text)));

    public static byte[] WithHeading2(string text)
        => Package(Para("Heading2", Run(text)));

    public static byte[] WithBoldRun(string text)
        => Package(Para("", Run(text, bold: true)));

    public static byte[] WithItalicRun(string text)
        => Package(Para("", Run(text, italic: true)));

    public static byte[] WithBulletList(params string[] items)
    {
        var numbering = BuildNumbering(ordered: false);
        var paras     = items.Select(i => BulletPara(i)).ToArray<XElement>();
        return Package(paras, numbering);
    }

    public static byte[] WithOrderedList(params string[] items)
    {
        var numbering = BuildNumbering(ordered: true);
        var paras     = items.Select(i => BulletPara(i, ordered: true)).ToArray<XElement>();
        return Package(paras, numbering);
    }

    public static byte[] WithHyperlink(string display, string url, string rId = "rId100")
    {
        var relNs = Rels.NamespaceName;
        var rels  = new XElement(XName.Get("Relationships", relNs),
            new XElement(XName.Get("Relationship", relNs),
                new XAttribute("Id", rId),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                new XAttribute("Target", url),
                new XAttribute("TargetMode", "External")));

        var hyperlink = new XElement(W + "hyperlink",
            new XAttribute(R + "id", rId),
            Run(display));
        var para = new XElement(W + "p", hyperlink);
        return Package(para, rels: rels);
    }

    public static byte[] Empty()
        => Package(new XElement(W + "p"));

    // ── Numbering XML helpers ─────────────────────────────────────────────────

    private static readonly XNamespace Num = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static XElement BuildNumbering(bool ordered)
    {
        var numFmt = ordered ? "decimal" : "bullet";
        return new XElement(W + "numbering",
            new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName),
            new XElement(W + "abstractNum",
                new XAttribute(W + "abstractNumId", "0"),
                new XElement(W + "lvl",
                    new XAttribute(W + "ilvl", "0"),
                    new XElement(W + "numFmt", new XAttribute(W + "val", numFmt)))),
            new XElement(W + "num",
                new XAttribute(W + "numId", ordered ? "2" : "1"),
                new XElement(W + "abstractNumId", new XAttribute(W + "val", "0"))));
    }
}
