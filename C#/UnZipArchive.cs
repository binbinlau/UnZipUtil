using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ZipArchive.TinyUnzip;

namespace ZipArchive
{
    public class UnZipArchive
    {
        /*
         * srcFilePath 要解压文件的地址
         * desDirPath 解压到某个文件夹
         * type 要解压的类型如（txt、json、java、cs）
         * name 解压的名字，不添加类型后缀，如（想解压1.txt 应制定name为 1 ）
         * 
         * author：binsix
         * createtime： 2018/8/3  15:25:30
         * */
        public static void UnZipFile(string srcFilePath, string desDirPath, string type = null, string name = null)
        {
            if (!(desDirPath.EndsWith("/") || desDirPath.EndsWith("\\")))
            {
                desDirPath = desDirPath + @"/";
                DirectoryInfo directoryInfo = new DirectoryInfo(desDirPath);
                if (directoryInfo.Exists)
                {
                    directoryInfo.Delete();
                }
            }
            //Console.WriteLine("srcFilePath: " + srcFilePath + "   desDirPath: " + desDirPath + "    type: " + type + "    name: " + name);
            using (FileStream zipFileStream = new FileStream(srcFilePath, FileMode.Open))
            {
                using (TinyUnzip tinyUnzip = new TinyUnzip(zipFileStream))
                {
                    Entry[] zipEntry = null;
                    if (type == null && name == null)
                    {
                        zipEntry = tinyUnzip.Entries.ToArray();
                    }
                    else if (type != null && name == null)
                    {
                        zipEntry = tinyUnzip.Entries.Where(e => e.FullName.EndsWith(type)).ToArray();
                    }
                    else if (type == null && name != null)
                    {
                        zipEntry = tinyUnzip.Entries.Where(e => {
                            if (e.FileName() != null && e.FileName().Substring(0, e.FileName().LastIndexOf(@".")).Equals(name))
                                return true;
                            return false;
                        }).ToArray();
                    }
                    else
                    {
                        zipEntry = tinyUnzip.Entries.Where(e => {
                            Console.WriteLine(e.FileName());
                            if (e.FileName() != null && e.FileName().Equals(name + @"." + type))
                                return true;
                             return false;

                        }).ToArray();
                    }
                    foreach (TinyUnzip.Entry entry in zipEntry)
                    {
                        if (entry.IsDirectory())
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(desDirPath + entry.FullName);
                            if (directoryInfo.Exists)
                            {
                                Console.WriteLine("文件夹已经存在");
                            }
                            else
                            {
                                Console.WriteLine("创建新的文件夹");
                                directoryInfo.Create();
                            }
                        }
                        else
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(desDirPath + entry.FullName.Substring(0, entry.FullName.LastIndexOf("/") + 1));
                            if (directoryInfo.Exists)
                            {
                                Console.WriteLine("文件夹已经存在");
                            }
                            else
                            {
                                Console.WriteLine("创建新的文件夹");
                                directoryInfo.Create();
                            }
                            Console.WriteLine(directoryInfo.FullName);
                            FileStream outStream = new FileStream(directoryInfo.FullName + entry.FileName(), FileMode.Create);
                            var uncompressStream = tinyUnzip.GetStream(entry.FullName);
                            byte[] buffer = new byte[1024 * 1024];
                            int read;
                            while ((read = uncompressStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                outStream.Write(buffer, 0, read);
                            }
                        }
                    }
                }
            }
        }
    }
}
