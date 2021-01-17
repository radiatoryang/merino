
// taken from YarnSpinnerConsole/Loader.cs, which is a bunch of internal classes that aren't normally exposed in YarnSpinner
// made it into a static utility class for loading Yarn files
// -- Robert Yang, 11 February 2018


/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

// Comment out to not catch exceptions
#define CATCH_EXCEPTIONS

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn;
using System.Text;
using System.IO;
using System.Linq;

namespace Merino {

	public class YarnSpinnerLoader {

		// The raw text of the Yarn node, plus metadata
		// All properties are serialised except tagsList, which is a derived property
		public struct NodeInfo {
			public struct Position {
				public int x { get; set; }
				public int y { get; set; }
			}

			public string title { get; set; }
			public string body { get; set; }

			// The raw "tags" field, containing space-separated tags. This is written
			// to the file.
			public string tags { get; set; }

			public int colorID { get; set; }
			public Position position { get; set; }

			// The tags for this node, as a list of individual strings.
			// public List<string> tagsList
			// {
			// 	get
			// 	{
			// 		// If we have no tags list, or it's empty, return the empty list
			// 		if (tags == null || tags.Length == 0) {
			// 			return new List<string>();
			// 		}

			// 		return new List<string>(tags.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
			// 	}
			// }
			
			// // 5 September 2018 -- this is just for Merino
			// public string parent { get; set; }

		}

		// This is bad and we probably shouldn't parse it ourselves.... but we are
		public static NodeInfo[] GetNodesFromText(string text)
		{
			// All the nodes we found in this file
			var nodes = new List<NodeInfo> ();

			if (string.IsNullOrEmpty(text) ) {
				Debug.Log("Error: can't read text file for some reason?");
				return null;
			}

			// check for the existence of at least one "---"+newline sentinel, which divides
			// the headers from the body

			// we use a regex to match either \r\n or \n line endings
			if (System.Text.RegularExpressions.Regex.IsMatch(text, "---.?\n") == false) {
//					dialogue.LogErrorMessage("Error parsing input: text appears corrupt (no header sentinel)");
				Debug.Log("Error parsing input: text appears corrupt (no header sentinel)");
				return null;
			}

			var headerRegex = new System.Text.RegularExpressions.Regex("(?<field>.*): *(?<value>.*)");

			var nodeProperties = typeof(NodeInfo).GetProperties();

			int lineNumber = 0;

			using (var reader = new System.IO.StringReader(text))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{

					// Create a new node
					NodeInfo node = new NodeInfo();

					// Read header lines
					do
					{
						lineNumber++;

						if (line == null) {
							Debug.Log("break??");
							break;
						}

						// skip empty lines
						if (line.Length == 0)
						{
							continue;
						}

						// Attempt to parse the header
						var headerMatches = headerRegex.Match(line);

						if (headerMatches == null)
						{
//								dialogue.LogErrorMessage(string.Format("Line {0}: Can't parse header '{1}'", lineNumber, line));
							Debug.Log(string.Format("Line {0}: Can't parse header '{1}'", lineNumber, line));
							continue;
						}

						var field = headerMatches.Groups["field"].Value;
						var value = headerMatches.Groups["value"].Value;

						// Attempt to set the appropriate property using this field
						foreach (var property in nodeProperties)
						{
							if (property.Name != field) {
								continue;
							}

							// skip properties that can't be written to
							if (property.CanWrite == false)
							{
								continue;
							}
							try
							{
								var propertyType = property.PropertyType;
								object convertedValue;
								if (propertyType.IsAssignableFrom(typeof(string)))
								{
									convertedValue = value;
								}
								else if (propertyType.IsAssignableFrom(typeof(int)))
								{
									convertedValue = int.Parse(value);
								}
								else if (propertyType.IsAssignableFrom(typeof(NodeInfo.Position)))
								{
									var components = value.Split(',');

									// we expect 2 components: x and y
									if (components.Length != 2)
									{
										throw new FormatException();
									}

									var position = new NodeInfo.Position();
									position.x = int.Parse(components[0]);
									position.y = int.Parse(components[1]);

									convertedValue = position;
								}
								else {
									throw new NotSupportedException();
								}
								// we need to box this because structs are value types,
								// so calling SetValue using 'node' would just modify a copy of 'node'
								object box = node;
								property.SetValue(box, convertedValue, null);
								node = (NodeInfo)box;
								break;
							}
							catch (FormatException)
							{
//									dialogue.LogErrorMessage(string.Format("{0}: Error setting '{1}': invalid value '{2}'", lineNumber, field, value));
								Debug.Log(string.Format("{0}: Error setting '{1}': invalid value '{2}'", lineNumber, field, value));
							}
							catch (NotSupportedException)
							{
//								dialogue.LogErrorMessage(string.Format("{0}: Error setting '{1}': This property cannot be set", lineNumber, field));
								Debug.Log(string.Format("{0}: Error setting '{1}': This property cannot be set", lineNumber, field));
							}
						}
					} while ((line = reader.ReadLine()) != "---");

					lineNumber++;

					// We're past the header; read the body

					var lines = new List<string>();

					// Read header lines until we hit the end of node sentinel or the end of the file
					while ((line = reader.ReadLine()) != "===" && line != null)
					{
						lineNumber++;
						lines.Add(line);
					}
					// We're done reading the lines! Zip 'em up into a string and
					// store it in the body
					node.body = string.Join("\n", lines.ToArray());

					// And add this node to the list
					nodes.Add(node);

					// And now we're ready to move on to the next line!
				}
			}

			// hooray we're done
			return nodes.ToArray();
		}

	}

}