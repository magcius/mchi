using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace MCHI
{
    static class JHI
    {
        public static bool StrEq(byte[] buf, int offset, string s)
        {
            byte[] chars = Encoding.ASCII.GetBytes(s);
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] != buf[offset + i])
                    return false;
            return true;
        }

        public static byte[] SwapBytes(byte[] x)
        {
            Array.Reverse(x);
            return x;
        }

        public static short SwapBytes(short x)
        {
            return (short)SwapBytes((ushort)x);
        }

        public static ushort SwapBytes(ushort x)
        {
            return (ushort)((x & 0x00FF) << 8 |
                            (x & 0xFF00) >> 8);
        }

        public static int SwapBytes(int x)
        {
            return (int)SwapBytes((uint)x);
        }

        public static uint SwapBytes(uint x)
        {
            return ((x & 0x000000FF) << 24) |
                   ((x & 0x0000FF00) << 8) |
                   ((x & 0x00FF0000) >> 8) |
                   ((x & 0xFF000000) >> 24);
        }

        public static string HexDump(byte[] data)
        {
            string S = "";
            for (int i = 0; i < data.Length; i++)
                S += String.Format("{0:X2} ", data[i]);
            return S;
        }
    }

    class JHIMccBuf
    {
        public static string MAGIC_CODE = "MCHI";
        const int MAGIC_OFFSET = 0x00;
        const int READ_OFFSET  = 0x20;
        const int WRITE_OFFSET = 0x40;
        const int DATA_OFFSET  = 0x60;

        HIO2ServerClient hio2;
        private int baseOffset;
        private int bufSize;
        private int bufDataSize;
        private int pointR = 0x60;
        private int pointW = 0x60;

        public JHIMccBuf(HIO2ServerClient hio2, int baseOffset, int bufSize)
        {
            this.hio2 = hio2;
            this.baseOffset = baseOffset;
            this.bufSize = bufSize;
            this.bufDataSize = this.bufSize - DATA_OFFSET;
        }

        public bool IsReady()
        {
            return JHI.StrEq(hio2.ReadBytes(MAGIC_OFFSET, 4), 0, MAGIC_CODE);
        }

        private void SyncPointsFromHIO2()
        {
            Debug.Assert(this.pointR >= DATA_OFFSET);
            Debug.Assert(this.pointW >= DATA_OFFSET);

            // Update our R/W points
            this.pointR = hio2.ReadU32(this.baseOffset + READ_OFFSET);
            this.pointW = hio2.ReadU32(this.baseOffset + WRITE_OFFSET);

            Debug.Assert(this.pointR >= DATA_OFFSET);
            Debug.Assert(this.pointW >= DATA_OFFSET);
        }

        private void SyncReadPointToHIO2()
        {
            hio2.WriteU32(this.baseOffset + READ_OFFSET, this.pointR);
            SyncPointsFromHIO2();
        }

        private void SyncWritePointToHIO2()
        {
            hio2.WriteU32(this.baseOffset + WRITE_OFFSET, this.pointW);
            SyncPointsFromHIO2();
        }

        public byte[] ReadData()
        {
            SyncPointsFromHIO2();

            if (this.pointW == this.pointR)
            {
                // Empty, nothing to read.
                return null;
            }

            // Rebase from 0x60
            int pointWZ = this.pointW - DATA_OFFSET;
            int pointRZ = this.pointR - DATA_OFFSET;

            int size = pointWZ - pointRZ;
            if (size < 0)
                size += this.bufDataSize;

            byte[] data = new byte[size];
            int data_offs = 0x00;

            // Check for wraparound -- where the new WP is less than our RP.
            // In this case, we need to split the read into two.
            if (pointWZ < pointRZ)
            {
                // First, read from pointW until end of buffer.
                int size1 = this.bufDataSize - pointRZ;
                hio2.ReadBytes(ref data, data_offs, this.baseOffset + DATA_OFFSET + pointRZ, size1);
                data_offs += size1;
                size -= size1;

                // Reset pointR back to the start to prepare for the next read.
                pointRZ = 0;
            }

            hio2.ReadBytes(ref data, data_offs, this.baseOffset + DATA_OFFSET + pointRZ, size);

            Debug.Assert(JHI.StrEq(data, 0, MAGIC_CODE));

            // Update our read point to match.
            this.pointR = this.pointW;
            SyncReadPointToHIO2();

            return data;
        }

        private int GetFullMessageSize(int size)
        {
            // Add on header size, and align.
            size += 6;
            size = (size + 0x1F) & ~0x1F;
            return size;
        }

        private int WriteMessage(int dstOffs, byte[] src, int srcOffs, int size)
        {
            hio2.WriteBytes(dstOffs + 0x00, Encoding.ASCII.GetBytes(MAGIC_CODE));
            hio2.WriteBytes(dstOffs + 0x04, BitConverter.GetBytes(JHI.SwapBytes((ushort)size)));
            hio2.WriteBytes(dstOffs + 0x06, src, srcOffs, size);
            int fullSize = GetFullMessageSize(size);
            int numZeroesToWrite = fullSize - (0x06 + size);
            byte[] zeroes = new byte[numZeroesToWrite];
            hio2.WriteBytes(dstOffs + 0x06 + size, zeroes);
            return fullSize;
        }

        public void Write(byte[] data)
        {
            if (!IsReady())
                return;

            SyncPointsFromHIO2();

            // Rebase from 0x60
            int pointWZ = this.pointW - DATA_OFFSET;
            int pointRZ = this.pointR - DATA_OFFSET;

            // If necessary, wait for Dolphin to read data from the ring buffer before continuing.
            while (((pointWZ + 0x20) % this.bufDataSize) == pointRZ)
            {
                Thread.Sleep(16);
                SyncPointsFromHIO2();
                pointWZ = this.pointW - DATA_OFFSET;
                pointRZ = this.pointR - DATA_OFFSET;
            }

            int dataOffs = 0;
            while (dataOffs < data.Length)
            {
                // See how many bytes are available to write in the current "run" -- meaning, no wraparound.
                int pointEndZ = pointWZ < pointRZ ? pointRZ : this.bufDataSize;
                int availableToWrite = pointEndZ - pointWZ - 0x20;

                int remainingBytes = data.Length - dataOffs;

                // Remember to leave space for the header.
                int bytesToWrite = Math.Min(availableToWrite, remainingBytes);

                int fullSize = WriteMessage(DATA_OFFSET + pointWZ, data, dataOffs, bytesToWrite);
                dataOffs += bytesToWrite;
                pointWZ += fullSize;

                if (pointWZ == this.bufDataSize)
                {
                    // Reset us back home.
                    pointWZ = 0x00;
                }
                else if (pointWZ > this.bufDataSize)
                {
                    // How did this happen?
                    throw new Exception("whoops");
                }
            }

            this.pointW = DATA_OFFSET + pointWZ;
            SyncWritePointToHIO2();
        }
    }

    struct JHITag
    {
        public string Magic;
        public byte[] Data;

        public string Dump()
        {
            return String.Format("JHITag {0}  {1}", this.Magic, JHI.HexDump(this.Data));
        }
    }

    interface IJHITagProcessor
    {
        string GetMagic();

        void ProcessTag(JHITag tag);
    }

    class ByteBuffer
    {
        public byte[] Data = new byte[0];

        public void Write(byte[] src, int srcOffs, int size)
        {
            int dstOffs = Data.Length;
            Array.Resize(ref Data, Data.Length + size);
            Array.Copy(src, srcOffs, Data, dstOffs, size);
        }

        public void DoneReading(int amount)
        {
            Array.Copy(Data, amount, Data, 0, Data.Length - amount);
            Array.Resize(ref Data, Data.Length - amount);
        }
    }

    class JHIClient
    {
        private HIO2ServerClient hio2;
        private JHIMccBuf dolphinToPC;
        private JHIMccBuf pcToDolphin;
        private ByteBuffer tagRecvBuffer = new ByteBuffer();

        public JHIClient(HIO2ServerClient hio2)
        {
            this.hio2 = hio2;
            this.dolphinToPC = new JHIMccBuf(this.hio2, 0x1000, 0x1000);
            this.pcToDolphin = new JHIMccBuf(this.hio2, 0x0000, 0x1000);
        }

        private Dictionary<string, IJHITagProcessor> tagDispatch = new Dictionary<string, IJHITagProcessor>();
        public void RegisterTagProcessor(IJHITagProcessor tagProcessor)
        {
            tagDispatch.Add(tagProcessor.GetMagic(), tagProcessor);
        }

        private void ProcessTags(ByteBuffer tagBuffer)
        {
            while (true)
            {
                if (tagBuffer.Data.Length < 0x08)
                {
                    // Not enough data...
                    return;
                }

                string tagMagic = Encoding.ASCII.GetString(tagBuffer.Data, 0x00, 0x04);
                IJHITagProcessor processor = tagDispatch[tagMagic];

                int tagSize = JHI.SwapBytes(BitConverter.ToInt32(tagBuffer.Data, 0x04));
                if (tagBuffer.Data.Length < 0x08 + tagSize)
                {
                    // Not enough data...
                    return;
                }

                // Tag is done!
                JHITag tag = new JHITag();
                tag.Magic = tagMagic;
                tag.Data = tagBuffer.Data.AsSpan(0x08, tagSize).ToArray();
                tagBuffer.DoneReading(0x08 + tagSize);

                processor.ProcessTag(tag);
            }
        }

        public int GetUnprocessedDataSize()
        {
            return this.tagRecvBuffer.Data.Length;
        }

        private void ProcessChunks(ByteBuffer outBuffer, byte[] data)
        {
            int offs = 0;

            while (offs < data.Length)
            {
                if (!JHI.StrEq(data, offs + 0x00, JHIMccBuf.MAGIC_CODE))
                {
                    throw new Exception("whoops!");
                }

                int chunkSize = JHI.SwapBytes(BitConverter.ToInt16(data, offs + 0x04));
                offs += 0x06;

                outBuffer.Write(data, offs, chunkSize);
                offs += chunkSize;

                // Align to 0x20.
                offs = (offs + 0x1F) & ~0x1F;
            }
        }

        public void Update()
        {
            // Read and process any messages that come from Dolphin.
            if (!this.dolphinToPC.IsReady())
                return;

            byte[] data = this.dolphinToPC.ReadData();
            if (data != null)
                ProcessChunks(tagRecvBuffer, data);

            ProcessTags(tagRecvBuffer);
        }

        public void WriteToDolphin(byte[] data)
        {
            this.pcToDolphin.Write(data);
        }

        public void WriteToDolphin(JHITag tag)
        {
            byte[] data = new byte[0x08 + tag.Data.Length];
            byte[] magicBytes = Encoding.ASCII.GetBytes(tag.Magic);
            Array.Copy(magicBytes, 0x00, data, 0x00, 0x04);
            byte[] lengthBytes = BitConverter.GetBytes(JHI.SwapBytes(tag.Data.Length));
            Array.Copy(lengthBytes, 0x00, data, 0x04, 0x04);
            Array.Copy(tag.Data, 0x00, data, 0x08, tag.Data.Length);
            WriteToDolphin(data);
        }
    }

    class HIO2ServerClient
    {
        const int EXIUSB_SHM_BASE = 0xd10000;
        const int EXIUSB_SHM_SIZE = 0x002000;
        const string DISCONNECT_CODE = "Kbai";

        public HIO2Server server;
        public MemoryMappedFile file;
        public MemoryMappedViewAccessor accessor;

        public HIO2ServerClient(HIO2Server server, string filename)
        {
            this.server = server;
            this.file = MemoryMappedFile.CreateOrOpen(filename, EXIUSB_SHM_SIZE);
            this.accessor = this.file.CreateViewAccessor();
        }

        public bool IsConnected()
        {
            return JHI.StrEq(ReadBytes(0x00, 0x04), 0x00, JHIMccBuf.MAGIC_CODE);
        }

        public void WriteBytes(int buf_idx, byte[] src, int src_offs = 0, int size = -1)
        {
            if (size < 0)
                size = src.Length - src_offs;
            if (size == 0)
                return;
            this.accessor.WriteArray(buf_idx, src, src_offs, size);
        }

        public void WriteU32(int buf_idx, int v)
        {
            WriteBytes(buf_idx, BitConverter.GetBytes(JHI.SwapBytes(v)));
        }

        public void ReadBytes(ref byte[] dst, int dst_idx, int buf_idx, int size)
        {
            this.accessor.ReadArray(buf_idx, dst, dst_idx, size);
        }

        public byte[] ReadBytes(int buf_idx, int size)
        {
            byte[] bytes = new byte[size];
            ReadBytes(ref bytes, 0, buf_idx, size);
            return bytes;
        }

        public int ReadU32(int buf_idx)
        {
            return JHI.SwapBytes(this.accessor.ReadInt32(buf_idx));
        }

        public bool HasDisconnectCode()
        {
            return JHI.StrEq(ReadBytes(0x04, 0x04), 0x00, DISCONNECT_CODE);
        }

        public void Close()
        {
            this.accessor.Dispose();
            this.file.Dispose();
        }
    }

    class HIO2Server
    {
        public HIO2ServerClient Client;

        public HIO2Server()
        {
            SpawnNewClient();
        }

        private void SpawnNewClient()
        {
            Client = new HIO2ServerClient(this, "Dolphin-EXIUSB-0");
        }

        public void Update()
        {
            if (Client.HasDisconnectCode())
            {
                Client.Close();
                SpawnNewClient();
            }
        }
    }
}
