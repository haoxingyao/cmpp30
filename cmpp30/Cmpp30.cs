using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace cmpp30
{
    public class Cmpp30
    {
        private CMPP30Client clien;

        /// <summary>
        /// 远程主机地址
        /// </summary>
        private readonly string remoteAddress;

        /// <summary>
        /// 远程主机端口
        /// </summary>
        private readonly int remotePort;

        /// <summary>
        /// 登录口令
        /// </summary>
        private readonly string loginPassword;

        /// <summary>
        /// 接入号码
        /// </summary>
        private readonly string spPhoneNumber;

        /// <summary>
        /// 企业代码
        /// </summary>
        private readonly string spId;

        /// <summary>
        /// 业务代码
        /// </summary>
        private readonly string serviceId;
        /// <summary>
        /// 运行商签名
        /// </summary>
        private string operatorSign = "";

        /// <summary>
        /// 日志输出
        /// </summary>
        public Action<string> WriteLog;
        /// <summary>
        /// 状态上报
        /// </summary>
        public Action<Cmpp30, CMPP_DELIVER_Msg_Content> StateReport;
        /// <summary>
        /// 上行消息
        /// </summary>
        public Action<Cmpp30, CMPP_DELIVER> MessageRecive;
        private void ClientStateReport(CMPP30Client clien, CMPP_DELIVER_Msg_Content content)
        {
            if (this.StateReport != null)
            {
                StateReport(this, content);
            }
        }
        private void ClientMessageRecive(CMPP30Client clien, CMPP_DELIVER deliver)
        {
            if (this.MessageRecive != null)
            {
                MessageRecive(this, deliver);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="sp_id"></param>
        /// <param name="pwd"></param>
        /// <param name="service_Id"></param>
        /// <param name="spNumber"></param>
        /// <param name="log"></param>
        public Cmpp30(string ip, int port, string sp_id, string pwd, string service_Id, string spNumber, string sign, Action<string> log, int timeOutSecond, int activeTestInterval)
        {
            WriteLog = new Action<string>((x) =>
            {
                if (log != null)
                {
                    log(string.Format("CMPP30({0})->{1}", spNumber, x));
                }
            });

            remoteAddress = ip;
            remotePort = port;
            spId = sp_id;
            loginPassword = pwd;
            serviceId = service_Id;
            spPhoneNumber = spNumber;

            if (sign != null)
            {
                operatorSign = sign;
            }

            //client
            clien = new CMPP30Client(ip, port, sp_id, pwd);
            clien.WriteLog = WriteLog;
            clien.StateReport = ClientStateReport;
            clien.UserMessageRecive = ClientMessageRecive;
            clien.MaxTimeOut = timeOutSecond;
            clien.ActiveTestInterval = activeTestInterval;
        }
        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            if (clien != null)
            {
                clien.Start();
                bool isBind = clien.Bind();

                WriteLog("bind ：" + isBind.ToString());
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            if (clien != null)
            {
                clien.Stop();
            }
        }
        /// <summary>
        /// 短信发送
        /// </summary>
        /// <param name="tel"></param>
        /// <param name="content"></param>
        /// <param name="res"></param>
        /// <returns></returns>
        public LocalErrCode SendMsg(string tel, string content, out CMPP_SUBMIT_RESP res, string spNumber = null)
        {
            if (spNumber == null)
            {
                spNumber = spPhoneNumber;
            }

            CMPP_SUBMIT[] subMsg = CreateSubmitMsg(serviceId, spId, spNumber, tel, content);
            //服务器响应
            CMPPMsgBody_Base resp = null;
            LocalErrCode localRes = LocalErrCode.组件未启动;
            res = null;
            for (int i = 0; i < subMsg.Length; i++)
            {
                localRes = clien.Submit(subMsg[i], out resp);
                res = resp as CMPP_SUBMIT_RESP;
                if (localRes != LocalErrCode.成功 || res.Result != DeliverResult.正确)
                {
                    break;
                }

            }
            return localRes;
        }
        /// <summary>
        /// 链路检测
        /// </summary>
        /// <returns></returns>
        public LocalErrCode ActiveTest()
        {
            return clien.Submit(new CMPP_ACTIVE_TEST(), true);
        }

        //长短信分割字数
        private readonly int LongMsgSplitLength = 67;
        /// <summary>
        /// 长短信分割内容 （第一条短信要计算签名）
        /// </summary>
        /// <param name="index">第几条 0开始</param>
        /// <param name="content"></param>
        /// <returns></returns>
        private string SplitContent(int index, string content)
        {
            int firstSpliteCount = LongMsgSplitLength - operatorSign.Length;
            if (index == 0)
            {
                return content.Substring(0, firstSpliteCount);
            }
            else
            {
                int spliteIndex = firstSpliteCount + ((index - 1) * LongMsgSplitLength);
                int spliteCount = Math.Min(LongMsgSplitLength, content.Length - spliteIndex);
                return content.Substring(spliteIndex, spliteCount);
            }
        }
        /// <summary>
        /// 创建短信发送包
        /// </summary>
        /// <param name="service_Id"></param>
        /// <param name="sp_Id"></param>
        /// <param name="sp_Number"></param>
        /// <param name="tel"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        private CMPP_SUBMIT[] CreateSubmitMsg(string service_Id, string sp_Id, string sp_Number, string tel, string content)
        {
            //短信字符数
            int msgLen = (string.IsNullOrEmpty(content) ? 0 : content.Length) + operatorSign.Length;
            CMPP_SUBMIT[] temp;
            if (msgLen > 70)
            {
                //长短信唯一码 方便手机合并
                byte longMsgId = LongMsgIdHelper.GetOne();
                //短信分割总条数
                double msgCount = Math.Ceiling((double)msgLen / (double)LongMsgSplitLength);
                //
                temp = new CMPP_SUBMIT[(int)msgCount];
                for (int i = 0; i < msgCount; i++)
                {
                    temp[i] = new CMPP_SUBMIT(longMsgId, (byte)msgCount, (byte)(i + 1), service_Id, sp_Id, sp_Number, tel, SplitContent(i, content));
                }
            }
            else
            {
                temp = new CMPP_SUBMIT[] { new CMPP_SUBMIT(service_Id, sp_Id, sp_Number, tel, content) };
            }

            return temp;
        }

    }
}
