using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DEObfuscar
{
    class Helper
    {
        /// <summary>
        /// List of gathered MethodDef to remove
        /// </summary>
        public static List<MethodDef> StrDecMeth = new List<MethodDef>();

        /// <summary>
        /// List of gathered TypeDef to remove
        /// </summary>
        public static List<TypeDef> Type2Remove = new List<TypeDef>();

        /// <summary>
        /// The Type which contains the decryption MethodDef
        /// </summary>
        public static TypeDef Typedecryption;

        /// <summary>
        /// The method which decrypts ecrypted String
        /// </summary>
        public static MethodDef DecryptionMethod;

        /// <summary>
        /// Our main .NET protected module
        /// </summary>
        public static ModuleDefMD module;

        /// <summary>
        /// A simple counter, to prompt 
        /// how many strings we have decrypted
        /// at the very end
        /// </summary>
        public static int DeobedStringNumber;

        /// <summary>
        /// The .NET protected module path
        /// </summary>
        public static string Filepath;


        /// <summary>
        /// Crawl for every MethodDef in every TypeDef
        /// to find the Decryption Method
        /// </summary>
        /// <param name="module">The .NET protected ModuleDefMD</param>
        public static void FindStringDecrypterMethods(ModuleDefMD module)
        {
            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasBody == false)
                        continue;
                    if (method.Body.HasInstructions)
                    {
                        var local = method.Body.Variables;
                        /*
                         .locals (
		                  [0] int32
	                     )
                         */
                        if (local.Count != 1) continue;
                        if (!local[0].Type.FullName.ToLower().Contains("string")) continue;
                        if (!method.IsStatic) continue;
                        if (!method.IsHideBySig) continue;

                        var instrs = method.Body.Instructions;
                        if (instrs.Count < 25)
                        {
                            for (int i = 0; i < instrs.Count - 3; i++)
                            {
                                if (instrs[i].Operand != null && instrs[i].Operand.ToString().ToLower().Contains("utf"))
                                {
                                    DecryptionMethod = method;
                                    Typedecryption = type;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="module">The .NET protected ModuleDefMD</param>
        /// <param name="Methoddecryption">The method which decrypt the strings</param>
        public static void DecryptStringsInMethod(ModuleDefMD module, MethodDef Methoddecryption)
        {
            foreach (TypeDef type in module.Types)
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody)
                        break;
                    for (int i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        if (method.Body.Instructions[i].OpCode == OpCodes.Call)
                        {
                            if (method.Body.Instructions[i].Operand.ToString().ToLower().Contains(Typedecryption.Name.ToLower()))
                            {
                                Type2Remove.Add(Typedecryption);
                                var CalledDecMethod = (MethodDef)method.Body.Instructions[i].Operand;
                                var decryptedstring = ExtractStringFromMethod(CalledDecMethod);
                                if (decryptedstring == "[DEObfuscar] Error")
                                {
                                    //
                                }
                                else
                                {
                                    CilBody body = method.Body;
                                    body.Instructions[i].OpCode = OpCodes.Ldstr;
                                    body.Instructions[i].Operand = decryptedstring;
                                    DeobedStringNumber = DeobedStringNumber + 1;
                                }
                            }

                        }
                    }
                }
            }
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="module">The .NET protected ModuleDefMD</param>
        //public static void FindMethodInDecryptorType(ModuleDefMD module)
        //{
        //    foreach (MethodDef method in Typedecryption.Methods)
        //    {
        //        if (!method.HasBody)
        //            break;
        //        for (int i = 0; i < method.Body.Instructions.Count; i++)
        //        {
        //            if (method.Body.Instructions[i].Operand != null && method.Body.Instructions[i].Operand.ToString().ToLower().Contains("get"))
        //            {
        //                DecryptionMethod = method;
        //                break;
        //            }
        //        }

        //    }
        //}

        /// <summary>
        /// Seek and decrypt the string from the method which call the 
        /// decryption method
        /// </summary>
        /// <param name="method">The .NET protected ModuleDefMD</param>
        /// <returns>The decrypted string</returns>
        public static string ExtractStringFromMethod(MethodDef method)
        {
            string decryptedstring = "";


            if (!method.Body.Instructions[6].IsLdcI4()) return "[DEObfuscar] Error";
            var arg1 = method.Body.Instructions[6].GetLdcI4Value();
            var arg2 = method.Body.Instructions[7].GetLdcI4Value();
            var arg3 = method.Body.Instructions[8].GetLdcI4Value();

            StrDecMeth.Add(method);
            CilBody body = method.Body;

            Assembly assembly = Assembly.LoadFile(Filepath);
            Type typez = assembly.GetType(Typedecryption.FullName);
            if (typez != null)
            {
                MethodInfo methodInfo = null;
                MethodInfo[] methodInfoz = typez.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
                foreach (MethodInfo mi in methodInfoz)
                {
                    if (mi.ReturnType.ToString() == "System.String" && mi.Name.ToLower().Contains(DecryptionMethod.Name.ToLower()))
                    {
                        methodInfo = mi;
                        break;
                    }
                }

                if (methodInfo != null)
                {
                    object result = null;
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (parameters.Length == 0)
                    {

                    }
                    else
                    {
                        object[] parametersArray = new object[] { arg1, arg2, arg3 };
                        result = methodInfo.Invoke(methodInfo, parametersArray);
                        decryptedstring = result.ToString();
                        return decryptedstring;
                    }
                }
            }

            return "[DEObfuscar] Error";
        }

        /// <summary>
        /// Remove un-used Methods and Types
        /// </summary>
        /// <param name="module">The protected module</param>
        public static void PruneModule(ModuleDefMD module)
        {
            //Remove String Method decryption
            foreach (var method in StrDecMeth)
            {
                var type = method.DeclaringType;
                try
                {
                    type.Methods.Remove(method);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            foreach (var typez in Type2Remove)
            {
                try
                {
                    module.Types.Remove(typez);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}
