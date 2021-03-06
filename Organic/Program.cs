﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using Organic.Plugins;

namespace Organic
{
	public partial class Assembler
	{
		[STAThread]
		public static int Main(string[] args)
		{
			DateTime startTime = DateTime.Now;
			int returnCode = 0;

			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("Organic DCPU-16 Assembler © 2017, Drew DeVault & DankParrot");
			if (args.Length == 0)
			{
				DisplayHelp();
				return 1;
			}
			string inputFile = null;
			string outputFile = null;
			string listingFile = null;
			string jsonFile = null;
			string pipe = null;
			string workingDirectory = Directory.GetCurrentDirectory();
			bool bigEndian = true;
			bool quiet = false;
			bool verbose = false;

			Assembler assembler = new Assembler();
			assembler.IncludePath = Environment.GetEnvironmentVariable("ORGINCLUDE");
			if (string.IsNullOrEmpty(assembler.IncludePath))
				assembler.IncludePath = "";
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (arg.StartsWith("-"))
				{
					try
					{
						switch (arg)
						{
							case "-h":
							case "-?":
							case "/h":
							case "/?":
							case "--help":
								DisplayHelp();
								return 1;
							case "-o":
							case "--output":
							case "--output-file":
								outputFile = args[++i];
								break;
							case "--input-file":
								inputFile = args[++i];
								break;
							case "-e":
							case "--equate":
								ExpressionResult result = assembler.ParseExpression(args[i + 2]);
								if (!result.Successful)
								{
									Console.WriteLine("Error: " + ListEntry.GetFriendlyErrorMessage(ErrorCode.IllegalExpression));
									return 1;
								}
								assembler.Values.Add(args[i + 1].ToLower(), result.Value);
								i += 2;
								break;
							case "-l":
							case "--listing":
								listingFile = args[++i];
								break;
							case "--little-endian":
								bigEndian = false;
								break;
							case "--long-literals":
								assembler.ForceLongLiterals = true;
								break;
							case "--quiet":
							case "-q":
								quiet = true;
								break;
							case "--pipe":
							case "-p":
								pipe = args[++i];
								break;
							case "--json":
								jsonFile = args[++i];
								break;
							case "--include":
							case "-i":
								assembler.IncludePath = Environment.GetEnvironmentVariable("ORGINCLUDE") + ";" + args[++i];
								break;
							case "--working-directory":
							case "-w":
								workingDirectory = args[++i];
								break;
							case "--verbose":
							case "-v":
								verbose = true;
								break;
							case "--pause":
								AppDomain.CurrentDomain.ProcessExit += (s, e) => {
									Console.Write("Press any key to continue...");
									Console.ReadKey();
									Console.WriteLine();
								};
								break;
							case "--debug-mode":
								Console.ReadKey();
								break;
								
							case "--plugins":
								ListPlugins(assembler);
								return 0;
							case "--install":
								assembler.InstallPlugin(args[++i]);
								return 0;
							case "--remove":
								assembler.RemovePlugin(args[++i]);
								return 0;
							case "--search":
								assembler.SearchPlugins(args[++i]);
								return 0;
							case "--info":
								assembler.GetInfo(args[++i]);
								return 0;
							
							default:
								HandleParameterEventArgs hpea = new HandleParameterEventArgs(arg);
								hpea.Arguments = args;
								hpea.Index = i;
								if (assembler.TryHandleParameter != null)
									assembler.TryHandleParameter(assembler, hpea);
								if (!hpea.Handled)
								{
									Console.WriteLine("Error: Invalid parameter: " + arg + "\nUse Organic.exe --help for usage information.");
									return 1;
								}
								else
									i = hpea.Index;
								if (hpea.StopProgram)
									return 0;
								break;
						}
					}
					catch (ArgumentOutOfRangeException)
					{
						Console.WriteLine("Error: Missing argument: " + arg + "\nUse Organic.exe --help for usage information.");
						return 1;
					}
				}
				else
				{
					if (inputFile == null)
						inputFile = arg;
					else if (outputFile == null)
						outputFile = arg;
					else
					{
						Console.WriteLine("Error: Invalid parameter: " + arg + "\nUse Organic.exe --help for usage information.");
						return 1;
					}
				}
			}
			if (inputFile == null && pipe == null)
			{
				Console.WriteLine("Error: No input file specified.\nUse Organic.exe --help for usage information.");
				return 1;
			}
			if (outputFile == null)
				outputFile = Path.GetFileNameWithoutExtension(inputFile) + ".bin";
			if (!File.Exists(inputFile) && pipe == null && inputFile != "-")
			{
				Console.WriteLine("Error: File not found (" + inputFile + ")");
				return 1;
			}

			string contents;
			if (pipe == null)
			{
				if (inputFile != "-")
				{
					StreamReader reader = new StreamReader(inputFile);
					contents = reader.ReadToEnd();
					reader.Close();
				}
				else
					contents = Console.In.ReadToEnd();
			}
			else
				contents = pipe;


			List<ListEntry> output;
			string wdOld = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(workingDirectory);
			if (pipe == null)
				output = assembler.Assemble(contents, inputFile);
			else
				output = assembler.Assemble(contents, "[piped input]");
			Directory.SetCurrentDirectory(wdOld);

			if (assembler.AssemblyComplete != null)
				assembler.AssemblyComplete(assembler, new AssemblyCompleteEventArgs(output));

			// Output errors
			if (!quiet)
			{
				foreach (var entry in output)
				{
					if (entry.ErrorCode != ErrorCode.Success)
					{
						Console.Error.WriteLine("Error " + entry.FileName + " (line " + entry.LineNumber + "): " +
										  ListEntry.GetFriendlyErrorMessage(entry.ErrorCode));
						returnCode = 1;
					}
					if (entry.WarningCode != WarningCode.None)
						Console.WriteLine("Warning " + entry.FileName + " (line " + entry.LineNumber + "): " +
							ListEntry.GetFriendlyWarningMessage(entry.WarningCode));
				}
			}

			ushort currentAddress = 0;
			Stream binStream = null;
			if (outputFile != "-")
			{
				if (!string.IsNullOrEmpty(Path.GetDirectoryName(outputFile)))
					Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
				binStream = File.Open(outputFile, FileMode.Create);
			}
			foreach (var entry in output)
			{
				if (entry.Output != null)
				{
					foreach (ushort value in entry.Output)
					{
						currentAddress++;
						byte[] buffer = BitConverter.GetBytes(value);
						if (bigEndian)
							Array.Reverse(buffer);
						if (outputFile != "-")
							binStream.Write(buffer, 0, buffer.Length);
						else
							Console.Out.Write(Encoding.ASCII.GetString(buffer));
					}
				}
			}

			string listing = "";

			if (listingFile != null || verbose)
				listing = CreateListing(output);

			string json = "";

			if (jsonFile != null)
				json = CreateJson(output);

			if (verbose)
				Console.Write(listing);
			if (listingFile != null)
			{
				if (!string.IsNullOrEmpty(Path.GetDirectoryName(listingFile)))
					Directory.CreateDirectory(Path.GetDirectoryName(listingFile));
				var writer = new StreamWriter(listingFile);
				writer.Write(listing);
				writer.Close();
			}

			if (jsonFile != null)
			{
				if (!string.IsNullOrEmpty(Path.GetDirectoryName(jsonFile)))
					Directory.CreateDirectory(Path.GetDirectoryName(jsonFile));
				var writer = new StreamWriter(jsonFile);
				writer.Write(json);
				writer.Close();
			}

			TimeSpan duration = DateTime.Now - startTime;
			Console.WriteLine("Organic build complete " + duration.TotalMilliseconds + "ms");
			return returnCode;
		}

		private static string CreateJson(List<ListEntry> output)
		{
			// TODO: Should we just use a JSON library?
			var builder = new StringBuilder();
			builder.AppendLine("[");
			const string indent = "	";
			foreach (var item in output)
			{
				builder.AppendLine(indent + "{");
				builder.AppendLine(indent + indent + "\"type\":\"" + item.CodeType + "\",");
				builder.AppendLine(indent + indent + "\"code\":\"" + item.Code.Replace("\"", "\\\"") + "\",");
				builder.AppendLine(indent + indent + "\"file\":\"" + Path.GetFileName(item.FileName) + "\",");
				builder.AppendLine(indent + indent + "\"line\":\"" + item.LineNumber + "\",");
				builder.AppendLine(indent + indent + "\"address\":\"0x" + item.Address.ToString("X4") + "\",");
				try
				{
					if (item.CodeType == CodeType.BasicInstruction || item.CodeType == CodeType.NonBasicInstruction)
					{
						builder.AppendLine(indent + indent + "\"a\":\"" + item.ValueA.original + "\",");
						if (item.CodeType != CodeType.NonBasicInstruction)
							builder.AppendLine(indent + indent + "\"b\":\"" + item.ValueB.original + "\",");
					}
				}
				catch { }
				if (item.Output != null && item.Output.Length != 0)
					builder.AppendLine(indent + indent + "\"output\":\"" + DumpArray(item.Output) + "\",");
				if (item.ErrorCode != ErrorCode.Success)
					builder.AppendLine(indent + indent + "\"error\":\"" + item.ErrorCode + "\",");
				else if (item.WarningCode != WarningCode.None)
					builder.AppendLine(indent + indent + "\"warning\":\"" + item.WarningCode + "\",");
				builder.Length -= Environment.NewLine.Length + 1;
				builder.AppendLine();
				builder.AppendLine(indent + "},");
			}
			builder.Length -= Environment.NewLine.Length + 1;
			builder.AppendLine();
			builder.AppendLine("]");
			return builder.ToString();
		}

		private static void ListPlugins(Assembler assembler)
		{
			assembler.LoadPlugins();
			Console.WriteLine("Listing plugins:");
			foreach (var plugin in assembler.LoadedPlugins)
				Console.WriteLine(plugin.Value.Name + ": " + plugin.Value.Description);
		}

		public static string CreateListing(List<ListEntry> output)
		{
			string listing = "";
			int maxLength = 0, maxFileLength = 0;
			foreach (var entry in output)
			{
				int length = entry.FileName.Length + 1;
				if (length > maxFileLength)
					maxFileLength = length;
			}
			foreach (var entry in output)
			{
				int length = maxFileLength + entry.LineNumber.ToString().Length + 9;
				if (length > maxLength)
					maxLength = length;
			}
			TabifiedStringBuilder tsb;
			foreach (var listentry in output)
			{
				tsb = new TabifiedStringBuilder();

				// FIX: TJMonk(04-20-2013) - Changing this code to use arrays, and adding support for '#' directives
				bool pass = false;

				if (listentry.ErrorCode == ErrorCode.Success)
				{
					string[] lineStarts = { ".", "#" };
					string[] directives = { 
						"dat", "dw", "db", "ascii","asciiz", "asciip", "asciic", "align", "fill",
						"pad", "incbin", "reserve", "incpack", "relocate"
					};

					string toCheck = listentry.Code.ToLower();
					bool checkLineStart = false;
					foreach (var s in lineStarts)
					{
						if (toCheck.StartsWith(s))
						{
							checkLineStart = true;
							toCheck = toCheck.Substring(s.Length);
							break;
						}
					}

					if (checkLineStart)
					{
						foreach (var s in directives)
						{
							if (toCheck.StartsWith(s))
							{
								pass = true;
								break;
							}
						}
					}
				}

				if (pass)
				{
					// ENDFIX: TJMonk(04-20-2013)
					// Write code line
					tsb = new TabifiedStringBuilder();
					tsb.WriteAt(0, listentry.FileName);
					tsb.WriteAt(maxFileLength, "(line " + listentry.LineNumber + "): ");
					if (listentry.Listed)
						tsb.WriteAt(maxLength, "[0x" + LongHex(listentry.Address) + "] ");
					else
						tsb.WriteAt(maxLength, "[NOLIST] ");
					tsb.WriteAt(maxLength + 25, listentry.Code);
					listing += tsb.Value + Environment.NewLine;
					// Write data
					if (listentry.Output != null)
					{
						for (int i = 0; i < listentry.Output.Length; i += 8)
						{
							tsb = new TabifiedStringBuilder();
							tsb.WriteAt(0, listentry.FileName);
							tsb.WriteAt(maxFileLength, "(line " + listentry.LineNumber + "): ");
							//if (listentry.Listed)
							//	tsb.WriteAt(maxLength, "[0x" + LongHex((ushort)(listentry.Address + i)) + "] ");
							//else
								tsb.WriteAt(maxLength, "[NOLIST] ");
							string data = "";
							for (int j = 0; j < 8 && i + j < listentry.Output.Length; j++)
							{
								data += LongHex(listentry.Output[i + j]) + " ";
							}
							tsb.WriteAt(maxLength + 30, data.Remove(data.Length - 1));
							listing += tsb.Value + Environment.NewLine;
						}
					}
				}
				else
				{
					if (listentry.ErrorCode != ErrorCode.Success)
					{
						tsb = new TabifiedStringBuilder();
						tsb.WriteAt(0, listentry.FileName);
						tsb.WriteAt(maxFileLength, "(line " + listentry.LineNumber + "): ");
						if(listentry.Listed && listentry.Output != null)
							tsb.WriteAt(maxLength, "[0x" + LongHex(listentry.Address) + "] ");
						else
							tsb.WriteAt(maxLength, "[NOLIST] ");
						tsb.WriteAt(maxLength + 8, "ERROR: " + ListEntry.GetFriendlyErrorMessage(listentry.ErrorCode));
						listing += tsb.Value + Environment.NewLine;
					}
					if (listentry.WarningCode != WarningCode.None)
					{
						tsb = new TabifiedStringBuilder();
						tsb.WriteAt(0, listentry.FileName);
						tsb.WriteAt(maxFileLength, "(line " + listentry.LineNumber + "): ");
						if(listentry.Listed && listentry.Output != null)
							tsb.WriteAt(maxLength, "[0x" + LongHex(listentry.Address) + "] ");
						else
							tsb.WriteAt(maxLength, "[NOLIST] ");
						tsb.WriteAt(maxLength + 8, "WARNING: " + ListEntry.GetFriendlyWarningMessage(listentry.WarningCode));
						listing += tsb.Value + Environment.NewLine;
					}
					tsb = new TabifiedStringBuilder();
					tsb.WriteAt(0, listentry.FileName);
					tsb.WriteAt(maxFileLength, "(line " + listentry.LineNumber + "): ");
					if (listentry.Listed && (listentry.CodeType == CodeType.Label || listentry.Output != null))
						tsb.WriteAt(maxLength, "[0x" + LongHex(listentry.Address) + "] ");
					else
						tsb.WriteAt(maxLength, "[NOLIST] ");
					if (listentry.Output != null)
					{
						if (listentry.Output.Length > 0)
						{
							tsb.WriteAt(maxLength + 8, DumpArray(listentry.Output));
							tsb.WriteAt(maxLength + 25, listentry.Code);
						}
					}
					else
						tsb.WriteAt(maxLength + 23, listentry.Code);
					listing += tsb.Value + Environment.NewLine;
				}
			}
			return listing;
		}

		private static string LongHex(ushort p)
		{
			string value = p.ToString("x");
			while (value.Length < 4)
				value = "0" + value;
			return value.ToUpper();
		}

		/// <summary>
		/// Creates a string of an array's content
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public static string DumpArray(ushort[] array)
		{
			string output = "";
			foreach (ushort u in array)
			{
				string val = u.ToString("x").ToUpper();
				while (val.Length < 4)
					val = "0" + val;
				output += " " + val;
			}
			return output.Substring(1);
		}

		internal static List<string> PluginHelp = new List<string>();

		private static void DisplayHelp()
		{
			Console.WriteLine(Properties.Resources.Help);

			if (PluginHelp.Count != 0)
			{
				Console.WriteLine("\n===Plugins");
				foreach (var help in PluginHelp)
					Console.WriteLine(help);
			}
		}
	}
}