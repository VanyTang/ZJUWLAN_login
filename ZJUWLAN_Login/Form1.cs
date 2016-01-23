//#define OUTPUTLOG

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Threading;

namespace ZJUWLAN_Login
{
    public partial class frmWLANLogin : Form
    {

        const string strConfigFileName = "zjuwlan_config.txt";
        const string strLoginRequestURL = @"https://net.zju.edu.cn/cgi-bin/srun_portal";
        const string strDropRequestURL = @"https://net.zju.edu.cn/rad_online.php";
        const string strPathLog = "wlanlogin.log";

#if OUTPUTLOG
        StreamWriter sw = new StreamWriter(new FileStream(strPathLog, FileMode.Append));
#endif

        bool isAutoLogin = true;
        bool isLoginOk = false;

        void log(string strlog)
        {
#if DEBUG
            MessageBox.Show (DateTime.Now.ToString() + ' ' + strlog);
#else 
#if OUTPUTLOG
            sw.WriteLine(DateTime.Now.ToString() + ' ' + strlog);
#endif
#endif
        }

        public frmWLANLogin()
        {
            InitializeComponent();
        }

        private void btnHidden_Click(object sender, EventArgs e)
        {
            this.Hide();
            notifyIcon1.Visible = true;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            notifyIcon1.Visible = false;
        }

        private void frmWLANLogin_Load(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            loadConfig();
            setAutoLogin(isAutoLogin);
            login();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            setConfig();
            btnSave.Text = "保存配置\n:)";
        }

        private void loadConfig()
        {
            if (File.Exists(strConfigFileName))
            {
                var lines = File.ReadAllLines(strConfigFileName);
                foreach (var line in lines)
                {
                    int npos = line.IndexOf('=');
                    string name = line.Substring(0, npos);
                    string value = line.Substring(npos + 1);
                    if (name.ToUpper() == "ID") txtStuID.Text = value;
                    else if (name.ToUpper() == "PWD") txtPwd.Text = value;
                    else if (name.ToUpper() == "AUTOLOGIN") isAutoLogin = (value == "Y" ? true : false);
                    else if (name.ToUpper() == "TIMERINTERVAL")
                    {
                        timer1.Interval = int.Parse(value);
                        txtInterval.Text = (timer1.Interval / 1000).ToString();
                    }
                }
            }
            else
            {
                txtPwd.Text = txtStuID.Text = "";
            }
        }
        private void setConfig()
        {
            int interval;
            if (int.TryParse(txtInterval.Text, out interval))
                timer1.Interval = interval * 1000;
            else
                MessageBox.Show("非法的时间间隔值，将不改变原值.");

            string strText = "";
            strText += "id=" + txtStuID.Text + "\n";
            strText += "pwd=" + txtPwd.Text + "\n";
            strText += "autologin=" + (isAutoLogin ? "Y" : "N") + "\n";
            strText += "timerInterval=" + timer1.Interval.ToString() + "\n";

            File.WriteAllText(strConfigFileName, strText);
        }

        private void setAutoLogin(bool value)
        {
            isAutoLogin = value;
            timer1.Enabled = isAutoLogin;
            if (value)
                btnOpt.Text = "自动登录已开启";
            else
                btnOpt.Text = "自动登录已关闭";
        }

        private void txtStuID_TextChanged(object sender, EventArgs e) { btnSave.Text = "保存配置"; }
        private void txtPwd_TextChanged(object sender, EventArgs e) { btnSave.Text = "保存配置"; }
        private void txtInterval_TextChanged(object sender, EventArgs e) { btnSave.Text = "保存配置"; }

        private void btnOpt_Click(object sender, EventArgs e)
        {
            btnSave.Text = "保存配置";
            setAutoLogin(!isAutoLogin);
        }

        private bool checkWLAN()
        {
            bool ret = false;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://net.zju.edu.cn/");
                var rsp = (HttpWebResponse)req.GetResponse();
                string strRsp = new StreamReader(rsp.GetResponseStream()).ReadToEnd();
                log("checkWLAN:" + strRsp);
                ret = true;
            }
            catch (Exception ex)
            {
                log("error: " + ex.Message);
                ret = false;
            }
            return ret;
        }

        private bool checkInternet()
        {
            bool ret = false;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://www.baidu.com/");
                req.Timeout = 5000;
                var rsp = (HttpWebResponse)req.GetResponse();
                string strRsp = new StreamReader(rsp.GetResponseStream()).ReadToEnd();
                ret = strRsp.IndexOf("net.zju.edu.cn") == -1;
                log("checkInternet:" + ret.ToString());
            }
            catch (Exception ex)
            {
                log("error: " + ex.Message);
                ret = false;
            }
            return ret;
        }

        private delegate void delegateSetStatusText(string value);
        private void setStatusText(string value)
        {
            delegateSetStatusText del = delegate(string txt)
            {
                lblStatus.Text = txt;
            };
            lblStatus.BeginInvoke(del, value);
        }

        bool isInLogin = false;
        private void login()
        {
            //为了避免多个线程进入login
            if (isInLogin) return;

            lblStatus.Text = "loging....";

            Thread t = new Thread(login_async);
            t.Start();
        }

        private void login_async()
        {
            try
            {
                isInLogin = true;

                if (isLoginOk) return;
                if (checkInternet())
                {
                    setStatusText("已连接Internet");
                    return;
                }
                if (!checkWLAN())
                {
                    setStatusText("无法连接到net.zju.edu.cn，请确认是ZJUWLAN");
                    return;
                }

                string strRsp = postData(strLoginRequestURL, getContentString());
                if (strRsp.Contains("login_ok"))//login Ok!
                {
                    setStatusText("Login Ok!");
                    isLoginOk = true;
                    log("Login Ok!");
                }
                else if(strRsp.Contains("密码错误"))
                {
                    setStatusText("用户名或密码错误");
                    log("Wrong UserName/Password");
                }
                else if (strRsp.Contains("您已在线"))
                {
                    setStatusText("您已在线，正在自动踢掉用户");
                    var strDropRsp = dropUser(txtStuID.Text, txtPwd.Text);
                    if (strDropRsp == "ok")
                    {
                        log("Kick out the other user.");
                    }
                    else
                    {
                        log("fail to kick out. " + strDropRsp);
                    }
                }
                else
                {
                    setStatusText(strRsp);
                    log("login: need update?  " + strRsp);
                }
            }
            catch (Exception ex)
            {
                log("login Error: " + ex.Message);
            }
            finally
            {
                isInLogin = false;
            }
        }

        private string postData(string strURL, string strData)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(strURL);
            req.Method = "POST";
            req.Accept = "*/*";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Host = "net.zju.edu.cn";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.10240";
            var reqStream = req.GetRequestStream();
            byte[] contentBytes = Encoding.Default.GetBytes(strData);
            reqStream.Write(contentBytes, 0, contentBytes.Length);

            var rsp = req.GetResponse();
            StreamReader sr = new StreamReader(rsp.GetResponseStream());
            string strRsp = sr.ReadToEnd();
            sr.Close();
            return strRsp;
        }

        private string dropUser(string id, string pwd)
        {
            StringWriter strw = new StringWriter();
            strw.Write("action=auto_dm&");
            strw.Write(string.Format("username={0}&", id.Trim()));
            strw.Write(string.Format("password={0}&", pwd));
            return postData(strDropRequestURL, strw.ToString());
        }

        private string getContentString()
        {
            StringWriter strw = new StringWriter();
            strw.Write(string.Format("ac_id={0}&", 3));
            strw.Write("action=login&");
            strw.Write("type=1&");
            strw.Write(string.Format("username={0}&", txtStuID.Text));
            strw.Write(string.Format("password={0}&", txtPwd.Text));
            return strw.ToString();
        }


        private void frmWLANLogin_FormClosed(object sender, FormClosedEventArgs e)
        {
            notifyIcon1.Visible = false;
#if OUTPUTLOG
            sw.Close();
#endif
        }

        //登录按钮的事件.
        private void button1_Click(object sender, EventArgs e)
        {
            login();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            isLoginOk = false; //定时再查一遍网络状况.
            login();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
#if OUTPUTLOG
            sw.Flush();
#endif
        }

    }
}
