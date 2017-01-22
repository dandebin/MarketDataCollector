using System;

using CTP;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Configuration;

namespace DemoMktData
{
    class Program
    {
        static void Main(string[] args)
        {
            new testMdUserApi().Run();
        }
    }

    class testMdUserApi
    {
        CTPMDAdapter api = null;
        string LogPath =DateTime.Now.ToString("yyyyMMdd")+ ".log";
        string FRONT_ADDR = ConfigurationManager.AppSettings["FRONT_ADDR"]; // "tcp://101.226.255.234:61715"; // "tcp://asp-sim2-md1.financial-trading-platform.com:26213";  // 前置地址
        string BrokerID = ConfigurationManager.AppSettings["BrokerID"]; // "99999"; // "2030";                       // 经纪公司代码
        string UserID = ConfigurationManager.AppSettings["UserID"]; // "18011006110"; // "888888";                       // 投资者代码
        string Password = ConfigurationManager.AppSettings["Password"]; // "131739"; // "888888";                     // 用户密码
        // 大连,上海代码为小写
        // 郑州,中金所代码为大写
        // 郑州品种年份为一位数
        string[] ppInstrumentID; // { "510050" };	// 行情订阅列表
        

       /*
        string FRONT_ADDR = "tcp://101.226.255.233:61723"; // "tcp://asp-sim2-md1.financial-trading-platform.com:26213";  // 前置地址
        string BrokerID = "99999"; // "2030";                       // 经纪公司代码
        string UserID = "18011006110"; // "888888";                       // 投资者代码
        string Password = "131739"; // "888888";                     // 用户密码
        // 大连,上海代码为小写
        // 郑州,中金所代码为大写
        // 郑州品种年份为一位数
        string[] ppInstrumentID = { "10000750" };	// 行情订阅列表
        */

        int iRequestID = 0;

        List<string> GetSymbols()
        {
            List<string> list = new List<string>();
            var symbols = ConfigurationManager.AppSettings["symbols"];
            string url = ConfigurationManager.AppSettings["symbols_url"];
            if (!string.IsNullOrEmpty(symbols))
            {
                var a = symbols.Split(',');
                for (int i = 0; i < a.Length; ++i)
                {
                    if(!list.Contains(a[i]))
                    {
                        list.Add(a[i]);
                    }
                }
            }
            else
            {
                WebClient wc = new WebClient(); // 创建WebClient实例提供向URI 标识的资源发送数据和从URI 标识的资源接收数据
                wc.Credentials = CredentialCache.DefaultCredentials; // 获取或设置用于对向 Internet 资源的请求进行身份验证的网络凭据。
                Encoding enc = Encoding.GetEncoding("utf-8"); // 如果是乱码就改成 utf-8 / GB2312
                Byte[] pageData = wc.DownloadData(url); // 从资源下载数据并返回字节数组。
                string content = enc.GetString(pageData); // 输出字符串(HTML代码)，ContentHtml为
                string pattern = "'100(.+)',";
                Regex reg = new Regex(pattern);
                var matchs = reg.Matches(content);
                for (int i = 0; i < matchs.Count; ++i)
                {

                    string a = matchs[i].Value;
                    a = a.Replace("'", "").Replace(",", "");
                    if (!list.Contains(a))
                    {
                        list.Add(a);
                    }
                }
            }
            return list;
        }

        public void Run()
        {
            var a = GetSymbols();
            ppInstrumentID = a.ToArray();
            iRequestID = a.Count;
            api = new CTPMDAdapter();
            api.OnFrontConnected += OnFrontConnected;
            api.OnFrontDisconnected += new FrontDisconnected(OnFrontDisconnected);
            api.OnRspError += new RspError(OnRspError);
            api.OnRspSubMarketData += new RspSubMarketData(OnRspSubMarketData);
            api.OnRspUnSubMarketData += new RspUnSubMarketData(OnRspUnSubMarketData);
            api.OnRspUserLogin += new RspUserLogin(OnRspUserLogin);
            api.OnRspUserLogout += new RspUserLogout(OnRspUserLogout);
            api.OnRtnDepthMarketData += new RtnDepthMarketData(OnRtnDepthMarketData);

            try
            {
                api.RegisterFront(FRONT_ADDR);
                api.Init();
                api.Join(); // 阻塞直到关闭或者CTRL+C
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                api.Release();
            }
        }

        void OnRspUserLogout(ThostFtdcUserLogoutField pUserLogout, ThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            __DEBUGPF__();
        }

        void OnRspUserLogin(ThostFtdcRspUserLoginField pRspUserLogin, ThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            __DEBUGPF__();
            if (bIsLast && !IsErrorRspInfo(pRspInfo))
            {
                ///获取当前交易日
                Console.WriteLine("--->>> 获取当前交易日 = " + api.GetTradingDay());
                // 请求订阅行情
                SubscribeMarketData();
            }
        }

        void OnRspUnSubMarketData(ThostFtdcSpecificInstrumentField pSpecificInstrument, ThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            __DEBUGPF__();
        }

        void OnRspSubMarketData(ThostFtdcSpecificInstrumentField pSpecificInstrument, ThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            __DEBUGPF__();
            Log(String.Format(">>>OnRspSubMarketData ID = {0}", pSpecificInstrument));
        }

        void OnRspError(ThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            __DEBUGPF__();
            IsErrorRspInfo(pRspInfo);
            Log(String.Format(">>>OnRspError ErrorMsg = {0}", pRspInfo.ErrorMsg));
        }

        void OnHeartBeatWarning(int nTimeLapse)
        {
            __DEBUGPF__();
            Console.WriteLine("--->>> nTimerLapse = " + nTimeLapse);
        }

        void OnFrontDisconnected(int nReason)
        {
            __DEBUGPF__();
            Console.WriteLine("--->>> Reason = {0}", nReason);
            Log(String.Format(">>>OnFrontDisconnected Reason = {0}", nReason));
        }

        void  OnFrontConnected()
        {
            __DEBUGPF__();
            ReqUserLogin();
            Log(String.Format(">>>OnFrontConnected",""));
        }

        bool IsErrorRspInfo(ThostFtdcRspInfoField pRspInfo)
        {
            // 如果ErrorID != 0, 说明收到了错误的响应
            bool bResult = ((pRspInfo != null) && (pRspInfo.ErrorID != 0));
            if (bResult)
                Console.WriteLine("--->>> ErrorID={0}, ErrorMsg={1}", pRspInfo.ErrorID, pRspInfo.ErrorMsg);
            return bResult;
        }

        void ReqUserLogin()
        {
            ThostFtdcReqUserLoginField req = new ThostFtdcReqUserLoginField();
            req.BrokerID = BrokerID;
            req.UserID = UserID;
            req.Password = Password;
            int iResult = api.ReqUserLogin(req, ++iRequestID);

            Console.WriteLine("--->>> 发送用户登录请求: " + ((iResult == 0) ? "成功" : "失败"));
        }

        void SubscribeMarketData()
        {
            int iResult = api.SubscribeMarketData(ppInstrumentID);
            Console.WriteLine("--->>> 发送行情订阅请求: " + ppInstrumentID[0] + ((iResult == 0) ? "成功" : "失败"));
        }

        void OnRtnDepthMarketData(ThostFtdcDepthMarketDataField pDepthMarketData)
        {
            //DateTime now = DateTime.Parse(pDepthMarketData.UpdateTime);
            //now.AddMilliseconds(pDepthMarketData.UpdateMillisec);
            string s = string.Format("{0}.{1:D3},{2},{3},{4},{5},{6},{7}", 
                pDepthMarketData.UpdateTime,
                pDepthMarketData.UpdateMillisec,
                pDepthMarketData.InstrumentID, 
                pDepthMarketData.LastPrice,
                pDepthMarketData.BidPrice1,
                pDepthMarketData.BidVolume1,
                pDepthMarketData.AskPrice1,
                pDepthMarketData.AskVolume1
                );
            string path = pDepthMarketData.TradingDay;
            using (var sw = File.AppendText(path))
            {
                sw.WriteLine(s);
                sw.Flush();
            }
           // Debug.WriteLine(s);
            Console.WriteLine(s);
        }

        void __DEBUGPF__()
        {
            StackTrace ss = new StackTrace(false);
            MethodBase mb = ss.GetFrame(1).GetMethod();
            string str = "--->>> " + mb.DeclaringType.Name + "." + mb.Name + "()";
            Debug.WriteLine(str);
            Console.WriteLine(str);
        }

        void Log(string msg)
        {
            using (var sw = File.AppendText(LogPath))
            {
                sw.WriteLine(msg);
                sw.Flush();
            }
        }

    }
}
