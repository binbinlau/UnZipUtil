# UnZipUtil
解压zip压缩的文件，目前仅是C#语言写的，后期会添加java，go

使用方法:
将文件引入到工程里面，然后再使用的类里引入
using ZipArchive;
调用UnZipFile方法即可：
UnZipArchive.UnZipFile(srcFilePath, desDirPath, type, name);

参数：srcFilePath 需要解压的文件
	  desDirPath 制定加压的文件夹
	  type 默认为null，制定则为解压某一类型的文件如（txt、json、java、cs）
	  name 解压的名字，不添加类型后缀，如（想解压1.txt 应制定name为 1 ）
	  