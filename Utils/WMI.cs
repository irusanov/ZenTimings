using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace ZenTimings
{
    public static class WMI
    {
        public static object TryGetProperty(ManagementObject wmiObj, string propertyName)
        {
            object retval;
            try
            {
                retval = wmiObj.GetPropertyValue(propertyName);
            }
            catch (ManagementException ex)
            {
                retval = null;
            }
            return retval;
        }

        //root\wmi
        public static ManagementScope Connect(string scope)
        {
            try
            {
                return new ManagementScope($@"{scope}");
            }
            catch (ManagementException e)
            {
                Console.WriteLine("WMI: Failed to connect", e.Message);
                throw;
            }
        }

        public static ManagementObject Query(string scope, string wmiClass)
        {
            using (var searcher = new ManagementObjectSearcher($"{scope}", $"SELECT * FROM {wmiClass}"))
            {
                ManagementObject queryObject = null;

                try
                {
                    queryObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                }
                catch { }

                return queryObject;
            }
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
            }
            catch (ManagementException me)
            {
                Console.WriteLine(me.Message);
            }

            return namespaces.OrderBy(s => s).ToList();
        }

        public static List<string> GetClassNamesWithinWmiNamespace(string wmiNamespaceName)
        {
            List<string> classes = new List<string>();
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
            return classes.OrderBy(s => s).ToList();
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
            catch (ManagementException err)
            {
                //MessageBox.Show("An error occurred while trying to execute the WMI method: " + err.Message);
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

                for (var i = 0; i < cmd.Length; ++i)
                {
                    buffer[i] = cmd[i];
                }

                for (var i = cmd.Length; i < args.Length; ++i)
                {
                    buffer[i] = args[i];
                }

                //buffer[4] = 0x01;

                inParams["Inbuf"] = buffer;

                // Execute the method and obtain the return values.
                ManagementBaseObject outParams = mo.InvokeMethod("RunCommand", inParams, null);

                // return outParam
                ManagementBaseObject pack = (ManagementBaseObject)outParams.Properties["Outbuf"].Value;
                return (byte[])pack.GetPropertyValue("result");
            }
            catch (ManagementException err)
            {
                //MessageBox.Show("An error occurred while trying to execute the WMI method: " + err.Message);
                return null;
            }
        }
    }
}
