using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace DreyZ
{
    public interface IKey
    {
        int ID { get; set; }
    }

    
    public class PendingChoice
    {
        public string Header { get; set; }        
        public Dictionary<string,string> Choices { get; set; }
        public int FiberId { get; set; }
    }

    public abstract class ObjectType
    { }

    public class LocationReference : ObjectType
    {

    }

    public class Location : ObjectType
    {

    }

    public class Function : ObjectType
    {
        public int Address { get; set; }
    }

    public class Array : ObjectType
    {
        public List<ObjectType> Values { get; set; }
    }

    public class StringValue : ObjectType
    {
        public string Value { get; set; }
    }

    public class IntValue : ObjectType
    {
        public int Value { get; set; }
    }

    public class GameObject : ObjectType, IKey
    {
        public int ID { get; set; }
        public Dictionary<string, ObjectType> Props { get; set; }
    }

    public class GameObjectReference : ObjectType , IKey
    {
        public int ID { get; set; }       
    }

    public class ObjectTypeDeserializer : JsonCreationConverter<ObjectType>
    {
        protected override ObjectType Create(Type objectType, JObject jsonObject)
        {
            var type = jsonObject["type"].Value<string>();
            switch (type)
            {
                case "function": return new Function();
                case "go": return new GameObjectReference();
                case "array": return new Array();
                case "int": return new IntValue();
                case "string": return new StringValue();
            }
            return null;
        }
    }

    public class Universe
    {
        public Dictionary<int, GameObject> GameObjects { get; set; }
        public Dictionary<int, Location> Locations { get; set; }
        public Dictionary<int, LocationReference> LocationReferences { get; set; }

    }

    public class Instruction
    {
        public int Address { get; set; }
        public string HexAddress { get; set; }
        public string Opcode { get; set; }
        public string Operand { get; set; }
    }

    public class Scope 
    {
        public Dictionary<string, ObjectType> Locals { get; set; }
        public int Is_Fiber { get; set; }
        public int Return_Address { get; set; }
    }


    public class ExecutionContext :  IKey
    {
        public int ID { get; set; }
        public int PC { get; set; }
        public List<Scope> Scopes { get; set; }
        public List<ObjectType> EvalStack { get; set; }
    }

    public class Fiber :  IKey
    {
        public int ID { get; set; }
        public List<ExecutionContext> Exec_Contexts { get; set; }
        public int Response_Type { get; set; }
        public string Waiting_Client { get; set; }
        public PendingChoice Waiting_Data { get; set; }        
    }

    public class MatchResults<T> where T : IKey
    {
        public class Pair { public T Old; public T New;  }
        public List<T> ToAdd { get; set; }
        public List<T> ToRemove { get; set; }
        public List<Pair> ToUpdate { get; set; }

        public static MatchResults<T> MatchData(List<T> oldItems, List<T> newItems) 
        {
            var newD = newItems.ToDictionary(x => x.ID);
            var oldD = oldItems.ToDictionary(x => x.ID);

            var toRemove = new List<T>();
            var toAdd = new List<T>();
            var toUpdate = new List<Pair>();

            foreach (var f in oldD)
            {
                if (!newD.ContainsKey(f.Key))
                {
                    toRemove.Add(f.Value);
                }
            }
            foreach (var f in newD)
            {
                if (!oldD.ContainsKey(f.Key))
                {
                    toAdd.Add(f.Value);
                }
                else
                {
                    toUpdate.Add(new Pair() { New = f.Value, Old = oldD[f.Key] });
                }
            }

            return new MatchResults<T>() { ToAdd = toAdd, ToRemove = toRemove, ToUpdate = toUpdate };
        }
    }

    

    public class DreyProgram
    {
        private class Disassembler
        {
            private byte[] _bytes;
            private int _index = 0;
            private Dictionary<int, string> _strings;
            private Dictionary<int, OpcodeData> _opcodes;
            public Disassembler(byte[] bytes, Dictionary<int, OpcodeData> opcodes, Dictionary<int, string> strings)
            {
                _bytes = bytes;
                _strings = strings;
                _opcodes = opcodes;
            }

            private int ReadByte()
            {
                var b = _bytes[_index];
                _index++;
                return Convert.ToInt32(b);
            }

            private int ReadInt()
            {

                var val = 0;
                val = ReadByte();
                val = (val << 8) | ReadByte();
                val = (val << 8) | ReadByte();
                val = (val << 8) | ReadByte();
                return val;
            }

            private string GetString()
            {
                var i = ReadInt();
                return _strings[i];
            }

            public List<Instruction> Disassemble()
            {
                var ret = new List<Instruction>();
                while (_index < _bytes.Length)
                {
                    var b = ReadByte();
                    var op = _opcodes[b];
                    var hexLoc = String.Format("{0:X}", _index);
                    if (op.Extended)
                    {
                        switch (op.Opcode)
                        {
                            case "stvar":
                            case "p_stvar":
                            case "ldvals":
                            case "ldvar":
                                ret.Add(new Instruction() { Address = _index, HexAddress = hexLoc, Opcode = op.Opcode, Operand = GetString() });
                                break;
                            default:
                                ret.Add(new Instruction() { Address = _index, HexAddress = hexLoc, Opcode = op.Opcode, Operand = ReadInt().ToString() });
                                break;
                        }
                    }
                    else
                    {
                        ret.Add(new Instruction() { Address = _index, HexAddress = hexLoc, Opcode = op.Opcode, Operand = "" });
                    }
                }
                return ret;
            }

        }
        private class OpcodeData { public string Opcode; public bool Extended; }
        public List<Instruction> ByteCode;
        public Dictionary<int, string> StringTable { get; set; }

        public static DreyProgram FromJson(JObject json)
        {
            var strings =

                    json["strings"]
                    .Cast<JProperty>()
                    .ToDictionary(x => Int32.Parse(x.Name), x => x.Value.Value<string>());


            var bytes = json["program"].Values<byte>().ToArray();

            var opcodes =
                json["opcodes"]
                .Cast<JProperty>()
                .ToDictionary(
                    x => x.Value["code"].Value<int>(),
                    x => new OpcodeData() { Opcode = x.Name, Extended = x.Value["extended"].Value<int>() == 1 });

            var diss = new Disassembler(bytes, opcodes, strings);

            var instructions = diss.Disassemble();

            var prog = new DreyProgram()
            {
                ByteCode = instructions,
                StringTable = strings
            };

            return prog;
        }
    }

    public class ExecDetails
    {
        public int fiberid;
        public int ecid;
    }

    public class GameState
    {
        public object SyncLock = new object();
        public PendingChoice PendingChoice;
        public Universe Universe;

        // debug only
        public DreyProgram Program;
        public List<Fiber> Fibers { get; set; }
        public ExecDetails ExecDetails;
        public GameState()
        {
            Fibers = new List<Fiber>();

        }
        public void Announce(GameState newState)
        {
            lock (SyncLock)
            {
                var res = MatchResults<Fiber>.MatchData(Fibers, newState.Fibers);

                var keys = res.ToRemove.ToDictionary(x => x.ID);
                for (int i = Fibers.Count - 1; i >= 0; i--)
                {
                    if (keys.ContainsKey(Fibers[i].ID))
                    {
                        Fibers.RemoveAt(i);
                    }
                }

                foreach (var f in res.ToUpdate)
                {
                    UpdateFiber(f.Old, f.New);
                }

                foreach (var f in res.ToAdd)
                {
                    Fibers.Add(f);
                }

                this.ExecDetails = newState.ExecDetails;
            }
        }

        private void UpdateFiber(Fiber oldF, Fiber newF)
        {
            if(oldF.Waiting_Data==null)
            {
                oldF.Waiting_Data = new PendingChoice() { Choices = new Dictionary<string, string>(), Header = "" };
            }
            oldF.Response_Type = newF.Response_Type;
            oldF.Waiting_Client = newF.Waiting_Client;
            
            if(newF.Waiting_Data == null)
            {
                oldF.Waiting_Data.Choices.Clear();
                oldF.Waiting_Data.Header = "";
            }
            else
            {
                oldF.Waiting_Data.FiberId = newF.Waiting_Data.FiberId;
                oldF.Waiting_Data.Header = newF.Waiting_Data.Header;
                oldF.Waiting_Data.Choices.Clear();
                foreach(var c in newF.Waiting_Data.Choices)
                {
                    oldF.Waiting_Data.Choices.Add(c.Key, c.Value);
                }

            }
            
            var res = MatchResults<ExecutionContext>.MatchData(oldF.Exec_Contexts, newF.Exec_Contexts);

            var keys = res.ToRemove.ToDictionary(x => x.ID);
            for (int i = oldF.Exec_Contexts.Count - 1; i >= 0; i--)
            {
                if (keys.ContainsKey(oldF.Exec_Contexts[i].ID))
                {
                    oldF.Exec_Contexts.RemoveAt(i);
                }
            }

            foreach (var f in res.ToUpdate)
            {
                UpdateExecContext(f.Old, f.New);
            }

            foreach (var f in res.ToAdd)
            {
                oldF.Exec_Contexts.Add(f);
            }
        }

        private void UpdateExecContext(ExecutionContext old, ExecutionContext @new)
        {
            old.PC = @new.PC;

            
            // eval stack

            old.EvalStack.Clear();
            foreach(var item in @new.EvalStack)
            {
                old.EvalStack.Add(item);
            }
            
            //scopes 



            if (old.Scopes.Count == @new.Scopes.Count)
            {
                for (int i = 0; i < old.Scopes.Count; i++)
                {
                    Scope s1 = old.Scopes[i];
                    Scope s2 = @new.Scopes[i];
                    s1.Is_Fiber = s2.Is_Fiber;
                    UpdateLocalsDict(s1.Locals, s2.Locals);
                    s1.Return_Address = s2.Return_Address;
                }
            }
            else if(old.Scopes.Count > @new.Scopes.Count)
            {
                for (int i = old.Scopes.Count - 1; i >= 0; i--)                
                {
                    if (i < @new.Scopes.Count)
                    {
                        Scope s1 = old.Scopes[i];
                        Scope s2 = @new.Scopes[i];
                        s1.Is_Fiber = s2.Is_Fiber;
                        UpdateLocalsDict(s1.Locals, s2.Locals);
                        s1.Return_Address = s2.Return_Address;
                    }
                    else
                    {
                        old.Scopes.RemoveAt(i);
                    }
                }
            }
            else
            {
                for (int i = 0; i < @new.Scopes.Count; i++)
                {
                    if (i < old.Scopes.Count)
                    {
                        Scope s1 = old.Scopes[i];
                        Scope s2 = @new.Scopes[i];
                        s1.Is_Fiber = s2.Is_Fiber;
                        UpdateLocalsDict(s1.Locals,s2.Locals);
                        s1.Return_Address = s2.Return_Address;
                    }
                    else
                    {
                        old.Scopes.Add(@new.Scopes[i]);
                    }

                }
            }
        }

        private void UpdateLocalsDict(Dictionary<string, ObjectType> old, Dictionary<string, ObjectType> @new)
        {
            foreach(var d in @new)
            {
                if(old.ContainsKey(d.Key))
                {
                    old[d.Key] = d.Value;
                }
                else
                {
                    old.Add(d.Key, d.Value);
                }
            }
            var toRemove = new List<string>();
            foreach(var d in old)
            {
                if(!@new.ContainsKey(d.Key))
                {
                    toRemove.Add(d.Key);
                }
            }
            foreach(var k in toRemove)
            {
                old.Remove(k);
            }
        }
    }



}
