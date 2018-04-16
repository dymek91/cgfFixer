using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryEngine;
using System.IO;
using cgfMerger;

namespace cgfFixer
{
    static class Fixer
    {


        public static void Fix(string path,string version="CE54")
        {
            if (version == "CE54")
            {
                string extension = Path.GetExtension(path);
                switch (extension)
                {
                    case ".cga":
                        Console.WriteLine("FILE {0}", path);

                        Fixer_CE_5_4 fixerCE = new Fixer_CE_5_4(path,path+"m");
                        fixerCE.RenderAndSaveFixedFile_Primary(path);
                        fixerCE.RenderAndSaveFixedFile_Secondary(path + "m");
                        
                        Console.Write("DONE\n");
                        break;
                    case ".skin":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_CE_5_4OLD.fixElements(path, true);
                        Fixer_CE_5_4OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        Fixer_CE_5_4OLD.fixSkin(path);
                        Console.Write("DONE\n");
                        break;
                    case ".cgf":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_CE_5_4OLD.fixElements(path);
                        Fixer_CE_5_4OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        Fixer_CE_5_4OLD.fixCga(path + "m");
                        Console.Write("DONE\n");
                        break;
                    case ".chr":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_CE_5_4OLD.fixElements(path);
                        Fixer_CE_5_4OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        break;
                    default:
                        break;
                }
            }
            else if(version == "LY")
            {
                string extension = Path.GetExtension(path);
                Merger merger;
                switch (extension)
                {
                    case ".cga":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_LY_1_12_0_1OLD.fixElements(path);
                        Fixer_LY_1_12_0_1OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        Fixer_LY_1_12_0_1OLD.fixCga(path);
                        merger = new Merger(path, path + "m");
                        merger.RenderAndSaveMergedFile(path);
                        File.Delete(path + "m");
                        Console.Write("DONE\n");
                        break;
                    case ".skin":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_LY_1_12_0_1OLD.fixElements(path, true);
                        Fixer_LY_1_12_0_1OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        Fixer_LY_1_12_0_1OLD.fixSkin(path);
                        merger = new Merger(path, path + "m");
                        merger.RenderAndSaveMergedFile(path);
                        File.Delete(path + "m");
                        Console.Write("DONE\n");
                        break;
                    case ".cgf":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_LY_1_12_0_1OLD.fixElements(path);
                        Fixer_LY_1_12_0_1OLD.fixElements(path + "m");
                        Console.Write("DONE\n");
                        Fixer_LY_1_12_0_1OLD.fixCga(path);
                        merger = new Merger(path,path+"m");
                        merger.RenderAndSaveMergedFile(path);
                        File.Delete(path + "m");
                        Console.Write("DONE\n");
                        break;
                    case ".chr":
                        Console.WriteLine("FILE {0}", path);
                        Console.Write("Fixing elements sizes");
                        Fixer_LY_1_12_0_1OLD.fixElements(path);
                        Fixer_LY_1_12_0_1OLD.fixElements(path + "m");
                       // merger = new Merger(path, path + "m");
                       // merger.RenderAndSaveMergedFile(path);
                        Console.Write("DONE\n");
                        break;
                    default:
                        break;
                }
            }
            
        }
       
        
    }
}
