﻿// ripped from https://github.com/thesecretlab/YarnSpinner/blob/master/YarnSpinnerConsole/FileFormatConverter.cs
// (YarnSpinnerConsole code isn't in YarnSpinner for Unity, so I had to copy it over)
// - changed internal methods to not internals
// - made it into public static utility class for YarnWeaver code to use
// - added ConvertFormatOptions and other relevant bits of YarnSpinnerConsole/Main.cs

// -- Robert Yang, 11 Feb 2018

using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace Merino
{
	public class YarnSpinnerFileFormatConverter
	{
		// hacked in from YarnSpinnerConsole/Main.cs https://github.com/thesecretlab/YarnSpinner/blob/master/YarnSpinnerConsole/Main.cs
	#region FROM_YARN_SPINNER_CONSOLE
		public class ConvertFormatOptions // : BaseOptions
		{
//			[Option('d', "debug", HelpText = "Show debugging information.")]
			public bool showDebuggingInfo { get; set; }

//			[Option('v', "verbose", HelpText = "Be verbose.")]
			public bool verbose { get; set; }

//			[Value(0, MetaName = "source files", HelpText = "The files to use.")]
			public IList<string> files { get; set; }

//			[Option("json", HelpText = "Convert to JSON", SetName = "format")]
			public bool convertToJSON { get; set; }

//			[Option("yarn", HelpText = "Convert to Yarn", SetName = "format")]
			public bool convertToYarn { get; set; }

//			[Option('o', "output-dir", HelpText = "The destination directory. Defaults to each file's source folder.")]
			public string outputDirectory { get; set; }
		}

		public static List<string> ALLOWED_EXTENSIONS = new List<string>(new string[] { ".json", ".node", ".yarn.bytes", ".yarn.txt" });

		public static void CheckFileList(IList<string> paths, List<string> allowedExtensions)
		{

			if (paths.Count == 0)
			{
				MerinoDebug.Log(LoggingLevel.Warning, "No files provided.");
				return;
			}

			var invalid = new List<string>();

			foreach (var path in paths)
			{
				// Does this file exist?
				var exists = System.IO.File.Exists(path);

				// Does this file have the right extension?
				var hasAllowedExtension = allowedExtensions.FindIndex(item => path.EndsWith(item)) != -1;

				if (!exists || !hasAllowedExtension)
				{
					invalid.Add(string.Format("\"{0}\"", path));
				}
			}

			if (invalid.Count != 0)
			{

				var message = string.Format("The file{0} {1} {2}.",
					invalid.Count == 1 ? "" : "s",
					string.Join(", ", invalid.ToArray()),
					invalid.Count == 1 ? "is not valid" : "are not valid"
				);

				MerinoDebug.Log(LoggingLevel.Error, message);
			}
		}
	#endregion

		public static string ConvertNodesToYarnText(IEnumerable<YarnSpinnerLoader.NodeInfo> nodes)
		{
			var sb = new System.Text.StringBuilder();

			var properties = typeof(YarnSpinnerLoader.NodeInfo).GetProperties();

			foreach (var node in nodes) {

				foreach (var property in properties) {

					// ignore the body attribute
					if (property.Name == "body") {
						continue;
					}

					// piggy-back off the JsonIgnoreAttribute to sense items that should not be serialised
					// if (property.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length > 0) {
					// 	continue;
					// }

					var field = property.Name;

					string value;

					var propertyType = property.PropertyType;
					if (propertyType.IsAssignableFrom(typeof(string)))
					{
						value = (string)property.GetValue(node, null);

						// avoid storing nulls when we could store the empty string instead
						if (value == null)
							value = "";
					}
					else if (propertyType.IsAssignableFrom(typeof(int)))
					{
						value = ((int)property.GetValue(node, null)).ToString();
					}
					else if (propertyType.IsAssignableFrom(typeof(YarnSpinnerLoader.NodeInfo.Position)))
					{
						var position = (YarnSpinnerLoader.NodeInfo.Position)property.GetValue(node, null);

						value = string.Format("{0},{1}", position.x, position.y);
					} else {
						MerinoDebug.LogFormat(LoggingLevel.Error, "Internal error: Node {0}'s property {1} has unsupported type {2}", node.title, property.Name, propertyType.FullName);

						// will never be run, but prevents the compiler being mean about us not returning a value
						throw new Exception();
					}

					var header = string.Format("{0}: {1}", field, value);

					sb.Append(MerinoPrefs.lineEnding + header);

				}
				// now write the body

				// 5 May 2019, changed all AppendLine to use regular Append, with manual line breaks
				// this is to preserve compatibility with the base Yarn Editor, which doesn't like "\r\n" (which AppendLine uses)
				// see: https://github.com/radiatoryang/merino/issues/26

				sb.Append(MerinoPrefs.lineEnding + "---");

				sb.Append(MerinoPrefs.lineEnding + node.body);

				sb.Append(MerinoPrefs.lineEnding + "===");

			}

			return sb.ToString();
		}


		public delegate string ConvertNodesToText(IEnumerable<YarnSpinnerLoader.NodeInfo> nodes);

		public static void ConvertNodesInFile(ConvertFormatOptions options, string file, string fileExtension, ConvertNodesToText convert)
		{
		//	var d = new Dialogue(null);

			var text = File.ReadAllText(file);

			IEnumerable<YarnSpinnerLoader.NodeInfo> nodes;
			try {
				nodes = YarnSpinnerLoader.GetNodesFromText(text);
			} catch (FormatException e) {
				MerinoDebug.Log(LoggingLevel.Error, e.Message);
				return;
			}

			var serialisedText = convert(nodes);

			var destinationDirectory = options.outputDirectory;

			if (destinationDirectory == null)
			{
				destinationDirectory = Path.GetDirectoryName(file);
			}

			var fileName = Path.GetFileName(file);

			// ChangeExtension thinks that the file "Foo.yarn.txt" has the extension "txt", so
			// to simplify things, just lop that extension off right away if it's there
			fileName = fileName.Replace(".yarn.txt", "");

			// change the filename's extension
			fileName = Path.ChangeExtension(fileName, fileExtension);

			// figure out where we're writing this file
			var destinationFilePath = Path.Combine(destinationDirectory, fileName);

			File.WriteAllText(destinationFilePath, serialisedText);

			if (options.verbose)
			{
				MerinoDebug.Log(LoggingLevel.Verbose, "Wrote " + destinationFilePath);
			}
		}


	}
}
