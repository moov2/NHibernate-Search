using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NHibernate.Search.Util
{
    public static class ReflectionHelpers
    {
        public static T GetInstance<T>(System.Type classType)
        {
            return (T)Activator.CreateInstance(classType);
        }

        public static T GetInstance<T, TProp>(System.Type classType, TProp property)
        {
            var constructor = classType.GetConstructor(new System.Type[] { typeof(TProp) });
            return (T)constructor.Invoke(new object[] { property });
        }

        public static bool HasParameterlessConstructor(System.Type type)
        {
            return !(type.GetConstructor(new System.Type[0]) == null);
        }

        public static bool HasConstructorWithParameterOf(System.Type typeClass, System.Type typeParameter)
        {
            var constructor = typeClass.GetConstructor(new System.Type[] { typeParameter });

            return !(constructor == null);
        }
    }
}
