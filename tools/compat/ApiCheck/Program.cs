using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// 版本兼容检查：mod DLL 里所有指向 sts2 的引用（类型、方法、字段，按名字 + 签名）和 Harmony 补丁目标（[HarmonyPatch(typeof(X), "名字")]），
// 逐个到给定版本的 sts2.dll 里找；找不到的打印出来。退出码 = 缺失数量（0 = 全部能找到）。
// 只查编译期能看到的引用；Traverse / AccessTools 里用字符串访问的私有成员不在此列（那些找不到时 mod 只是跳过那一处表现）。

if (args.Length < 2)
{
	Console.Error.WriteLine("用法：ApiCheck <mod.dll> <sts2.dll> [更多 mod.dll…]");
	return 100;
}
string gamePath = args[1];
Game game = new(gamePath);
Console.WriteLine($"游戏程序集：{gamePath}（{string.Join(" + ", game.Assemblies)}，{game.TypeCount} 个类型）");
int missing = 0;
foreach (string modPath in args.Take(1).Concat(args.Skip(2)))
{
	missing += ModCheck.Run(modPath, game);
}
Console.WriteLine(missing == 0 ? "结果：全部能找到 ✓" : $"结果：缺失 {missing} 处");
return missing;

/// <summary>签名转成可比较的字符串（类型按全名，泛型参数按位置）。</summary>
sealed class Sig : ISignatureTypeProvider<string, object?>
{
	public static readonly Sig I = new();
	public string GetArrayType(string e, ArrayShape s) => $"{e}[{new string(',', s.Rank - 1)}]";
	public string GetByReferenceType(string e) => e + "&";
	public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
	public string GetGenericInstantiation(string g, ImmutableArray<string> a) => $"{g}<{string.Join(",", a)}>";
	public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
	public string GetGenericTypeParameter(object? c, int i) => "!" + i;
	public string GetModifiedType(string m, string u, bool req) => u;
	public string GetPinnedType(string e) => e;
	public string GetPointerType(string e) => e + "*";
	public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
	public string GetSZArrayType(string e) => e + "[]";
	public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => Names.Def(r, h);
	public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => Names.Ref(r, h);
	public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) =>
		r.GetTypeSpecification(h).DecodeSignature(this, c);
}

static class Names
{
	public static string Def(MetadataReader r, TypeDefinitionHandle h)
	{
		TypeDefinition t = r.GetTypeDefinition(h);
		string name = r.GetString(t.Name);
		TypeDefinitionHandle outer = t.GetDeclaringType();
		if (!outer.IsNil)
		{
			return Def(r, outer) + "/" + name;
		}
		string ns = r.GetString(t.Namespace);
		return ns.Length == 0 ? name : ns + "." + name;
	}

	public static string Ref(MetadataReader r, TypeReferenceHandle h)
	{
		TypeReference t = r.GetTypeReference(h);
		string name = r.GetString(t.Name);
		if (t.ResolutionScope.Kind == HandleKind.TypeReference)
		{
			return Ref(r, (TypeReferenceHandle)t.ResolutionScope) + "/" + name;
		}
		string ns = r.GetString(t.Namespace);
		return ns.Length == 0 ? name : ns + "." + name;
	}

	/// <summary>类型引用最外层的作用域：程序集名。</summary>
	public static string? Assembly(MetadataReader r, TypeReferenceHandle h)
	{
		EntityHandle scope = r.GetTypeReference(h).ResolutionScope;
		while (scope.Kind == HandleKind.TypeReference)
		{
			scope = r.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
		}
		return scope.Kind == HandleKind.AssemblyReference ? r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) : null;
	}
}

/// <summary>游戏程序集里的全部类型及成员（名字 + 签名），含基类链上的继承成员。</summary>
sealed class Game
{
	/// <summary>读进来的程序集名（sts2，以及同目录的 GodotSharp）：指向它们的引用都要检查。</summary>
	public readonly List<string> Assemblies = new();
	public readonly int TypeCount;
	private readonly Dictionary<string, TypeInfo> _types = new();

	private sealed class TypeInfo
	{
		public string? Base;
		public readonly HashSet<string> Methods = new();          // 名字(参数)返回值
		public readonly HashSet<string> MethodNames = new();       // 方法名 + 属性名（[HarmonyPatch(typeof(X), "属性", MethodType.Getter)] 写的是属性名）
		public readonly HashSet<string> Fields = new();           // 名字:类型
	}

	public Game(string path)
	{
		Load(path);
		// 节点类的基类链会走进 Godot：同目录有 GodotSharp.dll 就一起读，继承自 Godot 的成员（Visible、AddChild…）也能查到
		string godot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "GodotSharp.dll");
		if (File.Exists(godot))
		{
			Load(godot);
		}
		TypeCount = _types.Count;
	}

	private void Load(string path)
	{
		using FileStream fs = File.OpenRead(path);
		using PEReader pe = new(fs);
		MetadataReader r = pe.GetMetadataReader();
		AssemblyDefinition a = r.GetAssemblyDefinition();
		foreach (TypeDefinitionHandle h in r.TypeDefinitions)
		{
			TypeDefinition t = r.GetTypeDefinition(h);
			TypeInfo info = new();
			if (!t.BaseType.IsNil)
			{
				info.Base = t.BaseType.Kind switch
				{
					HandleKind.TypeDefinition => Names.Def(r, (TypeDefinitionHandle)t.BaseType),
					HandleKind.TypeReference => Names.Ref(r, (TypeReferenceHandle)t.BaseType),
					HandleKind.TypeSpecification => Strip(r.GetTypeSpecification((TypeSpecificationHandle)t.BaseType).DecodeSignature(Sig.I, null)),
					_ => null
				};
			}
			foreach (MethodDefinitionHandle mh in t.GetMethods())
			{
				MethodDefinition m = r.GetMethodDefinition(mh);
				string n = r.GetString(m.Name);
				info.MethodNames.Add(n);
				info.Methods.Add(Key(n, m.DecodeSignature(Sig.I, null)));
			}
			foreach (PropertyDefinitionHandle ph in t.GetProperties())
			{
				info.MethodNames.Add(r.GetString(r.GetPropertyDefinition(ph).Name));
			}
			foreach (FieldDefinitionHandle fh in t.GetFields())
			{
				FieldDefinition f = r.GetFieldDefinition(fh);
				info.Fields.Add(r.GetString(f.Name) + ":" + f.DecodeSignature(Sig.I, null));
			}
			_types[Names.Def(r, h)] = info;
		}
		Assemblies.Add(r.GetString(a.Name));
	}

	public static string Key(string name, MethodSignature<string> s) =>
		$"{name}`{s.GenericParameterCount}({string.Join(",", s.ParameterTypes)}){s.ReturnType}";

	public static string Strip(string generic) => generic.Split('<')[0];

	public bool HasType(string t) => _types.ContainsKey(Strip(t));

	/// <summary>沿基类链找（基类在别的程序集里时停下，算找不到）。</summary>
	private IEnumerable<TypeInfo> Chain(string t)
	{
		for (string? cur = Strip(t); cur != null && _types.TryGetValue(cur, out TypeInfo? info); cur = info.Base == null ? null : Strip(info.Base))
		{
			yield return info;
		}
	}

	/// <summary>基类链最后落在已读程序集之外（System.Object 等），那里的成员不归本工具管。</summary>
	public bool EndsOutside(string t)
	{
		TypeInfo? last = Chain(t).LastOrDefault();
		return last?.Base != null && !_types.ContainsKey(Strip(last.Base)) && !last.Base.StartsWith("System.");
	}

	public bool HasMethod(string t, string key) => Chain(t).Any(i => i.Methods.Contains(key));

	public bool HasMethodName(string t, string name) => Chain(t).Any(i => i.MethodNames.Contains(name));

	public bool HasField(string t, string key) => Chain(t).Any(i => i.Fields.Contains(key));
}

static class ModCheck
{
	public static int Run(string path, Game game)
	{
		using FileStream fs = File.OpenRead(path);
		using PEReader pe = new(fs);
		MetadataReader r = pe.GetMetadataReader();
		Console.WriteLine($"\n== {Path.GetFileName(path)} ==");
		List<string> problems = new();
		int checkedRefs = 0;

		// 1) 类型
		foreach (TypeReferenceHandle h in r.TypeReferences)
		{
			if (!game.Assemblies.Contains(Names.Assembly(r, h) ?? ""))
			{
				continue;
			}
			checkedRefs++;
			string name = Names.Ref(r, h);
			if (!game.HasType(name))
			{
				problems.Add($"类型不存在：{name}");
			}
		}

		// 2) 方法 / 字段（父类型是游戏里的类型）
		foreach (MemberReferenceHandle h in r.MemberReferences)
		{
			MemberReference m = r.GetMemberReference(h);
			string? owner = null;
			if (m.Parent.Kind == HandleKind.TypeReference)
			{
				TypeReferenceHandle th = (TypeReferenceHandle)m.Parent;
				if (game.Assemblies.Contains(Names.Assembly(r, th) ?? ""))
				{
					owner = Names.Ref(r, th);
				}
			}
			else if (m.Parent.Kind == HandleKind.TypeSpecification)
			{
				string spec = r.GetTypeSpecification((TypeSpecificationHandle)m.Parent).DecodeSignature(Sig.I, null);
				if (game.HasType(spec))
				{
					owner = Game.Strip(spec);
				}
			}
			if (owner == null || !game.HasType(owner))
			{
				continue;
			}
			checkedRefs++;
			string name = r.GetString(m.Name);
			if (m.GetKind() == MemberReferenceKind.Method)
			{
				string key = Game.Key(name, m.DecodeMethodSignature(Sig.I, null));
				if (!game.HasMethod(owner, key) && !game.EndsOutside(owner))
				{
					problems.Add(game.HasMethodName(owner, name)
						? $"方法签名变了：{owner}.{key}"
						: $"方法不存在：{owner}.{name}");
				}
			}
			else
			{
				string key = name + ":" + m.DecodeFieldSignature(Sig.I, null);
				if (!game.HasField(owner, key) && !game.EndsOutside(owner))
				{
					problems.Add($"字段不存在：{owner}.{key}");
				}
			}
		}

		// 3) Harmony 补丁目标：[HarmonyPatch(typeof(X), "方法名", …)]
		foreach (CustomAttributeHandle ch in r.CustomAttributes)
		{
			CustomAttribute ca = r.GetCustomAttribute(ch);
			if (AttrType(r, ca) != "HarmonyLib.HarmonyPatch")
			{
				continue;
			}
			CustomAttributeValue<string> v;
			try
			{
				v = ca.DecodeValue(new AttrProvider());
			}
			catch
			{
				continue;
			}
			string? type = v.FixedArguments.Select(a => a.Value).OfType<string>().FirstOrDefault(s => s.StartsWith("T:"))?.Substring(2);
			string? method = v.FixedArguments.Where(a => a.Type == "string").Select(a => a.Value as string).FirstOrDefault();
			if (type == null || !game.HasType(type) && !type.StartsWith("MegaCrit"))
			{
				continue;
			}
			checkedRefs++;
			string owner = Names.Def(r, (TypeDefinitionHandle)ca.Parent);
			if (!game.HasType(type))
			{
				problems.Add($"补丁 {owner}：目标类型不存在 {type}");
			}
			else if (method != null && !game.HasMethodName(type, method) && !game.EndsOutside(type))
			{
				problems.Add($"补丁 {owner}：目标方法不存在 {type}.{method}");
			}
		}

		Console.WriteLine($"检查了 {checkedRefs} 处引用");
		foreach (string p in problems.Distinct().OrderBy(p => p))
		{
			Console.WriteLine("  ✗ " + p);
		}
		return problems.Distinct().Count();
	}

	private static string AttrType(MetadataReader r, CustomAttribute ca)
	{
		EntityHandle t = ca.Constructor.Kind switch
		{
			HandleKind.MemberReference => r.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent,
			HandleKind.MethodDefinition => r.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType(),
			_ => default
		};
		return t.Kind switch
		{
			HandleKind.TypeReference => Names.Ref(r, (TypeReferenceHandle)t),
			HandleKind.TypeDefinition => Names.Def(r, (TypeDefinitionHandle)t),
			_ => ""
		};
	}

	/// <summary>特性参数解码：typeof(X) 记成 "T:X"，其余照名字。</summary>
	private sealed class AttrProvider : ICustomAttributeTypeProvider<string>
	{
		public string GetPrimitiveType(PrimitiveTypeCode t) => t == PrimitiveTypeCode.String ? "string" : t.ToString();
		public string GetSystemType() => "type";
		public string GetSZArrayType(string e) => e + "[]";
		public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => Names.Def(r, h);
		public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => Names.Ref(r, h);
		public string GetTypeFromSerializedName(string name) => "T:" + name.Split(',')[0].Trim();
		public PrimitiveTypeCode GetUnderlyingEnumType(string t) => PrimitiveTypeCode.Int32;
		public bool IsSystemType(string t) => t == "System.Type" || t == "type";
	}
}
