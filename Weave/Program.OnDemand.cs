using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static bool WeaveOnDemand()
		{
			var q = from t in module.Types
					from p in t.Properties
					let a = p.CustomAttributes.FirstOrDefault(c => c.IsNamed("OnDemand"))
					where a != null
					select new { Type = t, Property = p, Attribute = a };
			q = q.ToList();
			foreach(var item in q)
			{
				try
				{
					var type = item.Type;
					var property = item.Property;
					if(isRemoving)
						property.CustomAttributes.Remove(item.Attribute);
					CheckWarning(property.GetMethod != null, "no getter for " + property.Name, property);
					if(!property.GetMethod.CustomAttributes.Any(c => c.IsNamed("CompilerGenerated")))
					{
						// If there is no setter, assume all of the code in the
						// getter is the property initializer.
						CheckWarning(property.SetMethod == null,
							property.Name + " must be compiler-generated or have no setter", property);
						var backingField = new FieldDefinition("Weave$" + property.Name,
							FieldAttributes.Private, property.PropertyType);
						type.Fields.Add(backingField);
						var methodBody = property.GetMethod.Body;
						var processor = methodBody.GetILProcessor();
						var instructions = methodBody.Instructions;
						Instruction ldarg0 = processor.Create(OpCodes.Ldarg_0);
						Instruction first = instructions[0];
						if(first.OpCode == OpCodes.Nop)
						{
							// This is a debug build.
							first = instructions[1];
							Instruction last = instructions.Last(i => i.OpCode == OpCodes.Stloc_0 || i.OpCode == OpCodes.Stloc_1);
							processor.InsertBefore(last, processor.Create(OpCodes.Stfld, backingField));
							processor.InsertBefore(last, ldarg0);
							processor.InsertBefore(last, processor.Create(OpCodes.Ldfld, backingField));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldfld, backingField));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldnull));
							processor.InsertBefore(first, processor.Create(OpCodes.Ceq));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldc_I4_0));
							processor.InsertBefore(first, processor.Create(OpCodes.Ceq));
						}
						else
						{
							Instruction ret = instructions.Last();
							processor.InsertBefore(ret, processor.Create(OpCodes.Stfld, backingField));
							processor.InsertBefore(ret, ldarg0);
							processor.InsertBefore(ret, processor.Create(OpCodes.Ldfld, backingField));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
							processor.InsertBefore(first, processor.Create(OpCodes.Ldfld, backingField));
						}
						processor.InsertBefore(first, processor.Create(OpCodes.Brtrue, ldarg0));
						processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
					}
					else
					{
						// Allow a type parameter to specify the type to create.
						TypeReference constructionType = GetConstructionType(property, item.Attribute);
						MethodReference constructorMethod = type.Methods.FirstOrDefault(m => m.Name == "Create" + property.Name
							&& m.Parameters.Count == 0 && m.ReturnType.FullName == property.PropertyType.FullName);
						var methodBody = property.GetMethod.Body;
						var processor = methodBody.GetILProcessor();
						var instructions = methodBody.Instructions;
						var backingField = instructions[1].Operand as FieldReference;
						CheckWarning(backingField != null, "unexpected IL for " + property.Name + " getter", property);
						CheckWarning(backingField.FieldType == property.PropertyType, "backing field type does not match property type of " + property.Name, property);
						MethodReference fieldTypeCtor = (constructionType ?? property.PropertyType).Resolve().Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 0);
						CheckWarning(constructorMethod != null || fieldTypeCtor != null,
							"no parameterless creator or ctor for " + property.PropertyType.Name, property);
						Instruction first = instructions.First();
						processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
						processor.InsertBefore(first, processor.Create(OpCodes.Ldfld, backingField));
						processor.InsertBefore(first, processor.Create(OpCodes.Brtrue_S, first));
						processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
						if(constructorMethod != null)
						{
							constructorMethod = module.Import(constructorMethod);
							processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
							processor.InsertBefore(first, processor.Create(OpCodes.Call, constructorMethod));
						}
						else
						{
							fieldTypeCtor = module.Import(fieldTypeCtor);
							processor.InsertBefore(first, processor.Create(OpCodes.Newobj, fieldTypeCtor));
						}
						processor.InsertBefore(first, processor.Create(OpCodes.Stfld, backingField));
					}
				}
				catch(WarningException wex)
				{
					Console.WriteLine(wex.Message);
				}
			}
			return q.Any();
		}

		private static TypeReference GetConstructionType(PropertyDefinition property, CustomAttribute attribute)
		{
			string baseTypeName = property.PropertyType.FullName;
			var consq = from a in attribute.ConstructorArguments
						where a.Type.FullName == "System.Type"
						select (TypeReference)a.Value;
			var propq = from a in attribute.Properties
						where a.Name == "Type" && a.Argument.Type.FullName == "System.Type"
						select (TypeReference)a.Argument.Value;
			TypeReference constructionType = consq.Concat(propq).FirstOrDefault();
			if(constructionType != null)
			{
				// Check that it implements or inherits the property type.
				if(property.PropertyType.Resolve().IsInterface)
				{
					CheckWarning(constructionType.Resolve().Interfaces.Any(i => i.FullName == baseTypeName),
						constructionType.FullName + " does not implement " + baseTypeName, property);
				}
				else
				{
					TypeReference type = constructionType;
					while(type != null && type.FullName != baseTypeName)
						type = type.Resolve().BaseType;
					CheckWarning(type != null, constructionType.FullName + " does not inherit " + baseTypeName, property);
				}
			}
			return constructionType;
		}
	}
}
