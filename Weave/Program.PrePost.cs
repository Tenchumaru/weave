using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Weave
{
	static partial class Program
	{
		static bool WeavePrePost()
		{
			MethodReference preInvokeMethod, postInvokeMethod;
			if(!GetInvoke("pre", preInvokeMethodName, module, out preInvokeMethod) & !GetInvoke("post", postInvokeMethodName, module, out postInvokeMethod))
				return false;
			var q = from t in GetAllTypes(module.Types)
					from m in t.Methods
					where m != preInvokeMethod && m != postInvokeMethod
					select m;
			q = q.ToList();
			foreach(var method in q)
			{
				if(preInvokeMethod != null)
				{
					var methodBody = method.Body;
					var processor = methodBody.GetILProcessor();
					var instructions = methodBody.Instructions;
					Instruction first = instructions[0];
					processor.InsertBefore(first, processor.Create(OpCodes.Ldstr, method.FullName));
					processor.InsertBefore(first, processor.Create(OpCodes.Call, preInvokeMethod));
				}
				if(postInvokeMethod != null && (method.Attributes & MethodAttributes.RTSpecialName) == 0)
				{
					if(HasCallsOrThrows(method, preInvokeMethod))
					{
						var innerMethod = new MethodDefinition("Weave$" + method.Name, method.Attributes & ~MethodAttributes.RTSpecialName, method.ReturnType);
						method.Parameters.ToList().ForEach(p => innerMethod.Parameters.Add(p));
						method.DeclaringType.Methods.Add(innerMethod);
						innerMethod.Body = new MethodBody(innerMethod) { InitLocals = method.Body.InitLocals };
						method.Body.ExceptionHandlers.ToList().ForEach(e => innerMethod.Body.ExceptionHandlers.Add(e));
						method.Body.Instructions.ToList().ForEach(i => innerMethod.Body.Instructions.Add(i));
						method.Body.Variables.ToList().ForEach(v => innerMethod.Body.Variables.Add(v));
						method.Body = new MethodBody(method);
						var methodBody = method.Body;
						bool isVoid = method.ReturnType.FullName == "System.Void";
						if(!isVoid)
						{
							methodBody.InitLocals = true;
							methodBody.Variables.Add(new VariableDefinition(method.ReturnType));
						}
						methodBody.Instructions.Clear();
						var processor = methodBody.GetILProcessor();
						Instruction last = processor.Create(OpCodes.Ldloc_0);
						processor.Append(last);
						if(isVoid)
							last.OpCode = OpCodes.Ret;
						else
							processor.Append(processor.Create(OpCodes.Ret));
						for(int i = 0, count = innerMethod.Parameters.Count + (innerMethod.HasThis ? 1 : 0); i < count; ++i)
						{
							var opCode = (OpCode)typeof(OpCodes).GetField("Ldarg_" + i).GetValue(null);
							processor.InsertBefore(last, processor.Create(opCode));
						}
						processor.InsertBefore(last, processor.Create(OpCodes.Call, innerMethod));
						if(!isVoid)
							processor.InsertBefore(last, processor.Create(OpCodes.Stloc_0));
						processor.InsertBefore(last, processor.Create(OpCodes.Leave, last));
						Instruction ldstr = processor.Create(OpCodes.Ldstr, method.FullName);
						processor.InsertBefore(last, ldstr);
						processor.InsertBefore(last, processor.Create(OpCodes.Call, postInvokeMethod));
						processor.InsertBefore(last, processor.Create(OpCodes.Endfinally));
						var instructions = methodBody.Instructions;
						var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
						{
							TryStart = instructions[0],
							TryEnd = ldstr,
							HandlerStart = ldstr,
							HandlerEnd = last,
						};
						methodBody.ExceptionHandlers.Add(handler);
					}
					else
					{
						// If there are more than one ret, replace all but the
						// last with branches to the last ret.
						var methodBody = method.Body;
						var processor = methodBody.GetILProcessor();
						var instructions = methodBody.Instructions;
						var lastRet = instructions.Last(i => i.OpCode == OpCodes.Ret);
						CheckMethodWarning(lastRet == instructions.Last(), "unexpected IL", method.DeclaringType.FullName, method.Name);
						var otherRets = instructions.Reverse().Where(i => i.OpCode == OpCodes.Ret).Skip(1).ToList();
						otherRets.ForEach(i => { i.OpCode = OpCodes.Br; i.Operand = lastRet; });
						Instruction last = instructions.Last();
						Instruction lastLast = processor.Create(OpCodes.Nop);
						lastLast.OpCode = last.OpCode;
						lastLast.Operand = last.Operand;
						last.OpCode = OpCodes.Ldstr;
						last.Operand = method.FullName;
						processor.Append(processor.Create(OpCodes.Call, postInvokeMethod));
						processor.Append(lastLast);
					}
				}
			}
			return q.Any();
		}

		private static bool HasCallsOrThrows(MethodDefinition method, MethodReference preInvokeMethod)
		{
			var q = from i in method.Body.Instructions
					let o = i.OpCode
					where (o == OpCodes.Call && i.Operand != preInvokeMethod) || o == OpCodes.Calli || o == OpCodes.Callvirt
					|| o == OpCodes.Rethrow || o == OpCodes.Throw
					select o;
			return q.Any();
		}

		private static bool GetInvoke(string prePost, string invokeMethodName, ModuleDefinition module, out MethodReference invokeMethod)
		{
			invokeMethod = null;
			if(string.IsNullOrWhiteSpace(invokeMethodName))
				return false;
			string invokeTypeName = string.Join(".", invokeMethodName.Split('.').Reverse().Skip(1).Reverse());
			var q = from m in modules
					from t in m.Types
					where t.FullName == invokeTypeName
					select t;
			TypeReference invokeType = q.FirstOrDefault();
			if(invokeType == null)
			{
				Console.WriteLine("warning:  cannot find type " + invokeTypeName);
				return false;
			}
			invokeType = module.Import(invokeType);
			invokeMethodName = invokeMethodName.Split('.').Last();
			invokeMethod = invokeType.Resolve().Methods.FirstOrDefault(m => m.Name == invokeMethodName);
			if(invokeMethod == null)
			{
				Console.WriteLine("warning:  cannot find " + prePost
					+ "-invoke method " + invokeTypeName + '.' + invokeMethodName);
				return false;
			}
			invokeMethod = module.Import(invokeMethod);
			if(invokeMethod.ReturnType.FullName != "System.Void"
				|| !invokeMethod.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.String" }))
			{
				Console.WriteLine("warning:  " + prePost + "-invoke method " + invokeMethodName + " has wrong signature");
				return false;
			}
			if(!invokeMethod.Resolve().IsStatic)
			{
				Console.WriteLine("warning:  " + prePost + "-invoke method " + invokeMethodName + " must be static");
				return false;
			}
			return true;
		}
	}
}
