using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static TypeReference typeType;
		static TypeReference dependencyObjectType;
		static TypeReference dependencyPropertyType;
		static MethodReference registerMethod;
		static MethodReference getTypeFromHandleMethod;
		static MethodReference getValueMethod;
		static MethodReference setValueMethod;
		static MethodReference propertyChangedCallbackCtor;
		static TypeReference propertyMetadataType;

		static bool WeaveDependency()
		{
			var q = from t in module.Types
					from p in t.Properties
					let a = p.CustomAttributes.FirstOrDefault(c => c.IsNamed("Dependency"))
					where a != null
					select new { Property = p, Attribute = a };
			q = q.ToList();
			foreach(var item in q)
			{
				try
				{
					var property = item.Property;
					var type = property.DeclaringType;
					var attribute = item.Attribute;
					ExtractDependencyTypes(property);
					CheckCompilerGenerated(property, property.GetMethod);
					CheckCompilerGenerated(property, property.SetMethod);
					var methodBody = property.GetMethod.Body;
					var backingField = methodBody.Instructions.Count > 1 ? methodBody.Instructions[1].Operand as FieldDefinition : null;
					CheckWarning(backingField != null,
						"unexpected IL for " + property.Name + " getter", property);
					var defaultValue = attribute.Properties.Where(a => a.Name == "DefaultValue").Select(a => a.Argument).FirstOrDefault();
					var instructionActions = ValidateDefaultValue(defaultValue, property).ToList();
					string methodName = "On" + property.Name + "Changed";
					var w = from m in type.Methods
							where m.Name == methodName
							let b = m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == dependencyObjectType.FullName
							&& m.Parameters[1].ParameterType.FullName == "System.Windows.DependencyPropertyChangedEventArgs"
							&& m.ReturnType.FullName == "System.Void"
							orderby b ? 0 : 1
							select new { Method = m, HasMatchingParameters = b };
					MethodReference changeNotificationHandler = w.Where(a => a.HasMatchingParameters).Select(a => a.Method).FirstOrDefault();
					CheckMethodWarning(!w.Any() || changeNotificationHandler != null,
						methodName + " has wrong signature", type.FullName, methodName);
					if(isRemoving)
						property.CustomAttributes.Remove(attribute);

					// Change the compiler generated backing field to the right
					// access, name, and type.
					backingField.Attributes = FieldAttributes.FamANDAssem | FieldAttributes.InitOnly | FieldAttributes.Public | FieldAttributes.Static;
					backingField.FieldType = dependencyPropertyType;
					backingField.Name = property.Name + "Property";

					// Modify the setter to set the dependency property value.
					methodBody = property.SetMethod.Body;
					var processor = methodBody.GetILProcessor();
					var instructions = methodBody.Instructions;
					Instruction ret = instructions.Last();
					Instruction two = instructions[2];
					var dependencyPropertyField = two.Operand as FieldDefinition;
					two.OpCode = OpCodes.Ldsfld;
					instructions.RemoveAt(2);
					instructions.Insert(1, two);
					if(property.PropertyType.IsValueType)
						processor.InsertBefore(ret, processor.Create(OpCodes.Box, property.PropertyType));
					processor.InsertBefore(ret, processor.Create(OpCodes.Call, setValueMethod));

					// Modify the getter to get the dependency property value.
					methodBody = property.GetMethod.Body;
					processor = methodBody.GetILProcessor();
					instructions = methodBody.Instructions;
					instructions[1].OpCode = OpCodes.Ldsfld;
					ret = instructions[2];
					processor.InsertBefore(ret, processor.Create(OpCodes.Call, getValueMethod));
					if(property.PropertyType.IsValueType)
						processor.InsertBefore(ret, processor.Create(OpCodes.Unbox_Any, property.PropertyType));
					if(instructions.Count > 5)
					{
						// Match debug IL in the getter and setter.
						processor.InsertBefore(instructions[0], processor.Create(OpCodes.Nop));
						methodBody = property.SetMethod.Body;
						processor = methodBody.GetILProcessor();
						instructions = methodBody.Instructions;
						processor.InsertBefore(instructions[0], processor.Create(OpCodes.Nop));
						processor.InsertBefore(instructions.Last(), processor.Create(OpCodes.Nop));
					}

					// Add code to the static constructor to initialize the
					// dependency property field.
					var cctor = type.Methods.FirstOrDefault(m => m.Name == ".cctor");
					if(cctor == null)
					{
						var attributes = MethodAttributes.HideBySig | MethodAttributes.Private
							| MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Static;
						cctor = new MethodDefinition(".cctor", attributes, module.TypeSystem.Void);
						type.Methods.Add(cctor);
					}
					methodBody = cctor.Body;
					processor = methodBody.GetILProcessor();
					instructions = methodBody.Instructions;
					Instruction first = instructions.FirstOrDefault();
					Action<int> InsertInt = i =>
					{
						OpCode opCode = i >= 0 && i < 9 ? (OpCode)typeof(OpCodes).GetField("Ldc_I4_" + i).GetValue(null) :
							i == -1 ? OpCodes.Ldc_I4_M1 : OpCodes.Ldc_I4_S;
						processor.InsertBefore(first, i == (sbyte)i ? opCode == OpCodes.Ldc_I4_S ?
							processor.Create(opCode, (sbyte)i) : processor.Create(opCode) : processor.Create(OpCodes.Ldc_I4, i));
					};
					if(first == null)
						processor.Append(first = processor.Create(OpCodes.Ret));
					processor.InsertBefore(first, processor.Create(OpCodes.Ldstr, property.Name));
					processor.InsertBefore(first, processor.Create(OpCodes.Ldtoken, property.PropertyType));
					processor.InsertBefore(first, processor.Create(OpCodes.Call, getTypeFromHandleMethod));
					processor.InsertBefore(first, processor.Create(OpCodes.Ldtoken, type));
					processor.InsertBefore(first, processor.Create(OpCodes.Call, getTypeFromHandleMethod));
					if(defaultValue.Value != null || changeNotificationHandler != null)
					{
						var parameterTypeNames = new List<string>();
						if(defaultValue.Value != null)
						{
							foreach(var action in instructionActions)
								action(processor, first, InsertInt);
							parameterTypeNames.Add("System.Object");
						}
						if(changeNotificationHandler != null)
						{
							processor.InsertBefore(first, processor.Create(OpCodes.Ldnull));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldftn, changeNotificationHandler));
							processor.InsertBefore(first, processor.Create(OpCodes.Newobj, propertyChangedCallbackCtor));
							parameterTypeNames.Add("System.Windows.PropertyChangedCallback");
						}
						MethodReference propertyMetadataCtor = ResolveMethod(property, propertyMetadataType, ".ctor", parameterTypeNames.ToArray());
						processor.InsertBefore(first, processor.Create(OpCodes.Newobj, propertyMetadataCtor));
					}
					else
						processor.InsertBefore(first, processor.Create(OpCodes.Ldnull));
					processor.InsertBefore(first, processor.Create(OpCodes.Call, registerMethod));
					processor.InsertBefore(first, processor.Create(OpCodes.Stsfld, dependencyPropertyField));
				}
				catch(WarningException wex)
				{
					Console.WriteLine(wex.Message);
				}
			}
			return q.Any();
		}

		private static IEnumerable<Action<ILProcessor, Instruction, Action<int>>> ValidateDefaultValue(CustomAttributeArgument defaultValue, PropertyDefinition property)
		{
			if(defaultValue.Value == null)
				yield break;
			decimal defaultDecimalValue = 0;
			CheckWarning(defaultValue.Type.FullName == property.PropertyType.FullName
				|| (property.PropertyType.FullName == "System.Decimal"
				&& decimal.TryParse(defaultValue.Value.ToString().TrimEnd('M', 'm'), out defaultDecimalValue)),
				property.Name + " default value type does not match the property type", property);
			if(property.PropertyType.IsValueType)
			{
				switch(property.PropertyType.FullName)
				{
				case "System.Bool":
					yield return (p, i, a) => p.InsertBefore(i, p.Create((bool)defaultValue.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
					break;
				case "System.Byte":
				case "System.SByte":
				case "System.Char":
				case "System.Int16":
				case "System.UInt16":
				case "System.Int32":
				case "System.UInt32":
					int defaultIntValue = defaultValue.Value.ToString()[0] == '-' ?
						Convert.ToInt32(defaultValue.Value) :
						(int)Convert.ToUInt32(defaultValue.Value);
					yield return (p, i, a) => a(defaultIntValue);
					break;
				case "System.Int64":
				case "System.UInt64":
					long defaultLongValue = defaultValue.Value.ToString()[0] == '-' ?
						Convert.ToInt64(defaultValue.Value) :
						(long)Convert.ToUInt64(defaultValue.Value);
					if(defaultLongValue == (int)defaultLongValue)
					{
						yield return (p, i, a) => a((int)defaultLongValue);
						yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Conv_I8));
					}
					else
						yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldc_I8, defaultLongValue));
					break;
				case "System.Single":
					yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldc_R4, (float)defaultValue.Value));
					break;
				case "System.Double":
					yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldc_R8, (double)defaultValue.Value));
					break;
				case "System.Decimal":
					if(defaultDecimalValue != 0)
					{
						defaultLongValue = defaultDecimalValue.ConvertTo<long>();
						if(defaultLongValue == 0)
						{
							int[] values = decimal.GetBits(defaultDecimalValue);
							foreach(int value in values.Take(3))
							{
								int n = value; // Local for lambda.
								yield return (p, i, a) => a(n);
							}
							yield return (p, i, a) => p.InsertBefore(i, p.Create(values[3] < 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
							yield return (p, i, a) => a((values[3] >> 16) & 0xff);
							MethodReference decimalCtor = ResolveMethod(property, property.PropertyType, ".ctor",
								"System.Int32", "System.Int32", "System.Int32", "System.Boolean", "System.Byte");
							yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Newobj, decimalCtor));
						}
						else if(defaultLongValue == (int)defaultLongValue)
						{
							yield return (p, i, a) => a((int)defaultLongValue);
							MethodReference decimalCtor = ResolveMethod(property, property.PropertyType, ".ctor", "System.Int32");
							yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Newobj, decimalCtor));
						}
						else
						{
							yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldc_I8, defaultLongValue));
							MethodReference decimalCtor = ResolveMethod(property, property.PropertyType, ".ctor", "System.Int64");
							yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Newobj, decimalCtor));
						}
					}
					else
					{
						yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldc_I4_0));
						MethodReference decimalCtor = ResolveMethod(property, property.PropertyType, ".ctor", "System.Int32");
						yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Newobj, decimalCtor));
					}
					break;
				default:
					CheckWarning(false, "unknown value type:  " + property.PropertyType.FullName, property);
					break;
				}
				yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Box, property.PropertyType));
			}
			else if(defaultValue.Type.FullName == "System.String")
				yield return (p, i, a) => p.InsertBefore(i, p.Create(OpCodes.Ldstr, (string)defaultValue.Value));
			else
				CheckWarning(false, "invalid default value type", property);
		}

		private static void ExtractDependencyTypes(PropertyDefinition contextProperty)
		{
			if(propertyChangedCallbackCtor == null)
			{
				TypeDefinition type = contextProperty.DeclaringType;
				dependencyObjectType = type.BaseType;
				while(dependencyObjectType != null && dependencyObjectType.FullName != "System.Windows.DependencyObject")
					dependencyObjectType = dependencyObjectType.Resolve().BaseType;
				CheckWarning(dependencyObjectType != null, "warning:  " + type.Name + " does not inherit from DependencyObject", contextProperty);
				getValueMethod = ResolveMethod(contextProperty, dependencyObjectType, "GetValue",
					"System.Windows.DependencyProperty");
				setValueMethod = ResolveMethod(contextProperty, dependencyObjectType, "SetValue",
					"System.Windows.DependencyProperty", "System.Object");
				ModuleDefinition module = contextProperty.Module;
				typeType = module.TypeSystem.Object.Resolve().Methods.First(m => m.ReturnType.FullName == "System.Type").ReturnType;
				typeType = module.Import(typeType);
				getTypeFromHandleMethod = ResolveMethod(contextProperty, typeType, "GetTypeFromHandle",
					"System.RuntimeTypeHandle");
				var q = from m in dependencyObjectType.Resolve().Methods
						from p in m.Parameters
						where p.ParameterType.FullName == "System.Windows.DependencyProperty"
						select p.ParameterType;
				dependencyPropertyType = q.First();
				dependencyPropertyType = module.Import(dependencyPropertyType);
				registerMethod = ResolveMethod(contextProperty, dependencyPropertyType, "Register",
					"System.String", "System.Type", "System.Type", "System.Windows.PropertyMetadata");
				q = from p in registerMethod.Parameters
					where p.ParameterType.FullName == "System.Windows.PropertyMetadata"
					select p.ParameterType;
				propertyMetadataType = q.First();
				q = from m in propertyMetadataType.Resolve().Methods
					from p in m.Parameters
					where p.ParameterType.FullName == "System.Windows.PropertyChangedCallback"
					select p.ParameterType;
				TypeReference propertyChangedCallback = q.First();
				propertyChangedCallbackCtor = ResolveMethod(contextProperty, propertyChangedCallback, ".ctor", "System.Object", "System.IntPtr");
			}
		}
	}
}
