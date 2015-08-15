using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Weave
{
	class SymbolReader
	{
		private List<SymbolEntry> symbols = new List<SymbolEntry>();

		public SymbolReader(ModuleDefinition module)
		{
			var field = module.GetType().GetField("SymbolReader", BindingFlags.Instance | BindingFlags.NonPublic);
			var reader = field.GetValue(module);
			field = reader.GetType().GetField("functions", BindingFlags.Instance | BindingFlags.NonPublic);
			var functions = field.GetValue(reader);
			var valuesProperty = functions.GetType().GetProperty("Values");
			var values = valuesProperty.GetValue(functions, null);
			var enumeratorMethod = values.GetType().GetMethod("GetEnumerator");
			var enumerator = (IEnumerator<object>)enumeratorMethod.Invoke(values, null);
			while(enumerator.MoveNext())
			{
				var function = enumerator.Current;
				var functionType = function.GetType();
				field = functionType.GetField("module", BindingFlags.Instance | BindingFlags.NonPublic);
				var moduleName = (string)field.GetValue(function);
				field = functionType.GetField("name", BindingFlags.Instance | BindingFlags.NonPublic);
				var functionName = (string)field.GetValue(function);
				field = functionType.GetField("lines", BindingFlags.Instance | BindingFlags.NonPublic);
				var lines = (Array)field.GetValue(function);
				var line = lines.Cast<object>().First();
				field = line.GetType().GetField("file", BindingFlags.Instance | BindingFlags.NonPublic);
				var file = field.GetValue(line);
				field = file.GetType().GetField("name", BindingFlags.Instance | BindingFlags.NonPublic);
				var filePath = (string)field.GetValue(file);
				field = line.GetType().GetField("lines", BindingFlags.Instance | BindingFlags.NonPublic);
				lines = (Array)field.GetValue(line);
				line = lines.Cast<object>().First();
				field = line.GetType().GetField("lineBegin", BindingFlags.Instance | BindingFlags.NonPublic);
				var lineNumber = (uint)field.GetValue(line);
				symbols.Add(new SymbolEntry
				{
					ModuleName = moduleName,
					FunctionName = functionName,
					FilePath = filePath,
					LineNumber = lineNumber,
				});
			}
			enumerator.Dispose();
		}

		public string FormatMethod(string typeName, string methodName)
		{
			var symbol = symbols.FirstOrDefault(e => e.ModuleName == typeName && e.FunctionName == methodName);
			return Format(symbol);
		}

		public string FormatProperty(string typeName, string propertyName)
		{
			var symbol = symbols.FirstOrDefault(e => e.ModuleName == typeName && e.FunctionName.Substring(1) == "et_" + propertyName);
			return Format(symbol);
		}

		private static string Format(SymbolEntry symbol)
		{
			return symbol != null ? string.Format("{0}({1})", symbol.FilePath, symbol.LineNumber) : Program.InputFilePath;
		}
	}

	class SymbolEntry
	{
		public string ModuleName { get; set; }
		public string FunctionName { get; set; }
		public string FilePath { get; set; }
		public uint LineNumber { get; set; }
	}
}
