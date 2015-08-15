using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static bool WeaveInitialValue()
		{
			var q = from t in module.Types
					from p in t.Properties
					let a = p.CustomAttributes.FirstOrDefault(c => c.IsNamed("InitialValue"))
					where a != null
					select new { Type = t, Property = p, Attribute = a };
			q = q.ToList();
			foreach(var item in q)
			{
				try
				{
					var type = item.Type;
					var property = item.Property;
					var attribute = item.Attribute;
					if(isRemoving)
						property.CustomAttributes.Remove(attribute);
					CheckWarning(property.GetMethod.CustomAttributes.Any(c => c.IsNamed("CompilerGenerated")),
						property.Name + " must be compiler-generated", property);
					CheckWarning(property.GetMethod != null, "no getter for " + property.Name, property);
					CheckWarning(attribute.ConstructorArguments.Any(), "The InitialValue attribute instance has no constructor arguments", property);
					var attributeArgument = attribute.ConstructorArguments[0];
					CheckWarning(attributeArgument.Type.FullName == property.PropertyType.FullName,
						"The InitialValue attribute constructor argument has the wrong type", property);

					// Get the backing field for the property.
					var methodBody = property.GetMethod.Body;
					var processor = methodBody.GetILProcessor();
					var instructions = methodBody.Instructions;
					var backingField = instructions[1].Operand as FieldReference;
					CheckWarning(backingField != null, "unexpected IL for " + property.Name + " getter", property);
					CheckWarning(backingField.FieldType == property.PropertyType, "backing field type does not match property type of " + property.Name, property);

					// Get the default constructor.
					var ctor = ResolveMethod(property, type, ".ctor");
					CheckWarning(ctor != null, "No default ctor for " + type.Name, property);
					methodBody = ctor.Resolve().Body;
					processor = methodBody.GetILProcessor();
					instructions = methodBody.Instructions;

					// TODO
					CheckWarning(attributeArgument.Type.FullName == "System.String" || attributeArgument.Type.FullName == "System.Int32",
						attributeArgument.Type.FullName + " not implemented", property);

					// Add the instructions to initialize the backing field.
					Instruction first = instructions.First();
					Action<int> InsertInt = i =>
					{
						OpCode opCode = i >= 0 && i < 9 ? (OpCode)typeof(OpCodes).GetField("Ldc_I4_" + i).GetValue(null) :
							i == -1 ? OpCodes.Ldc_I4_M1 : OpCodes.Ldc_I4_S;
						processor.InsertBefore(first, i == (sbyte)i ? opCode == OpCodes.Ldc_I4_S ?
							processor.Create(opCode, (sbyte)i) : processor.Create(opCode) : processor.Create(OpCodes.Ldc_I4, i));
					};
					processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
					if(attributeArgument.Type.FullName == "System.String")
					{
						if(attributeArgument.Value == null)
						{
							string filePathAndLine = symbolReader.FormatProperty(property.PropertyType.FullName, property.Name);
							string message = WarningException.ConstructMessage(filePathAndLine, "The InitialValue attribute constructor argument is null; using empty");
							Console.WriteLine(message);
						}
						var stringValue = (string)attributeArgument.Value ?? "";
						processor.InsertBefore(first, processor.Create(OpCodes.Ldstr, stringValue));
					}
					else
						InsertInt((int)attributeArgument.Value);
					processor.InsertBefore(first, processor.Create(OpCodes.Stfld, backingField));
				}
				catch(WarningException wex)
				{
					Console.WriteLine(wex.Message);
				}
			}
			return q.Any();
		}
	}
}
