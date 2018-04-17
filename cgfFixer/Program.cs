using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using CryEngine;
using cgfMerger;
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
                    if (args[0] == "-LY")
                    {
                        Console.WriteLine("LUMBERYARD"); 
                        foreach (string path in args)
                        {
                            if (path.Length > 0 && File.Exists(path) && File.Exists(path + "m"))
                            {
                                Fixer fixer = new Fixer(path, path + "m");
                                fixer.RenderAndSaveFixedFile_Primary(path,true);
                                fixer.RenderAndSaveFixedFile_Secondary(path + "m", true);
                                Merger merger = new Merger(path, path + "m");
                                merger.RenderAndSaveMergedFile(path);
                                File.Delete(path + "m");
                            }
                            else if (path.Length > 0 && Directory.Exists(path))
                            {
                                Console.WriteLine("Loading Directory Tree...");
                                string[] filesnames = FixerHelper.GetFiles(path);
                                Console.WriteLine("Found {0} files", filesnames.Count());
                                int count = 0;
                                foreach (string path2 in filesnames)
                                {
                                    try
                                    {
                                        count++;
                                        if (path2.Length > 0 && File.Exists(path2) && File.Exists(path2 + "m"))
                                        {
                                            Console.WriteLine("[{0}/{1}]", count, filesnames.Count());
                                            Fixer fixer = new Fixer(path2, path2 + "m");
                                            fixer.RenderAndSaveFixedFile_Primary(path2, true);
                                            fixer.RenderAndSaveFixedFile_Secondary(path2 + "m", true);
                                            Merger merger = new Merger(path2, path2 + "m");
                                            merger.RenderAndSaveMergedFile(path2);
                                            File.Delete(path2 + "m");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(">> Error: " + e);
                                        Console.WriteLine("");
                                        Console.WriteLine("Press Enter to continue converting.");
                                        Console.Read();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (string path in args)
                        {
                            if (path.Length > 0 && File.Exists(path) && File.Exists(path + "m"))
                            {
                                Fixer fixer = new Fixer(path, path + "m"); 
                                fixer.RenderAndSaveFixedFile_Primary(path, true);
                                fixer.RenderAndSaveFixedFile_Secondary(path + "m", true);
                            }
                            else if (path.Length > 0 && Directory.Exists(path))
                            {
                                Console.WriteLine("Loading Directory Tree...");
                                string[] filesnames = FixerHelper.GetFiles(path);
                                Console.WriteLine("Found {0} files", filesnames.Count());
                                int count = 0;
                                foreach (string path2 in filesnames)
                                {
                                    try
                                    {
                                        count++;
                                        if (path2.Length > 0 && File.Exists(path2) && File.Exists(path2 + "m"))
                                        {
                                            Console.WriteLine("[{0}/{1}]", count, filesnames.Count());
                                            Fixer fixer = new Fixer(path2, path2 + "m");
                                            fixer.RenderAndSaveFixedFile_Primary(path2, true);
                                            fixer.RenderAndSaveFixedFile_Secondary(path2 + "m", true);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(path2);
                                        Console.WriteLine(">> Error: " + e);
                                        Console.WriteLine("");
                                        Console.WriteLine("Press Enter to continue converting.");
                                        Console.Read();
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
