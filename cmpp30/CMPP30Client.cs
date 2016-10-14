using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;

namespace cmpp30
{
    /// <summary>
    /// 
    /// </summary>
    internal class CMPP30Client
    {
        /// <summary>
        /// 连接SMP服务器对象
        /// </summary>
        private System.Net.Sockets.TcpClient client_sk;

        /// <summary>
        /// 远程主机地址
        /// </summary>
        private readonly string remoteAddress;

        /// <summary>
        /// 远程主机端口
        /// </summary>
        private readonly int remotePort;

        /// <summary>
        /// spID
        /// </summary>
        private readonly string spId;

        /// <summary>
        /// 登录口令
        /// </summary>
        private readonly string loginPassword;

        /// <summary>
        /// 命令队列
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<uint, CMPPMsgBody_Base> CmppCmdQueue = new System.Collections.Concurrent.ConcurrentDictionary<uint, CMPPMsgBody_Base>();

        /// <summary>
        /// 等待命令响应锁
        /// 方便无需响应的响应包发送
        /// </summary>
        private object waitRespLock = new object();

        //通道状态
        /// <summary>
        /// 通道访问冲突锁
        /// </summary>
        private object channelLockFlag = new object();

        /// <summary>
        /// false通道未建立 true通道已建立可以发送数据
        /// </summary>
        private volatile bool channelStateReady = false;
        /// <summary>
        /// 通道被构建的序列号，如果通道被重新绑定，则序列号递增，序列号大于1000则归零，
        /// 命令在下发时，记录当前序号，若序号发生改变命令未收到应答，则返回错误
        /// </summary>
        private volatile int channelSN = 0;
        /// <summary>
        /// 通道最后活动时间
        /// </summary>
        private DateTime channelLastUpdate = DateTime.Now;
        /// <summary>
        ///处理线程 
        ///保存响应到命令队列，响应操作
        /// </summary>
        private BackgroundWorker bWorker;
        /// <summary>
        /// 运行标志
        /// </summary>
        private bool runFlag = false;

        /// <summary>
        /// 发送链路检测包频率（秒）
        /// </summary>
        public int ActiveTestInterval = 5;
        /// <summary>
        /// 超时最长秒数(默认为60 S)
        /// </summary>
        public int MaxTimeOut = 60;
        /// <summary>
        /// 日志输出
        /// </summary>
        public Action<String> WriteLog;

        /// <summary>
        /// 状态上报
        /// </summary>
        public Action<CMPP30Client, CMPP_DELIVER_Msg_Content> StateReport;
        /// <summary>
        /// 接收上行
        /// </summary>
        public Action<CMPP30Client, CMPP_DELIVER> UserMessageRecive;


        /// <summary>
        /// 实例化构造函数
        /// </summary>
        /// <param name="remote_address">服务器IP地址</param>
        /// <param name="remote_port">服务器IP端口</param>
        /// <param name="login_name">spid</param>
        /// <param name="login_password">登录密码</param>
        /// <param name="seq_addr"></param>
        public CMPP30Client(string remote_address, int remote_port, string sp_Id, string login_password)
        {
            remoteAddress = remote_address;
            remotePort = remote_port;

            spId = sp_Id;
            loginPassword = login_password;

            bWorker = new BackgroundWorker();
            bWorker.WorkerSupportsCancellation = true;
            bWorker.DoWork += new DoWorkEventHandler(bWorker_DoWork);

            WriteLog = new Action<string>((x) => { });//日志输出默认值
        }
        /// <summary>
        /// 后台读取线程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            //处理过程
            while (!bw.CancellationPending)
            {
                //判断通道是否可用
                if (channelStateReady)
                {
                    //从通道中读取数据对象，若返回值为null 则表示无数据或读取出错（读取出错处理在 read方法进行处理）
                    CMPPMsgBody_Base data = read();
                    if (data != null)
                    {
                        channelLastUpdate = DateTime.Now;
                        //对读取到的数据进行处理
                        switch (data.MyHead.Command_Id)
                        {
                            #region 命令响应
                            case Command_Id.CMPP_SUBMIT_RESP:
                            case Command_Id.CMPP_ACTIVE_TEST_RESP:
                                if (CmppCmdQueue.ContainsKey(data.MyHead.Sequence_Id))
                                {
                                    CmppCmdQueue[data.MyHead.Sequence_Id] = data;
                                }
                                break;
                            #endregion
                            #region 状态回报 上行
                            case Command_Id.CMPP_DELIVER:
                                ThreadPool.QueueUserWorkItem(ThreadPoolExcuteFuctione, data);
                                //响应服务器
                                CMPP_DELIVER deliver = data as CMPP_DELIVER;
                                CMPP_DELIVER_RESP deliverResp =
                                    new CMPP_DELIVER_RESP(deliver.MyHead.Sequence_Id, deliver.Msg_Id, (uint)DeliverResult.正确);
                                Submit(deliverResp);
                                break;
                            #endregion
                            #region 拆除连接
                            case Command_Id.CMPP_TERMINATE:
                                Submit(new CMPP_TERMINATE_RESP(data.MyHead.Sequence_Id));
                                CloseSoket();
                                break;
                            #endregion
                            #region 连接检测
                            case Command_Id.CMPP_ACTIVE_TEST:
                                Submit(new CMPP_ACTIVE_TEST_RESP(data.MyHead.Sequence_Id));
                                break;
                            #endregion
                            default://未知的命令丢弃
                                break;
                        }
                    }
                }
                //判断通道空闲时间间隔，进行超时处理
                if (channelLastUpdate.AddSeconds(ActiveTestInterval) < DateTime.Now)
                {
                    var err = Submit(new CMPP_ACTIVE_TEST());//
                    if (err != LocalErrCode.成功)
                    {
                        channelLastUpdate = channelLastUpdate.AddSeconds(ActiveTestInterval);//n秒后重试 防止过多发送
                        WriteLog("长连接链路检测发送失败：" + err.ToString());
                    }
                }
                Thread.Sleep(10);//每个周期休眠10毫秒
            }
        }
        /// <summary>
        /// 从网络流中读取一个缓冲对象，对象大小由长度字段指定。
        /// </summary>
        /// <param name="ns"></param>
        /// <returns></returns>
        private byte[] readBuffer(System.Net.Sockets.NetworkStream ns)
        {
            System.IO.BinaryReader br = new System.IO.BinaryReader(ns);
            byte[] len = br.ReadBytes(4);//头四个字节为命令长度
            uint size = ReverseByteOrder.ReverseBytes(BitConverter.ToUInt32(len, 0));
            int buffersize = 0;
            if (size > 40960)//不处理大于40k的数据包
            {
                throw new Exception("数据大于40K");
            }
            else
            {
                buffersize = (int)size;
            }
            byte[] result = new byte[buffersize];
            int count = br.Read(result, 4, buffersize - 4);
            if (count != buffersize - 4)
            {
                throw new Exception("不完整的数据");
            }
            //将头长度拷贝入结果
            Buffer.BlockCopy(len, 0, result, 0, 4);
            return result;
        }
        /// <summary>
        /// 读取失败或无可读数据返回 null
        /// </summary>
        /// <returns></returns>
        private CMPPMsgBody_Base read()
        {
            byte[] buffer;
            try
            {
                //判断是否有可用的数据
                if (channelStateReady && client_sk != null && client_sk.Available > 0)
                {
                    buffer = readBuffer(client_sk.GetStream());
                }
                else
                {
                    return null;//没有可供读取的数据
                }
                //将读取到的数据构建成对象
                CMPPMsgHeader head;

                //读出头部，判断命令类型
                head = CMPPMsgHeader.Read(buffer);
                //根据指令类型，构建应答对象，对于不处理的指令进行丢弃
                CMPPMsgBody_Base data = null;
                switch (head.Command_Id)
                {
                    case Command_Id.CMPP_SUBMIT_RESP:
                        data = new CMPP_SUBMIT_RESP(head.Sequence_Id);
                        break;
                    case Command_Id.CMPP_DELIVER:
                        data = new CMPP_DELIVER(head.Sequence_Id);
                        break;
                    case Command_Id.CMPP_ACTIVE_TEST:
                        data = new CMPP_ACTIVE_TEST(head.Sequence_Id);
                        break;
                    case Command_Id.CMPP_ACTIVE_TEST_RESP:
                        data = new CMPP_ACTIVE_TEST_RESP(head.Sequence_Id);
                        break;
                    case Command_Id.CMPP_TERMINATE:
                        data = new CMPP_TERMINATE(head.Sequence_Id);
                        break;
                    case Command_Id.CMPP_TERMINATERESP:
                        data = new CMPP_TERMINATE_RESP(head.Sequence_Id);
                        break;
                    default:
                        break;
                }
                if (data != null)
                {
                    data.ReadBytes(buffer);
                }
                return data;
            }
            catch (Exception)//流读取异常
            {
                CloseSoket();
                return null;
            }
        }
        /// <summary>
        /// 绑定操作 
        /// </summary>
        /// <returns></returns>
        public bool Bind()
        {
            lock (channelLockFlag)
            {
                if (channelStateReady)
                {
                    return true;
                }
                try
                {
                    client_sk = new System.Net.Sockets.TcpClient(remoteAddress, remotePort);
                    client_sk.SendTimeout = (MaxTimeOut / 2) * 1000;//发送超时
                    client_sk.ReceiveTimeout = (MaxTimeOut / 2) * 1000;//读数据等待超时时间

                    //与远端的连接已经建立，进行握手
                    CMPP_CONNECT bind = new CMPP_CONNECT(spId, loginPassword);
                    byte[] b_cmd = bind.WriteBytes();
                    client_sk.GetStream().Write(b_cmd, 0, b_cmd.Length);//将命令写入连接

                    //命令写入完毕，开始读取应答
                    CMPP_CONNECT_RESP bindResp = new CMPP_CONNECT_RESP(bind.MyHead.Sequence_Id);
                    b_cmd = readBuffer(client_sk.GetStream());//从网络流中读取一个缓冲对象
                    bindResp.ReadBytes(b_cmd);
                    if (bindResp.Status != 0)
                    {
                        WriteLog("登录失败0:正确1:消息结构错2:非法源地址3:认证错4:版本太高5~:其他错误-->" + bindResp.Status);
                        client_sk.Close();
                        client_sk = null;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);
                    client_sk = null;
                    return false;
                }

                //对通道状态进行更新
                channelLastUpdate = DateTime.Now;
                channelSN++;
                if (channelSN > 1000)
                {
                    channelSN = 0;
                }
                channelStateReady = true;
            }
            return true;
        }
        /// <summary>
        /// 解绑操作
        /// </summary>
        private void UnBind()
        {
            lock (channelLockFlag)
            {
                if (channelStateReady == false)
                {
                    return;
                }

                channelStateReady = false;
                CMPP_TERMINATE unBind = new CMPP_TERMINATE();

                //将命令写入连接
                try
                {
                    byte[] b_cmd = unBind.WriteBytes();
                    client_sk.GetStream().Write(b_cmd, 0, b_cmd.Length);

                    //命令写入完毕，开始读取应答
                    b_cmd = readBuffer(client_sk.GetStream());//从网络流中读取一个缓冲对象，对象大小由长度字段指定。
                    CMPP_TERMINATE_RESP unBindResp = new CMPP_TERMINATE_RESP(unBind.MyHead.Sequence_Id);
                    unBindResp.ReadBytes(b_cmd);
                    if (unBindResp.MyHead.Command_Id != Command_Id.CMPP_TERMINATERESP)
                    {
                        WriteLog("登出未成功取出响应包，当前包" + unBindResp.MyHead.Command_Id);
                    }
                    //解绑完毕
                    client_sk.Close();
                    client_sk = null;
                }
                catch (Exception)
                {
                    client_sk = null;
                    return;
                }
            }
        }
        /// <summary>
        /// 直接关闭流
        /// </summary>
        private void CloseSoket()
        {
            try
            {
                client_sk.Close();
            }
            catch (Exception) { }
            client_sk = null;
            channelStateReady = false;
        }
        /// <summary>
        /// 线程池调用处理上行 或 状态
        /// </summary>
        /// <param name="obj"></param>
        private void ThreadPoolExcuteFuctione(object obj)
        {
            CMPP_DELIVER deliver = obj as CMPP_DELIVER;
            if (deliver.Registered_Delivery == 0 && UserMessageRecive != null)
            {
                UserMessageRecive(this, deliver);
            }
            else if (StateReport != null)
            {
                CMPP_DELIVER_Msg_Content content = new CMPP_DELIVER_Msg_Content();
                content.ReadMessage(deliver.Msg_Content);
                StateReport(this, content);
            }
        }

        /// <summary>
        /// 等待响应
        /// (循环取命令队列)
        /// </summary>
        /// <param name="cmdSendTime"></param>
        /// <param name="currentChannelSN"></param>
        /// <param name="cmdId"></param>
        /// <param name="cmdSequence"></param>
        /// <param name="resp"></param>
        /// <returns></returns>
        private LocalErrCode WaiteResp(DateTime cmdSendTime, int currentChannelSN, uint cmdId, uint cmdSequence, out CMPPMsgBody_Base resp)
        {
            //判断命令超时情况，判断通道序列号变更情况，判断命令存活情况
            resp = null;
            do
            {
                //判断记录当前通道序列号 不等于构建通道的序列号 &&  当前信道是否建立成功
                if (currentChannelSN != channelSN && channelStateReady)
                {
                    return LocalErrCode.等待响应时通道已改变;
                    break;
                }
                //CMPPMsgBody_Base tmp;
                //验证当前队列中是否含有序列号
                if (!CmppCmdQueue.TryGetValue(cmdSequence, out resp))
                {
                    return LocalErrCode.指令异常丢失;//指令异常丢失
                    break;
                }
                if (resp != null)
                {
                    //判断是否为期望的返回值
                    if (((uint)resp.MyHead.Command_Id & 0x7fffffffU) == cmdId)
                    {
                        CmppCmdQueue.TryRemove(cmdSequence, out resp);
                        return LocalErrCode.成功;
                    }
                    else
                    {
                        return LocalErrCode.返回值类型非期望;//返回值类型非期望
                    }
                    //break;
                }
                Thread.Sleep(15);//每次进行15毫秒休眠
            }
            //验证是否在期望时间内返回数据
            while (cmdSendTime.AddSeconds(MaxTimeOut) > DateTime.Now && runFlag);

            return LocalErrCode.命令超时;
        }

        /// <summary>
        /// 启动后台处理线程
        /// </summary>
        public void Start()
        {
            if (!bWorker.IsBusy)
            {
                bWorker.RunWorkerAsync();
            }
            runFlag = true;
        }

        /// <summary>
        /// 关闭后台处理线程
        /// </summary>
        public void Stop()
        {
            runFlag = false;

            bWorker.CancelAsync();
            while (bWorker.IsBusy)
            {
                Thread.Sleep(200);
            }
            //
            UnBind();
            //关闭通道连接
            CloseSoket();

            //将队列中剩余未应答数据全部移除
            CmppCmdQueue.Clear();
        }
        /// <summary>
        /// 发送消息(同步等待服务器响应)
        /// （0命令成功处理 -1组件未启动 -2通道不可用 -3生成下发数据时错误 -4命令超时 -5指令异常丢失 -6重复的命令序列号 -7返回值类型非期望）
        /// </summary>
        /// <returns>返回值errorCode</returns>
        public LocalErrCode Submit(CMPPMsgBody_Base sendMsg, out CMPPMsgBody_Base resp, bool waitResp = true)
        {
            int currentChannelSN = channelSN; //记录当前通道序列号
            DateTime cmdSendTime;//处理命令超时
            resp = null;//响应

            if (waitResp)
            {
                //等待命令响应锁
                if (!System.Threading.Monitor.TryEnter(waitRespLock, MaxTimeOut * 1000))
                {
                    return LocalErrCode.命令超时;
                }
                //添加命令队列
                if (!CmppCmdQueue.TryAdd(sendMsg.MyHead.Sequence_Id, null))
                {
                    return LocalErrCode.重复的命令序列号;//重复的命令序列号
                }
            }

            cmdSendTime = DateTime.Now;

            //组件未启动，不能处理操作指令
            if (!runFlag)
            {
                return LocalErrCode.组件未启动;
            }

            //判断通道是否可用，不可用进行绑定
            if (!channelStateReady)
            {
                Bind();
            }
            //false通道未建立 true通道已建立可以发送数据
            if (!channelStateReady)
            {
                return LocalErrCode.通道不可用;//通道不可用
            }
            //信道建立 开心进行初始短信信息
            byte[] data;
            //填充
            data = sendMsg.WriteBytes();
            //进行发送，然后判断是否有返回值
            try
            {
                lock (channelLockFlag)
                {
                    currentChannelSN = channelSN;
                    client_sk.GetStream().Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                //写入操作失败，通道作废 移除命令
                //
                CloseSoket();
                WriteLog(ex.Message);
                if (waitResp)
                {
                    CmppCmdQueue.TryRemove(sendMsg.MyHead.Sequence_Id, out resp);
                }
                return LocalErrCode.通道不可用;
            }
            channelLastUpdate = DateTime.Now;


            if (waitResp)
            {
                LocalErrCode result = WaiteResp(cmdSendTime, currentChannelSN, (uint)sendMsg.MyHead.Command_Id, sendMsg.MyHead.Sequence_Id, out resp);
                Monitor.Exit(waitRespLock);
                return result;//
            }

            return LocalErrCode.成功;
        }
        public LocalErrCode Submit(CMPPMsgBody_Base sendMsg, bool waitResp = false)
        {
            CMPPMsgBody_Base result = null;
            return Submit(sendMsg, out result, waitResp);
        }
    }
}
