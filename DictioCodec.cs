using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnMetalTextTool
{
    internal static class DictioCodec
    {
        private static readonly byte[] Key =
        {
            0x68, 0x20, 0x78, 0xC3, 0xAA, 0x5D, 0x29, 0xD7,
            0xBB, 0x81, 0x55, 0x49, 0xF3, 0xE2, 0xA8, 0xD0
        };

        private static readonly Regex LineRegex = new Regex(
            @"^\s*(\d+)\s*:\s*""((?:\\.|[^""])*)""\s*$",
            RegexOptions.Compiled);

        internal static void UnpackBinToTxt(string inputPath, string outputPath)
        {
            using (FileStream fs = File.OpenRead(inputPath))
            using (BinaryReader br = new BinaryReader(fs))
            {
                uint baseCount = br.ReadUInt32();
                List<Record> baseRecords = new List<Record>((int)baseCount);

                for (uint i = 0; i < baseCount; i++)
                {
                    uint charCount = br.ReadUInt32();
                    byte[] encrypted = br.ReadBytes(checked((int)charCount * 2));
                    string text = DecodeString(encrypted);
                    baseRecords.Add(new Record(i, text));
                }

                List<Record> extraRecords = new List<Record>();
                if (fs.Position + 4 <= fs.Length)
                {
                    uint extraCount = br.ReadUInt32();
                    extraRecords.Capacity = (int)extraCount;

                    for (uint i = 0; i < extraCount; i++)
                    {
                        uint index = br.ReadUInt32();
                        uint charCount = br.ReadUInt32();
                        byte[] encrypted = br.ReadBytes(checked((int)charCount * 2));
                        string text = DecodeString(encrypted);
                        extraRecords.Add(new Record(index, text));
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# base_count: " + baseCount.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("[base]");
                foreach (Record record in baseRecords)
                {
                    AppendRecordLine(sb, record);
                }

                if (extraRecords.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[extra]");
                    foreach (Record record in extraRecords)
                    {
                        AppendRecordLine(sb, record);
                    }
                }

                File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
            }
        }

        internal static void PackTxtToBin(string inputPath, string outputPath)
        {
            string[] lines = File.ReadAllLines(inputPath, Encoding.UTF8);

            uint? baseCountFromHeader = null;
            List<Record> baseRecords = new List<Record>();
            List<Record> extraRecords = new List<Record>();

            bool sawSectionHeader = false;
            Section currentSection = Section.Base;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    const string prefix = "# base_count:";
                    if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(prefix.Length).Trim();
                        uint parsed;
                        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
                        {
                            throw new FormatException("Invalid base_count header: '" + rawLine + "'");
                        }

                        baseCountFromHeader = parsed;
                    }

                    continue;
                }

                if (line.Equals("[base]", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = Section.Base;
                    sawSectionHeader = true;
                    continue;
                }

                if (line.Equals("[extra]", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = Section.Extra;
                    sawSectionHeader = true;
                    continue;
                }

                Match match = LineRegex.Match(rawLine);
                if (!match.Success)
                {
                    throw new FormatException("Invalid line: '" + rawLine + "'");
                }

                uint index = uint.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                string text = UnescapeText(match.Groups[2].Value);

                Record record = new Record(index, text);
                if (sawSectionHeader)
                {
                    if (currentSection == Section.Base)
                    {
                        baseRecords.Add(record);
                    }
                    else
                    {
                        extraRecords.Add(record);
                    }
                }
                else
                {
                    baseRecords.Add(record);
                }
            }

            if (baseRecords.Count == 0 && extraRecords.Count == 0)
            {
                throw new InvalidDataException("The TXT file does not contain any records.");
            }

            uint inferredBaseCount = InferBaseCount(baseRecords);
            uint baseCount = baseCountFromHeader.HasValue ? baseCountFromHeader.Value : inferredBaseCount;

            ValidateBaseRecords(baseRecords, baseCount);
            ValidateExtraRecords(extraRecords);

            using (FileStream fs = File.Create(outputPath))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(baseCount);

                Dictionary<uint, string> baseMap = baseRecords
                    .OrderBy(r => r.Index)
                    .ToDictionary(r => r.Index, r => r.Text);

                for (uint i = 0; i < baseCount; i++)
                {
                    WriteStringRecord(bw, baseMap[i]);
                }

                bw.Write((uint)extraRecords.Count);
                foreach (Record record in extraRecords.OrderBy(r => r.Index))
                {
                    bw.Write(record.Index);
                    WriteStringRecord(bw, record.Text);
                }
            }
        }

        private static void AppendRecordLine(StringBuilder sb, Record record)
        {
            sb.Append(record.Index.ToString(CultureInfo.InvariantCulture));
            sb.Append(':');
            sb.Append('"');
            sb.Append(EscapeText(record.Text));
            sb.AppendLine("\"");
        }

        private static void ValidateBaseRecords(List<Record> baseRecords, uint baseCount)
        {
            if (baseCount == 0)
            {
                throw new InvalidDataException("base_count must not be 0.");
            }

            uint? duplicateBase = FindDuplicateIndex(baseRecords);
            if (duplicateBase.HasValue)
            {
                throw new InvalidDataException(
                    "Duplicate base ID: " + duplicateBase.Value.ToString(CultureInfo.InvariantCulture) +
                    ". Use [base] and [extra] sections if the source file contains repeated IDs in different sections.");
            }

            uint[] baseIndices = baseRecords.Select(r => r.Index).OrderBy(i => i).ToArray();
            if (baseIndices.Length != baseCount)
            {
                throw new InvalidDataException(
                    "Base record count (" + baseIndices.Length.ToString(CultureInfo.InvariantCulture) +
                    ") does not match base_count (" + baseCount.ToString(CultureInfo.InvariantCulture) + ").");
            }

            for (uint expected = 0; expected < baseCount; expected++)
            {
                if (baseIndices[expected] != expected)
                {
                    throw new InvalidDataException("Missing base ID " + expected.ToString(CultureInfo.InvariantCulture) + " in [base] section.");
                }
            }
        }

        private static void ValidateExtraRecords(List<Record> extraRecords)
        {
            uint? duplicateExtra = FindDuplicateIndex(extraRecords);
            if (duplicateExtra.HasValue)
            {
                throw new InvalidDataException("Duplicate extra ID: " + duplicateExtra.Value.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static uint? FindDuplicateIndex(List<Record> records)
        {
            IGrouping<uint, Record> duplicate = records
                .GroupBy(r => r.Index)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate == null)
            {
                return null;
            }

            return duplicate.Key;
        }

        private static uint InferBaseCount(List<Record> baseRecords)
        {
            uint n = 0;
            HashSet<uint> indices = new HashSet<uint>(baseRecords.Select(r => r.Index));
            while (indices.Contains(n))
            {
                n++;
            }

            if (n == 0)
            {
                throw new InvalidDataException("Could not infer base_count. Record 0 is required.");
            }

            return n;
        }

        private static void WriteStringRecord(BinaryWriter bw, string text)
        {
            byte[] encoded = Encoding.Unicode.GetBytes(text);
            if ((encoded.Length & 1) != 0)
            {
                throw new InvalidDataException("UTF-16LE byte length must be even.");
            }

            uint charCount = checked((uint)(encoded.Length / 2));
            bw.Write(charCount);
            bw.Write(XorWithKey(encoded));
        }

        private static string DecodeString(byte[] encrypted)
        {
            byte[] decoded = XorWithKey(encrypted);
            return Encoding.Unicode.GetString(decoded);
        }

        private static byte[] XorWithKey(byte[] data)
        {
            byte[] output = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                output[i] = (byte)(data[i] ^ Key[i % Key.Length]);
            }

            return output;
        }

        private static string EscapeText(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\"", "\\\"");
        }

        private static string UnescapeText(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i + 1 >= text.Length)
                {
                    throw new FormatException("Invalid escape sequence at end of line.");
                }

                char next = text[++i];
                switch (next)
                {
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '"':
                        sb.Append('"');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    default:
                        throw new FormatException("Unsupported escape sequence \\" + next);
                }
            }

            return sb.ToString();
        }

        private enum Section
        {
            Base,
            Extra
        }

        private struct Record
        {
            public readonly uint Index;
            public readonly string Text;

            public Record(uint index, string text)
            {
                Index = index;
                Text = text;
            }
        }
    }
}
