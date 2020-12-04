using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MCHI
{
    class MemoryInputStream
    {
        public int offset = 0;
        public byte[] data;

        public MemoryInputStream(byte[] data)
        {
            this.data = data;
            this.offset = 0;
        }

        public bool HasData()
        {
            return this.offset < this.data.Length;
        }

        public string ReadMagic(int size = 4)
        {
            int offset = this.offset;
            this.offset += size;
            return Encoding.ASCII.GetString(this.data, offset, size);
        }

        public float ReadF32()
        {
            int offset = this.offset;
            this.offset += 0x04;
            return BitConverter.ToSingle(JHI.SwapBytes(this.data.AsSpan(offset, 4).ToArray()), 0);
        }

        public uint ReadU32()
        {
            int offset = this.offset;
            this.offset += 0x04;
            return JHI.SwapBytes(BitConverter.ToUInt32(this.data, offset));
        }

        public int ReadS32()
        {
            int offset = this.offset;
            this.offset += 0x04;
            return JHI.SwapBytes(BitConverter.ToInt32(this.data, offset));
        }

        public uint ReadU16()
        {
            int offset = this.offset;
            this.offset += 0x02;
            return JHI.SwapBytes(BitConverter.ToUInt16(this.data, offset));
        }

        public int ReadS16()
        {
            int offset = this.offset;
            this.offset += 0x02;
            return JHI.SwapBytes(BitConverter.ToInt16(this.data, offset));
        }

        public byte ReadS8()
        {
            return this.data[this.offset++];
        }

        public string ReadSJIS()
        {
            int size = ReadS16();
            var sjis = Encoding.GetEncoding(932);
            int offset = this.offset;
            this.offset += size;
            return sjis.GetString(this.data, offset, size);
        }
    }

    class MemoryOutputStream
    {
        public int offset = 0;
        public byte[] data;

        public MemoryOutputStream()
        {
            this.data = new byte[8192];
            this.offset = 0;
        }

        public byte[] Finalize()
        {
            return this.data.AsSpan(0, this.offset).ToArray();
        }

        public void Write(byte[] data)
        {
            int offset = this.offset;
            this.offset += data.Length;
            Array.Copy(data, 0x00, this.data, offset, data.Length);
        }

        public void WriteMagic(string x)
        {
            Write(Encoding.ASCII.GetBytes(x));
        }

        public void Write(int x)
        {
            Write(BitConverter.GetBytes(JHI.SwapBytes(x)));
        }

        public void Write(float x)
        {
            Write(JHI.SwapBytes(BitConverter.GetBytes(x)));
        }

        public void Write(uint x)
        {
            Write(BitConverter.GetBytes(JHI.SwapBytes(x)));
        }

        public void Write(short x)
        {
            Write(BitConverter.GetBytes(JHI.SwapBytes(x)));
        }

        public void Write(ushort x)
        {
            Write(BitConverter.GetBytes(JHI.SwapBytes(x)));
        }
    }

    // PC -> Dolphin
    enum JOREventType
    {
        GetRootObjectRef    = 0x01,
        GenObjectInfo       = 0x03,
        NodeEvent           = 0x06,
        PropertyEvent       = 0x07,
        FIO                 = 0x08,
        ResultS32           = 0x0A,
        OR                  = 0x0B,
        Dir                 = 0x0D,
        HostInfo            = 0x0E,
        ResultU32           = 0x0F,
    };

    // Dolphin -> PC
    enum JORMessageType
    {
        // 0x00
        Reset            = 0x00,
        // 0x02 (str)Name (ptr)Node (ulong)flag1 (ulong)flag2
        GetRootObjectRef = 0x02,
        // 0x04 0x00 (str)Name (ptr)Node (ulong)flag1 (ulong)flag2 ; Message
        GenObjectInfo    = 0x04,
        // 0x05 0x07 Node 0x03
        InvalidNode      = 0x05,
        StartUpdateNode  = 0x08,
        FIO              = 0x09,
        // 0x0A (ptr)Return (str)Message (str)Title (ulong)Unk
        OpenMessageBox   = 0x0A,
        StartNode        = 0x0C,
    }

    enum EKind
    {
        HasListener = 0x40000000,
        ValueID     = 0x20000000,
        FloatValue  = 0x00000200,
        SizeMask    = 0x000000FF,
    }

    struct JORControlLocation
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    abstract class JORControl
    {
        public string Type;
        public EKind Kind;
        public string Name;
        public uint Style;
        public uint ID;
        public uint ListenerPtr = 0;
        public JORControlLocation Location;
        public JORNode Node;

        public void Update(uint updateMode, MemoryInputStream stream)
        {
            // TODO
        }

        protected MemoryOutputStream BeginSendPropertyEvent(JORServer server)
        {
            MemoryOutputStream stream = server.BeginSendEvent(JOREventType.PropertyEvent);
            stream.Write(Node.NodePtr);
            stream.Write(0); // Unknown, game skips over it
            stream.WriteMagic(Type);
            stream.Write((uint)Kind);
            stream.Write(ID);
            stream.Write(ListenerPtr);
            return stream;
        }
    }

    class JORControlImmutable : JORControl
    {
    }

    class JORControlButton : JORControl
    {
        public void Click(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            stream.Write(0);
            server.SendEvent(stream);
        }
    }

    class JORControlCheckBox : JORControl
    {
        public uint Mask = 0;
        public bool Value = false;

        private void SendPropertyEvent(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            stream.Write(4);
            stream.Write((ushort)(Value ? 0x01 : 0x00));
            stream.Write((ushort)Mask);
            server.SendEvent(stream);
        }

        public void SetValue(JORServer server, bool newValue)
        {
            if (newValue == Value)
                return;

            Value = newValue;
            SendPropertyEvent(server);
        }
    }

    class JORControlRangeInt : JORControl
    {
        public int RangeMin = -1;
        public int RangeMax = -1;
        public int Value = 0;

        private void SendPropertyEvent(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            stream.Write(4);
            stream.Write(Value);
            server.SendEvent(stream);
        }

        public void SetValue(JORServer server, int newValue)
        {
            if (newValue == Value)
                return;

            Value = newValue;
            SendPropertyEvent(server);
        }
    }

    class JORControlRangeFloat : JORControl
    {
        public float RangeMin = -1;
        public float RangeMax = -1;
        public float Value = 0;

        private void SendPropertyEvent(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            stream.Write(4);
            stream.Write(Value);
            server.SendEvent(stream);
        }

        public void SetValue(JORServer server, float newValue)
        {
            if (newValue == Value)
                return;

            Value = newValue;
            SendPropertyEvent(server);
        }
    }

    class JORControlEditBox : JORControl
    {
        public string Text;
        public uint MaxChars;

        private void SendPropertyEvent(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            var sjis = Encoding.GetEncoding(932);
            var bytes = sjis.GetBytes(Text);
            stream.Write(bytes.Length);
            stream.Write(bytes);
            server.SendEvent(stream);
        }

        public void SetValue(JORServer server, string newValue)
        {
            if (newValue == Text)
                return;

            Text = newValue;
            SendPropertyEvent(server);
        }
    }

    class JORControlSelectorItem
    {
        public string Name;
        public uint Value;
        public uint Unk3;
        public JORControlLocation Location;
    }

    class JORControlSelector : JORControl
    {
        public List<JORControlSelectorItem> Items = new List<JORControlSelectorItem>();
        public uint Value;

        private void SendPropertyEvent(JORServer server)
        {
            var stream = BeginSendPropertyEvent(server);
            stream.Write(4);
            stream.Write(Value);
            server.SendEvent(stream);
        }

        public void SetValue(JORServer server, uint newValue)
        {
            if (newValue == Value)
                return;

            Value = newValue;
            SendPropertyEvent(server);
        }
    }

    enum JORNodeStatus
    {
        Invalid,
        GenRequestSent,
        Valid,
    }

    class JORNode
    {
        public uint NodePtr;
        public string Name;
        public uint flag1;
        public uint flag2;
        public JORNodeStatus Status = JORNodeStatus.Invalid;

        public List<JORControl> Controls = new List<JORControl>();
        public List<JORNode> Children = new List<JORNode>();

        public JORControl FindControlByID(uint id)
        {
            foreach (var control in Controls)
                if (control.ID == id)
                    return control;
            return null;
        }

        public override string ToString()
        {
            return String.Format("{0} 0x{1:X8}", Name, NodePtr);
        }

        public void AddChild(JORNode child)
        {
            if (!Children.Contains(child))
                Children.Add(child);
        }

        public JORNode GetByPtr(uint nodePtr)
        {
            if (nodePtr == this.NodePtr)
                return this;
            foreach (var child in this.Children)
            {
                JORNode childNode = child.GetByPtr(nodePtr);
                if (childNode != null)
                    return childNode;
            }
            return null;
        }

        public void Invalidate()
        {
            this.Status = JORNodeStatus.Invalid;
            this.Controls.Clear();
            this.Children.Clear();
        }

        public IEnumerable<JORNode> DepthFirstIter()
        {
            yield return this;
            foreach (var child in this.Children)
            {
                foreach (var grand in child.DepthFirstIter())
                    yield return grand;
            }
        }
    }

    class JORRoot
    {
        public JORNode TreeRoot;

        public JORRoot()
        {
            Reset();
        }

        public void Reset()
        {
            this.TreeRoot = new JORNode();
            this.TreeRoot.NodePtr = 0xFFFFFFFF;
            this.TreeRoot.Status = JORNodeStatus.Valid;
        }

        public JORNode GetByPtr(uint nodePtr)
        {
            return this.TreeRoot.GetByPtr(nodePtr);
        }
    }

    class JORServer : IJHITagProcessor
    {
        public JORRoot Root = new JORRoot();
        private JHIClient client;

        public JORServer(JHIClient client)
        {
            this.client = client;
            this.client.RegisterTagProcessor(this);
        }

        public string GetMagic()
        {
            return "ORef";
        }

        public void SendEvent(MemoryOutputStream stream)
        {
            JHITag tag;
            tag.Magic = GetMagic();
            tag.Data = stream.Finalize();
            this.client.WriteToDolphin(tag);
        }

        public MemoryOutputStream BeginSendEvent(JOREventType type)
        {
            var stream = new MemoryOutputStream();
            stream.Write((int) type);
            return stream;
        }

        public void SendGetRootObjectRef()
        {
            var stream = BeginSendEvent(JOREventType.GetRootObjectRef);
            SendEvent(stream);
        }

        public void SendGenObjectInfo(JORNode node)
        {
            Debug.WriteLine("-> GenObjectInfo 0x{0:X8}", node.NodePtr);
            var stream = BeginSendEvent(JOREventType.GenObjectInfo);
            stream.Write(node.NodePtr);
            SendEvent(stream);
            node.Status = JORNodeStatus.GenRequestSent;
        }

        private void Assert(bool b)
        {
            if (!b)
                throw new Exception("whoops!");
        }

        public JORNode ReadNodeID(MemoryInputStream stream)
        {
            uint nodePtr = stream.ReadU32();
            return this.Root.GetByPtr(nodePtr);
        }

        public JORNode ReadGenNodeSub(MemoryInputStream stream, bool create = false, JORNode node = null)
        {
            string name = stream.ReadSJIS();

            uint nodePtr = stream.ReadU32();

            Debug.WriteLine("<- ORef GenNodeSub {0} 0x{1:X8}", name, nodePtr);

            if (node == null)
            {
                node = this.Root.GetByPtr(nodePtr);
                if (node == null && create)
                    node = new JORNode();
            }

            uint flag1 = stream.ReadU32();
            uint flag2 = 0;
            if ((flag1 & 0x04) != 0)
                flag2 = stream.ReadU32();

            if (node != null)
            {
                if (name != String.Empty)
                {
                    node.Name = name;
                    node.flag1 = flag1;
                    node.flag2 = flag2;
                }
                node.NodePtr = nodePtr;
            }

            return node;
        }

        enum JORMessageCommand
        {
            StartNode = 0,
            EndNode = 1,
            GenControl = 2,
            GenNode = 3,
            StartSelector = 4,
            EndSelector = 5,
            SelectorItem = 6,

            UpdateControl = 8,
        }

        private void ReadControlLocation(MemoryInputStream stream, ref JORControlLocation location)
        {
            location.X = stream.ReadS16();
            location.Y = stream.ReadS16();
            location.Width = stream.ReadS16();
            location.Height = stream.ReadS16();
        }

        private JORControl ReadControlSub(MemoryInputStream stream, JORNode node)
        {
            string controlType = stream.ReadMagic();

            JORControl control;
            if (controlType == "RNGi")
                control = new JORControlRangeInt();
            else if (controlType == "RNGf")
                control = new JORControlRangeFloat();
            else if (controlType == "EDBX")
                control = new JORControlEditBox();
            else if (controlType == "CMBX" || controlType == "RBTN")
                control = new JORControlSelector();
            else if (controlType == "CHBX")
                control = new JORControlCheckBox();
            else if (controlType == "BUTN")
                control = new JORControlButton();
            else
                control = new JORControlImmutable();

            control.Node = node;
            control.Type = controlType;
            control.Kind = (EKind) stream.ReadU32();
            control.Name = stream.ReadSJIS();
            control.Style = stream.ReadU32();
            control.ID = stream.ReadU32();
            Debug.WriteLine("<- Control {0} {1} {2}", control.Type, control.Kind, control.Name);

            if ((control.Kind & EKind.HasListener) != 0)
            {
                control.ListenerPtr = stream.ReadU32();
            }

            if (((control.Kind & EKind.ValueID) != 0) && control.Type != "EDBX")
            {
                control.Kind |= (EKind) 0x20;
            }

            float valueF = 0.0f;
            uint valueU = 0xFFFFFFFF;

            uint kindSize = (uint) (control.Kind & EKind.SizeMask);
            if (kindSize != 0)
            {
                if ((control.Kind & EKind.FloatValue) != 0)
                {
                    valueF = stream.ReadF32();
                }
                else
                {
                    valueU = stream.ReadU32();
                }
            }

            if (control is JORControlCheckBox)
            {
                var cb = control as JORControlCheckBox;
                cb.Mask = valueU >> 16;
                cb.Value = (valueU & 0xFF) != 0;
            }
            else if (control is JORControlRangeInt)
            {
                var range = control as JORControlRangeInt;
                range.RangeMin = stream.ReadS32();
                range.RangeMax = stream.ReadS32();
                range.Value = (int) valueU;
            }
            else if (control is JORControlRangeFloat)
            {
                var range = control as JORControlRangeFloat;
                range.RangeMin = stream.ReadF32();
                range.RangeMax = stream.ReadF32();
                range.Value = valueF;
            }
            else if (control is JORControlEditBox)
            {
                var editBox = control as JORControlEditBox;
                editBox.MaxChars = stream.ReadU16();
                editBox.Text = stream.ReadSJIS();
            }

            ReadControlLocation(stream, ref control.Location);

            node.Controls.Add(control);
            return control;
        }

        private void ProcessObjectInfo(MemoryInputStream stream)
        {
            JORControlSelector currentSelector = null;
            Stack<JORNode> nodeStack = new Stack<JORNode>();
            JORNode node = null;

            while (stream.HasData())
            {
                JORMessageCommand command = (JORMessageCommand)stream.ReadU32();

                Debug.WriteLine("<- ORef MessageCommand {0}", command);

                if (command == JORMessageCommand.StartNode)
                {
                    node = ReadGenNodeSub(stream, true);
                    Debug.WriteLine("StartNodeClear  {0}", node);
                    nodeStack.Push(node);
                    node.Children.Clear();
                    node.Controls.Clear();
                }
                else if (command == JORMessageCommand.EndNode)
                {
                    node.Status = JORNodeStatus.Valid;
                    node = nodeStack.Pop();
                }
                else if (command == JORMessageCommand.GenNode)
                {
                    JORNode child = ReadGenNodeSub(stream, true);
                    node.AddChild(child);
                }
                else if (command == JORMessageCommand.GenControl)
                {
                    var control = ReadControlSub(stream, node);
                }
                else if (command == JORMessageCommand.StartSelector)
                {
                    var control = ReadControlSub(stream, node);
                    Debug.Assert(control is JORControlSelector);
                    currentSelector = control as JORControlSelector;
                }
                else if (command == JORMessageCommand.EndSelector)
                {
                    currentSelector = null;
                }
                else if (command == JORMessageCommand.SelectorItem)
                {
                    var selection = new JORControlSelectorItem();
                    selection.Name = stream.ReadSJIS();
                    selection.Value = stream.ReadU32();
                    selection.Unk3 = stream.ReadU32();
                    ReadControlLocation(stream, ref selection.Location);

                    if (currentSelector != null)
                        currentSelector.Items.Add(selection);
                }
                else
                {
                    throw new Exception("Unknown message type");
                }
            }
        }

        private void ProcessUpdateNode(MemoryInputStream stream, JORNode node)
        {
            while (stream.HasData())
            {
                JORMessageCommand command = (JORMessageCommand)stream.ReadU32();

                Debug.WriteLine("<- ORef ProcessUpdate {0}", command);

                if (command == JORMessageCommand.UpdateControl)
                {
                    uint updateMode = stream.ReadU32();
                    uint id = stream.ReadU32();
                    var control = node.FindControlByID(id);

                    if (control == null)
                    {
                        // No clue about this control; can't parse.
                        return;
                    }

                    // TODO: The rest
                    return;

                    control.Update(updateMode, stream);
                }
            }
        }

        public void ProcessTag(JHITag tag)
        {
            var stream = new MemoryInputStream(tag.Data);
            JORMessageType messageType = (JORMessageType)stream.ReadS32();

            Debug.WriteLine("<- ORef {1}  {0}", tag.Dump(), messageType);

            if (messageType == JORMessageType.Reset)
            {
                this.Root.Reset();
                SendGetRootObjectRef();
            }
            else if (messageType == JORMessageType.InvalidNode)
            {
                Assert(stream.ReadU32() == 0x07u);
                JORNode node = ReadNodeID(stream);
                Debug.WriteLine("<- InvalidNode {0}", node);
                if (node != null)
                    node.Invalidate();
                Assert(stream.ReadU32() == 0x03u);
            }
            else if (messageType == JORMessageType.GetRootObjectRef)
            {
                // Reply from GetRootObjectRef request
                JORNode node = ReadGenNodeSub(stream, false, this.Root.TreeRoot);
                SendGenObjectInfo(node);
            }
            else if (messageType == JORMessageType.GenObjectInfo)
            {
                // Reply from GenObjectInfo request
                ProcessObjectInfo(stream);
            }
            else if (messageType == JORMessageType.StartNode)
            {
                uint mode = stream.ReadU32();
                // startNode = 0, genNode = 3
                Assert(mode == 0u || mode == 3u || mode == 11u);

                if (mode == 0u || mode == 3u)
                {
                    uint unk1 = stream.ReadU32();
                    JORNode parentNode = ReadNodeID(stream);
                    JORNode node = ReadGenNodeSub(stream, true);
                    Debug.WriteLine("<- StartNode {0} Parent = 0x{1:X8}", node?.Name, parentNode);
                }
                else if (mode == 11u)
                {
                    JORNode node = ReadNodeID(stream);
                }
            }
            else if (messageType == JORMessageType.StartUpdateNode)
            {
                JORNode node = ReadNodeID(stream);
                if (node == null)
                    return;

                ProcessUpdateNode(stream, node);
            }
            else
            {
                Debug.WriteLine("<- JOR UNKNOWN!!!");
            }
        }

        public void Update()
        {
            foreach (var node in this.Root.TreeRoot.DepthFirstIter())
            {
                if (node.Status == JORNodeStatus.Invalid)
                    SendGenObjectInfo(node);
            }
        }
    }
}
