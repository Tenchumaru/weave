using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static TypeReference propertyChangedEventHandlerType;
		static MethodReference propertyChangedEventHandlerInvokeMethod;
		static MethodReference propertyChangedEventArgsCtor;

		static bool WeaveNotify()
		{
			var q = from t in module.Types
					let b = t.CustomAttributes.FirstOrDefault(c => c.IsNamed("Notify"))
					where b == null || !b.ConstructorArguments.Select(c => c.Value).OfType<bool>().Any(v => !v)
					let m = b != null ? b.ConstructorArguments.Select(c => c.Value).OfType<string>().FirstOrDefault() : null
					from p in t.Properties
					let a = p.CustomAttributes.FirstOrDefault(c => c.IsNamed("Notify"))
					where b == null
						? a != null && !a.ConstructorArguments.Select(c => c.Value).OfType<bool>().Any(v => !v)
						: a == null || !a.ConstructorArguments.Select(c => c.Value).OfType<bool>().Any(v => !v)
					select new { Property = p, Attribute = a, MethodName = m };
			q = q.ToList();
			foreach(var item in q)
			{
				try
				{
					var property = item.Property;
					var type = property.DeclaringType;
					if(item.Attribute != null)
					{
						if(isRemoving)
							property.CustomAttributes.Remove(item.Attribute);
						CheckCompilerGenerated(property, property.SetMethod);
					}
					else if(property.SetMethod == null || !property.SetMethod.CustomAttributes.Any(c => c.IsNamed("CompilerGenerated")))
						continue; // Skip non-automatic properties when weaving the entire class.
					MethodDefinition notifyingMethod = null;
					for(TypeDefinition baseType = type; baseType != null && notifyingMethod == null; baseType = baseType.BaseType != null ? null : baseType.BaseType.Resolve())
						notifyingMethod = baseType.Methods.FirstOrDefault(m => m.Name == item.MethodName);
					var methodBody = property.SetMethod.Body;
					var processor = methodBody.GetILProcessor();
					var instructions = methodBody.Instructions;
					Instruction first = instructions.First();
					Instruction ret = instructions.Last();
					var backingField = instructions[2].Operand as FieldReference;
					CheckWarning(backingField != null && ret.OpCode == OpCodes.Ret,
						"unexpected IL for " + property.Name + " setter", property);
					if(notifyingMethod != null)
					{
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldarg_0));
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldstr, property.Name));
						processor.InsertBefore(ret, processor.Create(OpCodes.Call, module.Import(notifyingMethod)));
					}
					else
					{
						FieldReference propertyChangedEvent = type.Fields.FirstOrDefault(f => f.Name == "PropertyChanged");
						CheckWarning(propertyChangedEvent != null,
							"no method or event for notification of " + property.Name, property);
						ExtractNotifyTypes(propertyChangedEvent, property);
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldarg_0));
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldfld, propertyChangedEvent));
						if(!type.CtorInitializesEvent(propertyChangedEvent))
						{
							methodBody.InitLocals = true;
							methodBody.Variables.Add(new VariableDefinition(propertyChangedEventHandlerType));
							processor.InsertBefore(ret, processor.Create(OpCodes.Stloc_0));
							processor.InsertBefore(ret, processor.Create(OpCodes.Ldloc_0));
							processor.InsertBefore(ret, processor.Create(OpCodes.Brfalse_S, ret));
							processor.InsertBefore(ret, processor.Create(OpCodes.Ldloc_0));
						}
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldarg_0));
						processor.InsertBefore(ret, processor.Create(OpCodes.Ldstr, property.Name));
						processor.InsertBefore(ret, processor.Create(OpCodes.Newobj, propertyChangedEventArgsCtor));
						processor.InsertBefore(ret, processor.Create(OpCodes.Callvirt, propertyChangedEventHandlerInvokeMethod));
					}
					processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
					processor.InsertBefore(first, processor.Create(OpCodes.Ldfld, backingField));
					processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_1));
					IEnumerable<MethodDefinition> propertyTypeMethods = property.PropertyType.Resolve().Methods;
					MethodReference equalityOperator = propertyTypeMethods.FirstOrDefault(m => m.Name == "op_Equality");
					MethodReference inequalityOperator = propertyTypeMethods.FirstOrDefault(m => m.Name == "op_Inequality");
					if(inequalityOperator != null)
					{
						inequalityOperator = module.Import(inequalityOperator);
						processor.InsertBefore(first, processor.Create(OpCodes.Call, inequalityOperator));
						processor.InsertBefore(first, processor.Create(OpCodes.Brfalse_S, ret));
					}
					else if(equalityOperator != null)
					{
						equalityOperator = module.Import(equalityOperator);
						processor.InsertBefore(first, processor.Create(OpCodes.Call, equalityOperator));
						processor.InsertBefore(first, processor.Create(OpCodes.Brtrue_S, ret));
					}
					else
						processor.InsertBefore(first, processor.Create(OpCodes.Beq_S, ret));
				}
				catch(WarningException wex)
				{
					Console.WriteLine(wex.Message);
				}
			}
			return q.Any();
		}

		private static void ExtractNotifyTypes(FieldReference propertyChangedEvent, PropertyDefinition contextProperty)
		{
			if(propertyChangedEventArgsCtor == null)
			{
				propertyChangedEventHandlerType = propertyChangedEvent.FieldType;
				propertyChangedEventHandlerInvokeMethod = ResolveMethod(contextProperty, propertyChangedEventHandlerType,
					"Invoke", "System.Object", "System.ComponentModel.PropertyChangedEventArgs");
				var q = from p in propertyChangedEventHandlerInvokeMethod.Parameters
						where p.ParameterType.FullName == "System.ComponentModel.PropertyChangedEventArgs"
						select p.ParameterType;
				TypeReference eventArgsType = q.FirstOrDefault();
				propertyChangedEventArgsCtor = ResolveMethod(contextProperty, eventArgsType, ".ctor", "System.String");
			}
		}

		static bool CtorInitializesEvent(this TypeDefinition type, FieldReference propertyChangedEvent)
		{
			var q = from m in type.Methods
					where m.IsConstructor
					from i in m.Body.Instructions
					where i.OpCode == OpCodes.Stfld
					let o = i.Operand as FieldReference
					where o != null && o.Name == propertyChangedEvent.Name
					select o;
			return q.Any();
		}
	}
}
