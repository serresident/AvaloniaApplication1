using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Industrial service for importing and exporting tag databases
    /// in CSV (Excel / PLC signal tables: Codesys, TIA Portal) and JSON formats.
    /// </summary>
    public static class TagExportImportService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string ExportJson(IEnumerable<TagDefinition> tags)
        {
            var list = tags?.ToList() ?? new List<TagDefinition>();
            return JsonSerializer.Serialize(list, JsonOpts);
        }

        public static List<TagDefinition> ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<TagDefinition>();

            try
            {
                // Support both direct list [...] and object containing "Tags": [...]
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<TagDefinition>>(json, JsonOpts) ?? new List<TagDefinition>();
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("Tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                    {
                        return JsonSerializer.Deserialize<List<TagDefinition>>(tagsProp.GetRawText(), JsonOpts) ?? new List<TagDefinition>();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TagExportImportService] JSON parse error: {ex.Message}");
            }

            return new List<TagDefinition>();
        }

        public static string ExportCsv(IEnumerable<TagDefinition> tags, char delimiter = ';')
        {
            var sb = new StringBuilder();
            // Header
            sb.Append("TagId").Append(delimiter)
              .Append("Name").Append(delimiter)
              .Append("ConnId").Append(delimiter)
              .Append("Address").Append(delimiter)
              .Append("DataType").Append(delimiter)
              .Append("Unit").Append(delimiter)
              .Append("ScaleA").Append(delimiter)
              .Append("ScaleB").Append(delimiter)
              .Append("Deadband").Append(delimiter)
              .Append("StaleTimeoutSec").Append(delimiter)
              .Append("Description");
            sb.AppendLine();

            foreach (var t in tags ?? Enumerable.Empty<TagDefinition>())
            {
                sb.Append(EscapeCsv(t.TagId, delimiter)).Append(delimiter)
                  .Append(EscapeCsv(t.Name, delimiter)).Append(delimiter)
                  .Append(EscapeCsv(t.ConnId, delimiter)).Append(delimiter)
                  .Append(EscapeCsv(t.Address, delimiter)).Append(delimiter)
                  .Append(EscapeCsv(t.DataType, delimiter)).Append(delimiter)
                  .Append(EscapeCsv(t.Unit ?? "", delimiter)).Append(delimiter)
                  .Append(t.ScaleA.ToString(CultureInfo.InvariantCulture)).Append(delimiter)
                  .Append(t.ScaleB.ToString(CultureInfo.InvariantCulture)).Append(delimiter)
                  .Append(t.Deadband.ToString(CultureInfo.InvariantCulture)).Append(delimiter)
                  .Append(t.StaleTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)).Append(delimiter)
                  .Append(EscapeCsv(t.Description ?? "", delimiter));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static List<TagDefinition> ImportCsv(string csv)
        {
            var result = new List<TagDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return result;

            using var reader = new StringReader(csv);
            string? firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine)) return result;

            // Auto-detect delimiter: check semicolon, comma, or tab
            char delimiter = DetectDelimiter(firstLine);

            var headers = ParseCsvLine(firstLine, delimiter)
                .Select(h => NormalizeHeader(h))
                .ToList();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var tokens = ParseCsvLine(line, delimiter);
                if (tokens.Count == 0 || tokens.All(string.IsNullOrWhiteSpace)) continue;

                var tag = new TagDefinition();
                for (int i = 0; i < tokens.Count && i < headers.Count; i++)
                {
                    var val = tokens[i].Trim();
                    var header = headers[i];

                    switch (header)
                    {
                        case "tagid":
                            tag.TagId = val;
                            break;
                        case "name":
                            tag.Name = val;
                            break;
                        case "connid":
                            tag.ConnId = val;
                            break;
                        case "address":
                            tag.Address = val;
                            break;
                        case "datatype":
                            tag.DataType = string.IsNullOrWhiteSpace(val) ? "Float32" : val;
                            break;
                        case "unit":
                            tag.Unit = string.IsNullOrWhiteSpace(val) ? null : val;
                            break;
                        case "scalea":
                            if (double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double sa))
                                tag.ScaleA = sa;
                            break;
                        case "scaleb":
                            if (double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double sb))
                                tag.ScaleB = sb;
                            break;
                        case "deadband":
                            if (double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double db))
                                tag.Deadband = db;
                            break;
                        case "staletimeoutsec":
                            if (double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double sec) && sec > 0)
                                tag.StaleTimeout = TimeSpan.FromSeconds(sec);
                            break;
                        case "description":
                            tag.Description = string.IsNullOrWhiteSpace(val) ? null : val;
                            break;
                    }
                }

                // If TagId was missing but Name or Address exists, synthesize a clean TagId
                if (string.IsNullOrWhiteSpace(tag.TagId))
                {
                    if (!string.IsNullOrWhiteSpace(tag.ConnId) && !string.IsNullOrWhiteSpace(tag.Address))
                        tag.TagId = ConfigMigrationService.GenerateTagId(tag.ConnId, tag.Address);
                    else if (!string.IsNullOrWhiteSpace(tag.Name))
                        tag.TagId = "tag_" + tag.Name.ToLowerInvariant().Replace(' ', '_');
                }

                if (!string.IsNullOrWhiteSpace(tag.TagId))
                {
                    if (string.IsNullOrWhiteSpace(tag.Name)) tag.Name = tag.TagId;
                    result.Add(tag);
                }
            }

            return result;
        }

        private static char DetectDelimiter(string line)
        {
            int semicolons = line.Count(c => c == ';');
            int commas = line.Count(c => c == ',');
            int tabs = line.Count(c => c == '\t');

            if (semicolons >= commas && semicolons >= tabs && semicolons > 0) return ';';
            if (tabs > semicolons && tabs > commas) return '\t';
            return ',';
        }

        private static string NormalizeHeader(string header)
        {
            var h = header.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
            if (h is "tagid" or "id" or "tag" or "идентификатор" or "тэг" or "тег") return "tagid";
            if (h is "name" or "title" or "наименование" or "имя" or "название") return "name";
            if (h is "connid" or "connection" or "connectionid" or "подключение" or "драйвер" or "связь") return "connid";
            if (h is "address" or "addr" or "адрес" or "регистр" or "topic" or "топик") return "address";
            if (h is "datatype" or "type" or "тип" or "типданных") return "datatype";
            if (h is "unit" or "units" or "ед" or "едизм" or "единица" or "размерность") return "unit";
            if (h.StartsWith("масштаб") || h.StartsWith("scale") || h.StartsWith("коэф") || h.StartsWith("множител"))
            {
                if (h.EndsWith("b") || h.EndsWith("\u0431") || h.EndsWith("\u0432") || h.EndsWith("2")) return "scaleb";
                return "scalea";
            }
            if (h.StartsWith("смещен") || h.StartsWith("offset") || h is "b" or "\u0431" or "\u0432") return "scaleb";
            if (h is "scalea" or "gain" or "a" or "\u0430") return "scalea";
            if (h is "deadband" or "апертура" or "порог" or "зонанечувствительности") return "deadband";
            if (h is "staletimeoutsec" or "staletimeout" or "timeout" or "таймаут") return "staletimeoutsec";
            if (h is "description" or "desc" or "описание" or "примечание" or "комментарий") return "description";
            return h;
        }

        private static string EscapeCsv(string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++; // Skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            tokens.Add(sb.ToString());
            return tokens;
        }
    }
}
