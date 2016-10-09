using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace cmpp30
{
    public enum LocalErrCode
    {
        成功 = 0,//发送服务器成功（若要求等待返回表示服务器已返回）
        组件未启动 = -1,
        通道不可用 = -2,
        生成下发数据时错误 = -3,
        命令超时 = -4,
        指令异常丢失 = -5,
        重复的命令序列号 = -6,
        返回值类型非期望 = -7,
        未响应命令达到上限请稍后 = -8,
        等待响应时通道已改变 = -9
    }
}
