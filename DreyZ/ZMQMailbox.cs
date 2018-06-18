using NetMQ.Sockets;

using NetMQ;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace DreyZ
{
    public class ZMQMailbox
    {
        public enum MachineState
        {
            Disconnected,
            Connected,
            Registered
        }

        public enum MessageType
        {
            Connect = 1,
            Heartbeat,
            Data,
            Status,
            Universe,
            Debug

        }

        public enum DebugMessageType
        {
            GetProgram,
            StepInto,
            Run,
            EmulateResponse
        }


        public class DataEventArgs : EventArgs
        {
            public string Data;
        }

        public class GetProgramEventArgs : DataEventArgs
        {
            public DreyProgram Program;
        }
        public class AnnounceEventArgs : DataEventArgs
        {
            public GameState State;
        }
        public delegate void DataArivedEventHandler(DataEventArgs args);
        public event DataArivedEventHandler DataArrived;

        private MachineState _state = MachineState.Disconnected;
        private string _address;

        Thread _worker = null;
        DealerSocket _socket = null;
        DateTime _lastHeart = DateTime.Now;
        private bool _finished;

        private int _outgoingMessageId = 0;
        
        public GameState GameState { get; private set; }

        public ZMQMailbox()
        {
            _worker = new Thread(new ThreadStart(Worker));
            _worker.IsBackground = true;
            _worker.Name = "DreyZThread";
            _worker.Start();
            _socket = new DealerSocket();
            GameState = new GameState();
        }

        public void Worker()
        {
            while (!_finished)
            {
                switch (_state)
                {
                    case MachineState.Disconnected:

                        break;
                    case MachineState.Connected:
                        Register();
                        break;
                    case MachineState.Registered:
                        MessagePoll();
                        Heartbeat();

                        break;
                    default:
                        break;
                }
                Thread.Sleep(10);
            }
        }

        public void Register()
        {
            NetMQ.Msg msg = new NetMQ.Msg();
            msg.InitGC(new byte[] { 1 }, 1);
            _socket.TrySend(ref msg, TimeSpan.FromMilliseconds(100), false);
            _state = MachineState.Registered;
#if DEBUG
            GetProgramData();
#endif
        }

        public void Heartbeat()
        {
            if (DateTime.Now - _lastHeart > TimeSpan.FromMilliseconds(100))
            {
                NetMQ.Msg msg = new NetMQ.Msg();
                msg.InitGC(new byte[] { 2 }, 1);
                _socket.TrySend(ref msg, TimeSpan.FromMilliseconds(100), false);
                _lastHeart = DateTime.Now;
            }
        }
#if DEBUG
        private string CreateDebugMessage(DebugMessageType type, string extra = "")
        {
            switch (type)
            {
                case DebugMessageType.GetProgram:
                    return string.Format("{{\"id\":{0},\"type\":\"get-program\"}}", _outgoingMessageId++);
                case DebugMessageType.StepInto:
                    return string.Format("{{\"id\":{0},\"type\":\"step-into\"}}", _outgoingMessageId++);
                case DebugMessageType.Run:
                    return string.Format("{{\"id\":{0},\"type\":\"run\"}}", _outgoingMessageId++);
                case DebugMessageType.EmulateResponse:
                    return string.Format("{{\"id\":{0},\"type\":\"emulate-response\"{1} }}", _outgoingMessageId++, extra);
            }

            return "";
        }


        public void GetProgramData()
        {
            var msg = CreateDebugMessage(DebugMessageType.GetProgram);
            SendDebugMessage(msg);
        }

        public void EmulateClientReponse(string client, string response)
        {
            // this isn;t very nice. change this design later!
            var msg = CreateDebugMessage(DebugMessageType.EmulateResponse, string.Format(",\"clientid\":\"{0}\",\"key\":\"{1}\"",client,response));
            SendDebugMessage(msg);
        }

        public void StepInto()
        {
            var msg = CreateDebugMessage(DebugMessageType.StepInto);
            SendDebugMessage(msg);
        }

        public void Run()
        {
            var msg = CreateDebugMessage(DebugMessageType.Run);
            SendDebugMessage(msg);
        }

        private void SendDebugMessage(string json)
        {
            var msg = new NetMQ.NetMQMessage();
            msg.Append(new byte[] { 6 });
            msg.Append(json);
            _socket.SendMultipartMessage(msg);            
        }
#endif
        public void MessagePoll()
        {
            NetMQ.Msg msg = new NetMQ.Msg();
            msg.InitEmpty();
            if (_socket.TryReceive(ref msg, TimeSpan.FromMilliseconds(1)))
            {
                switch ((MessageType)msg.Data[0])
                {
                    case MessageType.Heartbeat: //heartbeat

                        break;

                    case MessageType.Debug: // debug
                        if (_socket.TryReceive(ref msg, TimeSpan.FromMilliseconds(1)))
                        {
                            string text = System.Text.Encoding.ASCII.GetString(msg.Data);
                            if(text == "{}")
                            {
                                break;
                            }
                            JObject jo = (JObject)JsonConvert.DeserializeObject(text);
                            var type = jo["type"].Value<string>();
                            if (type == "get-program")
                            {
                                GameState.Program = DreyProgram.FromJson(jo);
                                DataArrived?.Invoke(new GetProgramEventArgs() { Program = GameState.Program });
                            }
                            else if(type == "announce")
                            {
                                //Console.WriteLine(text);
                                var deserialized = JsonConvert.DeserializeObject<GameState>(text,  new ObjectTypeDeserializer());
                                GameState.Announce(deserialized);
                                DataArrived?.Invoke(new AnnounceEventArgs() { State= GameState});
                            }
                           
                        }
                        break;

                    case MessageType.Data: // data
                        if (_socket.TryReceive(ref msg, TimeSpan.FromMilliseconds(1)))
                        {
                            string text = System.Text.Encoding.ASCII.GetString(msg.Data);

                            var ser = new Newtonsoft.Json.JsonSerializer();
                            using (var reader = new JsonTextReader(new StringReader(text)))
                            {
                                PendingChoice choice = ser.Deserialize<PendingChoice>(reader);
                                GameState.PendingChoice = choice;
                            }

                            DataArrived?.Invoke(new DataEventArgs() { Data = text });
                        }

                        break;
                }


            }
        }

        public void SetIdentity(string identity)
        {
            if (_state == MachineState.Disconnected)
            {
                _socket.Options.Identity = System.Text.Encoding.ASCII.GetBytes(identity);
            }
        }

        public void Connect(string address)
        {
            try
            {
                _socket.Connect(address);
                _address = address;
                _state = MachineState.Connected;
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
            }
        }

        public void Shutdown()
        {
            if (_socket != null)
            {
                try
                {
                    _socket.Close();

                }
                catch (Exception)
                {


                }

                try
                {
                    _socket.Dispose();
                }
                catch (Exception)
                {


                }

            }
            _socket = null;
            _finished = true;
            _worker.Join();
            NetMQConfig.Cleanup(true);
        }

    }


}
