﻿using RTTIScanner.ClassExtensions;
using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace __cxxabiv1
{
	/**
	  *  @brief  Part of RTTI.
	  *
	  *  The @c type_info class describes type information generated by
	  *  an implementation.
	 */
	public class type_info
	{
		public string name { get; set; }

		[Obsolete("TODO: GCC __cxa_demangle", true)]
		public static string __cxa_demangle(string str)
		{
			return null;
		}

		public static string demangle(string str)
		{
			return new Regex(@"^\d+").Replace(str, "");
		}
	}

	// Helper class for __vmi_class_type.
	public class __base_class_type_info
	{
		public IntPtr address { get; set; }
		public long offset_flags { get; set; }
	}

	// Type information for a class.
	public class __class_type_info : type_info
	{
		public __base_class_type_info currentObj { get; protected set; }
		public __class_type_info(IntPtr objAddress)
		{
			currentObj = new();
			currentObj.address = objAddress;
		}
	}

	// Type information for a class with a single non-virtual base.
	public class __si_class_type_info : __class_type_info
	{
		public __base_class_type_info baseClassInfo { get; set; }
		public __si_class_type_info(IntPtr objAddress) : base(objAddress)
		{
			baseClassInfo = new();
		}
	}

	// Type information for a class with multiple and/or virtual bases.
	public class __vmi_class_type_info : __class_type_info
	{
		public uint flags { get; set; }
		public uint base_count { get; set; }
		public __base_class_type_info[] arrBaseClassAddress { get; set; }
		public __vmi_class_type_info(IntPtr objAddress) : base(objAddress) { }
	}
}

namespace RTTIScanner.RTTI
{
	public class GCC : Parser
	{

		private static readonly Dictionary<string, Type> typeMapping = new Dictionary<string, Type>
		{
			// i dont care
			// { "St9type_info", typeof(__cxxabiv1.type_info) }, 
			{ "N10__cxxabiv117__class_type_infoE", typeof(__cxxabiv1.__class_type_info) },
			{ "N10__cxxabiv120__si_class_type_infoE", typeof(__cxxabiv1.__si_class_type_info) },
			{ "N10__cxxabiv121__vmi_class_type_infoE", typeof(__cxxabiv1.__vmi_class_type_info) }
		};

		private async Task<__cxxabiv1.type_info> CreateType(IntPtr pTypeInfo)
		{
			IntPtr classTypeInfo = await ReadRemoteIntPtr(pTypeInfo);
			string classTypeName = await ReadRemoteString(await ReadRemoteIntPtr(classTypeInfo - 32));

			if (!typeMapping.TryGetValue(classTypeName, out Type type))
			{
				throw new Exception("Failed to get typeinfo.");
			}

			__cxxabiv1.type_info instance = (__cxxabiv1.type_info)Activator.CreateInstance(type, new object[] { pTypeInfo });
			if (instance == null)
			{
				throw new Exception("Failed to CreateInstance on CreateType.");
			}

			return instance;
		}

		public override async Task<string[]> ReadRemoteRuntimeTypeInformation64(IntPtr rootTypeInfo)
		{
			if (!rootTypeInfo.IsValid())
			{
				return null;
			}

			var ret = await TraverseRTTIByRoot(rootTypeInfo);
			string treeString = GetInheritanceTreeString(ret.Item1, ret.Item2, "", true, true);
			return new string[] { treeString };
		}

		private async Task<(string, Dictionary<string, List<string>>)> TraverseRTTIByRoot(IntPtr rootTypeInfo)
		{
			m_hsTypeVisited = new();
			m_dictTypeAddress = new();
			m_sInheritance = new();
			var inheritanceMap = new Dictionary<string, List<string>>();

			__cxxabiv1.type_info rootType = await GetRTTIByTypeinfo(rootTypeInfo);
			m_dictTypeAddress[rootType.name] = rootTypeInfo;
			m_sInheritance.Push(rootType.name);

			while (m_sInheritance.Count > 0)
			{
				string node = m_sInheritance.Pop();

				if (!m_hsTypeVisited.Contains(node))
				{
					m_hsTypeVisited.Add(node);

					var currentNodeAddr = m_dictTypeAddress[node];
					var currentType = await GetRTTIByTypeinfo(currentNodeAddr);
					if (currentType is __cxxabiv1.__vmi_class_type_info vmi)
					{
						for (int i = 0; i < vmi.base_count; i++)
						{
							await AddRelationship(inheritanceMap, currentType.name, vmi.arrBaseClassAddress[i]);
						}
					}
					else if (currentType is __cxxabiv1.__si_class_type_info si)
					{
						await AddRelationship(inheritanceMap, currentType.name, si.baseClassInfo);
					}
					else if (currentType is __cxxabiv1.__class_type_info baseClass)
					{
						// node end.
					}

					// Push children to the stack in reverse order  
					if (inheritanceMap.ContainsKey(node))
					{
						var children = inheritanceMap[node];
						for (int i = children.Count - 1; i >= 0; i--)
						{
							m_sInheritance.Push(children[i]);
						}
					}
				}
			}

			return (rootType.name, inheritanceMap);
		}

		private async Task AddRelationship(Dictionary<string, List<string>> map, string parent, __cxxabiv1.__base_class_type_info child)
		{
			if (!map.ContainsKey(parent))
			{
				map[parent] = new List<string>();
			}
			string childName = await GetTypeName(child.address);
			map[parent].Add(childName);
			m_dictTypeAddress[childName] = child.address;
		}

		private string GetInheritanceTreeString(string node, Dictionary<string, List<string>> map, string indent, bool last, bool isRoot = false)
		{
			var sb = new StringBuilder();

			if (!isRoot)
			{
				sb.Append(indent);
				if (last)
				{
					sb.Append("└── ");
					indent += "    ";
				}
				else
				{
					sb.Append("├── ");
					indent += "│   ";
				}
			}
			sb.AppendLine(node);

			if (map.ContainsKey(node))
			{
				var children = map[node];
				for (int i = 0; i < children.Count; i++)
				{
					// Recursively build the string for each child  
					sb.Append(GetInheritanceTreeString(children[i], map, indent, i == children.Count - 1));
				}
			}

			return sb.ToString();
		}

		private async Task<__cxxabiv1.type_info> GetRTTIByTypeinfo(IntPtr pTypeinfo)
		{
			__cxxabiv1.type_info currentObj = await CreateType(pTypeinfo);
			currentObj.name = await GetTypeName(pTypeinfo);

			IntPtr pCurrentAddr = pTypeinfo;
			if (currentObj is __cxxabiv1.__class_type_info baseClass)
			{
				pCurrentAddr += 16;
			}

			if (currentObj is __cxxabiv1.__si_class_type_info si)
			{
				si.baseClassInfo.address = await ReadRemoteIntPtr(pCurrentAddr);
			}
			else if (currentObj is __cxxabiv1.__vmi_class_type_info vmi)
			{
				vmi.flags = await ReadRemote<uint>(pCurrentAddr);
				pCurrentAddr += 4;

				vmi.base_count = await ReadRemote<uint>(pCurrentAddr);
				pCurrentAddr += 4;

				vmi.arrBaseClassAddress = new __cxxabiv1.__base_class_type_info[vmi.base_count];
				for (int i = 0; i < vmi.base_count; i++)
				{
					vmi.arrBaseClassAddress[i] = new __cxxabiv1.__base_class_type_info();

					vmi.arrBaseClassAddress[i].address = await ReadRemoteIntPtr(pCurrentAddr);
					pCurrentAddr += 8;

					vmi.arrBaseClassAddress[i].offset_flags = await ReadRemote<long>(pCurrentAddr);
					pCurrentAddr += 8;
				}
			}

			return currentObj;
		}

		private async Task<string> GetTypeName(IntPtr pTypeinfo)
		{
			return __cxxabiv1.type_info.demangle(await ReadRemoteString(await ReadRemoteIntPtr(pTypeinfo + 8)));
		}
	}
}
