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
                foreach (string path in args)
                {
                    if (path.Length > 0 && File.Exists(path))
                    {
                        string extension = Path.GetExtension(path);
                        switch (extension)
                        {
                            case ".cga":
                                Console.WriteLine("FILE {0}", path);
                                Console.Write("Fixing elements sizes");
                                Fixer.fixElements(path);
                                Fixer.fixElements(path + "m");
                                Console.Write("DONE\n");
                                Fixer.fixCga(path + "m");
                                Console.Write("DONE\n");
                                break;
                            case ".skin":
                                Console.WriteLine("FILE {0}", path);
                                Console.Write("Fixing elements sizes");
                                Fixer.fixElements(path);
                                //Fixer.fixElements(path + "m");
                                Console.Write("DONE\n");
                                Fixer.fixSkin(path + "m");
                                Console.Write("DONE\n");
                                break;
                            case ".cgf":
                                Console.WriteLine("FILE {0}", path);
                                Console.Write("Fixing elements sizes");
                                Fixer.fixElements(path);
                                Fixer.fixElements(path + "m");
                                Console.Write("DONE\n");
                                Fixer.fixCga(path + "m");
                                Console.Write("DONE\n");
                                break;
                            case ".chr":
                                Console.WriteLine("FILE {0}", path);
                                Console.Write("Fixing elements sizes");
                                Fixer.fixElements(path);
                                Fixer.fixElements(path + "m");
                                Console.Write("DONE\n");
                                break;
                            default:
                                break;
                        }
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
                                string extension = Path.GetExtension(path2);
                                switch (extension)
                                {
                                    case ".cga":
                                        Console.WriteLine("FILE {0}", path2);
                                        Console.Write("Fixing elements sizes");
                                        Fixer.fixElements(path2);
                                        Fixer.fixElements(path2 + "m");
                                        Console.Write("DONE\n");
                                        Fixer.fixCga(path2 + "m");
                                        Console.Write("DONE\n");
                                        break;
                                    case ".skin":
                                        Console.WriteLine("FILE {0}", path2);
                                        Console.Write("Fixing elements sizes");
                                        Fixer.fixElements(path2);
                                        //Fixer.fixElements(path2 + "m");
                                        Console.Write("DONE\n");
                                        Fixer.fixSkin(path2 + "m");
                                        Console.Write("DONE\n");
                                        break;
                                    case ".cgf":
                                        Console.WriteLine("FILE {0}", path2);
                                        Console.Write("Fixing elements sizes");
                                        Fixer.fixElements(path2);
                                        Fixer.fixElements(path2 + "m");
                                        Console.Write("DONE\n");
                                        Fixer.fixCga(path2 + "m");
                                        Console.Write("DONE\n");
                                        break;
                                    case ".chr":
                                        Console.WriteLine("FILE {0}", path2);
                                        Console.Write("Fixing elements sizes");
                                        Fixer.fixElements(path2);
                                        Fixer.fixElements(path2 + "m");
                                        Console.Write("DONE\n");
                                        break;
                                    default:
                                        break;
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
