using System;
using System.Collections.Generic;
using System.Text;

namespace tsc8
{
    //tsc8 opcodes
    //Assembly:
    //LIR0        0 | imm
    //MFR0        1 | op | rd       
    //MTR0        1 | op | rs       
    //jz          1 | op | rpc      
    //jg          1 | op | rpc      
    //add         1 | op | rs       
    //neg         1 | op | rs       
    //draw        1 | op | rc       
    //vsync       1 | op | 0
    //key1        1 | op | 1
    //key2        1 | op | 2
    //jr0         1 | op | 3

    enum Opcodes
    {
        lir0 = 0,    //r0 = imm

        mfr0 = 0x80 | (0 << 4),     //rd = r0
        mtr0 = 0x80 | (1 << 4),     //r0 = rs
        add = 0x80 | (2 << 4),     //r0 += rs
        neg = 0x80 | (3 << 4),     //r0 = -rs

        draw = 0x80 | (4 << 4),     //draw @ rx,ry,rc
        vsync = 0x80 | (5 << 4) | 0, //vsync
        key1 = 0x80 | (5 << 4) | 1, //key1
        key2 = 0x80 | (5 << 4) | 2, //key2
        jr0 = 0x80 | (5 << 4) | 3, //jr0

        jz = 0x80 | (6 << 4),     //if (rs!=0) pc=r0
        jgtz = 0x80 | (7 << 4),     //if (rs>0)  pc=r0
        //others : nops ;p
    }

    //small hacky assembler
    class Assembler
    {
        public delegate void CompileEvent(Assembler sender);
        public delegate void CompileMessage(Assembler sender, int line, string msg);

        public event CompileEvent OnCompileStart;
        public event CompileEvent OnCompileEnd;
        public event CompileMessage OnReportError;

        class TokenItem
        {
            public TokenItem(string Value, int line) { this.Value = Value; this.line = line; }
            public string Value;
            public int line;
        }

        enum ParameterType
        {
            Register,
            Immidiate,
            Label
        }

        class Label
        {
            public const int Invalid = int.MaxValue;
            public bool IsValid { get { return value != Invalid; } }
            public Label(string name, int value, int line) { this.name = name; this.value = value; this.line = line; }
            public string name;
            public int value;
            public int line;
        }

        struct Parameter
        {
            public Parameter(ParameterType type, int payload) { this.type = type; this.payload = payload; }
            public bool IsR0 { get { return type == ParameterType.Register && payload == 0; } }
            public bool IsReg { get { return type == ParameterType.Register; } }
            public bool IsLabel { get { return type == ParameterType.Label; } }
            public bool IsImm { get { return type == ParameterType.Immidiate; } }
            public Parameter AsImm() { Parameter rv = this; rv.type = ParameterType.Immidiate; return rv; }

            public ParameterType type;
            public int payload;   //register id, imm value / label value
        }

        //Syntax :
        //mov rd,rs/imm
        //add rd,rs/imm
        //sub rd,rs/imm
        //neg rd,rs
        //draw rx,ry
        //vsync
        //jz rs,rd/label
        //jg rs,rd/label
        //jmp rd/label

        abstract class Opcode
        {
            public abstract string Keyword { get; }
            public abstract void Parse(List<Parameter> param);
            public List<byte> OpcodeStream;
            protected void Check(bool x, string msg)
            {
                if (!x)
                    throw new Exception(msg);
            }
            protected void Emit(byte opcode)
            {
                OpcodeStream.Add(opcode);
            }
            protected void EmitReg(Opcodes op, int Reg)
            {
                if ((Reg & 15) != Reg)
                    throw new ArgumentOutOfRangeException("Register " + Reg.ToString() + " is out of range");
                Emit((byte)((int)op | Reg));
            }
            protected void EmitImm(Opcodes op, int Imm)
            {
                if ((Imm & 127) != Imm)
                    throw new ArgumentOutOfRangeException("Immidate " + Imm.ToString() + " is out of range");
                Emit((byte)((int)op | Imm));
            }
            protected void EmitLImm(int Imm)
            {
                if ((Imm & 255) != Imm || Imm == 0x80)
                    throw new ArgumentOutOfRangeException("Immidate " + Imm.ToString() + " is out of range");

                if ((Imm & 127) == Imm)
                {
                    EmitImm(Opcodes.lir0, Imm);
                }
                else
                {
                    EmitImm(Opcodes.lir0, (byte)-Imm);
                    EmitReg(Opcodes.neg, 0);
                }
            }
            protected void EmitLoadR0(Parameter p)
            {
                if (p.IsLabel)
                    p = p.AsImm();

                if (!p.IsR0)
                {
                    if (p.IsImm)
                        EmitLImm(p.payload);
                    else
                        EmitReg(Opcodes.mtr0, p.payload);
                }
            }

            protected void Emit(Opcodes op)
            {
                Emit((byte)op);
            }
        }

        class MovOpcode : Opcode
        {
            public override string Keyword { get { return "mov"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs two parameters");
                Check(param[0].IsReg, "rd must be a register");

                EmitLoadR0(param[1]);

                if (!param[0].IsR0)
                {
                    EmitReg(Opcodes.mfr0, param[0].payload);
                }
            }
        }
        class AddOpcode : Opcode
        {
            public override string Keyword { get { return "add"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs two parameters");
                Check(param[0].IsReg, "rd must be a register");
                Check(!param[0].IsR0 && !param[1].IsR0, "add rd,rs: rd=r0 && rs!=reg is not suported");

                if (!param[1].IsReg)
                {
                    EmitLoadR0(param[1]);
                }
                else if (!param[1].IsR0)
                {
                    EmitReg(Opcodes.mtr0, param[1].payload);
                }

                EmitReg(Opcodes.add, param[0].payload);

                if (!param[0].IsR0)
                    EmitReg(Opcodes.mfr0, param[0].payload);
            }
        }
        class SubOpcode : Opcode
        {
            public override string Keyword { get { return "sub"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs two parameters");
                Check(param[0].IsReg, "rd must be a register");
                Check(!param[0].IsR0 && !param[1].IsR0, "sub rd,rs: rd=r0 && !rs=r0 is not suported");

                if (!param[1].IsReg)
                {
                    int v = param[1].payload;
                    EmitLImm((byte)-v);
                }
                else
                {
                    EmitReg(Opcodes.neg, param[1].payload);
                }

                EmitReg(Opcodes.add, param[0].payload);

                EmitReg(Opcodes.mfr0, param[0].payload);
            }
        }
        class NegOpcode : Opcode
        {
            public override string Keyword { get { return "neg"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs two parameters");
                Check(param[0].IsReg && param[1].IsReg, "rs,rd must be registers");


                EmitReg(Opcodes.neg, param[1].payload);
                if (!param[0].IsR0)
                    EmitReg(Opcodes.mfr0, param[0].payload);
            }
        }
        class DrawOpcode : Opcode
        {
            public override string Keyword { get { return "draw"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 3, "This opcode needs rx,ry,rs/imm");
                Check(param[0].IsReg && param[0].payload == 6, "parameter 1 must be rx");
                Check(param[1].IsReg && param[1].payload == 7, "parameter 2 must be ry");
                Check(!param[2].IsLabel, "parameter 3 must be reg/imm");

                if (!param[2].IsReg)
                {
                    EmitLoadR0(param[2]);
                    EmitReg(Opcodes.draw, 0);
                }
                else
                {
                    EmitReg(Opcodes.draw, param[2].payload);
                }
            }
        }
        class VsyncOpcode : Opcode
        {
            public override string Keyword { get { return "vsync"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 0, "This opcode needs no parameters");

                Emit(Opcodes.vsync);
            }
        }
        class JgtzOpcode : Opcode
        {
            public override string Keyword { get { return "jgtz"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs rt,rs/label");
                Check(param[0].IsReg, "rt must be register");
                Check(!param[1].IsImm, "rs must be register/label");

                EmitLoadR0(param[1]);
                EmitReg(Opcodes.jgtz, param[0].payload);
            }
        }
        class JzOpcode : Opcode
        {
            public override string Keyword { get { return "jz"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs rt,rs/label");
                Check(param[0].IsReg, "rt must be register");
                Check(!param[1].IsImm, "rs must be register/label");

                EmitLoadR0(param[1]);
                EmitReg(Opcodes.jz, param[0].payload);
            }
        }
        class JmpOpcode : Opcode
        {
            public override string Keyword { get { return "jmp"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 1 || param[0].IsImm, "This opcode needs rs/label");

                if (!param[0].IsReg)
                {
                    EmitLoadR0(param[0]);
                }
                else
                {
                    EmitReg(Opcodes.mtr0, param[0].payload);
                }

                Emit(Opcodes.jr0);
            }
        }
        class GetKeyOpcode : Opcode
        {
            public override string Keyword { get { return "getkey"; } }

            public override void Parse(List<Parameter> param)
            {
                Check(param.Count == 2, "This opcode needs two params");
                Check(param[0].IsReg, "rd must be reg");
                Check(param[1].IsImm && (param[1].payload == 1 || param[1].payload == 2), "rs must be 1 or 2");

                Emit(param[1].payload == 1 ? Opcodes.key1 : Opcodes.key2);
                if (!param[0].IsR0)
                    EmitReg(Opcodes.mfr0, param[0].payload);
            }
        }


        public int ErrorCount;

        public string MifFile;
        public byte[] BinaryCode;

        void ReportError(int line, string error)
        {
            ErrorCount += line == -1 ? 0 : 1;
            OnReportError(this, line, error);
        }

        public bool Assemble(string AssemblyText)
        {
            ErrorCount = 0;
            OnCompileStart(this);

            string code = AssemblyText.Replace(":", " : ").Replace(",", " , ").ToLower().Replace(",", " ");

            Opcode[] Ops = new Opcode[] { new MovOpcode(), new AddOpcode(), new SubOpcode(), new NegOpcode(), new DrawOpcode(), new VsyncOpcode(), new JgtzOpcode(), new JzOpcode(), new JmpOpcode(), new GetKeyOpcode() };

            List<byte> OutputCode = new List<byte>();

            foreach (Opcode op in Ops)
                op.OpcodeStream = OutputCode;

            //parser map
            Dictionary<string, Parameter> pmap = new Dictionary<string, Parameter>();

            //base regs
            for (int i = 0; i <= 15; i++)
                pmap.Add("r" + i.ToString(), new Parameter(ParameterType.Register, i));

            //special names for regs
            pmap.Add("rx", new Parameter(ParameterType.Register, 6));
            pmap.Add("ry", new Parameter(ParameterType.Register, 7));

            //numbers

            //dec
            for (int i = 0; i <= 255; i++)
                pmap.Add(i.ToString(), new Parameter(ParameterType.Immidiate, i));
            //hex
            for (int i = 0; i <= 255; i++)
                pmap.Add("0x" + Convert.ToString(i, 16), new Parameter(ParameterType.Immidiate, i));

            for (int i = 0; i <= 0xF; i++)
                pmap.Add("0x0" + Convert.ToString(i, 16), new Parameter(ParameterType.Immidiate, i));

            string[] lines = code.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<List<TokenItem>> tokens = new List<List<TokenItem>>();

            List<Label> Labels = new List<Label>();
            List<Label> LabelsToMark = new List<Label>();

            //tokenise, parse and remove labels ...
            for (int i = 0; i < lines.Length; i++)
            {
                List<string> t = new List<string>(lines[i].Split(';')[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                //remove labels
                if (t.Count >= 2 && t[1] == ":")
                {
                    Label lbl = new Label(t[0], Label.Invalid, i + 1);

                    Labels.Add(lbl);
                    LabelsToMark.Add(lbl);
                    t.RemoveAt(0);
                    t.RemoveAt(0);
                }
                //handle and remove constant defines
                if (t.Count == 3 && t[1] == "equ")
                {
                    if (pmap.ContainsKey(t[0]))
                        ReportError(i + 1, "Constant " + t[0] + "already specified");
                    else
                    {
                        if (!pmap.ContainsKey(t[2]))
                            ReportError(i + 1, "Constant " + t[0] + "has invalid value " + t[2]);
                        else
                            pmap.Add(t[0], pmap[t[2]]);
                    }
                    t.Clear();
                }

                //is anything left? lets see if its an opcode :p
                if (t.Count > 0)
                {
                    foreach (Label lbl in LabelsToMark)
                    {
                        lbl.value = tokens.Count;
                    }
                    LabelsToMark.Clear();
                    List<TokenItem> Tokenlist = new List<TokenItem>();
                    for (int j = 0; j < t.Count; j++)
                    {
                        Tokenlist.Add(new TokenItem(t[j], i + 1));
                    }
                    tokens.Add(Tokenlist);
                }
            }

            //add labels to pmap
            foreach (Label l in Labels)
            {
                if (!l.IsValid)
                    ReportError(l.line, "Invalid label \"" + l.name + "\" defined in line " + l.line + ", probably not marked !");
                else
                    try
                    {
                        pmap.Add(l.name, new Parameter(ParameterType.Label, l.value));
                    }
                    catch (Exception ex)
                    {
                        ReportError(l.line, "label \"" + l.name + "\" defined in line " + l.line + " cannot be created.Probably it already exists ! \nError:" + ex.Message);
                    }
            }

            for (int cc = 0; cc < 3 && ErrorCount == 0; cc++)
            {
                OutputCode.Clear();
                for (int i = 0; i < tokens.Count; i++)
                {
                    List<Parameter> param = new List<Parameter>();
                    foreach (Label l in Labels)
                    {
                        if (l.value == i)
                        {
                            Parameter p = pmap[l.name];
                            p.payload = OutputCode.Count;
                            pmap[l.name] = p;
                        }
                    }
                    for (int j = 1; j < tokens[i].Count; j++)
                    {
                        try
                        {
                            param.Add(pmap[tokens[i][j].Value]);
                        }
                        catch (Exception ex)
                        {
                            string error = ex.Message;
                            if (ex is KeyNotFoundException)
                                error = "Token unknown !";
                            ReportError(tokens[i][j].line, "Error from line " + tokens[i][j].line + " @ token " + tokens[i][j].Value + ": \n" + error);
                        }
                    }
                    foreach (Opcode op in Ops)
                    {
                        if (op.Keyword == tokens[i][0].Value)
                        {
                            try
                            {
                                op.Parse(param);
                                //OutputCode.Add();
                            }
                            catch (Exception ex)
                            {
                                ReportError(tokens[i][0].line, "Error from line " + tokens[i][0].line + " @ token " + tokens[i][0].Value + ": \n" + ex.Message);
                            }
                            goto next_line;
                        }
                    }

                    ReportError(tokens[i][0].line, "Error from line " + tokens[i][0].line + " @ token " + tokens[i][0].Value + ": \n" + "Unknown opcode");
                next_line:
                    ;
                }
            }

            OnCompileEnd(this);
            ReportError(-1, "Generated " + OutputCode.Count + " bytes !");
            string mif =
@"--Generated by tsc8 IDE
WIDTH=8;
DEPTH=256;

ADDRESS_RADIX=UNS;
DATA_RADIX=UNS;

CONTENT BEGIN
";
            for (int i = 0; i < OutputCode.Count; i++)
            {
                byte b = OutputCode[i];
                mif += i.ToString() + " : " + b.ToString() + "; -- " + Emu.DissasmOpcode(b) + "\n";
            }
            mif += "END;";

            BinaryCode = OutputCode.ToArray();
            MifFile = mif;

            return ErrorCount == 0;
        }
    }

    class Emu
    {
        public byte[] rom;
        public byte[] reg;
        public byte pc;
        public bool[][] vram;
        public byte[] code;
        public bool KEY_1;
        public bool KEY_2;

        public Emu() { Init(null); }

        public void Init(byte[] program)
        {
            code = program;
            rom = new byte[256];
            reg = new byte[16];
            Random x = new Random();
            for (int i = 0; i < 16; i++)
                reg[i] = (byte)x.Next();
            vram = new bool[32][];
            for (int i = 0; i < 32; i++)
                vram[i] = new bool[64];
            pc = 0;
            KEY_1 = false;
            KEY_2 = false;
            if (code != null)
            {
                for (int i = 0; i < Math.Min(code.Length, rom.Length); i++)
                {
                    rom[i] = code[i];
                }
            }
        }

        public void Reset()
        {
            Init(code);
        }

        public static string DissasmOpcode(byte op)
        {
            if (0 == (op & 0x80))
            {
                return "mov r0," + (op & 127);
            }
            else
            {
                int rn = op & 15;
                switch ((Opcodes)(op - rn))
                {
                    case Opcodes.mfr0:
                        return "mov r" + rn + ",r0";

                    case Opcodes.mtr0:
                        return "mov r0,r" + rn;

                    case Opcodes.add:
                        return "add r0,r" + rn;

                    case Opcodes.neg:
                        return "neg r0,r" + rn;

                    case Opcodes.draw:
                        return "draw rx,ry,r" + rn;

                    case Opcodes.vsync:
                        switch (rn)
                        {
                            case (int)Opcodes.vsync & 15:
                                return "vsync";

                            case (int)Opcodes.key1 & 15:
                                return "key1 r0";

                            case (int)Opcodes.key2 & 15:
                                return "key2 r0";

                            case (int)Opcodes.jr0 & 15:
                                return "jmp r0";

                            default:
                                return "nop ; 0x" + Convert.ToString(op, 16);
                        }

                    case Opcodes.jz:
                        return "jz r" + rn + ",r0";


                    case Opcodes.jgtz:
                        return "jgtz r" + rn + ",r0";

                    default:
                        return "nop ; 0x" + Convert.ToString(op, 16);
                }
            }
        }

        public bool ExecuteOpcode()
        {
            byte op = rom[pc++];
            if (0 == (op & 0x80))
            {
                reg[0] = (byte)(op & 0x7f);
            }
            else
            {
                int rn = op & 15;
                switch ((Opcodes)(op - rn))
                {
                    case Opcodes.mfr0:
                        reg[rn] = reg[0];
                        break;

                    case Opcodes.mtr0:
                        reg[0] = reg[rn];
                        break;

                    case Opcodes.add:
                        reg[0] += reg[rn];
                        break;

                    case Opcodes.neg:
                        reg[0] = (byte)-reg[rn];
                        break;

                    case Opcodes.draw:
                        bool old = vram[reg[7] & 31][reg[6] & 63];
                        vram[reg[7] & 31][reg[6] & 63] = (reg[rn] & 1) != 0;
                        reg[0] = (byte)(old ? 1 : 0);
                        break;

                    case Opcodes.vsync:
                        switch (rn)
                        {
                            case (int)Opcodes.vsync & 15:
                                return true;

                            case (int)Opcodes.key1 & 15:
                                reg[0] = (byte)(KEY_1 ? 1 : 0);
                                break;

                            case (int)Opcodes.key2 & 15:
                                reg[0] = (byte)(KEY_2 ? 1 : 0);
                                break;

                            case (int)Opcodes.jr0 & 15:
                                pc = reg[0];
                                break;
                            default:
                                //do nothing!
                                break;
                        }
                        break;


                    case Opcodes.jz:
                        if (reg[rn] == 0)
                            pc = reg[0];
                        break;

                    case Opcodes.jgtz:
                        if ((sbyte)reg[rn] > 0)
                            pc = reg[0];
                        break;

                    default:
                        //do nothing !
                        break;
                }
            }

            return false;
        }

        public bool ExecuteFrame()
        {
            int i = 10000;
            try
            {
                while (!ExecuteOpcode() && i > 1) i--;
            }
            catch (Exception)
            {
                return false;
            }

            return i > 1;
        }
    }
}
