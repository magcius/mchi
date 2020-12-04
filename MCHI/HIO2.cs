using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

        public static bool StrEq(List<byte> buf, int offset, string s)
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
            return JHI.StrEq(hio2.buf, MAGIC_OFFSET, MAGIC_CODE);
        }

        private void SyncPointsFromHIO2()
        {
            // Update our R/W points
            this.pointR = hio2.ReadU32(this.baseOffset + READ_OFFSET);
            this.pointW = hio2.ReadU32(this.baseOffset + WRITE_OFFSET);
        }

        private void SyncReadPointToHIO2()
        {
            hio2.WriteU32(this.baseOffset + READ_OFFSET, this.pointR);
        }

        private void SyncWritePointToHIO2()
        {
            hio2.WriteU32(this.baseOffset + WRITE_OFFSET, this.pointW);
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
            hio2.WriteBytes(dstOffs + 0x00, Encoding.ASCII.GetBytes(MAGIC_CODE), 4);
            hio2.WriteBytes(dstOffs + 0x04, BitConverter.GetBytes(JHI.SwapBytes((ushort)size)));
            for (int i = 0x00; i < size; i++)
                hio2.WriteByte(dstOffs + 0x06 + i, src[srcOffs + i]);
            int fullSize = GetFullMessageSize(size);
            for (int i = 0x06 + size; i < fullSize; i++)
                hio2.WriteByte(dstOffs + i, 0);
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

            int dataOffs = 0;
            while (dataOffs < data.Length)
            {
                // See how many bytes are available to write in the current "run" -- meaning, no wraparound.
                int pointEndZ = pointWZ < pointRZ ? pointRZ : this.bufDataSize;
                int availableToWrite = pointEndZ - pointWZ;

                if (availableToWrite < 0x06)
                {
                    // Literally no space available... Just sleep until we have enough?
                    Thread.Sleep(16);
                }

                int remainingBytes = data.Length - dataOffs;

                // Remember to leave space for the header.
                int bytesToWrite = Math.Min(availableToWrite - 0x06, remainingBytes);

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

        ByteBuffer tagRecvBuffer = new ByteBuffer();

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

        public List<byte> buf = new List<byte>(EXIUSB_SHM_SIZE);
        public HIO2Server server;
        public IPEndPoint sender;

        public HIO2ServerClient(HIO2Server server, IPEndPoint sender)
        {
            this.server = server;
            this.sender = sender;
            for (int i = 0; i < EXIUSB_SHM_SIZE; i++)
                buf.Add(0);
        }

        public delegate void WriteDelegate(HIO2ServerClient client, int offs, int size);

        public WriteDelegate Write = null;

        private bool BufSet(int buf_idx, byte v)
        {
            if (buf_idx < 0 || buf_idx >= buf.Count)
            {
                // bad data!
                return false;
            }

            buf[buf_idx] = v;
            if (Write != null)
                Write(this, buf_idx, 1);
            return true;
        }

        public void ReceiveMessage(byte[] data)
        {
            if (data.Length == 4)
            {
                // read/write buf
                int buf_idx = BitConverter.ToInt16(data);

                byte cmd = data[2];
                byte v = data[3];

                bool is_write = cmd == 'W';
                if (is_write)
                    BufSet(buf_idx, v);
            }
        }

        public void WriteByte(int buf_idx, byte v)
        {
            if (BufSet(buf_idx, v))
            {
                server.SendCommand(buf_idx, 'W', v, this);
            }
        }

        public void WriteBytes(int buf_idx, byte[] data, int size = -1)
        {
            if (size < 0)
                size = data.Length;
            for (int i = 0; i < size; i++)
                WriteByte(buf_idx + i, data[i]);
        }

        public void WriteU32(int buf_idx, int v)
        {
            WriteBytes(buf_idx, BitConverter.GetBytes(JHI.SwapBytes(v)));
        }

        public void ReadBytes(ref byte[] dst, int dst_idx, int buf_idx, int size)
        {
            for (int i = 0; i < size; i++)
                dst[dst_idx + i] = buf[buf_idx + i];
        }

        public int ReadU32(int buf_idx)
        {
            return JHI.SwapBytes(BitConverter.ToInt32(buf.ToArray(), buf_idx));
        }
    }

    class HIO2Server
    {
        public UdpClient server;
        public Dictionary<IPEndPoint, HIO2ServerClient> clients = new Dictionary<IPEndPoint, HIO2ServerClient>();
        public Thread thread;

        private HIO2ServerClient GetClient(IPEndPoint remote)
        {
            HIO2ServerClient client;
            if (clients.TryGetValue(remote, out client))
            {
                return client;
            }
            else
            {
                client = new HIO2ServerClient(this, remote);
                clients.Add(remote, client);
                return client;
            }
        }

        public void SendCommand(int buf_idx, char cmd, byte value, HIO2ServerClient client)
        {
            byte[] data = new byte[4];
            Array.Copy(BitConverter.GetBytes((short)buf_idx), data, 2);
            data[2] = (byte)cmd;
            data[3] = value;

            IPEndPoint ep;
            if (client != null)
            {
                ep = client.sender;
            }
            else
            {
                ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1235);
            }

            server.Send(data, data.Length, ep);
        }

        public HIO2Server()
        {
            server = new UdpClient(1234);

            thread = new Thread(() =>
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    byte[] data = server.Receive(ref remote);

                    if (data.Length == 1)
                    {
                        // handshake! remove existing client
                        clients.Clear();
                        continue;
                    }

                    HIO2ServerClient client = GetClient(remote);
                    client.ReceiveMessage(data);
                }
            });
            thread.Start();
        }
    }
}
