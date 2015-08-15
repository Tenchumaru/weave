using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using Mono.Cecil;

namespace Weave
{
	static partial class Program
	{
		static bool WeaveRename()
		{
			// Get the embedded resources.
			var unicode = Encoding.Unicode;
			byte[] unicodePremble = unicode.GetPreamble();
			var utf8 = Encoding.UTF8;
			byte[] utf8Preamble = utf8.GetPreamble();
			var sb = new StringBuilder();
			foreach(var resource in module.Resources.OfType<EmbeddedResource>())
			{
				using(IResourceReader reader = new ResourceReader(resource.GetResourceStream()))
				{
					foreach(DictionaryEntry en in reader)
					{
						var stream = en.Value as Stream;
						if(stream != null)
						{
							var buffer = new byte[stream.Length];
							stream.Read(buffer, 0, buffer.Length);
							if(buffer.Take(unicodePremble.Length).SequenceEqual(unicodePremble))
								sb.Append(unicode.GetString(buffer, unicodePremble.Length, buffer.Length - unicodePremble.Length));
							else
							{
								int offset = buffer.Take(utf8Preamble.Length).SequenceEqual(utf8Preamble) ? utf8Preamble.Length : 0;
								sb.Append(utf8.GetString(buffer, offset, buffer.Length - offset));
							}
						}
					}
				}
			}
			string text = sb.ToString();

			// Change the names of private methods that don't appear in the
			// resources for any XAML (e.g. event handlers).
			Func<ICustomAttributeProvider, bool> IsRenamable = p => !p.CustomAttributes.Any(c => c.IsNamed("Rename")
				&& !c.ConstructorArguments.Select(a => a.Value).OfType<bool>().Any(b => b));
			var q = from t in module.Types
					from m in t.Methods
					where m.IsPrivate && !text.Contains(m.Name) && IsRenamable(m)
					select m;
			if(isRemoving)
			{
				q = q.ToList(); // Prevent the following query from invalidating this one.
				var u = from t in module.Types
						from m in t.Methods
						from a in m.CustomAttributes
						where a.IsNamed("Rename")
						select new { Attribute = a, Method = m };
				u = u.ToList(); // Prevent the following removal from invalidating this query.
				foreach(var item in u)
					item.Method.CustomAttributes.Remove(item.Attribute);
			}
			int i = 0;
			foreach(var method in q)
				method.Name = "_" + i++;

			// Change the names of private fields.
			var v = from t in module.Types
					from f in t.Fields
					where f.IsPrivate && IsRenamable(f)
					select f;
			foreach(var field in v)
				field.Name = "_" + i++;

			return i > 0;
		}
	}
}
