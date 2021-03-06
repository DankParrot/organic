﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Organic.Plugins;
using System.Xml.Linq;
using System.Net;
using System.Xml;

namespace Organic
{
	// Plugin support for the assembler
	public partial class Assembler
	{
		internal Dictionary<string, Assembly> LoadedAssemblies;
		internal Dictionary<string, IPlugin> LoadedPlugins;

		public void LoadPlugins()
		{
			if (LoadedAssemblies != null)
				return;
			LoadedAssemblies = new Dictionary<string, Assembly>();
			LoadedPlugins = new Dictionary<string, IPlugin>();

			string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.dll");
			files = files.Concat(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.exe")).ToArray();
			foreach (string file in files)
			{
				try
				{
					string assemblyFile = Path.GetTempFileName();
					if (File.Exists(assemblyFile))
						File.Delete(assemblyFile);
					File.Copy(file, assemblyFile);
					Stream assemblyStream = File.Open(assemblyFile, FileMode.Open);
					byte[] assemblyData = new byte[assemblyStream.Length];
					assemblyStream.Read(assemblyData, 0, assemblyData.Length);
					Assembly assembly = Assembly.Load(assemblyData);
					LoadedAssemblies.Add(file, assembly);
					foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t)).Take(1))
					{
						IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
						plugin.Loaded(this);
						LoadedPlugins.Add(file, plugin);
					}
				}
				catch
				{
				}
			}

			CustomOperators = new List<CustomExpressionOperator>();
		}

		#region Install Stuff

		private const string pluginListUrl = "https://raw.github.com/SirCmpwn/Organic-Plugins/master/plugin-list.xml";
		private XDocument PluginList;

		private void DownloadPluginList()
		{
			try
			{
				WebClient wc = new WebClient();
				Stream s = wc.OpenRead(pluginListUrl);
				PluginList = XDocument.Load(new StreamReader(s));
				s.Close();
			}
			catch
			{
				Console.WriteLine("Unable to download plugin list.");
			}
		}

		internal void InstallPlugin(string name)
		{
			DownloadPluginList();
			if (PluginList == null)
				return;
			name = name.ToLower();
			foreach (var node in PluginList.Root.Elements("plugin"))
			{
				if (node.Attribute("name").Value.ToLower() == name)
				{
					WebClient wc = new WebClient();
					string file = node.Attribute("name").Value.ToLower() + ".dll";
					wc.DownloadFile(node.Attribute("url").Value, file);
					Console.WriteLine("Plugin installed.");
					return;
				}
			}
			Console.WriteLine("Plugin not found.");
		}

		internal void SearchPlugins(string terms)
		{
			DownloadPluginList();
			if (PluginList == null)
				return;
			terms = terms.ToLower();
			Console.WriteLine("Search results:");
			foreach (var node in PluginList.Root.Elements("plugin"))
			{
				if (node.Attribute("name").Value.ToLower().Contains(terms) || node.Attribute("description").Value.ToLower().Contains(terms))
					Console.WriteLine(node.Attribute("name").Value + ": " + node.Attribute("description").Value);
			}
		}

		internal void RemovePlugin(string name)
		{
			foreach (var plugin in LoadedPlugins)
			{
				if (plugin.Value.Name.ToLower() == name)
				{
					File.Delete(plugin.Key);
					Console.WriteLine("Plugin uninstalled.");
					return;
				}
			}
		}

		internal void GetInfo(string name)
		{
			DownloadPluginList();
			if (PluginList == null)
				return;
			name = name.ToLower();
			foreach (var node in PluginList.Root.Elements("plugin"))
			{
				if (node.Attribute("name").Value.ToLower() == name)
				{
					Console.WriteLine("Plugin Information:");
					Console.WriteLine(node.Attribute("name").Value);
					Console.WriteLine(node.Attribute("description").Value);
					Console.WriteLine("Version: " + node.Attribute("version").Value);
					return;
				}
			}
			Console.WriteLine("Plugin not found.");
		}

		#endregion

		#region Plugin Resources

		#region Events

		/// <summary>
		/// Run when Organic is unable to parse a command-line parameter itself.
		/// </summary>
		public event EventHandler<HandleParameterEventArgs> TryHandleParameter;
		/// <summary>
		/// Run when Organic completes assembly.
		/// </summary>
		public event EventHandler<AssemblyCompleteEventArgs> AssemblyComplete;
		/// <summary>
		/// Run when Organic handles a line of code.
		/// The code will be trimmed of whitespace and stripped of comments.
		/// </summary>
		public event EventHandler<HandleCodeEventArgs> HandleCodeLine;
		/// <summary>
		/// Run before an expression is evalulated.
		/// </summary>
		public event EventHandler<HandleExpressionEventArgs> HandleExpression;
		/// <summary>
		/// Run before handling a value in an expression (in "2+4", 2 and 4 values)
		/// </summary>
		public event EventHandler<EvaluateValueEventArgs> EvaluateExpressionValue;

		#endregion

		#region Methods

		public void AddHelpEntry(string Entry)
		{
			PluginHelp.Add(Entry);
		}

		/// <summary>
		/// Adds an operator to the expression evaluation system
		/// </summary>
		/// <param name="Operator"></param>
		public void AddCustomOperator(CustomExpressionOperator Operator)
		{
			CustomOperators.Add(Operator);
		}

		#endregion

		#endregion
	}
}
