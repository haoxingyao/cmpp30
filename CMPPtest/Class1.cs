using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cmpp30;

namespace CMPPtest
{
    public class Class1
    {
        static int temUp;
        public static void ReciveMessage(Cmpp30 cmpp, CMPP_DELIVER message)
        {
            Console.WriteLine(++temUp + string.Format("收到{0}->{1}:{2}", message.Src_terminal_Id, message.Dest_Id, message.ReadMessage()));
        }
        static int tem;
        public static void StateReport(Cmpp30 cmpp, CMPP_DELIVER_Msg_Content content)
        {
            Console.WriteLine(++tem + " 收到状态回报:" + content.State + " tel:" + content.Dest_terminal_Id);

            ulong mongth = content.Msg_Id >> 60;
            ulong day = (content.Msg_Id & 0x0f80000000000000ul) >> 55;
            ulong hour = (content.Msg_Id & 0x007C000000000000ul) >> 50;

            Console.WriteLine("content msgId :" + content.Msg_Id + " " + mongth + "-" + day + " " + hour);
        }

        public static void Main(string[] args)
        {
            var appSettings = System.Configuration.ConfigurationSettings.AppSettings;
            string serviceId = appSettings["serviceId"];
            string sp_id = appSettings["spId"];
            string spNumber = appSettings["spNumber"];
            string ip = appSettings["ip"];
            int port = int.Parse(appSettings["port"]);
            string pwd = appSettings["pwd"];
            int timeOut = 30;
#if DEBUG
            serviceId = "123";
            sp_id = "901234";
            spNumber = "01850";
            ip = "127.0.0.1";
            port = 7891;
            pwd = "1234";
            timeOut = 6;
#endif

            string tel = appSettings["tel"];
            string content = appSettings["content"];

            var writeLog = new Action<string>(x =>
            {
                Console.WriteLine(x);
            });

            Cmpp30 cmpp = new Cmpp30(ip, port, sp_id, pwd, serviceId, spNumber, "", writeLog, timeOut);
            cmpp.MessageRecive = ReciveMessage;
            cmpp.StateReport = StateReport;
            Console.WriteLine("connect: " + ip);
            Console.Write("input command:");
            Console.WriteLine("start, send, stop");
            while (true)
            {
                string action = Console.ReadLine();
                CMPP_SUBMIT_RESP resp;
                switch (action)
                {
                    case "start": cmpp.Start(); break;
                    case "send":
                        var result = cmpp.SendMsg(tel, content, out resp);
                        if (result != 0)
                        {
                            Console.WriteLine(result.ToString());
                        }
                        else
                        {
                            Console.WriteLine(resp.Result + "" + resp.Msg_Id);
                        }
                        break;
                    case "stop": cmpp.Stop(); break;
                    //case "send99":
                    //    for (int i = 0; i < 99; i++)
                    //    {
                    //        var resul = client.Submit(new CMPP_SUBMIT(serviceId, sp_id, spNumber, tel, content), out resp);
                    //        if (resul.ErrorCode != 0)
                    //        {
                    //            Console.WriteLine("main:" + resul.ErrorCode.ToString());
                    //        }
                    //        else
                    //        {
                    //            Console.WriteLine((i + 1) + "  " + (resp as CMPP_SUBMIT_RESP).Result);
                    //        }
                    //        System.Threading.Thread.Sleep(10);
                    //    }
                    //    break;
                    default: break;
                }
            }
        }
    }
}
