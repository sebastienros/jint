using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Jint.Runtime.Interop
{
	public class MethodGroup
	{
		public static readonly MethodGroupMethodInfo[] Empty = new MethodGroupMethodInfo[0];

		public readonly MethodBase[] Methods;
		public readonly bool HasVariableParameters;
		public readonly Dictionary<int, MethodGroupMethodInfo[]> ByParameterCount;
		
		public MethodGroup(MethodBase[] methods)
		{
			Methods = methods;

			// Micro-optimization: If there are no methods taking variable number of arguments in this method group,
			// there is no point in calling ProcessParamsArrays at later time.
			HasVariableParameters = CheckVariableParameters(methods);
			
            // Group methods by number of parameters.
			ByParameterCount = methods
				.Select(method => new { Parameters = method.GetParameters().Count(), Method = method })
				.GroupBy(methodInfo => methodInfo.Parameters)
				.ToDictionary(methodInfo => methodInfo.Key, methodInfo => methodInfo.Select(
                    m => new MethodGroupMethodInfo()
                    {
                        Method = new MethodProxy(m.Method),
                        Parameters = m.Method.GetParameters()
                    }).ToArray());
		}

		public MethodGroupMethodInfo[] WithParameterCount(int count)
		{
			MethodGroupMethodInfo[] methods;
			if (ByParameterCount.TryGetValue(count, out methods))
			{
				return methods;
			}
			return Empty;
		}
			
		private bool CheckVariableParameters(MethodBase[] methods)
		{
			foreach (var methodInfo in methods)
			{
				var parameters = methodInfo.GetParameters();
				if (parameters.Any(p => Attribute.IsDefined(p, typeof(ParamArrayAttribute))))
				{
					return true;
				}
			}

			return false;
		}
	}

	public class MethodGroupMethodInfo
	{
		public MethodProxy Method;
		public ParameterInfo[] Parameters;
	}
}
