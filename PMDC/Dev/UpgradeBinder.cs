using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;

namespace PMDC.Dev
{
    public sealed class UpgradeBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            Type typeToDeserialize = Type.GetType(String.Format("{0}, {1}",
                typeName, assemblyName));

            if (typeToDeserialize == null)
            {
                //typeName = typeName;
                //assemblyName = assemblyName;
                ////then the type moved to a new namespace
                //typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));
            }
            return typeToDeserialize;
        }
    }
}
