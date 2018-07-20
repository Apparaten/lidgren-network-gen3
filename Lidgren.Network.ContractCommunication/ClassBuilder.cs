using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    //public static class ClassBuilder
    //{
    //    private static readonly AssemblyName AssemblyName;
    //    static ClassBuilder()
    //    {
    //        AssemblyName = new AssemblyName("__dynamicInterfaceAssembly");
    //    }

    //    public static T BuildProType<T>()
    //    {
    //        var buildType = typeof(T);
    //        AssemblyBuilder asmBuild = Thread.GetDomain().DefineDynamicAssembly(AssemblyName, AssemblyBuilderAccess.RunAndSave);
    //        ModuleBuilder modBuild = asmBuild.DefineDynamicModule("ModuleOne", "NestedEnum.dll");
    //        TypeBuilder tb = modBuild.DefineType("AType", TypeAttributes.Public);
    //        tb.AddInterfaceImplementation(buildType);
    //        var iinfos = buildType.GetMethods();
    //        foreach (var iinfo in iinfos)
    //        {
    //            var methodBuilder = tb.DefineMethod(iinfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, iinfo.ReturnType,
    //                iinfo.GetParameters().Select(s => s.ParameterType).ToArray());
    //            var methodILGenerator = methodBuilder.GetILGenerator();
    //            methodILGenerator.Emit(OpCodes.Ret);
    //            tb.DefineMethodOverride(methodBuilder, iinfo);
    //        }
    //        var type = tb.CreateType();
    //        return (T)Activator.CreateInstance(type);
    //    }
    //}
}
