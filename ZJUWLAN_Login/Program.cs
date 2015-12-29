using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZJUWLAN_Login
{
    static class Program
    {
        private static System.Threading.Mutex mutex;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //防止启动多个实例.
            mutex = new System.Threading.Mutex(true, "ZJUWLAN001");
            if (mutex.WaitOne(0, false))
            {
                Application.Run(new frmWLANLogin());
            }
        }
    }
}