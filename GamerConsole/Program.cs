using GamerSourceBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GamerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            GamerBotService gbs = new GamerBotService();

            gbs.Init();

            Console.ReadLine();
        }
    }
}
