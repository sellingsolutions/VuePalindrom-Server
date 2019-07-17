using System;
using Starcounter;
using Starcounter.Startup;

namespace VuePalindrom_Server
{
    class Program
    {
        /*
         Create a handler which is going to create a session and return its id.

          - View front end requests Starcounter URL with `accept application/json` header.
           - Starcounter responds with JSON view-model and `X-Location` header to establish WebSocket connection.
           - Vue reads the headers and establishes WebSocket connection by the specified location.
         */
        static void Main()
        {
            DefaultStarcounterBootstrapper.Start(new Startup());

            Handle.GET("/VuePalindrom-Server/", (Request request) =>
            {

                var session = Session.Ensure();

                var resp = new Response()
                {
                    //StatusCode = 200,
                    //ContentType = "text/html",
                    // X-Location = "...ws server?.."
                    //Body = session.SessionId
                };

                return resp;
            });
        }
    }
}