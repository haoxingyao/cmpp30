using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//
namespace cmpp30
{
    /// <summary>
    /// 
    /// </summary>
    public class SequenceIdHelper
    {
        static volatile uint SequenceId = 1;

        public static uint GetOne()
        {
            if (SequenceId == uint.MaxValue)
            {
                return 1;
            }
            return SequenceId++;
        }
    }
    /// <summary>
    /// 长短信头唯一标示
    /// </summary>
    public class LongMsgIdHelper
    {
        static volatile byte Id = 0;

        public static byte GetOne()
        {
            if (Id == byte.MaxValue)
            {
                return 0;
            }
            return Id++;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public enum Command_Id : uint
    {
        CMPP_CONNECT = 0x00000001,
        CMPP_CONNECT_RESP = 0x80000001,
        CMPP_TERMINATE = 0x00000002,
        CMPP_TERMINATERESP = 0x80000002,
        CMPP_SUBMIT = 0x00000004,
        CMPP_SUBMIT_RESP = 0x80000004,
        CMPP_DELIVER = 0x00000005,
        CMPP_DELIVER_RESP = 0x80000005,
        CMPP_QUERY = 0x00000006,
        CMPP_QUERY_RESP = 0x80000006,
        CMPP_CANCEL = 0x00000007,
        CMPP_CANCEL_RESP = 0x80000007,
        CMPP_ACTIVE_TEST = 0x00000008,
        CMPP_ACTIVE_TEST_RESP = 0x80000008,
        CMPP_FWD = 0x00000009,
        CMPP_FWD_RESP = 0x80000009,
        CMPP_MT_ROUTE = 0x00000010,
        CMPP_MT_ROUTE_RESP = 0x80000010,
        CMPP_MO_ROUTE = 0x00000011,
        CMPP_MO_ROUTE_RESP = 0x80000011,
        CMPP_GET_MT_ROUTE = 0x00000012,
        CMPP_GET_MT_ROUTE_RESP = 0x80000012,
        CMPP_MT_ROUTE_UPDATE = 0x00000013,
        CMPP_MT_ROUTE_UPDATE_RESP = 0x80000013,
        CMPP_MO_ROUTE_UPDATE = 0x00000014,
        CMPP_MO_ROUTE_UPDATE_RESP = 0x80000014,
        CMPP_PUSH_MT_ROUTE_UPDATE = 0x00000015,
        CMPP_PUSH_MT_ROUTE_UPDATE_RESP = 0x80000015,
        CMPP_PUSH_MO_ROUTE_UPDATE = 0x00000016,
        CMPP_PUSH_MO_ROUTE_UPDATE_RESP = 0x80000016,
        CMPP_GET_MO_ROUTE = 0x00000017,
        CMPP_GET_MO_ROUTE_RESP = 0x80000017
    }
    /// <summary>
    /// 
    /// </summary>
    public enum DeliverResult : uint
    {
        正确 = 0,
        消息结构错 = 1,
        命令字错 = 2,
        消息序号重复 = 3,
        消息长度错 = 4,
        资费错 = 5,
        超过最大信息长 = 6,
        业务代码错 = 7,
        流量控制错 = 8,
        本网关不负责服务此计费号码 = 9,
        Src_Id错误 = 10,
        Msg_src错误 = 11,
        Fee_terminal_Id错误 = 12,
        Dest_terminal_Id错误 = 13
    }
    /// <summary>
    /// 消息头 长度12
    /// </summary>
    public class CMPPMsgHeader
    {
        public const int HeadLength = 12;
        /// <summary>
        /// 包长度
        /// </summary>
        public uint Total_Length { get; set; }
        /// <summary>
        /// 命令id
        /// </summary>
        public Command_Id Command_Id { get; set; }
        /// <summary>
        /// 流水
        /// </summary>
        public uint Sequence_Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="totalLength"></param>
        /// <param name="commandId"></param>
        /// <param name="sequenceId"></param>
        public CMPPMsgHeader(uint bodyLength, Command_Id commandId)
        {
            Total_Length = HeadLength + bodyLength;
            Command_Id = commandId;
            Sequence_Id = SequenceIdHelper.GetOne();
        }
        public CMPPMsgHeader(uint bodyLength, Command_Id commandId, uint sequenceId)
        {
            Total_Length = HeadLength + bodyLength;
            Command_Id = commandId;
            Sequence_Id = sequenceId;
        }

        private CMPPMsgHeader()
        {
        }

        public byte[] Write()
        {
            byte[] byts = new byte[12];

            Buffer.BlockCopy(BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Total_Length)), 0, byts, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseByteOrder.ReverseBytes((uint)Command_Id)), 0, byts, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Sequence_Id)), 0, byts, 8, 4);

            return byts;
        }

        public static CMPPMsgHeader Read(byte[] byts)
        {
            if (byts.Length >= 12)
            {
                CMPPMsgHeader head = new CMPPMsgHeader();
                head.Total_Length = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 0));
                head.Command_Id = (Command_Id)ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 4));
                head.Sequence_Id = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 8));

                return head;
            }
            return null;
        }
    }
    /// <summary>
    /// base message
    /// </summary>
    public abstract class CMPPMsgBody_Base
    {
        public CMPPMsgHeader MyHead { get; protected set; }

        public virtual uint BodyLength { get; protected set; }

        public virtual byte[] WriteBytes()
        {
            return MyHead.Write();
        }
        public virtual void ReadBytes(byte[] byts)
        {
            MyHead = CMPPMsgHeader.Read(byts);
        }
    }
}
/**
 * 双向包： SP在运行中既要根据自身情况发送也要响应服务端的请求
 *          拆除连接  CMPP_TERMINATE     (SP<->ISMG)
 *          链路检测  CMPP_ACTIVE_TEST   (SP<->ISMG)
 *          
 * 单向包：只存在 SP->ISMG 或 ISMG->SP 两种情况之一的包
**/
//请求
namespace cmpp30
{
    /// <summary>
    /// 连接包
    /// SP->ISMG
    /// </summary>
    public class CMPP_CONNECT : CMPPMsgBody_Base
    {
        #region 协议
        /// <summary>
        /// 源地址 spid  长度6
        /// </summary>
        public string SP_ID { get; set; }
        /// <summary>
        ///AuthenticatorSource：企业代码+9个二进制的0+passwd+timestamp，timestamp以字符串按格式MMDDHHMMSS表示，再用md5得到 16
        /// </summary>
        public string AuthenticatorSource { get; set; }
        /// <summary>
        /// 版本号   1（高位4bit表示主版本号,低位4bit表示次版本号） 3.0=48 (00110000)
        /// </summary>
        public readonly byte Version = 48;
        /// <summary>
        /// MMDDHHMMSS  4
        /// </summary>
        public uint Timestamp { get; set; }
        #endregion
        //
        private string _pwd;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="spId"></param>
        /// <param name="pwd"></param>
        public CMPP_CONNECT(string spId, string pwd)
        {
            BodyLength = 27;

            SP_ID = spId;
            _pwd = pwd;

            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_CONNECT);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override byte[] WriteBytes()
        {
            byte[] msgByts = new byte[MyHead.Total_Length];
            string strTimestamp = DateTime.Now.ToString("MMddHHmmss");
            Timestamp = UInt32.Parse(strTimestamp);
            //
            AuthenticatorSource = SP_ID + "\0\0\0\0\0\0\0\0\0" + _pwd + strTimestamp;
            //
            Tools.CopyAll(0, MyHead.Write(), ref msgByts);
            Tools.CopyAll(12, Tools.StringToBytes(SP_ID), ref msgByts, 6);
            Tools.CopyAll(18, MyMD5.GetMD5Byts_32(AuthenticatorSource), ref msgByts);
            msgByts[34] = Version;
            Tools.CopyAll(35, BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Timestamp)), ref msgByts);
            //
            return msgByts;
        }
    }
    /// <summary>
    /// 断开连接 
    /// 双向包
    /// </summary>
    public class CMPP_TERMINATE : CMPPMsgBody_Base
    {
        public CMPP_TERMINATE(uint sequenceId)
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_TERMINATE, sequenceId);
        }
        public CMPP_TERMINATE()
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_TERMINATE);
        }
    }
    /// <summary>
    /// 提交短信
    /// SP->ISMG
    /// </summary>
    public class CMPP_SUBMIT : CMPPMsgBody_Base
    {
        #region 协议内容 私有值为默认
        /// <summary>
        /// 信息标识。
        /// </summary>
        public UInt64 Msg_Id { get; set; }
        /// <summary>
        /// 相同Msg_Id的信息总条数，从1开始。
        /// </summary>
        byte Pk_total = 1;
        /// <summary>
        /// 相同Msg_Id的信息序号，从1开始
        /// </summary>
        byte Pk_number = 1;
        /// <summary>
        /// 是否要求返回状态确认报告：0：不需要；1：需要。
        /// </summary>
        byte Registered_Delivery = 1;
        /// <summary>
        /// 信息级别。
        /// </summary>
        byte Msg_level = 5;
        /// <summary>
        /// 10 长度
        /// 业务标识，是数字、字母和符号的组合。
        /// </summary>
        public string Service_Id { get; set; }
        /// <summary>
        /// 计费用户类型字段：
        ///0：对目的终端MSISDN计费；
        ///1：对源终端MSISDN计费；
        ///2：对SP计费；
        ///3：表示本字段无效，对谁计费参见Fee_terminal_Id字段。
        /// </summary>
        byte Fee_UserType = 0;
        /// <summary>
        /// 32 长度
        /// 被计费用户的号码，当Fee_UserType为3时该值有效，当Fee_UserType为0、1、2时该值无意义   
        /// </summary>
        string Fee_terminal_Id { get; set; }
        /// <summary>
        /// 被计费用户的号码类型，0：真实号码；1：伪码
        /// </summary>
        byte Fee_terminal_type = 0;
        /// <summary>
        /// GSM协议类型。
        /// </summary>
        byte TP_pId = 0;
        /// <summary>
        /// GSM协议类型
        /// </summary>
        byte TP_udhi = 0;
        /// <summary>
        /// 信息格式：
        ///0：ASCII串；
        ///3：短信写卡操作；
        ///4：二进制信息；
        ///8：UCS2编码；
        ///15：含GB汉字。。。。。。
        /// </summary>
        byte Msg_Fmt = 8;
        /// <summary>
        /// 6
        /// 信息内容来源(SP_Id)。
        /// </summary>
        public string Msg_src { get; set; }
        /// <summary>
        /// 2
        /// 资费类别： 
        ///01：对“计费用户号码”免费；
        ///02：对“计费用户号码”按条计信息费；
        ///03：对“计费用户号码”按包月收取信息费
        /// </summary>
        string FeeType = "01";
        /// <summary>
        /// 6
        /// 资费（以分为单位）。  
        /// </summary>
        string FeeCode = "000000";
        /// <summary>
        /// 17
        /// 存活有效期
        /// </summary>
        string ValId_Time = "";
        /// <summary>
        /// 17
        /// 定时发送时间
        /// </summary>
        string At_Time = "";
        /// <summary>
        /// 21
        /// 源号码。SP的服务代码或前缀为服务代码的长号码, 网关将该号码完整的填到SMPP协议Submit_SM消息相应的source_addr字段，该号码最终在用户手机上显示为短消息的主叫号码。
        /// </summary>
        public string Src_Id { get; set; }
        /// <summary>
        /// 接收信息的用户数量(小于100个用户)
        /// </summary>
        byte DestUsr_tl = 1;
        /// <summary>
        /// 32
        /// 接收短信的tel号码 后补\0
        /// </summary>
        public string Dest_terminal_Id { get; set; }
        /// <summary>
        /// 接收短信的用户的号码类型，0：真实号码；1：伪码。
        /// </summary>
        byte Dest_terminal_type = 0;
        /// <summary>
        /// 信息长度(Msg_Fmt值为0时：小于160个字节；其它<=140个字节)，取值大于或等于0
        /// </summary>
        public byte Msg_Length { get; set; }
        /// <summary>
        /// Msg_Length
        /// 信息内容。
        /// </summary>
        public string Msg_Content { get; set; }
        /// <summary>
        /// 20
        /// 点播业务使用的LinkID，非点播类业务的MT流程不使用该字段。
        /// </summary>
        string LinkID { get; set; }
        //以上协议 
        #endregion
        byte _longMsgId;
        bool _isLongMsg = false;
        byte[] _contentByts;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="service_Id">业务代码</param>
        /// <param name="sp_Id">sp编号</param>
        /// <param name="sp_Number">接入号</param>
        /// <param name="tel">电话</param>
        /// <param name="content">信息</param>
        public CMPP_SUBMIT(string service_Id, string sp_Id, string sp_Number, string tel, string content)
        {
            Service_Id = service_Id;
            Msg_src = sp_Id;
            Src_Id = sp_Number;
            Dest_terminal_Id = tel;
            Msg_Content = content;
            _contentByts = Tools.GetUSCBytes(content);
            Msg_Length = Convert.ToByte(_contentByts.Length);
            //
            BodyLength = 183U + Msg_Length;
            //
            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_SUBMIT);

        }
        //长短信
        public CMPP_SUBMIT(byte longMsgId, byte msgCount, byte msgOrder, string service_Id, string sp_Id, string sp_Number, string tel, string content)
            : this(service_Id, sp_Id, sp_Number, tel, content)
        {
            _longMsgId = longMsgId;
            _isLongMsg = true;
            TP_udhi = 1;
            Pk_total = msgCount;
            Pk_number = msgOrder;
            //
            BodyLength = 183U + Msg_Length + 6;
            MyHead.Total_Length = BodyLength + CMPPMsgHeader.HeadLength;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override byte[] WriteBytes()
        {
            byte[] msgByts = new byte[MyHead.Total_Length];

            //注释掉空值 无需赋值
            Tools.CopyAll(0, MyHead.Write(), ref msgByts);
            //Tools.CopyAll(12, BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Msg_Id)), ref msgByts);//msgId由网关生成
            Tools.CopyAll(20, BitConverter.GetBytes(Pk_total), ref msgByts);
            Tools.CopyAll(21, BitConverter.GetBytes(Pk_number), ref msgByts);
            Tools.CopyAll(22, BitConverter.GetBytes(Registered_Delivery), ref msgByts);
            Tools.CopyAll(23, BitConverter.GetBytes(Msg_level), ref msgByts);
            Tools.CopyAll(24, Tools.StringToBytes(Service_Id), ref msgByts, 10);
            Tools.CopyAll(34, BitConverter.GetBytes(Fee_UserType), ref msgByts);
            //CopyAll(35, StringToBytes(Fee_terminal_Id), ref msgByts);
            Tools.CopyAll(67, BitConverter.GetBytes(Fee_terminal_type), ref msgByts);
            Tools.CopyAll(68, BitConverter.GetBytes(TP_pId), ref msgByts);
            Tools.CopyAll(69, BitConverter.GetBytes(TP_udhi), ref msgByts);
            Tools.CopyAll(70, BitConverter.GetBytes(Msg_Fmt), ref msgByts);
            Tools.CopyAll(71, Tools.StringToBytes(Msg_src), ref msgByts, 6);
            Tools.CopyAll(77, Tools.StringToBytes(FeeType), ref msgByts, 2);
            Tools.CopyAll(79, Tools.StringToBytes(FeeCode), ref msgByts, 6);
            //CopyAll(85, StringToBytes(ValId_Time), ref msgByts);
            //CopyAll(102, StringToBytes(At_Time), ref msgByts);
            Tools.CopyAll(119, Tools.StringToBytes(Src_Id), ref msgByts, 21);
            Tools.CopyAll(140, BitConverter.GetBytes(DestUsr_tl), ref msgByts);
            Tools.CopyAll(141, Tools.StringToBytes(Dest_terminal_Id), ref msgByts, 32);
            Tools.CopyAll(173, BitConverter.GetBytes(Dest_terminal_type), ref msgByts);
            /**TP_udhi 协议
             * 6 位协议头格式：05 00 03 XX MM NN
                XX，这批短信的唯一标志，这个标志是否唯一并不是很重要。
                MM, 这批短信的数量
                NN, 这批短信的第几条
             * 
             * CMPP 协议发长短信：
              1. TP_udhi 设置为 0x01
              2. Msg_Content：按 TP_udhi 协议填写 6 字节或者 7 字节的 TP_udhi 协议头然后加上经过USC2 编码的消息内容。由 TP_udhi 协议头和消息内容体组成的 Msg_Content 总长度不能超过 140 个字节
              3. Msg_Fmt：设置为 0x08 UCS2 编码；
              4. Pk_total 和 Pk_number 可以不设置，如果要设置，就要分别跟 TP_udhi 的 MM 和 NN 字段一致
             * **/
            if (_isLongMsg)
            {
                byte[] longMsgHeadBytes = new byte[] { 05, 00, 03, _longMsgId, Pk_total, Pk_number };
                Tools.CopyAll(174, BitConverter.GetBytes(Msg_Length + 6), ref msgByts);
                Tools.CopyAll(175, longMsgHeadBytes, ref msgByts, 6);
                Tools.CopyAll(181, _contentByts, ref msgByts, Msg_Length);
            }
            else
            {
                Tools.CopyAll(174, BitConverter.GetBytes(Msg_Length), ref msgByts);
                Tools.CopyAll(175, _contentByts, ref msgByts, Msg_Length);
            }
            //CopyAll(175+Msg_Length, StringToBytes(LinkID), ref msgByts,20);

            return msgByts;
        }
    }
    /// <summary>
    /// 链路检测
    /// 双向包
    /// </summary>
    public class CMPP_ACTIVE_TEST : CMPPMsgBody_Base
    {
        public CMPP_ACTIVE_TEST(uint sequenceId)
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_ACTIVE_TEST, sequenceId);
        }
        public CMPP_ACTIVE_TEST()
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_ACTIVE_TEST);
        }

    }
    /// <summary>
    /// 上行 或 下行状态回报
    /// ISMG->SP
    /// </summary>
    public class CMPP_DELIVER : CMPPMsgBody_Base
    {
        #region 协议
        /// <summary>
        /// 
        /// </summary>
        public ulong Msg_Id { get; set; }
        /// <summary>
        /// 目的号码。SP的服务代码，或者是前缀为服务代码的长号码；该号码是手机用户短消息的被叫号码。
        /// </summary>
        public string Dest_Id { get; set; }
        /// <summary>
        /// 业务标识，是数字、字母和符号的组合。
        /// </summary>
        public string Service_Id { get; set; }
        /// <summary>
        /// GSM协议类型
        /// </summary>
        public byte TP_pid { get; set; }
        /// <summary>
        /// GSM协议类型
        /// </summary>
        public byte TP_udhi { get; set; }
        /// <summary>
        /// 信息格式：
        ///0：ASCII串；
        ///3：短信写卡操作；
        ///4：二进制信息；
        ///8：UCS2编码；
        ///15：含GB汉字。
        /// </summary>
        public byte Msg_Fmt { get; set; }
        /// <summary>
        /// 源终端MSISDN号码（状态报告时填为CMPP_SUBMIT消息的目的终端号码）
        /// </summary>
        public string Src_terminal_Id { get; set; }
        /// <summary>
        /// 源终端号码类型，0：真实号码；1：伪码。
        /// </summary>
        public byte Src_terminal_type { get; set; }
        /// <summary>
        /// 是否为状态报告：0：非状态报告；1：状态报告
        /// </summary>
        public byte Registered_Delivery { get; set; }
        /// <summary>
        /// 消息长度
        /// </summary>
        public byte Msg_Length { get; set; }
        /// <summary>
        /// 消息
        /// </summary>
        public byte[] Msg_Content { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string LinkID { get; set; }
        #endregion

        public CMPP_DELIVER(uint sequenceId)
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_DELIVER, sequenceId);
        }

        public override void ReadBytes(byte[] byts)
        {
            MyHead = CMPPMsgHeader.Read(byts);
            Msg_Id = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt64(byts, 12));
            Dest_Id = Tools.BytesToString(byts, 20, 21);
            Service_Id = Tools.BytesToString(byts, 41, 10);
            TP_pid = byts[51];
            TP_udhi = byts[52];
            Msg_Fmt = byts[53];
            Src_terminal_Id = Tools.BytesToString(byts, 54, 32);
            Src_terminal_type = byts[86];
            Registered_Delivery = byts[87];
            Msg_Length = byts[88];
            Msg_Content = new byte[Msg_Length];
            Buffer.BlockCopy(byts, 89, Msg_Content, 0, Msg_Length);
        }
        /// <summary>
        /// 上行短信直接获取内容
        /// </summary>
        /// <returns></returns>
        public string ReadMessage()
        {
            return Tools.decText(Msg_Content, Msg_Fmt);
        }
    }
}
//响应
namespace cmpp30
{
    /// <summary>
    /// 连接响应
    /// ISMG->SP
    /// </summary>
    public class CMPP_CONNECT_RESP : CMPPMsgBody_Base
    {
        /// <summary>
        /// 0：正确 1：消息结构错2：非法源地址3：认证错4：版本太高5~ ：其他错误
        /// </summary>
        public uint Status { get; set; }
        /// <summary>
        /// MD5（Status+AuthenticatorSource+shared secret），Shared secret 由中国移动与源地址实体事先商定，AuthenticatorSource为源地址实体发送给ISMG的对应消息CMPP_Connect中的值。认证出错时，此项为空
        /// </summary>
        public string AuthenticatorISMG { get; set; }
        /// <summary>
        /// 服务器支持的最高版本号，对于3.0的版本，高4bit为3，低4位为0  3.0(00110000 = 48)
        /// </summary>
        public byte Version { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CMPP_CONNECT_RESP(uint sequenceId)
        {
            BodyLength = 33;
            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_CONNECT_RESP, sequenceId);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="head"></param>
        /// <param name="byts"></param>
        public override void ReadBytes(byte[] byts)
        {
            MyHead = CMPPMsgHeader.Read(byts);
            this.Status = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 12));
            this.AuthenticatorISMG = BitConverter.ToString(byts, 16, 16).Replace("-", "");
            Version = byts[32];
        }
    }
    /// <summary>
    /// 断开响应
    /// 双向
    /// </summary>
    public class CMPP_TERMINATE_RESP : CMPPMsgBody_Base
    {
        public CMPP_TERMINATE_RESP(uint sequenceId)
        {
            MyHead = new CMPPMsgHeader(0, Command_Id.CMPP_TERMINATERESP, sequenceId);
        }
    }
    /// <summary>
    /// 提交相应
    /// ISMG->SP
    /// </summary>
    public class CMPP_SUBMIT_RESP : CMPPMsgBody_Base
    {
        /// <summary>
        /// 
        /// </summary>
        public ulong Msg_Id { get; set; }
        /// <summary>
        /// 0：正确；
        ///1：消息结构错；
        /// 2：命令字错；
        /// 3：消息序号重复；
        ///4：消息长度错；
        ///5：资费错；
        ///6：超过最大信息长；
        ///7：业务代码错；
        ///8：流量控制错；
        ///9：本网关不负责服务此计费号码；
        ///10：Src_Id错误；
        ///11：Msg_src错误；
        ///12：Fee_terminal_Id错误；
        ///13：Dest_terminal_Id错误；
        ///...
        /// </summary>
        public DeliverResult Result { get; set; }

        public CMPP_SUBMIT_RESP(uint sequenceId)
        {
            BodyLength = 12;
            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_SUBMIT_RESP, sequenceId);
        }

        public override void ReadBytes(byte[] byts)
        {
            MyHead = CMPPMsgHeader.Read(byts);
            Msg_Id = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt64(byts, 12));
            Result = (DeliverResult)ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 20));
        }
    }
    /// <summary>
    /// 状态上报 上行短信 响应
    /// SP->ISMG
    /// </summary>
    public class CMPP_DELIVER_RESP : CMPPMsgBody_Base
    {
        public ulong Msg_Id { get; set; }

        public uint Result { get; set; }

        public CMPP_DELIVER_RESP(uint sequenceId, ulong msgId, uint result)
        {
            BodyLength = 12;
            Msg_Id = msgId;
            Result = result;
            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_DELIVER_RESP, sequenceId);
        }

        public override byte[] WriteBytes()
        {
            byte[] msg = new byte[MyHead.Total_Length];
            Tools.CopyAll(0, MyHead.Write(), ref msg);
            Tools.CopyAll(12, BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Msg_Id)), ref msg);
            Tools.CopyAll(20, BitConverter.GetBytes(ReverseByteOrder.ReverseBytes(Result)), ref msg);

            return msg;
        }
    }
    /// <summary>
    /// 下行状态回报状态消息体
    /// </summary>
    public class CMPP_DELIVER_Msg_Content
    {
        /// <summary>
        /// 
        /// </summary>
        public ulong Msg_Id { get; set; }
        /// <summary>
        /// 短信的应答结果
        /// </summary>
        public string State { get; set; }
        /// <summary>
        /// YYMMDDHHMM（YY为年的后两位00-99，MM：01-12，DD：01-31，HH：00-23，MM：00-59）。
        /// </summary>
        public string Submit_time { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Done_time { get; set; }
        /// <summary>
        /// 目的终端MSISDN号码(SP发送CMPP_SUBMIT消息的目标终端)
        /// </summary>
        public string Dest_terminal_Id { get; set; }
        /// <summary>
        /// 取自SMSC发送状态报告的消息体中的消息标识
        /// </summary>
        public uint SMSC_sequence { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="byts"></param>
        public void ReadMessage(byte[] byts)
        {
            Msg_Id = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt64(byts, 0));
            State = Tools.BytesToString(byts, 8, 7);
            Submit_time = Tools.BytesToString(byts, 15, 10);
            Done_time = Tools.BytesToString(byts, 25, 10);
            Dest_terminal_Id = Tools.BytesToString(byts, 35, 32).TrimEnd('\0');
            SMSC_sequence = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(byts, 67));
        }
    }
    /// <summary>
    /// 链路检测响应
    /// 双向
    /// </summary>
    public class CMPP_ACTIVE_TEST_RESP : CMPPMsgBody_Base
    {
        /// <summary>
        /// 保留
        /// </summary>
        public byte Reserved { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequenceId"></param>
        public CMPP_ACTIVE_TEST_RESP(uint sequenceId)
        {
            BodyLength = 1;
            MyHead = new CMPPMsgHeader(BodyLength, Command_Id.CMPP_ACTIVE_TEST_RESP, sequenceId);
        }

        public override byte[] WriteBytes()
        {
            byte[] msg = new byte[MyHead.Total_Length];
            Tools.CopyAll(0, MyHead.Write(), ref msg);
            msg[12] = Reserved;
            return msg;
        }
    }
}
