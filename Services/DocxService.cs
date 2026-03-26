using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Simply.JobApplication.Services;

public class DocxService
{
    private static readonly XNamespace W    = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R    = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string HyperlinkRelType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink";

    // ── DOCX → Markdown ───────────────────────────────────────────────────────
    // Converts the resume DOCX to Markdown, preserving structure, emphasis,
    // and hyperlinks. Used as the single resume representation for both
    // Turn 1 (evaluation) and Turn 2 (generation input).
    //
    // Style mapping:
    //   First non-empty paragraph     → title line (will become Title style)
    //   Paragraphs before first #     → subtitle lines (Subtitle style)
    //   # / ## / ###                  → Heading1 / Heading2 / Heading3
    //   Emphasis character style      → **...**
    //   Hyperlink r:id lookup         → [display](url)

    // Returns the page count stored in docProps/app.xml, or null if unavailable.
    // Word and LibreOffice update this value on every save; third-party converters may omit it.
    public int? GetPageCount(byte[] docxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry("docProps/app.xml");
        if (entry is null) return null;
        using var stream = entry.Open();
        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        var pages = XDocument.Load(stream).Root?.Element(ep + "Pages");
        return pages is not null && int.TryParse(pages.Value, out var n) && n > 0 ? n : null;
    }

    public string ExtractMarkdown(byte[] docxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);

        // Build rId → URL map from the relationship file.
        var hyperlinkUrls = new Dictionary<string, string>();
        var relsEntry = zip.GetEntry("word/_rels/document.xml.rels");
        if (relsEntry != null)
        {
            using var rs = relsEntry.Open();
            foreach (var rel in XDocument.Load(rs).Descendants(Rels + "Relationship"))
            {
                if ((rel.Attribute("Type")?.Value ?? "").EndsWith("/hyperlink"))
                {
                    var id  = rel.Attribute("Id")?.Value     ?? "";
                    var url = rel.Attribute("Target")?.Value ?? "";
                    if (!string.IsNullOrEmpty(id)) hyperlinkUrls[id] = url;
                }
            }
        }

        var docEntry = zip.GetEntry("word/document.xml")
                       ?? throw new InvalidOperationException("Not a valid DOCX file.");
        using var stream = docEntry.Open();
        var body = XDocument.Load(stream).Descendants(W + "body").FirstOrDefault()
                   ?? throw new InvalidOperationException("word/document.xml has no <w:body>.");

        var sb = new StringBuilder();
        bool firstNonEmpty    = true;
        bool beforeFirstHeading = true;

        foreach (var para in body.Elements(W + "p"))
        {
            var pStyle = para.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? "";

            string prefix = pStyle switch
            {
                "Heading1"              => "# ",
                "Heading2"              => "## ",
                "Heading3" or "Heading4" => "### ",
                _                       => ""
            };

            if (prefix.Length > 0) beforeFirstHeading = false;

            var inline = BuildInlineMarkdown(para, hyperlinkUrls);

            if (string.IsNullOrWhiteSpace(inline))
            {
                if (!firstNonEmpty) sb.AppendLine();
                continue;
            }

            if (!firstNonEmpty) sb.AppendLine();

            if (firstNonEmpty)
                sb.Append(inline);                       // Title
            else if (beforeFirstHeading && prefix == "")
                sb.Append(inline);                       // Subtitle
            else
                sb.Append(prefix + inline);              // Heading or body

            firstNonEmpty = false;
        }

        return sb.ToString().Trim();
    }

    private static string BuildInlineMarkdown(XElement para, Dictionary<string, string> hyperlinkUrls)
    {
        var sb = new StringBuilder();
        foreach (var child in para.Elements())
        {
            if (child.Name == W + "r")
            {
                var rPr    = child.Element(W + "rPr");
                var rStyle = rPr?.Element(W + "rStyle")?.Attribute(W + "val")?.Value ?? "";
                var isBold = rPr?.Element(W + "b") != null;
                var text   = string.Concat(child.Elements(W + "t").Select(t => t.Value));
                if (string.IsNullOrEmpty(text)) continue;

                if (rStyle == "Emphasis" || isBold)
                    sb.Append($"**{text}**");
                else
                    sb.Append(text);
            }
            else if (child.Name == W + "hyperlink")
            {
                var rId  = child.Attribute(R + "id")?.Value ?? "";
                var text = string.Concat(child.Descendants(W + "t").Select(t => t.Value));
                if (!string.IsNullOrEmpty(rId) && hyperlinkUrls.TryGetValue(rId, out var url))
                    sb.Append($"[{text}]({url})");
                else
                    sb.Append(text);
            }
        }
        return sb.ToString();
    }

    // ── Markdown → OOXML body fragment ────────────────────────────────────────
    // Converts the AI-generated tailored resume Markdown back to the OOXML
    // body content that goes inside <w:body>. Named paragraph styles from the
    // original document (preserved in styles.xml) are referenced by ID so the
    // visual appearance is identical to the original.
    //
    // Also outputs the hyperlink relationship map (rId → url) so the caller
    // can inject the entries into word/_rels/document.xml.rels.

    public string MarkdownToBodyXml(string markdown, out Dictionary<string, string> hyperlinkRels)
    {
        var urlToRId = new Dictionary<string, string>(); // url → rId (built during scan)
        var sb = new StringBuilder();

        bool firstNonEmpty      = true;
        bool beforeFirstHeading = true;

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append("<w:p/>");
                continue;
            }

            // Strip leading Markdown list markers the model may emit despite instructions.
            if (line.StartsWith("- ") || line.StartsWith("* "))
                line = line[2..];
            else if (line.StartsWith("• "))
                line = line[2..];

            string styleId;
            string content;

            if (line.StartsWith("#### ") || line.StartsWith("### "))
            {
                var prefixLen = line.StartsWith("#### ") ? 5 : 4;
                styleId = "Heading3";
                content = line[prefixLen..];
                beforeFirstHeading = false;
            }
            else if (line.StartsWith("## "))
            {
                styleId = "Heading2";
                content = line[3..];
                beforeFirstHeading = false;
            }
            else if (line.StartsWith("# "))
            {
                styleId = "Heading1";
                content = line[2..];
                beforeFirstHeading = false;
            }
            else if (firstNonEmpty)
            {
                styleId = "Title";
                content = line;
            }
            else if (beforeFirstHeading)
            {
                styleId = "Subtitle";
                content = line;
            }
            else
            {
                styleId = "";
                content = line;
            }

            firstNonEmpty = false;

            sb.Append("<w:p>");
            if (!string.IsNullOrEmpty(styleId))
                sb.Append($"<w:pPr><w:pStyle w:val=\"{styleId}\"/></w:pPr>");
            AppendInlineOoxml(sb, content, urlToRId);
            sb.Append("</w:p>");
        }

        // Return rId→url for the caller to inject into the rels file.
        hyperlinkRels = urlToRId.ToDictionary(kv => kv.Value, kv => kv.Key);
        return sb.ToString();
    }

    // Regex matches **emphasis** (group 1) and [text](url) (groups 2+3).
    private static readonly Regex InlinePattern = new(
        @"\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)]+)\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static void AppendInlineOoxml(StringBuilder sb, string line, Dictionary<string, string> urlToRId)
    {
        int pos = 0;
        foreach (Match m in InlinePattern.Matches(line))
        {
            if (m.Index > pos)
                AppendPlainRun(sb, line[pos..m.Index]);

            if (m.Groups[1].Success) // **emphasis**
            {
                sb.Append("<w:r><w:rPr><w:rStyle w:val=\"Emphasis\"/></w:rPr>");
                sb.Append($"<w:t xml:space=\"preserve\">{XmlEsc(m.Groups[1].Value)}</w:t></w:r>");
            }
            else // [text](url)
            {
                var linkText = m.Groups[2].Value;
                var url      = m.Groups[3].Value;
                if (!urlToRId.TryGetValue(url, out var rId))
                {
                    rId = $"rId_md_{urlToRId.Count + 1}";
                    urlToRId[url] = rId;
                }
                sb.Append($"<w:hyperlink r:id=\"{rId}\">");
                sb.Append("<w:r><w:rPr><w:rStyle w:val=\"Hyperlink\"/></w:rPr>");
                sb.Append($"<w:t>{XmlEsc(linkText)}</w:t></w:r></w:hyperlink>");
            }

            pos = m.Index + m.Length;
        }

        if (pos < line.Length)
            AppendPlainRun(sb, line[pos..]);
    }

    private static void AppendPlainRun(StringBuilder sb, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        sb.Append($"<w:r><w:t xml:space=\"preserve\">{XmlEsc(text)}</w:t></w:r>");
    }

    // ── Tailored resume DOCX ──────────────────────────────────────────────────
    // Clones the original ZIP, replaces word/document.xml with content built
    // from the AI's Markdown output, and merges new hyperlink relationships
    // into word/_rels/document.xml.rels.

    public byte[] GenerateResumeDocx(byte[] originalDocxBytes, string resumeMarkdown)
    {
        string originalDocXml;
        string originalRelsXml;

        using (var zip = new ZipArchive(new MemoryStream(originalDocxBytes), ZipArchiveMode.Read))
        {
            var docEntry = zip.GetEntry("word/document.xml")
                           ?? throw new InvalidOperationException("Not a valid DOCX file.");
            using (var s = docEntry.Open())
            using (var r = new StreamReader(s, Encoding.UTF8))
                originalDocXml = r.ReadToEnd();

            var relsEntry = zip.GetEntry("word/_rels/document.xml.rels");
            if (relsEntry != null)
            {
                using var s = relsEntry.Open();
                using var r = new StreamReader(s, Encoding.UTF8);
                originalRelsXml = r.ReadToEnd();
            }
            else
                originalRelsXml = MinimalWordRels();
        }

        var docOpenTag   = ExtractDocumentOpenTag(originalDocXml);
        var sectPrXml    = ExtractSectPrXml(originalDocXml);
        var bodyContent  = MarkdownToBodyXml(resumeMarkdown, out var newHyperlinkRels);
        var newDocXml    = docOpenTag + "<w:body>" + bodyContent + (sectPrXml ?? "") + "</w:body></w:document>";
        var updatedRelsXml = MergeHyperlinkRels(originalRelsXml, newHyperlinkRels);

        var outputMs = new MemoryStream();
        using (var readZip  = new ZipArchive(new MemoryStream(originalDocxBytes), ZipArchiveMode.Read))
        using (var writeZip = new ZipArchive(outputMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            bool relsWritten = false;
            foreach (var entry in readZip.Entries)
            {
                var dest = writeZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                if (entry.FullName == "word/document.xml")
                {
                    using var s = dest.Open();
                    s.Write(Encoding.UTF8.GetBytes(newDocXml));
                }
                else if (entry.FullName == "word/_rels/document.xml.rels")
                {
                    relsWritten = true;
                    using var s = dest.Open();
                    s.Write(Encoding.UTF8.GetBytes(updatedRelsXml));
                }
                else
                {
                    using var src = entry.Open();
                    using var s   = dest.Open();
                    src.CopyTo(s);
                }
            }

            // Create the rels file if the original didn't have one.
            if (!relsWritten)
            {
                var e = writeZip.CreateEntry("word/_rels/document.xml.rels", CompressionLevel.Optimal);
                using var s = e.Open();
                s.Write(Encoding.UTF8.GetBytes(updatedRelsXml));
            }
        }

        return outputMs.ToArray();
    }

    // ── Cover letter DOCX (minimal new document) ──────────────────────────────

    public byte[] GenerateCoverLetterDocx(string coverLetterText)
    {
        var paragraphs = coverLetterText.Split(
            new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var outputMs = new MemoryStream();
        using (var zip = new ZipArchive(outputMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", MinimalContentTypes());
            WriteEntry(zip, "_rels/.rels",          MinimalRels());
            WriteEntry(zip, "word/_rels/document.xml.rels", MinimalWordRels());
            WriteEntry(zip, "word/document.xml",    BuildCoverLetterXml(paragraphs));
        }

        return outputMs.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Merges new hyperlink relationship entries into the existing rels XML.
    private static string MergeHyperlinkRels(string relsXml, Dictionary<string, string> hyperlinkRels)
    {
        if (hyperlinkRels.Count == 0) return relsXml;
        try
        {
            var doc  = XDocument.Parse(relsXml);
            var root = doc.Root!;
            var existing = new HashSet<string>(
                root.Elements(Rels + "Relationship").Select(r => r.Attribute("Id")?.Value ?? ""));

            foreach (var (rId, url) in hyperlinkRels)
            {
                if (!existing.Contains(rId))
                    root.Add(new XElement(Rels + "Relationship",
                        new XAttribute("Id",         rId),
                        new XAttribute("Type",       HyperlinkRelType),
                        new XAttribute("Target",     url),
                        new XAttribute("TargetMode", "External")));
            }

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   doc.Root!.ToString(SaveOptions.DisableFormatting);
        }
        catch { return relsXml; }
    }

    // Returns everything up through and including the closing '>' of <w:document ...>,
    // preserving all namespace declarations by using a quote-aware character scan.
    private static string ExtractDocumentOpenTag(string xml)
    {
        int start = 0;
        if (xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            start = xml.IndexOf("?>", StringComparison.Ordinal) + 2;

        bool inQuote  = false;
        char quoteChar = '"';
        for (int i = start; i < xml.Length; i++)
        {
            char c = xml[i];
            if (!inQuote && (c == '"' || c == '\'')) { inQuote = true; quoteChar = c; }
            else if (inQuote && c == quoteChar)        inQuote = false;
            else if (!inQuote && c == '>')             return xml[..(i + 1)];
        }
        return xml;
    }

    private static string? ExtractSectPrXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml)
                .Descendants(W + "sectPr").FirstOrDefault()
                ?.ToString(SaveOptions.DisableFormatting);
        }
        catch { return null; }
    }

    private static string BuildCoverLetterXml(string[] paragraphs)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">");
        sb.Append("<w:body>");

        foreach (var para in paragraphs)
        {
            sb.Append("<w:p>");
            var lines = para.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append("<w:r><w:br/></w:r>");
                sb.Append($"<w:r><w:t xml:space=\"preserve\">{XmlEsc(lines[i].Trim())}</w:t></w:r>");
            }
            sb.Append("</w:p>");
            sb.Append("<w:p/>");
        }

        sb.Append("</w:body></w:document>");
        return sb.ToString();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static string XmlEsc(string s) =>
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\"", "&quot;")
         .Replace("'", "&apos;");

    private static string MinimalContentTypes() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "</Types>";

    private static string MinimalRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private static string MinimalWordRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>";
}
