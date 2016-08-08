using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
namespace string2agb
{
    class Program
    {
        const string REGEX = @"^([\s\S]+)=(?:0[1xX]){0,1}([0-9a-fA-F]{1,2})$";
        const string REGEX_CON = @"^([\s\S]+)=([\s\S]+)$";
        const string REGEX_HEX = @"^(?:0[xX]){0,1}([0-9a-fA-F]{1,2})$";
        static Dictionary<string, byte> _lookup = new Dictionary<string, byte>();
        static void Main(string[] args)
        {
            Options parsedOptions = new Options();
            if (Parser.Default.ParseArguments(args, parsedOptions))
            {
                if (!File.Exists(parsedOptions.TablePath))
                {
                    Console.Error.WriteLine("\"" + parsedOptions.TablePath + "\" was not found.");
                }
                if (!File.Exists(parsedOptions.Input))
                {
                    Console.Error.WriteLine("\"" + parsedOptions.TablePath + "\" was not found.");
                }
                string[] table = File.ReadAllLines(parsedOptions.TablePath, System.Text.Encoding.Default);

                foreach (string s in table)
                {
                    Match m = Regex.Match(s, REGEX);
                    if (m == null)
                    {
                        Console.Error.WriteLine("Error while parsing \"" + s + "\" in file \"" + parsedOptions.TablePath + "\"");
                        return;
                    }
                    _lookup.Add(m.Groups[1].ToString(), Convert.ToByte(m.Groups[2].ToString(), 16));
                }
                for (int i = 0; i < 256; ++i)
                {
                    _lookup.Add("\\h" + i.ToString("X2"), (byte)i);
                }
                string[] inputlines = File.ReadAllLines(parsedOptions.Input, System.Text.Encoding.Default);
                StreamWriter outWriter = new StreamWriter(parsedOptions.Output, parsedOptions.Append);
                if (outWriter.BaseStream.Length == 0)
                {
                    PrintHead(parsedOptions, outWriter);
                    outWriter.WriteLine(".text");
                }
                else
                {
                    outWriter.WriteLine();
                    PrintHead(parsedOptions, outWriter);
                }
                outWriter.WriteLine(".align 2");
                List<string> symList = new List<string>();
                foreach (string line in inputlines)
                {
                    outWriter.WriteLine();
                    Match m = Regex.Match(line, REGEX_CON);
                    if (m == null)
                    {
                        Console.Error.WriteLine("Error while parsing \"" + line + "\" in file \"" + parsedOptions.Input + "\"");
                        return;
                    }
                    string symbol = m.Groups[1].ToString();
                    symList.Add(symbol);
                    string text = m.Groups[2].ToString();
                    outWriter.WriteLine(".global " + symbol);
                    outWriter.WriteLine(symbol + ":");
                    outWriter.Write(".byte ");
                    int cursor = 0;
                    int lenght = 1;
                    List<string> strings = new List<string>();
                    while (cursor + lenght <= text.Length)
                    {
                        if (_lookup.ContainsKey(text.Substring(cursor, lenght)))
                        {
                            strings.Add("0x" + _lookup[text.Substring(cursor, lenght)].ToString("X2"));
                            cursor += lenght;
                            lenght = 1;
                        }
                        else
                        {
                            lenght++;
                        }
                    }
                    if (parsedOptions.Termination != null)
                    {
                        Match hexMatch = Regex.Match(parsedOptions.Termination, REGEX_HEX);
                        if (hexMatch != null)
                            strings.Add("0x" + hexMatch.Groups[1]);
                    }
                    //Create reference symbol for use with GCC compiler
                    outWriter.WriteLine(string.Join(",", strings));
                }
                outWriter.WriteLine();
                outWriter.WriteLine("@@@@ reference table for \"" + parsedOptions.Input + "\" @@@@");
                outWriter.WriteLine();
                outWriter.WriteLine(".align 2");
                outWriter.WriteLine();
                outWriter.WriteLine(".global " + Path.GetFileNameWithoutExtension(parsedOptions.Input) + "_array");
                outWriter.WriteLine(Path.GetFileNameWithoutExtension(parsedOptions.Input) + "_array:");
                outWriter.WriteLine();
                for (int i = 0; i < symList.Count; ++i)
                {
                    outWriter.WriteLine(".global " + symList[i] + "_ref");
                    outWriter.WriteLine(symList[i] + "_ref:");
                    outWriter.WriteLine(".word " + symList[i]);
                    outWriter.WriteLine();
                }
                outWriter.Flush();
                outWriter.Close();
            }
        }

        static void PrintHead(Options opt, StreamWriter write)
        {
            string fileLine = "result of file \"" + opt.Input + "\"";
            string tableLine = "using table \"" + opt.TablePath + "\"";
            string string2agbLine = "converted using string2agb";
            int maxLen = Math.Max(Math.Max(fileLine.Length, string2agbLine.Length), tableLine.Length);
            maxLen += 25;
            write.WriteLine(new string('@', maxLen));
            write.WriteLine("@" + new string(' ', maxLen - 2) + "@");

            write.Write("@" + new string(' ', (maxLen - fileLine.Length - 2) / 2));
            write.Write(fileLine + new string(' ', (int)Math.Ceiling((decimal)(maxLen - fileLine.Length - 2) / 2)));
            write.WriteLine("@");

            write.Write("@" + new string(' ', (maxLen - tableLine.Length - 2) / 2));
            write.Write(tableLine + new string(' ', (int)Math.Ceiling((decimal)(maxLen - tableLine.Length - 2) / 2)));
            write.WriteLine("@");

            write.Write("@" + new string(' ', (maxLen - string2agbLine.Length - 2) / 2));
            write.Write(string2agbLine + new string(' ', (int)Math.Ceiling((decimal)(maxLen - string2agbLine.Length - 2) / 2)));
            write.WriteLine("@");

            write.WriteLine("@" + new string(' ', maxLen - 2) + "@");
            write.WriteLine(new string('@', maxLen));
            write.WriteLine();
        }
    }

    class Options
    {
        [Option('t', "table", Required = true, HelpText = "file used to convert text into hex string.")]
        public string TablePath { get; set; }

        [Option('i', "input", Required = true, HelpText = "input file to read text from.")]
        public string Input { get; set; }

        [Option('o', "output", Required = true, HelpText = "output file to write converted text to.")]
        public string Output { get; set; }

        [Option('a', "append", Required = false, DefaultValue = false, HelpText = "append to output file instead of overwriting")]
        public bool Append { get; set; }

        [Option('e', "terminate", Required =false, DefaultValue =null, HelpText ="terminate the string with given byte (hexadecimal).")]
        public string Termination { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
