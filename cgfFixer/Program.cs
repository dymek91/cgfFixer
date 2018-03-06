using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using CryEngine;
namespace cgfFixer
{

    public static class Program
    {

        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    if (args[0] == "/LY")
                    {
                        foreach (string path in args)
                        {
                            if (path.Length > 0 && File.Exists(path))
                            {
                                Fixer.Fix(path,"LY");
                            }
                            else if (path.Length > 0 && Directory.Exists(path))
                            {
                                string[] filesnames = Fixer.GetFiles(path);
                                Console.WriteLine("Found {0} files", filesnames.Count());
                                int count = 0;
                                foreach (string path2 in filesnames)
                                {
                                    count++;
                                    if (path2.Length > 0 && File.Exists(path2))
                                    {
                                        Console.WriteLine("[{0}/{1}]", count, filesnames.Count());
                                        Fixer.Fix(path2,"LY");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (string path in args)
                        {
                            if (path.Length > 0 && File.Exists(path))
                            {
                                Fixer.Fix(path);
                            }
                            else if (path.Length > 0 && Directory.Exists(path))
                            {
                                string[] filesnames = Fixer.GetFiles(path);
                                Console.WriteLine("Found {0} files", filesnames.Count());
                                int count = 0;
                                foreach (string path2 in filesnames)
                                {
                                    count++;
                                    if (path2.Length > 0 && File.Exists(path2))
                                    {
                                        Console.WriteLine("[{0}/{1}]", count, filesnames.Count());
                                        Fixer.Fix(path2);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(">> Error: " + e);
                Console.Read();
            }
            Console.Write("ALL DONE\n");
            Console.Read();
        }
    }
}
