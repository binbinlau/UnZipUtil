using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System;

namespace ZipArchive
{
    class TinyUnzip : IDisposable
    {
        #region internal members and types
        bool m_needDispose = false;
        bool m_disposed = false;
        Stream m_stream;
        BinaryReader m_reader;
        EOCD m_eocd;
        CentralDirectory[] m_centralDirEntries;
        List<Entry> m_entries;

        struct EOCD
        {
            public UInt32 signature;//核心目录结束标记（0x06054b50）
            public UInt16 numberOfDisk;//当前磁盘编号
            public UInt16 disk;//核心目录开始位置的磁盘编号
            public UInt16 curDiskCentralDirsNum;//该磁盘上所记录的核心目录数量
            public UInt16 centralDirsTotalNum;//核心目录结构总数
            public UInt32 sizeOfCentralDir;//核心目录的大小
            public UInt32 offsetToCentralDir;//核心目录开始位置相对于archive开始的位移
            public string comment;//注释长度,注释内容

            public static EOCD ReadEOCD(Stream stream)
            {
                stream.Seek(0, SeekOrigin.End);
                long streamSize = stream.Position;
                int bufferLength = 22 + 65536;
                if (bufferLength > streamSize)
                    bufferLength = (int)streamSize;

                stream.Seek(-bufferLength, SeekOrigin.End);//位于文件流末尾之前bufferLength个字节长度

                byte[] buffer = new byte[bufferLength];
                stream.Read(buffer, 0, bufferLength);

                //search for zip eocd marker
                for (int i = bufferLength - 22; i >= 0; i--)
                {
                    //if (buffer[i + 3] == 0x06 && buffer[i + 2] == 0x05 && buffer[i + 1] == 0x4b && buffer[i] == 0x50)
                    if (true)
                    {
                        //found start of eocd
                        var memStream = new MemoryStream(buffer, i, bufferLength - i, false);
                        var reader = new BinaryReader(memStream);

                        return ReadEOCD(reader);
                    }
                }

                throw new Exception("Can't find zip signature.");
            }

            static EOCD ReadEOCD(BinaryReader reader)
            {
                EOCD eocd = new EOCD();

                eocd.signature = reader.ReadUInt32();
                eocd.numberOfDisk = reader.ReadUInt16();
                eocd.disk = reader.ReadUInt16();
                eocd.curDiskCentralDirsNum = reader.ReadUInt16();
                eocd.centralDirsTotalNum = reader.ReadUInt16();
                eocd.sizeOfCentralDir = reader.ReadUInt32();
                eocd.offsetToCentralDir = reader.ReadUInt32();
                ushort commentLength = reader.ReadUInt16();

                eocd.comment = ReadAsString(reader, commentLength);

                if (eocd.signature != 0x06054b50)
                    throw new Exception("Broken signature in zip archive");

                return eocd;
            }
        }

        struct CentralDirectory
        {
            public UInt32 signature;//核心目录文件header标识=（0x02014b50）
            public UInt16 version;//压缩所用的pkware版本
            public UInt16 extractVersion;//解压所需pkware的最低版本
            public UInt16 generalFlags;//通用位标记
            public UInt16 compressionMethod;//压缩方法
            public UInt16 fileLastModificationTime;//文件最后修改时间
            public UInt16 fileLastModificationDate;//文件最后修改日期
            public UInt32 crc32;//CRC-32校验码
            public UInt32 compressedSize;//压缩后的大小
            public UInt32 uncompressedSize;//未压缩的大小
            public UInt16 fileNameLength;//文件名长度
            public UInt16 extraFieldLength;//扩展域长度
            public UInt16 fileCommentLength;//文件注释长度
            public UInt16 diskNumber;//文件开始位置的磁盘编号
            public UInt16 internalFileAttributes;//内部文件属性
            public UInt32 externalFileAttributes;//外部文件属性
            public UInt32 relativeFileHeaderOffset;//本地文件头的相对位移

            public string fileName;//目录文件名
            public string comment;//扩展域
            public byte[] extraField;//文件注释内容

            public static CentralDirectory Read(BinaryReader reader)
            {
                CentralDirectory dir = new CentralDirectory();

                dir.signature = reader.ReadUInt32();

                if (dir.signature != 0x02014b50)
                    throw new Exception("Bad central directory signature");

                dir.version = reader.ReadUInt16();
                dir.extractVersion = reader.ReadUInt16();
                dir.generalFlags = reader.ReadUInt16();
                dir.compressionMethod = reader.ReadUInt16();
                dir.fileLastModificationTime = reader.ReadUInt16();
                dir.fileLastModificationDate = reader.ReadUInt16();
                dir.crc32 = reader.ReadUInt32();
                dir.compressedSize = reader.ReadUInt32();
                dir.uncompressedSize = reader.ReadUInt32();
                dir.fileNameLength = reader.ReadUInt16();
                dir.extraFieldLength = reader.ReadUInt16();
                dir.fileCommentLength = reader.ReadUInt16();
                dir.diskNumber = reader.ReadUInt16();
                dir.internalFileAttributes = reader.ReadUInt16();
                dir.externalFileAttributes = reader.ReadUInt32();
                dir.relativeFileHeaderOffset = reader.ReadUInt32();

                dir.fileName = ReadAsString(reader, dir.fileNameLength);
                dir.extraField = reader.ReadBytes(dir.extraFieldLength);
                dir.comment = ReadAsString(reader, dir.fileCommentLength);

                return dir;
            }
        }

        struct LocalFileHeader
        {
            public UInt32 signature;//文件头标识，值固定(0x04034b50)
            public UInt16 extractVersion;//解压文件所需 pkware最低版本
            public UInt16 generalFlags;//通用比特标志位(置比特0位=加密，详情见后)
            public UInt16 compressionMethod;//压缩方式（详情见后）
            public UInt16 fileLastModificationTime;//文件最后修改时间
            public UInt16 fileLastModificationDate;//文件最后修改日期
            public UInt32 crc32;//CRC-32校验码
            public UInt32 compressedSize;//压缩后的大小
            public UInt32 uncompressedSize;//未压缩的大小
            public UInt16 fileNameLength;//文件名长度
            public UInt16 extraFieldLength;//扩展区长度

            public string fileName;//文件名
            public byte[] extraField;//扩展区

            public static LocalFileHeader Read(BinaryReader reader, CentralDirectory dirEntry)
            {
                reader.BaseStream.Seek(dirEntry.relativeFileHeaderOffset, SeekOrigin.Begin);

                LocalFileHeader fileHeader = new LocalFileHeader();

                fileHeader.signature = reader.ReadUInt32();

                if (fileHeader.signature != 0x04034b50)
                    throw new Exception("Bad central file signature");

                fileHeader.extractVersion = reader.ReadUInt16();
                fileHeader.generalFlags = reader.ReadUInt16();
                fileHeader.compressionMethod = reader.ReadUInt16();
                fileHeader.fileLastModificationTime = reader.ReadUInt16();
                fileHeader.fileLastModificationDate = reader.ReadUInt16();
                fileHeader.crc32 = reader.ReadUInt32();
                fileHeader.compressedSize = reader.ReadUInt32();
                fileHeader.uncompressedSize = reader.ReadUInt32();
                fileHeader.fileNameLength = reader.ReadUInt16();
                fileHeader.extraFieldLength = reader.ReadUInt16();

                fileHeader.fileName = ReadAsString(reader, fileHeader.fileNameLength);
                fileHeader.extraField = reader.ReadBytes(fileHeader.extraFieldLength);

                return fileHeader;
            }
        }


        class ProxyStream : Stream
        {
            private Stream m_stream;
            private readonly long m_rangeStart;
            private readonly long m_rangeEnd;
            public ProxyStream(Stream stream, long offset, long length)
            {
                m_stream = stream;
                m_stream.Seek(offset, SeekOrigin.Begin);
                m_rangeStart = offset;
                m_rangeEnd = offset + length;
            }

            public ProxyStream(Stream stream, long length)
            {
                m_stream = stream;
                m_rangeStart = m_stream.Position;
                m_rangeEnd = m_rangeStart + length;
            }

            public override bool CanRead { get { IsDisposed(); return true; } }
            public override bool CanWrite { get { IsDisposed(); return false; } }
            public override bool CanSeek { get { IsDisposed(); return false; } }
            public override long Length { get { IsDisposed(); return m_rangeEnd - m_rangeStart; } }
            public override long Position
            {
                get
                {
                    IsDisposed();
                    return m_stream.Position - m_rangeStart;
                }
                set { throw new NotSupportedException(); }
            }

            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
            public override void Flush() { throw new NotImplementedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            {
                IsDisposed();

                long remaining = m_rangeEnd - m_stream.Position;

                if (remaining <= 0)
                    return 0;
                if (count > remaining)
                    count = (int)remaining;

                return m_stream.Read(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    m_stream = null;
                }
            }

            void IsDisposed()
            {
                if (m_stream == null)
                    throw new ObjectDisposedException(GetType().Name);
            }
        }

        static string ReadAsString(BinaryReader reader, UInt16 length)
        {
            if (length == 0)
                return "";

            byte[] commentBytes = reader.ReadBytes(length);
            return System.Text.Encoding.UTF8.GetString(commentBytes);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_disposed || !m_needDispose)
                return;

            if (disposing)
            {
                m_stream.Dispose();
            }

            m_disposed = true;
        }
        #endregion

        #region Public API
        public class Entry
        {
            string m_fileName;
            string m_comment;
            long m_compressedSize;
            long m_uncompressedSize;
            DateTime m_modified;
            UInt32 m_crc32;

            public Entry(string fileName, string comment, UInt32 compressedSize, UInt32 uncompressedSize, UInt16 modifiedTime, UInt16 modifiedDate, UInt32 crc32)
            {
                m_fileName = fileName;
                m_comment = comment;
                m_compressedSize = compressedSize;
                m_uncompressedSize = uncompressedSize;

                int seconds = (modifiedTime & 0x1f) * 2;
                int minutes = (modifiedTime & 0x7e0) >> 5;
                int hours = modifiedTime >> 11;

                int day = modifiedDate & 0x1f;
                int month = (modifiedDate & 0x1e0) >> 5;
                int year = 1980 + (modifiedTime >> 9);

                m_modified = new DateTime(year, month, day, hours, minutes, seconds);

                m_crc32 = crc32;
            }

            public string FullName { get { return m_fileName; } }
            public string Comment { get { return m_comment; } }
            public long CompressedSize { get { return m_compressedSize; } }
            public long UncompressedSize { get { return m_uncompressedSize; } }
            public DateTime Modified { get { return m_modified; } }
            public UInt32 CRC32 { get { return m_crc32; } }
            public bool IsFile()
            {
                if (m_fileName.Trim() != null && (m_fileName.Trim().EndsWith(@"/") || m_fileName.Trim().EndsWith(@"\\")))
                    return false;
                else
                    return true;
            }

            public bool IsDirectory()
            {
                if (IsFile())
                    return false;
                else
                    return true;
            }

            public string FileName()
            {
                if (IsFile())
                {
                    return FullName.Substring(FullName.LastIndexOf(@"/") + 1);
                }
                return null;
            }
        }

        //construct an instance of the class and parse list of zip entries
        public TinyUnzip(Stream stream, bool needDispose = true)
        {
            if (!stream.CanSeek)
                throw new Exception("Unzip - stream should be able to perform Seek operation!");

            m_needDispose = needDispose;

            m_stream = stream;

            m_eocd = EOCD.ReadEOCD(m_stream);//可以设置多种类型的文件格式，这里只判断了zip压缩格式的
            m_stream.Seek(m_eocd.offsetToCentralDir, SeekOrigin.Begin);

            m_reader = new BinaryReader(m_stream);
            m_centralDirEntries = new CentralDirectory[m_eocd.centralDirsTotalNum];
            m_entries = new List<Entry>(m_eocd.centralDirsTotalNum);

            for (int i = 0; i < m_centralDirEntries.Length; i++)
            {
                m_centralDirEntries[i] = CentralDirectory.Read(m_reader);

                Entry entry = new Entry(m_centralDirEntries[i].fileName,
                                        m_centralDirEntries[i].comment,
                                        m_centralDirEntries[i].compressedSize,
                                        m_centralDirEntries[i].uncompressedSize,
                                        m_centralDirEntries[i].fileLastModificationTime,
                                        m_centralDirEntries[i].fileLastModificationDate,
                                        m_centralDirEntries[i].crc32);
                m_entries.Add(entry);
            }
        }

        //return collection of entries, describing zip content
        public ReadOnlyCollection<Entry> Entries
        {
            get
            {
                return new ReadOnlyCollection<Entry>(m_entries);
            }
        }

        //gets stream for reading uncompressed data
        //only one stream can be used at the same time
        public Stream GetStream(string fileName)
        {
            CentralDirectory dir = m_centralDirEntries.Where(d => d.fileName == fileName).First();
            LocalFileHeader fileHeader = LocalFileHeader.Read(m_reader, dir);

            switch (fileHeader.compressionMethod)
            {
                //no compression
                case 0:
                    return new ProxyStream(m_stream, dir.compressedSize);
                //deflate
                case 8:
                    var proxy = new ProxyStream(m_stream, dir.compressedSize);
                    return new DeflateStream(proxy, CompressionMode.Decompress, true);
            }

            throw new NotSupportedException("Compression method not supported. Method id: " + fileHeader.compressionMethod);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}