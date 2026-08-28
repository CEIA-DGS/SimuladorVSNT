using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>What kind of value a <see cref="YamlNode"/> holds.</summary>
    public enum YamlNodeKind
    {
        /// <summary>A single value: text, number, boolean.</summary>
        Scalar,

        /// <summary>An ordered set of key/value pairs.</summary>
        Mapping,

        /// <summary>An ordered list of values.</summary>
        Sequence
    }

    /// <summary>
    /// Raised when a configuration file does not follow the supported YAML subset.
    /// The message always carries the line number, so a broken test suite is easy to fix.
    /// </summary>
    public class YamlParseException : Exception
    {
        /// <summary>Creates the exception with an already formatted message.</summary>
        /// <param name="message">Description of the problem, including the line number.</param>
        public YamlParseException(string message) : base(message) { }
    }

    /// <summary>
    /// One node of a parsed configuration document, plus the typed accessors the
    /// scenario schema needs (numbers, booleans, vectors, colours, enums).
    ///
    /// Reading is deliberately forgiving: every accessor takes a fallback and returns it
    /// when the key is absent, so a test file only has to declare what differs from the
    /// defaults. Values that are present but malformed are NOT forgiven — they throw,
    /// because silently running a different test than the one written in the file is the
    /// worst possible outcome for a benchmark.
    /// </summary>
    public class YamlNode
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        readonly List<YamlNode> items = new();
        readonly List<KeyValuePair<string, YamlNode>> entries = new();
        string scalar = "";

        /// <summary>What this node holds.</summary>
        public YamlNodeKind Kind { get; private set; }

        /// <summary>Line of the source file this node came from, used in error messages.</summary>
        public int Line { get; internal set; }

        /// <summary>
        /// For sequences: whether it was written inline (<c>[1, 2]</c>) instead of as a
        /// block of dashes. Kept so a file re-written by the exporter keeps the compact
        /// form for coordinate pairs.
        /// </summary>
        public bool Flow { get; set; }

        /// <summary>Items of a sequence node. Empty for other kinds.</summary>
        public IReadOnlyList<YamlNode> Items => items;

        /// <summary>Key/value pairs of a mapping node, in file order. Empty for other kinds.</summary>
        public IReadOnlyList<KeyValuePair<string, YamlNode>> Entries => entries;

        /// <summary>Number of items (sequence) or entries (mapping); 0 for a scalar.</summary>
        public int Count => Kind == YamlNodeKind.Sequence ? items.Count : entries.Count;

        // ---------------- construction ----------------

        /// <summary>Creates a scalar node.</summary>
        /// <param name="value">The raw text value.</param>
        /// <param name="line">Source line, for error messages.</param>
        /// <returns>The new node.</returns>
        public static YamlNode NewScalar(string value, int line = 0)
        {
            return new YamlNode { Kind = YamlNodeKind.Scalar, scalar = value ?? "", Line = line };
        }

        /// <summary>Creates an empty mapping node.</summary>
        /// <param name="line">Source line, for error messages.</param>
        /// <returns>The new node.</returns>
        public static YamlNode NewMapping(int line = 0)
        {
            return new YamlNode { Kind = YamlNodeKind.Mapping, Line = line };
        }

        /// <summary>Creates an empty sequence node.</summary>
        /// <param name="flow">True to write it inline as <c>[a, b]</c>.</param>
        /// <param name="line">Source line, for error messages.</param>
        /// <returns>The new node.</returns>
        public static YamlNode NewSequence(bool flow = false, int line = 0)
        {
            return new YamlNode { Kind = YamlNodeKind.Sequence, Flow = flow, Line = line };
        }

        /// <summary>Appends an item to a sequence node.</summary>
        /// <param name="item">The item to append.</param>
        public void AddItem(YamlNode item)
        {
            items.Add(item);
        }

        /// <summary>Adds a key/value pair to a mapping node.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value node.</param>
        public void Add(string key, YamlNode value)
        {
            entries.Add(new KeyValuePair<string, YamlNode>(key, value));
        }

        /// <summary>Adds a text entry.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The text value.</param>
        public void Add(string key, string value) => Add(key, NewScalar(value));

        /// <summary>Adds a numeric entry, formatted without locale surprises.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The number.</param>
        public void Add(string key, float value) => Add(key, NewScalar(Format(value)));

        /// <summary>Adds an integer entry.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The number.</param>
        public void Add(string key, int value) => Add(key, NewScalar(value.ToString(Invariant)));

        /// <summary>Adds a boolean entry.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The flag.</param>
        public void Add(string key, bool value) => Add(key, NewScalar(value ? "true" : "false"));

        /// <summary>Adds a coordinate pair, written inline as <c>[x, y]</c>.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The pair.</param>
        public void Add(string key, Vector2 value) => Add(key, FromVector2(value));

        /// <summary>Adds a colour, written as an HTML hex string.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The colour.</param>
        public void Add(string key, Color value)
        {
            Add(key, NewScalar("#" + ColorUtility.ToHtmlStringRGB(value)));
        }

        /// <summary>Builds an inline <c>[x, y]</c> node from a coordinate pair.</summary>
        /// <param name="value">The pair.</param>
        /// <returns>The new sequence node.</returns>
        public static YamlNode FromVector2(Vector2 value)
        {
            var node = NewSequence(flow: true);
            node.AddItem(NewScalar(Format(value.x)));
            node.AddItem(NewScalar(Format(value.y)));
            return node;
        }

        /// <summary>Builds a block sequence of inline coordinate pairs.</summary>
        /// <param name="points">The pairs, in order.</param>
        /// <returns>The new sequence node.</returns>
        public static YamlNode FromVector2List(IEnumerable<Vector2> points)
        {
            var node = NewSequence();
            if (points != null)
                foreach (Vector2 point in points)
                    node.AddItem(FromVector2(point));
            return node;
        }

        /// <summary>Formats a number the same way in every locale, without trailing zeros.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The formatted text.</returns>
        public static string Format(float value)
        {
            return value.ToString("0.######", Invariant);
        }

        // ---------------- lookup ----------------

        /// <summary>
        /// Returns the value stored under the first key that exists, or null. Accepting
        /// several names lets a file keep working after a key is renamed.
        /// </summary>
        /// <param name="keys">Key names to try, in order of preference.</param>
        /// <returns>The value node, or null when none of the keys is present.</returns>
        public YamlNode Child(params string[] keys)
        {
            if (Kind != YamlNodeKind.Mapping) return null;

            foreach (string key in keys)
                foreach (var entry in entries)
                    if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;

            return null;
        }

        /// <summary>Returns the value under a key, or throws when it is missing.</summary>
        /// <param name="key">The required key.</param>
        /// <returns>The value node.</returns>
        public YamlNode Require(string key)
        {
            YamlNode child = Child(key);
            if (child == null)
                throw new YamlParseException($"Linha {Line}: falta a chave obrigatória '{key}'.");
            return child;
        }

        // ---------------- typed reads ----------------

        /// <summary>The raw text of a scalar node.</summary>
        public string Text => Kind == YamlNodeKind.Scalar ? scalar : "";

        /// <summary>Reads this node as text.</summary>
        /// <param name="fallback">Value returned when the node is not a scalar.</param>
        /// <returns>The text.</returns>
        public string AsString(string fallback = "")
        {
            return Kind == YamlNodeKind.Scalar ? scalar : fallback;
        }

        /// <summary>Reads this node as a number.</summary>
        /// <param name="fallback">Unused placeholder kept for symmetry; malformed numbers throw.</param>
        /// <returns>The number.</returns>
        public float AsFloat(float fallback = 0f)
        {
            if (Kind != YamlNodeKind.Scalar) return fallback;
            if (float.TryParse(scalar, NumberStyles.Float, Invariant, out float value)) return value;
            throw new YamlParseException($"Linha {Line}: '{scalar}' não é um número.");
        }

        /// <summary>Reads this node as an integer.</summary>
        /// <param name="fallback">Value returned when the node is not a scalar.</param>
        /// <returns>The number.</returns>
        public int AsInt(int fallback = 0)
        {
            if (Kind != YamlNodeKind.Scalar) return fallback;
            if (int.TryParse(scalar, NumberStyles.Integer, Invariant, out int value)) return value;
            throw new YamlParseException($"Linha {Line}: '{scalar}' não é um número inteiro.");
        }

        /// <summary>Reads this node as a boolean, accepting the usual YAML spellings.</summary>
        /// <param name="fallback">Value returned when the node is not a scalar.</param>
        /// <returns>The flag.</returns>
        public bool AsBool(bool fallback = false)
        {
            if (Kind != YamlNodeKind.Scalar) return fallback;

            switch (scalar.Trim().ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": return true;
                case "false": case "no": case "off": case "0": return false;
            }
            throw new YamlParseException($"Linha {Line}: '{scalar}' não é verdadeiro/falso.");
        }

        /// <summary>Reads this node as a coordinate pair written as <c>[x, y]</c>.</summary>
        /// <param name="fallback">Value returned when the node is absent.</param>
        /// <returns>The pair.</returns>
        public Vector2 AsVector2(Vector2 fallback)
        {
            if (Kind != YamlNodeKind.Sequence)
                throw new YamlParseException($"Linha {Line}: esperava um par no formato [x, y].");
            if (items.Count != 2)
                throw new YamlParseException($"Linha {Line}: um par precisa de exatamente 2 números, veio {items.Count}.");

            return new Vector2(items[0].AsFloat(fallback.x), items[1].AsFloat(fallback.y));
        }

        /// <summary>Reads this node as a list of coordinate pairs.</summary>
        /// <returns>The pairs, in file order.</returns>
        public List<Vector2> AsVector2List()
        {
            var list = new List<Vector2>();
            if (Kind != YamlNodeKind.Sequence)
                throw new YamlParseException($"Linha {Line}: esperava uma lista de pares [x, y].");

            foreach (YamlNode item in items)
                list.Add(item.AsVector2(Vector2.zero));

            return list;
        }

        /// <summary>Reads this node as an HTML colour such as <c>#8C8C94</c>.</summary>
        /// <param name="fallback">Value returned when the node is absent.</param>
        /// <returns>The colour.</returns>
        public Color AsColor(Color fallback)
        {
            if (Kind == YamlNodeKind.Sequence)
            {
                if (items.Count < 3)
                    throw new YamlParseException($"Linha {Line}: uma cor em lista precisa de [r, g, b].");
                return new Color(items[0].AsFloat(), items[1].AsFloat(), items[2].AsFloat());
            }

            string text = scalar.Trim();
            if (!text.StartsWith("#")) text = "#" + text;
            if (ColorUtility.TryParseHtmlString(text, out Color color)) return color;

            throw new YamlParseException($"Linha {Line}: '{scalar}' não é uma cor (use #RRGGBB).");
        }

        /// <summary>Reads this node as one of the values of an enum, ignoring case.</summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="fallback">Value returned when the node is absent.</param>
        /// <returns>The enum value.</returns>
        public T AsEnum<T>(T fallback) where T : struct, Enum
        {
            if (Kind != YamlNodeKind.Scalar) return fallback;
            if (Enum.TryParse(scalar.Trim(), true, out T value)) return value;

            throw new YamlParseException(
                $"Linha {Line}: '{scalar}' não é um valor de {typeof(T).Name}. " +
                $"Valores aceitos: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }

        // ---------------- typed reads by key ----------------

        /// <summary>Reads a text entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The text.</returns>
        public string GetString(string key, string fallback = "") => Child(key)?.AsString(fallback) ?? fallback;

        /// <summary>Reads a numeric entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The number.</returns>
        public float GetFloat(string key, float fallback) => Child(key)?.AsFloat(fallback) ?? fallback;

        /// <summary>Reads an integer entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The number.</returns>
        public int GetInt(string key, int fallback) => Child(key)?.AsInt(fallback) ?? fallback;

        /// <summary>Reads a boolean entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The flag.</returns>
        public bool GetBool(string key, bool fallback) => Child(key)?.AsBool(fallback) ?? fallback;

        /// <summary>Reads a coordinate pair entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The pair.</returns>
        public Vector2 GetVector2(string key, Vector2 fallback) => Child(key)?.AsVector2(fallback) ?? fallback;

        /// <summary>Reads a colour entry, or the fallback when the key is absent.</summary>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The colour.</returns>
        public Color GetColor(string key, Color fallback) => Child(key)?.AsColor(fallback) ?? fallback;

        /// <summary>Reads an enum entry, or the fallback when the key is absent.</summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="fallback">Value used when the key is absent.</param>
        /// <returns>The enum value.</returns>
        public T GetEnum<T>(string key, T fallback) where T : struct, Enum
            => Child(key)?.AsEnum(fallback) ?? fallback;
    }

    /// <summary>
    /// A small reader and writer for the subset of YAML the test bench configuration
    /// needs. It exists so scenarios can be declared in plain text files without adding
    /// an external dependency to the project: the only YAML library reachable from here
    /// ships inside the Visual Scripting package, is editor-only and lives in an internal
    /// namespace, so depending on it would break builds.
    ///
    /// Supported: block mappings, block sequences, inline sequences (<c>[1, 2]</c>),
    /// quoted and plain scalars, multi-line text blocks (<c>&gt;</c> and <c>|</c>),
    /// <c>#</c> comments, blank lines and <c>---</c> markers.
    ///
    /// Not supported (and rejected with a line number): inline mappings (<c>{a: 1}</c>),
    /// anchors, aliases, tags and tabs used for indentation. Blank lines inside a text
    /// block are dropped, so a block holds a single paragraph.
    ///
    /// Writing always produces the plain single-line form, never a text block: a document
    /// read and written back keeps every value, but not necessarily its original layout.
    /// </summary>
    public static class YamlLite
    {
        /// <summary>
        /// Parses a document into a node tree.
        /// </summary>
        /// <param name="text">The whole file contents.</param>
        /// <returns>The root node; an empty mapping when the document has no content.</returns>
        public static YamlNode Parse(string text)
        {
            var reader = new Reader(text);
            return reader.ParseDocument();
        }

        /// <summary>
        /// Writes a node tree back to text, in the same style the parser reads.
        /// </summary>
        /// <param name="root">The root node.</param>
        /// <returns>The formatted document.</returns>
        public static string Write(YamlNode root)
        {
            var sb = new StringBuilder();
            foreach (string line in RenderBlock(root, 0))
                sb.AppendLine(line);
            return sb.ToString();
        }

        // ---------------- writing ----------------

        /// <summary>Renders a node as a list of already indented lines.</summary>
        /// <param name="node">The node to render.</param>
        /// <param name="indent">Indentation column for this block.</param>
        /// <returns>The rendered lines.</returns>
        static List<string> RenderBlock(YamlNode node, int indent)
        {
            var lines = new List<string>();
            string pad = new string(' ', indent);

            if (node == null) return lines;

            if (node.Kind == YamlNodeKind.Scalar)
            {
                lines.Add(pad + Quote(node.Text));
                return lines;
            }

            if (node.Kind == YamlNodeKind.Sequence)
            {
                if (node.Flow || node.Count == 0)
                {
                    lines.Add(pad + RenderFlow(node));
                    return lines;
                }

                foreach (YamlNode item in node.Items)
                {
                    if (IsInline(item))
                    {
                        lines.Add(pad + "- " + RenderInline(item));
                        continue;
                    }

                    // A mapping item is written with its first key on the dash line, which
                    // is the shape people expect to read and to type.
                    List<string> itemLines = RenderBlock(item, indent + 2);
                    if (itemLines.Count == 0) continue;

                    itemLines[0] = pad + "- " + itemLines[0].TrimStart();
                    lines.AddRange(itemLines);
                }
                return lines;
            }

            foreach (var entry in node.Entries)
            {
                YamlNode value = entry.Value;

                if (IsInline(value))
                {
                    lines.Add($"{pad}{entry.Key}: {RenderInline(value)}");
                    continue;
                }

                // An empty mapping has nothing to write and no inline form here, so the
                // key is dropped entirely rather than producing an unreadable document.
                if (value.Kind == YamlNodeKind.Mapping && value.Count == 0) continue;

                lines.Add($"{pad}{entry.Key}:");
                lines.AddRange(RenderBlock(value, indent + 2));
            }

            return lines;
        }

        /// <summary>True when the node fits on the same line as its key or dash.</summary>
        /// <param name="node">The node to test.</param>
        /// <returns>True for scalars, inline sequences and empty sequences.</returns>
        static bool IsInline(YamlNode node)
        {
            if (node.Kind == YamlNodeKind.Scalar) return true;
            return node.Kind == YamlNodeKind.Sequence && (node.Flow || node.Count == 0);
        }

        /// <summary>Renders a node that fits on one line.</summary>
        /// <param name="node">The node to render.</param>
        /// <returns>The inline text.</returns>
        static string RenderInline(YamlNode node)
        {
            return node.Kind == YamlNodeKind.Scalar ? Quote(node.Text) : RenderFlow(node);
        }

        /// <summary>Renders a sequence in the inline <c>[a, b]</c> form.</summary>
        /// <param name="node">The sequence node.</param>
        /// <returns>The inline text.</returns>
        static string RenderFlow(YamlNode node)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < node.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RenderInline(node.Items[i]));
            }
            return sb.Append(']').ToString();
        }

        /// <summary>
        /// Quotes a value when leaving it bare would make the parser read something else.
        /// </summary>
        /// <param name="value">The raw text.</param>
        /// <returns>The text, quoted when needed.</returns>
        static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            bool risky =
                value != value.Trim() ||
                value.Contains(": ") ||
                value.EndsWith(":") ||
                value.Contains(" #") ||
                value.Contains("\n") ||
                value.Contains("\"") ||
                "-[]{}#&*!|>%@`,'\"".IndexOf(value[0]) >= 0;

            if (!risky) return value;

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // ---------------- reading ----------------

        /// <summary>One meaningful line of the source, already stripped of comments.</summary>
        sealed class Line
        {
            /// <summary>Column where the content starts.</summary>
            public int Indent;

            /// <summary>Content, without indentation or trailing comment.</summary>
            public string Text;

            /// <summary>
            /// Content without indentation but WITH anything that looks like a comment.
            /// Inside a block scalar a '#' is ordinary text, so the raw form is the one
            /// that must be used there.
            /// </summary>
            public string Raw;

            /// <summary>Line number in the original file, 1-based.</summary>
            public int Number;
        }

        /// <summary>Recursive-descent reader over the meaningful lines of a document.</summary>
        sealed class Reader
        {
            readonly List<Line> lines = new();
            int cursor;

            /// <summary>Splits the text into meaningful lines, dropping comments and blanks.</summary>
            /// <param name="text">The whole file contents.</param>
            public Reader(string text)
            {
                string[] raw = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

                for (int i = 0; i < raw.Length; i++)
                {
                    string line = raw[i];
                    int indent = 0;
                    while (indent < line.Length && line[indent] == ' ') indent++;

                    if (indent < line.Length && line[indent] == '\t')
                        throw new YamlParseException(
                            $"Linha {i + 1}: YAML não aceita TAB na indentação — use espaços.");

                    string body = line.Substring(indent).TrimEnd();
                    string content = StripComment(body).TrimEnd();
                    if (content.Length == 0) continue;
                    if (content == "---" || content == "...") continue;

                    lines.Add(new Line { Indent = indent, Text = content, Raw = body, Number = i + 1 });
                }
            }

            /// <summary>Parses the whole document.</summary>
            /// <returns>The root node, or an empty mapping for an empty document.</returns>
            public YamlNode ParseDocument()
            {
                if (lines.Count == 0) return YamlNode.NewMapping(1);

                YamlNode root = ParseBlock(lines[0].Indent);

                if (cursor < lines.Count)
                    throw new YamlParseException(
                        $"Linha {lines[cursor].Number}: indentação inesperada em '{lines[cursor].Text}'.");

                return root;
            }

            /// <summary>Parses a block whose first line sits at the given indentation.</summary>
            /// <param name="indent">Indentation column of the block.</param>
            /// <returns>The parsed node.</returns>
            YamlNode ParseBlock(int indent)
            {
                Line first = lines[cursor];

                if (IsSequenceItem(first.Text)) return ParseSequence(indent);
                if (FindKeySeparator(first.Text) >= 0) return ParseMapping(indent);

                cursor++;
                return ParseValue(first.Text, first.Number);
            }

            /// <summary>Parses a block sequence.</summary>
            /// <param name="indent">Indentation column of the dashes.</param>
            /// <returns>The sequence node.</returns>
            YamlNode ParseSequence(int indent)
            {
                YamlNode node = YamlNode.NewSequence(false, lines[cursor].Number);

                while (cursor < lines.Count)
                {
                    Line line = lines[cursor];
                    if (line.Indent < indent) break;
                    if (line.Indent > indent)
                        throw new YamlParseException(
                            $"Linha {line.Number}: indentação inesperada em '{line.Text}'.");
                    if (!IsSequenceItem(line.Text)) break;

                    string rest = line.Text.Length > 1 ? line.Text.Substring(1) : "";
                    int lead = 0;
                    while (lead < rest.Length && rest[lead] == ' ') lead++;
                    string content = rest.Trim();

                    if (content.Length == 0)
                    {
                        // A bare dash: the item is the indented block that follows it.
                        cursor++;
                        if (cursor < lines.Count && lines[cursor].Indent > indent)
                            node.AddItem(ParseBlock(lines[cursor].Indent));
                        else
                            node.AddItem(YamlNode.NewScalar("", line.Number));
                        continue;
                    }

                    // The content after the dash is the first line of the item's own block,
                    // sitting at the column where it was actually written.
                    int contentIndent = line.Indent + 1 + lead;
                    lines[cursor] = new Line
                    {
                        Indent = contentIndent,
                        Text = content,
                        Raw = line.Raw.Substring(1).Trim(),
                        Number = line.Number
                    };
                    node.AddItem(ParseBlock(contentIndent));
                }

                return node;
            }

            /// <summary>Parses a block mapping.</summary>
            /// <param name="indent">Indentation column of the keys.</param>
            /// <returns>The mapping node.</returns>
            YamlNode ParseMapping(int indent)
            {
                YamlNode node = YamlNode.NewMapping(lines[cursor].Number);

                while (cursor < lines.Count)
                {
                    Line line = lines[cursor];
                    if (line.Indent < indent) break;
                    if (line.Indent > indent)
                        throw new YamlParseException(
                            $"Linha {line.Number}: indentação inesperada em '{line.Text}'.");
                    if (IsSequenceItem(line.Text)) break;

                    int separator = FindKeySeparator(line.Text);
                    if (separator < 0)
                        throw new YamlParseException(
                            $"Linha {line.Number}: esperava 'chave: valor', veio '{line.Text}'.");

                    string key = Unquote(line.Text.Substring(0, separator).Trim(), line.Number);
                    string inlineValue = line.Text.Substring(separator + 1).Trim();
                    cursor++;

                    YamlNode value;
                    if (IsBlockScalarHeader(inlineValue))
                    {
                        value = ParseBlockScalar(inlineValue, indent, line.Number);
                    }
                    else if (inlineValue.Length > 0)
                    {
                        value = ParseValue(inlineValue, line.Number);
                    }
                    else if (cursor < lines.Count && HasNestedBlock(indent))
                    {
                        value = ParseBlock(lines[cursor].Indent);
                    }
                    else
                    {
                        value = YamlNode.NewScalar("", line.Number);
                    }

                    node.Add(key, value);
                }

                return node;
            }

            /// <summary>
            /// True when the lines after a bare key belong to it. A nested mapping is
            /// indented further; a sequence is allowed to sit at the key's own column,
            /// which is how most people write lists.
            /// </summary>
            /// <param name="keyIndent">Indentation column of the key.</param>
            /// <returns>True when the following block is the key's value.</returns>
            bool HasNestedBlock(int keyIndent)
            {
                Line next = lines[cursor];
                if (next.Indent > keyIndent) return true;
                return next.Indent == keyIndent && IsSequenceItem(next.Text);
            }

            /// <summary>
            /// True when a value is one of the markers that open a multi-line text block:
            /// '&gt;' folds the following lines into one paragraph, '|' keeps the line
            /// breaks. Both accept the '-' and '+' chomping suffixes.
            /// </summary>
            /// <param name="value">The text written after the colon.</param>
            /// <returns>True when a text block follows.</returns>
            static bool IsBlockScalarHeader(string value)
            {
                if (value.Length == 0 || value.Length > 2) return false;
                if (value[0] != '>' && value[0] != '|') return false;
                return value.Length == 1 || value[1] == '-' || value[1] == '+';
            }

            /// <summary>
            /// Reads a multi-line text block: every following line indented past the key.
            /// This is what lets a scenario description be written across several lines
            /// instead of one very long one.
            ///
            /// Blank lines inside the block are dropped, so a block cannot hold paragraph
            /// breaks — descriptions do not need them, and keeping them would mean giving
            /// up the simple line preprocessing the rest of the reader relies on.
            /// </summary>
            /// <param name="header">The marker written after the colon.</param>
            /// <param name="keyIndent">Indentation column of the key that owns the block.</param>
            /// <param name="lineNumber">Source line, for error messages.</param>
            /// <returns>The scalar node holding the joined text.</returns>
            YamlNode ParseBlockScalar(string header, int keyIndent, int lineNumber)
            {
                bool folded = header[0] == '>';
                var sb = new StringBuilder();
                bool first = true;

                while (cursor < lines.Count && lines[cursor].Indent > keyIndent)
                {
                    if (!first) sb.Append(folded ? ' ' : '\n');

                    // The raw form is used on purpose: inside a text block a '#' is part of
                    // the sentence, not the start of a comment.
                    sb.Append(lines[cursor].Raw.Trim());
                    first = false;
                    cursor++;
                }

                return YamlNode.NewScalar(sb.ToString(), lineNumber);
            }

            /// <summary>Parses a value written on a single line.</summary>
            /// <param name="text">The trimmed value text.</param>
            /// <param name="lineNumber">Source line, for error messages.</param>
            /// <returns>A scalar node, or a sequence node for the inline form.</returns>
            static YamlNode ParseValue(string text, int lineNumber)
            {
                if (text.StartsWith("[")) return ParseFlowSequence(text, lineNumber);

                if (text.StartsWith("{"))
                    throw new YamlParseException(
                        $"Linha {lineNumber}: mapa em linha ({{...}}) não é suportado — " +
                        "escreva as chaves em linhas indentadas.");

                return YamlNode.NewScalar(Unquote(text, lineNumber), lineNumber);
            }

            /// <summary>Parses an inline sequence such as <c>[1, 2]</c>, including nested ones.</summary>
            /// <param name="text">The value text, starting with '['.</param>
            /// <param name="lineNumber">Source line, for error messages.</param>
            /// <returns>The sequence node.</returns>
            static YamlNode ParseFlowSequence(string text, int lineNumber)
            {
                if (!text.EndsWith("]"))
                    throw new YamlParseException($"Linha {lineNumber}: falta fechar ']' em '{text}'.");

                YamlNode node = YamlNode.NewSequence(true, lineNumber);
                string inner = text.Substring(1, text.Length - 2).Trim();
                if (inner.Length == 0) return node;

                int depth = 0;
                char quote = '\0';
                int start = 0;

                for (int i = 0; i < inner.Length; i++)
                {
                    char c = inner[i];

                    if (quote != '\0')
                    {
                        if (c == '\\' && quote == '"') i++;
                        else if (c == quote) quote = '\0';
                        continue;
                    }

                    if (c == '"' || c == '\'') quote = c;
                    else if (c == '[') depth++;
                    else if (c == ']') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        node.AddItem(ParseValue(inner.Substring(start, i - start).Trim(), lineNumber));
                        start = i + 1;
                    }
                }

                node.AddItem(ParseValue(inner.Substring(start).Trim(), lineNumber));
                return node;
            }

            /// <summary>True when a line starts a sequence item.</summary>
            /// <param name="text">The trimmed line content.</param>
            /// <returns>True for "-" alone or a line starting with "- ".</returns>
            static bool IsSequenceItem(string text)
            {
                return text == "-" || text.StartsWith("- ");
            }

            /// <summary>
            /// Finds the colon that separates a key from its value: the first one outside
            /// quotes and brackets that is followed by a space or ends the line. Requiring
            /// the space is what keeps values like <c>http://host</c> from being split.
            /// </summary>
            /// <param name="text">The trimmed line content.</param>
            /// <returns>Index of the separator, or -1 when the line has no key.</returns>
            static int FindKeySeparator(string text)
            {
                char quote = '\0';
                int depth = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];

                    if (quote != '\0')
                    {
                        if (c == '\\' && quote == '"') i++;
                        else if (c == quote) quote = '\0';
                        continue;
                    }

                    if (c == '"' || c == '\'') quote = c;
                    else if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}') depth--;
                    else if (c == ':' && depth == 0 && (i + 1 >= text.Length || text[i + 1] == ' '))
                        return i;
                }

                return -1;
            }

            /// <summary>Removes a trailing comment, ignoring '#' inside quotes.</summary>
            /// <param name="text">The line content, without indentation.</param>
            /// <returns>The content up to the comment.</returns>
            static string StripComment(string text)
            {
                char quote = '\0';

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];

                    if (quote != '\0')
                    {
                        if (c == '\\' && quote == '"') i++;
                        else if (c == quote) quote = '\0';
                        continue;
                    }

                    if (c == '"' || c == '\'') quote = c;

                    // Only a '#' that starts the line or follows a space opens a comment;
                    // otherwise a value such as a colour (#8C8C94) would be cut in half.
                    else if (c == '#' && (i == 0 || text[i - 1] == ' '))
                        return text.Substring(0, i);
                }

                return text;
            }

            /// <summary>Removes the surrounding quotes of a scalar and undoes the escapes.</summary>
            /// <param name="text">The raw scalar text.</param>
            /// <param name="lineNumber">Source line, for error messages.</param>
            /// <returns>The unquoted value.</returns>
            static string Unquote(string text, int lineNumber)
            {
                if (text.Length < 2) return text;

                if (text[0] == '"' && text[text.Length - 1] == '"')
                    return text.Substring(1, text.Length - 2)
                               .Replace("\\\"", "\"")
                               .Replace("\\\\", "\\");

                if (text[0] == '\'' && text[text.Length - 1] == '\'')
                    return text.Substring(1, text.Length - 2).Replace("''", "'");

                return text;
            }
        }
    }
}
