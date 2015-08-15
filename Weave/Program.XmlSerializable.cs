using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static MethodReference objectToStringMethod;
		static TypeReference xmlElementType;
		static TypeReference xmlIgnoreType;

		static bool WeaveXmlSerializable()
		{
			var typeq = from t in module.Types
						let a = t.CustomAttributes.FirstOrDefault(i => i.IsNamed("XmlSerializable"))
						where a != null
						let p = ExtractXmlSerializableAttributeParameters(t, a)
						let n = ExtractNameCaseStyle(p)
						let s = ExtractSerializationStyle(p)
						select new { Type = t, Attribute = a, NameCaseStyle = n, SerializationStyle = s };
			typeq = typeq.ToList();
			foreach(var typeItem in typeq)
			{
				try
				{
					var type = typeItem.Type;
					var xmlSerializationStyle = typeItem.SerializationStyle;
					var nameCaseStyle = typeItem.NameCaseStyle;
					if(isRemoving)
						type.CustomAttributes.Remove(typeItem.Attribute);
					var propq = from p in type.Properties
								let c = p.CustomAttributes
								where !c.Any(i => i.IsNamed("XmlIgnore"))
								select p;
					propq = propq.ToList();
					foreach(var property in propq)
					{
						try
						{
							switch(property.PropertyType.FullName)
							{
							case "System.DateTime":
							case "System.TimeSpan":
							case "System.Uri":
								var getter = new MethodDefinition("get_Weave$" + property.Name,
									MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
									module.TypeSystem.String) { DeclaringType = type, IsGetter = true };
								type.Methods.Add(getter);
								var body = getter.Body;
								body.InitLocals = true;
								body.Variables.Add(new VariableDefinition(property.PropertyType));
								var instructions = getter.Body.Instructions;
								var processor = body.GetILProcessor();
								processor.Append(processor.Create(OpCodes.Ldarg_0));
								processor.Append(processor.Create(OpCodes.Call, property.GetMethod));
								processor.Append(processor.Create(OpCodes.Stloc_0));
								if(property.PropertyType.IsValueType)
								{
									processor.Append(processor.Create(OpCodes.Ldloca_S, (byte)0));
									processor.Append(processor.Create(OpCodes.Constrained, property.PropertyType));
								}
								else
								{
									processor.Append(processor.Create(OpCodes.Ldloc_0));
									processor.Append(processor.Create(OpCodes.Ldnull));
									var inequalityOperator = ResolveMethod(property, property.PropertyType, "op_Inequality", "System.Uri", "System.Uri");
									processor.Append(processor.Create(OpCodes.Call, inequalityOperator));
									Instruction ldloc0 = processor.Create(OpCodes.Ldloc_0);
									processor.Append(ldloc0);
									processor.InsertBefore(ldloc0, processor.Create(OpCodes.Brtrue_S, ldloc0));
									processor.InsertBefore(ldloc0, processor.Create(OpCodes.Ldnull));
									// TODO:  this additional return causes post-invoke methods to fail.  Perhaps use try-finally blocks for those.
									processor.InsertBefore(ldloc0, processor.Create(OpCodes.Ret));
								}
								processor.Append(processor.Create(OpCodes.Callvirt, objectToStringMethod));
								processor.Append(processor.Create(OpCodes.Ret));
								var setter = new MethodDefinition("set_Weave$" + property.Name,
									MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
									module.TypeSystem.Void) { DeclaringType = type, IsSetter = true };
								setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
								type.Methods.Add(setter);
								body = setter.Body;
								body.InitLocals = true;
								body.Variables.Add(new VariableDefinition(property.PropertyType));
								instructions = setter.Body.Instructions;
								processor = body.GetILProcessor();
								processor.Append(processor.Create(OpCodes.Ldarg_1));
								if(!property.PropertyType.IsValueType)
									processor.Append(processor.Create(OpCodes.Ldc_I4_0));
								processor.Append(processor.Create(OpCodes.Ldloca_S, (byte)0));
								var parseMethod = property.PropertyType.IsValueType
									? ResolveMethod(property, property.PropertyType, "TryParse", "System.String", property.PropertyType.FullName + '&')
									: ResolveMethod(property, property.PropertyType, "TryCreate", "System.String", "System.UriKind", property.PropertyType.FullName + '&');
								processor.Append(processor.Create(OpCodes.Call, parseMethod));
								processor.Append(processor.Create(OpCodes.Pop));
								processor.Append(processor.Create(OpCodes.Ldarg_0));
								processor.Append(processor.Create(OpCodes.Ldloc_0));
								processor.Append(processor.Create(OpCodes.Call, property.SetMethod));
								processor.Append(processor.Create(OpCodes.Ret));
								var stringProperty = new PropertyDefinition("Weave$" + property.Name,
									PropertyAttributes.None, module.TypeSystem.String) { HasThis = true };
								type.Properties.Add(stringProperty);
								stringProperty.GetMethod = getter;
								stringProperty.SetMethod = setter;
								stringProperty.CustomAttributes.Add(CreateXmlAttribute(property, property.Name, nameCaseStyle, xmlSerializationStyle));
								var ignoreCtor = ResolveMethod(property, xmlIgnoreType, ".ctor");
								property.CustomAttributes.Add(new CustomAttribute(ignoreCtor));
								break;
							default:
								if(!property.CustomAttributes.Any(a => a.AttributeType.Name.StartsWith("Xml")))
									property.CustomAttributes.Add(CreateXmlAttribute(property, null, nameCaseStyle, xmlSerializationStyle));
								break;
							}
						}
						catch(WarningException wex)
						{
							Console.WriteLine(wex.Message);
						}
					}
				}
				catch(WarningException wex)
				{
					Console.WriteLine(wex.Message);
				}
			}
			return typeq.Any();
		}

		private static CustomAttribute CreateXmlAttribute(PropertyDefinition property, string propertyName, string nameCaseStyle, TypeReference xmlSerializationStyle)
		{
			string name = property.Name;
			name = nameCaseStyle == "CamelCase"
				? char.ToLowerInvariant(name[0]) + name.Substring(1)
				: nameCaseStyle == "PascalCase"
				? name = char.ToUpperInvariant(name[0]) + name.Substring(1)
				: propertyName;
			MethodReference attributeCtor;
			CustomAttribute attribute;
			if(name != null)
			{
				attributeCtor = ResolveMethod(property, xmlSerializationStyle, ".ctor", "System.String");
				attribute = new CustomAttribute(attributeCtor);
				attribute.ConstructorArguments.Add(new CustomAttributeArgument(attributeCtor.Parameters[0].ParameterType, name));
			}
			else
			{
				attributeCtor = ResolveMethod(property, xmlSerializationStyle, ".ctor");
				attribute = new CustomAttribute(attributeCtor);
			}
			return attribute;
		}

		private static List<CustomAttributeArgument> ExtractXmlSerializableAttributeParameters(TypeDefinition type, CustomAttribute attribute)
		{
			ExtractXmlSerializableMembers();
			return attribute.Properties.Select(a => a.Argument).Concat(attribute.ConstructorArguments).ToList();
		}

		private static TypeReference ExtractSerializationStyle(List<CustomAttributeArgument> arguments)
		{
			var seriq = from a in arguments
						where a.Type.FullName == "System.Type"
						let v = a.Value
						let s = v.ToString()
						where s == "System.Xml.Serialization.XmlAttributeAttribute"
						|| s == "System.Xml.Serialization.XmlElementAttribute"
						select (TypeReference)v;
			return seriq.FirstOrDefault() ?? xmlElementType;
		}

		private static string ExtractNameCaseStyle(List<CustomAttributeArgument> arguments)
		{
			var caseq = from a in arguments
						let t = a.Type as TypeDefinition
						where t != null
						let v = t.BaseType.FullName == "System.Enum"
						? from f in t.Fields
						  where a.Value.Equals(f.Constant)
						  select f.Name
						: new[] { a.Value.ToString() }
						select v.FirstOrDefault();
			return caseq.FirstOrDefault();
		}

		private static void ExtractXmlSerializableMembers()
		{
			if(xmlIgnoreType == null)
			{
				var objectType = module.TypeSystem.Object;
				objectToStringMethod = ResolveMethod(module, objectType, "ToString");
				var q = from a in new[] { resolver.Resolve("System.Xml") }
						where a != null
						from t in a.MainModule.Types
						select t;
				q = q.ToList();
				xmlElementType = ExtractXmlAttributeType(q, "XmlElementAttribute");
				xmlIgnoreType = ExtractXmlAttributeType(q, "XmlIgnoreAttribute");
			}
		}

		private static TypeReference ExtractXmlAttributeType(IEnumerable<TypeDefinition> types, string typeName)
		{
			var q = from t in types
					where t.FullName == "System.Xml.Serialization." + typeName
					select module.Import(t);
			var type = q.FirstOrDefault();
			CheckWarning(type != null, "cannot resolve " + typeName);
			return type;
		}
	}
}
