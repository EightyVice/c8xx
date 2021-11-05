using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace c8xx
{
    public enum Pixel : uint
    {
        Off,
        On
    }
    public struct RGBAPixel
	{
		public RGBAPixel(uint fromUint) {
            R = (byte)(fromUint & 0xFF0000000 >> 32);
            G = (byte)(fromUint & 0x00FF00000 >> 16);
            B = (byte)(fromUint & 0x00000FF00 >> 8);
            A = (byte)(fromUint & 0x0000000FF);
        }
        public byte R, G, B, A;
	}



    public class Opcode
    {
        private ushort _opcode;
        public Opcode(ushort opcode, bool bigEndian)
        {
            if (bigEndian)
                _opcode = opcode;
            else
                _opcode = (ushort)((opcode >> 8) | (opcode << 8));

            DisssectOpcode();
        }
        private void DisssectOpcode()
        {
            OpcodeWord = _opcode;                                                  // 0xABCD
            ID = (byte)(_opcode >> 12);                                  // 0x000A
            SecondNibble = (byte)((_opcode >> 8) & 0x000F);                        // 0x000B
            ThirdNibble = (byte)((_opcode >> 4) & 0x000F);                        // 0x000C
            LastNibble = (byte)(_opcode & 0x000F);                               // 0x000D
            LastByte = (byte)(_opcode & 0x00FF);                               // 0x00CD
            LocationParameter = (ushort)(_opcode & 0x0FFF);                               // 0x0BCD
        }
        public ushort OpcodeWord { get; set; }
        public byte ID { get; set; }
        public byte SecondNibble { get; set; }
        public byte ThirdNibble { get; set; }
        public byte LastNibble { get; set; }
        public byte LastByte { get; set; }
        public ushort LocationParameter { get; set; }
    }
    public class Instruction
    {
        public Opcode Opcode { get; set; }
        public string Disassembly { get; set; }

    }
    public class C8VM
    {
        #region Properties
        // RAM
        private byte[] _ram = new byte[0xFFF];
        public byte[] RAM
        {
            get { return _ram; }
        }

        // Inputs
        private bool[] _input = new bool[16];
        public bool[] Keyboard { get => _input; set => _input = value; }

        // Registers
        private byte[] _v = new byte[16];
        public byte[] V
        {
            get { return _v; }
        }

        // 16-bit Register
        private ushort _i;
        public ushort I
        {
            get { return _i; }
            set { _i = value; }
        }

        // Program Counter (Instruction Pointer)ذ
        public ushort PC { get; set; }

		#region Timers
		// Delay Timer
		private byte dt;
        public byte DelayTimer
        {
            get { return dt; }
            set { dt = value; }
        }

        // Sound Timer
        private byte st;
        public byte SoundTimer
        {
            get { return st; }
            set { st = value; }
        }
		#endregion

		#region Display
        private byte[] fontSet =
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
            0x20, 0x60, 0x20, 0x20, 0x70, // 1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
            0x90, 0x90, 0xF0, 0x10, 0x10, // 4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
            0xF0, 0x10, 0x20, 0x40, 0x40, // 7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
            0xF0, 0x90, 0xF0, 0x90, 0x90, // A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
            0xF0, 0x80, 0x80, 0x80, 0xF0, // C
            0xE0, 0x90, 0x90, 0x90, 0xE0, // D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
            0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

		public uint OnPixel { get; set; }
        public uint OffPixel { get; set; }

        private uint[] _display = new uint[100 * 100];

        public int[] RawBuffer
        {
            get; private set;
        } = new int[100 * 100];

        public uint[] RGBABuffer
        {
            get;
            private set;
        } = new uint[64 * 32];

        public void ClearDisplay()
        {
            Array.Clear(_display, 0, _display.Length);
            RGBABuffer = Enumerable.Repeat(OffPixel, 64*32).ToArray();
        }

        #endregion
        public Stack<ushort> Stack { get; set; }

        public Opcode CurrentOpcode { get; set; }
        #endregion

        public delegate void DisplayUpdatedEventHandler(int X, int Y, uint[,] Pixels);
        public event DisplayUpdatedEventHandler DisplayUpdated;

        public event KeyPressEventHandler KeyNeeded;

        BinaryReader reader;
        byte[] _romBytes;
        int inputregister;

        Dictionary<uint, Action<Opcode>> Instructions;
        Dictionary<uint, Func<Opcode, string>> Disassembly;
        private bool _inputNeeded = false;
        public string GetCurrentInstructionDisassembly()
		{
            return Disassembly[CurrentOpcode.ID](CurrentOpcode);
		}
        public C8VM(byte[] romBytes)
        {
            // Init Components
            Stack = new Stack<ushort>();
            OffPixel = 0x9BBC0F;
            OnPixel = 0x0F380F; 
            ClearDisplay();

            _romBytes = romBytes;
            // Mount ROM to RAM
            Array.Copy(romBytes, 0, _ram, 0x200, romBytes.Length);

            // Load font to RAM
            Array.Copy(fontSet, 0, _ram, 0, fontSet.Length);

            // Set Entry Point
            PC = 0x200;

            // Set Instructions Actions
            Instructions = new Dictionary<uint, Action<Opcode>>() {
                {0, (opcode) => {
                    if(opcode.LastByte == 0xE0)
                        ClearDisplay();
                    else if(opcode.LastByte == 0xEE)
                        PC = Stack.Pop();
                    else
                        throw new Exception();

                    PC += 2;
                } },

                {1, (opcode) => {PC = opcode.LocationParameter; } },

                {2, (opcode) => {Stack.Push(PC); PC = opcode.LocationParameter; } },

                {3, (opcode) => {
                    if (V[opcode.SecondNibble] == opcode.LastByte)
                        PC += 2;
                    PC += 2;
                } },
                
                {4, (opcode) => {
                    if (V[opcode.SecondNibble] != opcode.LastByte)
                        PC += 2;
                    PC += 2;

                } },

                {5, (opcode) => {
                    if (V[opcode.SecondNibble] == V[opcode.ThirdNibble])
                        PC += 2;
                    PC += 2;
                } },

                {6, (opcode) => {V[opcode.SecondNibble] = opcode.LastByte; PC += 2;} },

                {7, (opcode) => {V[opcode.SecondNibble] += opcode.LastByte; PC += 2;} },

                {8, (opcode) => {
                    switch (opcode.LastNibble)
                    {
                        case 0:
                            V[opcode.SecondNibble] = V[opcode.ThirdNibble];
                            break;
                        case 1:
                            V[opcode.SecondNibble] |= V[opcode.ThirdNibble];
                            break;
                        case 2:
                            V[opcode.SecondNibble] &= V[opcode.ThirdNibble];
                            break;
                        case 3:
                            V[opcode.SecondNibble] ^= V[opcode.ThirdNibble];
                            break;
                        case 4:
                            if (V[opcode.ThirdNibble] > (0xFF - V[opcode.SecondNibble]))
                                 V[0xF] = 1;
                            else
                                 V[0xF] = 0;

                            V[opcode.SecondNibble] += V[opcode.ThirdNibble];
                            break;
                        case 5:
                            if (V[opcode.SecondNibble] > V[opcode.ThirdNibble])
                                 V[0xF] = 1;
                            else
                                 V[0xF] = 0;

                            V[opcode.SecondNibble] -= V[opcode.ThirdNibble];
                            break;
                        case 6:
                            V[0xF] = (byte)(V[opcode.SecondNibble] & 0x0001);
                            V[opcode.SecondNibble] <<= 1;
                            break;

                        case 7:
                            if (V[opcode.ThirdNibble] > V[opcode.SecondNibble])
                                 V[0xF] = 1;
                            else
                                 V[0xF] = 0;
                            V[opcode.SecondNibble] =  (byte)(V[opcode.ThirdNibble] - V[opcode.SecondNibble]);
                            break;

                        case 0xE:
                            V[0xF] = (byte)(V[opcode.SecondNibble] & 0x8000);
                            V[opcode.SecondNibble] >>= 1; break;
                    }
                    PC += 2;
                } },

                {9, (opcode) => {
                    if (V[opcode.SecondNibble] != V[opcode.ThirdNibble])
                         PC += 2;
                } },

                {0xA, (opcode) => {I = opcode.LocationParameter; PC += 2; } },

                {0xB, (opcode) => {PC =  (ushort)(V[0] + opcode.LocationParameter);} },


                {0xC, (opcode) => {
                    Random rand = new Random();
                    V[opcode.SecondNibble] = (byte)(rand.Next(0, 255) & (uint)opcode.LastByte);
                    PC += 2;
                } },

                {0xD, (opcode) => {
                    int x = V[opcode.SecondNibble];
                    int y = V[opcode.ThirdNibble]; 
                    int n = opcode.LastNibble;

                    V[15] = 0;

                    for (int i = 0; i < n; i++)
                    {
                        byte mem = RAM[I + i];

                        for (int j = 0; j < 8; j++)
                        {
                            byte pixel = (byte)((mem >> (7 - j)) & 0x01);
                            int index = x + j + (y + i) * 64;

                            if (index > 2047) continue;

                            if (pixel == 1 && RawBuffer[index] != 0) V[15] = 1;

                            RawBuffer[index] = (RawBuffer[index] != 0 && pixel == 0) || (RawBuffer[index] == 0 && pixel == 1) ? 1 : 0;

                            if(RawBuffer[index] == 1)
                                RGBABuffer[index] = OnPixel;
                            else
                                RGBABuffer[index] = OffPixel;

                        }
                    }

                    //V[0xF] = 0;

                    //int x = V[opcode.SecondNibble];
                    //int y = V[opcode.ThirdNibble];
                    //int n = opcode.LastNibble;

                    //byte[] spritebytes = new byte[n];
                    //Array.Copy(_ram, I, spritebytes, 0, n);

                    //byte reverse(byte b) {
                    //    b = (byte)((b & 0xF0) >> 4 | (b & 0x0F) << 4);
                    //    b = (byte)((b & 0xCC) >> 2 | (b & 0x33) << 2);
                    //    b = (byte)((b & 0xAA) >> 1 | (b & 0x55) << 1);
                    //    return b;
                    //}

                    //uint[,] pixels = new uint[8,n];

                    //// Get pixels from spritebytes
                    //for (int sprY = 0; sprY < n; sprY++)
                    //{
                    //    byte row = reverse(spritebytes[sprY]);
                    //    BitArray bits = new BitArray(new byte[] { row });

                    //    for (int sprX = 0; sprX < 8; sprX++)
                    //    {
                    //        int posX = (x + sprX) % 64;
                    //        int posY = (y + sprY) % 32;

                    //        if(Display[posX, posY] == 0xffffffff && (Display[posX, posY] ^ Convert.ToUInt32(bits[sprX])) == 0)
                    //            V[0xF] = 1;

                    //        Display[posX, posY] ^= Convert.ToUInt32(bits[sprX]);

                    //        pixels[sprX, sprY] = Display[posX, posY];
                    //    }
                    //}
                    //DisplayUpdated?.Invoke(x, y, pixels);

                    //PC += 2;

                    //return;
                    //for (int sprY = 0; sprY < n; sprY++)
                    //{

                    //    byte row = reverse(spritebytes[sprY]);

                    //    BitArray bits = new BitArray(new byte[] { row });
                    //    for (int sprX = 0; sprX < 8; sprX++)
                    //    {
                    //        if(bits[sprX] == true){
                    //            int posX = (x + sprX) % 64;
                    //            int posY = (y + sprY) % 32;

                    //            Pixel currPixel = Display[posX, posY];
                    //            if(currPixel == Pixel.On && ((int)currPixel ^ Convert.ToInt32(bits[sprX])) == 0)
                    //                V[0xF] = 1;

                    //            Display[posX, posY] ^= (Pixel)Convert.ToInt32(bits[sprX]);
                    //        }
                    //    }
                    //}

                    //DisplayUpdated();
                    PC += 2;
                }  },

                {0xE, (opcode) => {
                    //Debug.WriteLine("input");
                    if(opcode.LastByte == 0x9E){
                        if(Keyboard[V[opcode.SecondNibble]])
                            PC += 2;
                    }
                    else if(opcode.LastByte == 0xA1){
                        if (!Keyboard[V[opcode.SecondNibble]])
                            PC += 2;
                    }
                    else
                        throw new Exception();

                    PC += 2;
                } },

                {0xF, (opcode) =>
                {
                    switch ((byte)opcode.LastByte)
                    {
                        case 0x07:
                            V[opcode.SecondNibble] = DelayTimer;
                            break;
                        case 0x0A:
                            inputregister = opcode.SecondNibble;
                            _inputNeeded = true;
                            break;
                        case 0x15:
                            DelayTimer = V[opcode.SecondNibble];
                            break;
                        case 0x18:
                            SoundTimer = V[opcode.SecondNibble]; break;
                        case 0x1E:
                            I += V[opcode.SecondNibble];
                            break;
                        case 0x29:
                            I = (ushort)(V[opcode.SecondNibble] * 5);
                            break;
                        case 0x33:
                            RAM[I] = (byte)(V[opcode.SecondNibble] / 100);
                            RAM[I + 1 ] = (byte)((V[opcode.SecondNibble] % 100) / 10);
                            RAM[I + 2] = (byte)(V[opcode.SecondNibble] % 10);
                            break;
                        case 0x55:
                            for (int i = 0; i <= opcode.SecondNibble; i++)
                            {
                                RAM[I+ i] = V[i];
                            }
                            break;
                        case 0x65:
                            for (int i = 0; i <= opcode.SecondNibble; i++)
                            {
                                V[i] = RAM[I + i];
                            }
                            break;
                    }
                    PC += 2;
                } }
            };

            Disassembly = new Dictionary<uint, Func<Opcode, string>>() {
                {0, (opcode) => {
                    if(opcode.LastByte == 0xE0)
                        return "CLS";
                    else if(opcode.LastByte == 0xEE)
                        return "RET";
                    else
                        return "UNKWN";
                } },

                {1, (opcode) => {
                    return $"JP {opcode.LocationParameter:X4}";
                } },

                {2, (opcode) => {
                    return $"CALL {opcode.LocationParameter:X4}";
                } },

                {3, (opcode) => {
                    return $"SE V{opcode.SecondNibble}, {opcode.LastByte}";
                } },

                {4, (opcode) => {
                    return $"SNE V{opcode.SecondNibble}, {opcode.LastByte}";
                } },

                {5, (opcode) => {
                    return $"SE V{opcode.SecondNibble}, V{opcode.ThirdNibble}";      
                } },

                {6, (opcode) => {
                    return $"LD V{opcode.SecondNibble}, V{opcode.LastByte}";
                } },

                {7, (opcode) => {
                    return $"ADD V{opcode.SecondNibble}, V{opcode.LastByte}";
                } },

                {8, (opcode) => {
                    switch (opcode.LastNibble)
                    {
                        case 0:
                            return $"LD V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 1:
                            return $"OR V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 2:
                            return $"AND V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 3:
                            return $"XOR V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 4:
                            return $"ADD V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 5:
                            return $"SUB V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 6:
                            return $"SHR V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 7:
                            return $"SUBN V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        case 0xE:
                            return $"SHL V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                        default:
                            return $"UNKNWN: {opcode.OpcodeWord:X4}";
                    }
                } },

                {9, (opcode) => {
                    return $"SNE V{opcode.SecondNibble}, V{opcode.ThirdNibble}";
                } },

                {0xA, (opcode) => { 
                    return $"LD I, {opcode.LocationParameter:X4}";
                } },

                {0xB, (opcode) => {
                    return $"JP V0, {opcode.LocationParameter:X4}";
                } },


                {0xC, (opcode) => {
                    return $"RND V{opcode.SecondNibble}, {opcode.LastByte}";
                } },

                {0xD, (opcode) => {
                    return $"DRW V{opcode.SecondNibble}, V{opcode.ThirdNibble}, {opcode.LastNibble}";
                }  },

                {0xE, (opcode) => {
					switch (opcode.ThirdNibble)
					{
                        case 0x9:
                            return $"SKP V{opcode.SecondNibble}";
                        case 0xA:
                            return $"SKNP V{opcode.SecondNibble}";
                        default:
                            return "UNKNWN";
					}
                } },

                {0xF, (opcode) =>
                {
                    switch ((byte)opcode.LastByte)
                    {
                        case 0x07:
                            return $"LD V{opcode.SecondNibble}, DT";
                        case 0x0A:
                            return $"LD V{opcode.SecondNibble}, K";
                        case 0x15:
                            return $"LD DT, V{opcode.SecondNibble}";
                        case 0x18:
                            return $"LD ST, V{opcode.SecondNibble}";
                        case 0x1E:
                            return $"ADD I, V{opcode.SecondNibble}";
                        case 0x29:
                            return $"LD F, V{opcode.SecondNibble}";
                        case 0x33:
                            return $"LD B, V{opcode.SecondNibble}";
                        case 0x55:
                            return $"LD [I], V{opcode.SecondNibble}";
                        case 0x65:
                            return $"LD V{opcode.SecondNibble}, [I] ";
                         default:
                            return $"UNKNWN";
                    }
                } }
            };

        }

        public void SendKey(int keyCode)
        {
            if (_inputNeeded)
            {
                V[inputregister] = (byte)keyCode;
                _inputNeeded = false;
            }
        }

        public void EmulateCycle()
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            if (PC >= 0xFFF)
                throw new OutOfMemoryException("Program counter is beyond RAM size");

            // Fetch opcode
            ushort bigEndianWord = (ushort)(_ram[PC] << 8 | _ram[PC + 1]);
            var opcodeInfo = new Opcode(bigEndianWord, true);
            CurrentOpcode = opcodeInfo;
            //Debug.WriteLine($"{PC:X}: {opcodeInfo.OpcodeWord:X4} [{opcodeInfo.ID:X}: {opcodeInfo.SecondNibble:X}, {opcodeInfo.ThirdNibble:X}, {opcodeInfo.LocationParameter:X} ]\n");

            // Execute & Store
            if (!_inputNeeded)
            {
                Instructions[opcodeInfo.ID](opcodeInfo);
                if (DelayTimer > 0)
                    DelayTimer--;

                if (SoundTimer > 0)
                    SoundTimer--;
            }

            //sw.Stop();
            //Debug.WriteLine($"Tick Time: {sw.ElapsedTicks}");
        }

        private IEnumerable<ushort> GetOpcodes()
        {
            for (int i = 0; i < _romBytes.Length; i += 2)
            {
                yield return (ushort)(_romBytes[i] << 8 | _romBytes[i + 1]);

            }
        }
        public ReadOnlyCollection<string> Disassemble()
        {
            var mnems = new List<string>();
            int address = 0x200;
            foreach (var op in GetOpcodes())
            {

                var opcode = new Opcode(op, true);

                uint inst_id = opcode.ID;
                uint second_nibble = opcode.SecondNibble;
                uint third_nibble = opcode.ThirdNibble;
                uint last_nibble = opcode.LastNibble;
                uint last_byte = opcode.LastByte;
                uint loc_arg = opcode.LocationParameter;

                string mnem_line = $"0x{address:X4}: {BitConverter.ToString(BitConverter.GetBytes(op).Reverse().ToArray()).Replace("-", " ")} "; // reversed because of endianess
                address += 2;

                switch (inst_id)
                {
                    case 0:
                        {
                            switch (last_byte)
                            {
                                case 0xE0: mnem_line += string.Format("CLS"); break;
                                case 0xEE:
                                    mnem_line += string.Format("RET");
                                    break;
                            }

                            break;
                        }

                    case 1: mnem_line += string.Format("JP {0:X}", loc_arg); break;
                    case 2:
                        mnem_line += string.Format("CALL {0:X}", loc_arg);

                        break;
                    case 3:
                        mnem_line += string.Format("SE V{0}, {1}", second_nibble, last_byte);

                        break;
                    case 4:
                        mnem_line += string.Format("SNE V{0}, {1}", second_nibble, last_byte);

                        break;
                    case 5:
                        mnem_line += string.Format("SE V{0}, V{1}", second_nibble, third_nibble);

                        break;
                    case 6:
                        mnem_line += string.Format("LD V{0}, {1}", second_nibble, last_byte);

                        break;
                    case 7:
                        mnem_line += string.Format("ADD V{0}, {1}", second_nibble, last_byte);

                        break;

                    case 8:
                        {
                            switch (last_nibble)
                            {
                                case 0:
                                    mnem_line += string.Format("LD V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 1:
                                    mnem_line += string.Format("OR V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 2:
                                    mnem_line += string.Format("AND V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 3:
                                    mnem_line += string.Format("XOR V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 4:
                                    mnem_line += string.Format("ADD V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 5:
                                    mnem_line += string.Format("ADD V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                                case 6:
                                    mnem_line += string.Format("SHR V{0}, V{1}", second_nibble, third_nibble);
                                    break;

                                case 7:
                                    mnem_line += string.Format("SUBN V{0}, V{1}", second_nibble, third_nibble);
                                    break;

                                case 0xE:
                                    mnem_line += string.Format("SHL V{0}, V{1}", second_nibble, third_nibble);
                                    break;
                            }
                            break;
                        }
                    case 9:
                        mnem_line += string.Format("SNE V{0}, V{1}", second_nibble, third_nibble);
                        break;
                    case 0xA:
                        mnem_line += string.Format("LD I, {0:X}", loc_arg);
                        break;
                    case 0xB:
                        mnem_line += string.Format("JP V0, {0:X}", loc_arg);
                        break;
                    case 0xC:
                        mnem_line += string.Format("RND V{0}, {1}", second_nibble, last_byte);
                        break;
                    case 0xD:
                        mnem_line += string.Format("DRW V{0}, V{1}, V{2}", second_nibble, third_nibble, last_nibble);
                        break;
                    case 0xE:
                        {
                            switch (last_byte)
                            {
                                case 0x9E:
                                    mnem_line += string.Format("SKP V{0}", second_nibble);
                                    break;
                                case 0xA1:
                                    mnem_line += string.Format("SKNP V{0}", second_nibble);
                                    break;
                            }
                            break;
                        }
                    case 0xF:
                        {
                            switch (last_byte)
                            {
                                case 0x07:
                                    mnem_line += string.Format("LD V{0}, DT", second_nibble);
                                    break;
                                case 0x0A:
                                    mnem_line += string.Format("LD V{0}, K", second_nibble);
                                    break;
                                case 0x15:
                                    mnem_line += string.Format("LD DT, V{0}", second_nibble);
                                    break;
                                case 0x18:
                                    mnem_line += string.Format("LD ST, V{0}", second_nibble);
                                    break;
                                case 0x1E:
                                    mnem_line += string.Format("ADD I, V{0}", second_nibble);
                                    break;
                                case 0x29:
                                    mnem_line += string.Format("LD F, V{0}", second_nibble);
                                    break;
                                case 0x33:
                                    mnem_line += string.Format("LD B, V{0}", second_nibble);
                                    break;
                                case 0x55:
                                    mnem_line += string.Format("LD [I], V{0}", second_nibble);
                                    break;
                                case 0x65:
                                    mnem_line += string.Format("LD V{0}, [I]", second_nibble);
                                    break;
                            }
                            break;
                        }
                }
                mnems.Add(mnem_line);
            }

            PC = 0x200;
            return mnems.AsReadOnly();
        }

    }
}
