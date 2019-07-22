using System;
using System.Collections.Generic;
using Starcounter;
using Starcounter.XSON.Advanced;

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
            PalindromRestHandler.RegisterJsonPatchHandlers();

            Handle.GET("/VuePalindromServer/NewConnection", (Request request) =>
            {
                PalindromRestHandler.SetDefaultCorsReponseHeaders(request);

                Session session = Session.Ensure();
                string url = $"http://localhost:8080/vue-palindrom/{session.SessionId}";

                session.SetClientRoot(new UserViewModel());
                Handle.AddOutgoingHeader("X-Location", url);

                return url;
            });
        }
    }
}