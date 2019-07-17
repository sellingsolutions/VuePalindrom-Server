using System;
using Starcounter;
using Starcounter.Startup;

namespace VuePalindrom_Server
{
    class Program
    {
        static void Main()
        {
            DefaultStarcounterBootstrapper.Start(new Startup());

            Handle.GET("/VuePalindrom-Server/session", (Request request) =>
            {
                var session = Session.Ensure();
                return session.SessionId;
            });
        }
    }
}