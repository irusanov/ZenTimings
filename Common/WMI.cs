using System;
using System.Collections.Generic;
using System.Management;
using System.ServiceProcess;
using static System.Management.ManagementObjectCollection;

namespace ZenTimings
{
    public static class WMI
    {
        public static object TryGetProperty(ManagementObject wmiObj, string propertyName)
        {
            object retval = null;
            try
            {
                retval = wmiObj.GetPropertyValue(propertyName);
            }
            catch (ManagementException ex) { Console.WriteLine(ex.Message); }

            return retval;
        }

        //root\wmi
        public static ManagementScope Connect(string scope)
        {
            try
            {
                var sc = new ServiceController("Windows Management Instrumentation");
                if (sc.Status != ServiceControllerStatus.Running)
                    throw new ManagementException(@"Windows Management Instrumentation service is not running");

                ManagementScope mScope = new ManagementScope($@"{scope}");
                mScope.Connect();

                if (mScope.IsConnected)
                    return mScope;
                else
                    throw new ManagementException($@"Failed to connect to {scope}");

            }
            catch (ManagementException ex)
            {
                Console.WriteLine("WMI: {0}", ex.Message);
                throw ex;
            }
        }

        public static ManagementObject Query(string scope, string wmiClass)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"{scope}", $"SELECT * FROM {wmiClass}"))
                {
                    ManagementObjectEnumerator enumerator = searcher.Get().GetEnumerator();
                    if (enumerator.MoveNext())
                        return enumerator.Current as ManagementObject;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return null;
        }

        public static List<string> GetWmiNamespaces(string root)
        {
            List<string> namespaces = new List<string>();
            try
            {
                ManagementClass nsClass = new ManagementClass(new ManagementScope(root), new ManagementPath("__namespace"), null);
                foreach (ManagementObject ns in nsClass.GetInstances())
                {
                    string namespaceName = root + "\\" + ns["Name"].ToString();
                    namespaces.Add(namespaceName);
                    namespaces.AddRange(GetWmiNamespaces(namespaceName));
                }
                namespaces.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return namespaces;
        }

        public static List<string> GetClassNamesWithinWmiNamespace(string wmiNamespaceName)
        {
            List<string> classes = new List<string>();
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher
                            (new ManagementScope(wmiNamespaceName),
                            new WqlObjectQuery("SELECT * FROM meta_class"));
                List<string> classNames = new List<string>();
                ManagementObjectCollection objectCollection = searcher.Get();
                foreach (ManagementClass wmiClass in objectCollection)
                {
                    string stringified = wmiClass.ToString();
                    string[] parts = stringified.Split(new char[] { ':' });
                    classes.Add(parts[1]);
                }
                classes.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return classes;
        }

        public static string GetInstanceName(string scope, string wmiClass)
        {
            using (ManagementObject queryObject = Query(scope, wmiClass))
            {
                string name = "";
                object obj;

                if (queryObject == null)
                    return name;

                try
                {
                    obj = TryGetProperty(queryObject, "InstanceName");
                    if (obj != null) name = obj.ToString();
                }
                catch { }

                return name;
            }
        }

        public static ManagementBaseObject InvokeMethod(ManagementObject mo, string methodName, string propName, string inParamName, uint arg)
        {
            try
            {
                // Obtain in-parameters for the method
                ManagementBaseObject inParams = mo.GetMethodParameters($"{methodName}");

                // Add the input parameters.
                if (inParams != null)
                    inParams[$"{inParamName}"] = arg;

                // Execute the method and obtain the return values.
                ManagementBaseObject outParams = mo.InvokeMethod($"{methodName}", inParams, null);

                return (ManagementBaseObject)outParams.Properties[$"{propName}"].Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public static byte[] RunCommand(ManagementObject mo, uint commandID, uint commandArgs = 0x0)
        {
            try
            {
                // Obtain in-parameters for the method
                ManagementBaseObject inParams = mo.GetMethodParameters("RunCommand");

                // Add the input parameters.
                byte[] cmd = new byte[4];
                byte[] args = new byte[4];
                byte[] buffer = new byte[8];

                cmd = BitConverter.GetBytes(commandID);
                args = BitConverter.GetBytes(commandArgs);

                Buffer.BlockCopy(cmd, 0, buffer, 0, cmd.Length);
                Buffer.BlockCopy(args, 0, buffer, cmd.Length, args.Length);

                inParams["Inbuf"] = buffer;

                // Execute the method and obtain the return values.
                ManagementBaseObject outParams = mo.InvokeMethod("RunCommand", inParams, null);

                // return outParam
                ManagementBaseObject pack = (ManagementBaseObject)outParams.Properties["Outbuf"].Value;
                return (byte[])pack.GetPropertyValue("Result");
            }
            catch (ManagementException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
