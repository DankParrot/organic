﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Organic
{
    public partial class Assembler
    {
        #region Preprocessor Directives

        private void ParseDirectives(List<ListEntry> output, string line, bool lineFromMacro)
        {
            string directive = line.Substring(1);
            string[] parameters = directive.Split(' ');
            if (directive.ToLower() == "endif" || directive.ToLower() == "end")
            {
                if (IfStack.Count == 1)
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.UncoupledStatement, lineFromMacro ) );
                else
                {
                    IfStack.Pop();
                    output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                }
            }
            else if (directive.ToLower().StartsWith("elseif") || directive.ToLower().StartsWith("elif"))
            {
                if (IfStack.Count == 1)
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.UncoupledStatement, lineFromMacro ) );
                else
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        var result = ParseExpression(line.Substring(line.IndexOf(' ')));
                        if (result.Successful)
                        {
                            if (result.Value > 0)
                                IfStack.Push(!IfStack.Pop());
                            output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                        }
                        else
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                    }
                }
            }
            else if (directive.ToLower() == "else")
            {
                if (IfStack.Count == 1)
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.UncoupledStatement, lineFromMacro ) );
                else
                {
                    output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                    IfStack.Push(!IfStack.Pop());
                }
            }
            else if (IfStack.Peek())
            {
                if (directive.ToLower() == "region" || directive.ToLower() == "endregion") { } // Allowed but ignored
                else if (directive.ToLower() == "nolist")
                {
                    noList = true;
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                }
                else if (directive.ToLower() == "list")
                {
                    noList = false;
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                }
                else if ((directive.ToLower().StartsWith("dat ") || directive.ToLower().StartsWith("dw ")))
                {
                    if (parameters.Length == 1)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    else
                    {
                        string[] dataStrings = directive.Substring(directive.IndexOf(" ")).SafeSplit(',');
                        List<ushort> binOutput = new List<ushort>();
                        Dictionary<ushort, string> postponedExpressions = new Dictionary<ushort, string>();
                        foreach (string data in dataStrings)
                        {
                            if (data.Trim().StartsWith("\""))
                            {
                                if (!data.Trim().EndsWith("\""))
                                {
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                }
                                else
                                {
                                    string str = data.Trim().Substring(1, data.Trim().Length - 2).Unescape();
                                    foreach (byte b in Encoding.ASCII.GetBytes(str))
                                        binOutput.Add(b);
                                }
                            }
                            else
                            {
                                postponedExpressions.Add((ushort)binOutput.Count, data.Trim());
                                binOutput.Add(0);
                            }
                        }
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), binOutput.ToArray(), currentAddress, !noList));
                        output[output.Count - 1].PostponedExpressions = postponedExpressions;
                        if (!noList)
                            currentAddress += (ushort)binOutput.Count;
                    }
                }
                else if (directive.ToLower().StartsWith("scope"))
                {
                    if (parameters.Length < 2)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    else if (parameters.Length > 2)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                    else
                    {
                        PriorGlobalLabel = parameters[1];
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                    }
                }
                else if (directive.ToLower() == "uniquescope")
                {
                    PriorGlobalLabel = "_unique" + UniqueScopeNumber++;
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                }
                else if (directive.ToLower().StartsWith("echo"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        string[] dataStrings = directive.Substring(directive.IndexOf(" ")).SafeSplit(',');
                        string consoleOutput = "";
                        foreach (string data in dataStrings)
                        {
                            if (data.Trim().StartsWith("\""))
                            {
                                if (!data.Trim().EndsWith("\""))
                                {
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                }
                                else
                                {
                                    string str = data.Trim().Substring(1, data.Trim().Length - 2).Unescape();
                                    consoleOutput += str;
                                }
                            }
                            else
                            {
                                ExpressionResult value = ParseExpression(data.Trim());
                                if (!value.Successful)
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                else
                                    consoleOutput += value.Value;
                            }
                        }
                        Console.Write(consoleOutput + "\n");
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
						output.Add( new ListEntry( consoleOutput, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                    }
                }
                else if (directive.ToLower().StartsWith("ref"))
                {
                    ReferencedValues.Add(directive.Substring(directive.IndexOf(" ") + 1));
                }
                else if (directive.ToLower().StartsWith("asciip"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        string[] dataStrings = directive.Substring(directive.IndexOf(" ")).SafeSplit(',');
                        List<ushort> binOutput = new List<ushort>();
                        foreach (string data in dataStrings)
                        {
                            if (data.Trim().StartsWith("\""))
                            {
                                if (!data.Trim().EndsWith("\""))
                                {
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                }
                                else
                                {
                                    string str = data.Trim().Substring(1, data.Trim().Length - 2).Unescape();
                                    binOutput.Add((ushort)str.Length);
                                    foreach (byte b in Encoding.ASCII.GetBytes(str))
                                        binOutput.Add(b);
                                }
                            }
                            else
								output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        }
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), binOutput.ToArray(), currentAddress, !noList));
                        if (!noList)
                            currentAddress += (ushort)binOutput.Count;
                    }
                }
                else if (directive.ToLower().StartsWith("asciic") || directive.ToLower().StartsWith("asciiz"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        string[] dataStrings = directive.Substring(directive.IndexOf(" ")).SafeSplit(',');
                        List<ushort> binOutput = new List<ushort>();
                        foreach (string data in dataStrings)
                        {
                            if (data.Trim().StartsWith("\""))
                            {
                                if (!data.Trim().EndsWith("\""))
                                {
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                }
                                else
                                {
                                    string str = data.Trim().Substring(1, data.Trim().Length - 2).Unescape();
                                    foreach (byte b in Encoding.ASCII.GetBytes(str))
                                        binOutput.Add(b);
                                    binOutput.Add(0);
                                }
                            }
                            else
								output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        }
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), binOutput.ToArray(), currentAddress, !noList));
                        if (!noList)
                            currentAddress += (ushort)binOutput.Count;
                    }
                }
				else if (directive.ToLower().StartsWith("ascii"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        string[] dataStrings = directive.Substring(directive.IndexOf(" ")).SafeSplit(',');
                        List<ushort> binOutput = new List<ushort>();
                        foreach (string data in dataStrings)
                        {
                            if (data.Trim().StartsWith("\""))
                            {
                                if (!data.Trim().EndsWith("\""))
                                {
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                                }
                                else
                                {
                                    string str = data.Trim().Substring(1, data.Trim().Length - 2).Unescape();
                                    foreach (byte b in Encoding.ASCII.GetBytes(str))
                                        binOutput.Add(b);
                                }
                            }
                            else
								output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        }
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), binOutput.ToArray(), currentAddress, !noList));
                        if (!noList)
                            currentAddress += (ushort)binOutput.Count;
                    }
                }
                else if (directive.ToLower() == "longform") // Handled properly in the second pass
                    output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                else if (directive.ToLower() == "shortform")
                    output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                else if (directive.ToLower().StartsWith("org"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else if (parameters.Length > 2)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                    }
                    else
                    {
                        ExpressionResult value = ParseExpression(parameters[1]);
                        if (value == null)
                        {
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        }
                        else
                        {
                            currentAddress = value.Value;
                            var entry = new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList);
                            entry.RootLineNumber = RootLineNumber;
                            output.Add(entry);
                        }
                    }
                }
                else if (directive.ToLower().StartsWith("ifdef"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else if (parameters.Length > 2)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                    }
                    else
                    {
                        if (Values.ContainsKey(parameters[1].ToLower()))
                            IfStack.Push(true);
                        else
                            IfStack.Push(false);
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                    }
                }
                else if (directive.ToLower().StartsWith("ifndef"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else if (parameters.Length > 2)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                    }
                    else
                    {
                        if (Values.ContainsKey(parameters[1].ToLower()))
                            IfStack.Push(false);
                        else
                            IfStack.Push(true);
                        output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                    }
                }
                else if (directive.ToLower().StartsWith("if"))
                {
                    if (parameters.Length == 1)
                    {
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    }
                    else
                    {
                        var result = ParseExpression(line.Substring(3));
                        if (result.Successful)
                        {
                            if (result.Value > 0)
                                IfStack.Push(true);
                            else
                                IfStack.Push(false);
                            output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                        }
                        else
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                    }
                }
                else if (directive.ToLower().StartsWith("equ") || directive.ToLower().StartsWith("define") || directive.ToLower().StartsWith("equate"))
                {
                    if (parameters.Length > 1)
                    {
                        if (Values.ContainsKey(parameters[1]))
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.DuplicateName, lineFromMacro ) );
                        else
                        {
                            if (parameters.Length == 2)
                            {
                                Values.Add(parameters[1], 1);
                                output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                            }
                            else if (parameters.Length > 2)
                            {
                                string expression = directive.TrimExcessWhitespace();
                                expression = expression.Substring(expression.IndexOf(' ') + 1);
                                expression = expression.Substring(expression.IndexOf(' ') + 1);
                                ExpressionResult value = ParseExpression(expression); // TODO: find a way to forward reference
                                if (value != null)
                                {
                                    Values.Add(parameters[1], value.Value);
                                    output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, !noList));
                                }
                                else
									output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                            }
                            else
								output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                        }
                    }
                }
                else if (directive.ToLower().StartsWith("pad") || directive.ToLower().StartsWith("fill")) // .pad length, value
                {
                    parameters = line.SafeSplit(',', ' ');
                    string[] fixedParams = new string[0];
                    foreach (string parameter in parameters)
                        if (!string.IsNullOrEmpty(parameter))
                            fixedParams = fixedParams.Concat(new string[] { parameter }).ToArray();
                    parameters = fixedParams;
                    if (parameters.Length == 3)
                    {
                        var length = ParseExpression(parameters[1]);
                        var value = ParseExpression(parameters[2]);
                        if (!length.Successful)
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        else
                        {
                            ushort[] padding = new ushort[length.Value];
                            Dictionary<ushort, string> postponed = new Dictionary<ushort, string>();
                            for (int i = 0; i < padding.Length; i++)
                            {
                                padding[i] = value.Value;
                                if (!value.Successful)
                                    postponed.Add((ushort)i, parameters[2]);
                            }
                            output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), padding, currentAddress, !noList));
                            output[output.Count - 1].PostponedExpressions = postponed;
                            if (!noList)
                                currentAddress += (ushort)padding.Length;
                        }
                    }
                    else if (parameters.Length == 1)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    else
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                }
                else if (directive.ToLower().StartsWith("reserve"))
                {
                    if (parameters.Length == 1)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    else
                    {
                        var expression = directive.Substring(7).Trim();
                        var result = ParseExpression(expression);
                        if (result.Successful)
                        {
                            output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), new ushort[result.Value], currentAddress, !noList));
                            currentAddress += result.Value;
                        }
                        else
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                    }
                }
                else if (directive.ToLower().StartsWith("align")) // .align addr
                {
                    if (parameters.Length == 2)
                    {
                        var addr = ParseExpression(parameters[1]);
                        if (!addr.Successful)
							output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.IllegalExpression, lineFromMacro ) );
                        else
                        {
                            if (currentAddress > addr.Value)
								output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.AlignToPast, lineFromMacro ) );
                            else
                            {
                                var amount = (ushort)(addr.Value - currentAddress);
                                ushort[] padding = new ushort[amount];
                                output.Add(new ListEntry(line, FileNames.Peek(), LineNumbers.Peek(), padding, currentAddress, !noList));
                                if (!noList)
                                    currentAddress = addr.Value;
                            }
                        }
                    }
                    else if (parameters.Length == 1)
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InsufficientParamters, lineFromMacro ) );
                    else
						output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.TooManyParamters, lineFromMacro ) );
                }
                else if (directive == "relocate")
                {
                    OldAddress = currentAddress;
                    currentAddress = 0;
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                    output[output.Count - 1].CodeType = CodeType.RelocationTable;
                    RelocationGroup++;
                    IsRelocating = true;
                }
                else if (directive == "endrelocate")
                {
                    // TODO: Errors
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, lineFromMacro ) );
                    currentAddress = OldAddress;
                    IsRelocating = false;
                }
                else
                {
					output.Add( new ListEntry( line, FileNames.Peek(), LineNumbers.Peek(), currentAddress, ErrorCode.InvalidDirective, lineFromMacro ) );
                }
            }
            output[output.Count - 1].CodeType = CodeType.Directive;
        }

        #endregion
    }
}
