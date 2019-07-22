using System;
using System.Collections.Generic;
using Starcounter;
using Starcounter.Logging;
using Starcounter.XSON.JsonPatch;
using Starcounter.XSON.Serializer;
using Starcounter.XSON;
using Starcounter.XSON.Advanced;
using Starcounter.XSON.Internal;

namespace VuePalindrom_Server
{
    /// <summary>
    /// Registers the built in REST handler allowing clients to communicate with
    /// the public Session data of a Starcounter application.
    /// </summary>
    internal static class PalindromRestHandler
    {
        private const string DataLocationUriPrefix = "/vue-palindrom/";

        private const string ERR_SESSION_NO_ROOT = "Session contains no root viewmodel.";
        private const string ERR_SESSION_NOT_FOUND = "No session found.";
        private const string ERR_UNSUPPORTED_MIMETYPE = "Unsupported mime type {0}.";

        private static JsonPatch jsonPatch = new JsonPatch();
        private static LogSource patchLog = new LogSource("Starcounter.XSON");
        private static JsonSerializerSettings rootSettings = new JsonSerializerSettings()
        {
            CacheBoundValues = true
        };

        /// <summary>
        /// Name of the WebSocket Json-Patch channel.
        /// </summary>
        static String JsonPatchWebSocketGroupName = "jsonpatchws";

        private static readonly string ReconnectUriPart = "/reconnect";

        /// <summary>
        /// Handles incoming WebSocket byte data.
        /// </summary>
        /// <param name="bs"></param>
        /// <param name="ws"></param>
        static void HandleWebSocketJson(string bs, WebSocket ws)
        {
            // Incrementing the initial call level for handles.
            Handle.CallLevel++;

            Json root = null;
            Session session = (Session)ws.Session;
            string patchResponse;
            int patchCount;

            try
            {
                // Checking if session is presented still.
                if (session == null)
                {
                    ws.Disconnect(ERR_SESSION_NOT_FOUND, WebSocket.WebSocketCloseCodes.WS_CLOSE_UNEXPECTED_CONDITION);
                    return;
                }

                // Checking if session has a tree.
                root = session.GetClientRoot();
                if (root == null)
                {
                    ws.Disconnect(ERR_SESSION_NO_ROOT, WebSocket.WebSocketCloseCodes.WS_CLOSE_UNEXPECTED_CONDITION);
                    return;
                }

                long remoteVersionBefore = 0;
                ViewModelVersion version = root.ChangeLog?.Version;

                if (version != null)
                {
                    remoteVersionBefore = version.RemoteVersion;
                }

                // Running patches evaluation.
                JsonPatchStatus status = jsonPatch.Apply(root, bs, session.HasFlag(Session.Flags.StrictPatchRejection), out patchCount);

                if (status == JsonPatchStatus.Applied)
                {
                    if (patchCount > 0)
                    {
                        // Getting changes from the root.
                        patchResponse = jsonPatch.Generate(root, true, session.HasFlag(Session.Flags.IncludeNamespaces));
                        if (!string.IsNullOrEmpty(patchResponse))
                        {
                            // Sending the patch bytes to the client.
                            ws.Send(patchResponse);
                        }
                    }
                    else
                    { // Ping
                        patchResponse = jsonPatch.GenerateNoUpdate(root, true);
                        ws.Send(patchResponse);
                    }
                }
                else if (status == JsonPatchStatus.AlreadyApplied)
                {
                    if (patchCount == 2 && (version.RemoteVersion == remoteVersionBefore))
                    {
                        patchResponse = jsonPatch.GenerateNoUpdate(root, false);
                        ws.Send(patchResponse);
                    }
                }
            }
            catch (JsonPatchException nex)
            {
                patchLog.LogException(nex);
                ws.Send(nex.Message);
                ws.Disconnect("The error stack trace is sent in the previous message.", WebSocket.WebSocketCloseCodes.WS_CLOSE_UNEXPECTED_CONDITION);
                session?.Destroy();
                return;
            }
        }

        internal static void RegisterJsonPatchHandlers()
        {
            Handle.PATCH(DataLocationUriPrefix + Handle.UriParameterIndicator, (Session session, Request request) =>
            {
                SetDefaultCorsReponseHeaders(request);

                Json root = null;

                // Incrementing the initial call level for handles.
                Handle.CallLevel++;

                try
                {
                    if (session == null)
                    {
                        return CreateErrorResponse(404, ERR_SESSION_NOT_FOUND);
                    }

                    root = session.GetClientRoot();
                    if (root == null)
                    {
                        return CreateErrorResponse(404, ERR_SESSION_NO_ROOT);
                    }

                    int patchCount;
                    JsonPatchStatus status;
                    long remoteVersionBefore = 0;
                    ViewModelVersion version = root.ChangeLog?.Version;

                    if (version != null)
                    {
                        remoteVersionBefore = version.RemoteVersion;
                    }

                    status = jsonPatch.Apply(root, request.Body, session.HasFlag(Session.Flags.StrictPatchRejection), out patchCount);

                    if (status == JsonPatchStatus.Applied)
                    {
                        if (patchCount == 0)
                        { // Empty ping from client. Return empty patch. Note: No changes should be collected.
                            return CreateJsonPingResponse(root, true);
                        }
                        return root; // Return root to trigger patches back.
                    }
                    else if (status == JsonPatchStatus.Queued)
                    {
                        return new Response()
                        {
                            Resource = root,
                            StatusCode = 202,
                            StatusDescription = "Patch enqueued until earlier versions have arrived. Last known version is " + root.ChangeLog.Version.RemoteVersion
                        };
                    }
                    else if (status == JsonPatchStatus.AlreadyApplied)
                    {
                        if (patchCount == 2 && (version.RemoteVersion == remoteVersionBefore))
                        { // AlreadyApplied implies versioning so no need to check for null.
                            // Ping including current versions.
                            return CreateJsonPingResponse(root, false);
                        }

                        return new Response()
                        {
                            Resource = root,
                            StatusCode = 200,
                            StatusDescription = "Patch already applied"
                        };
                    }

                    return root;
                }
                catch (JsonPatchException nex)
                {
                    patchLog.LogException(nex);
                    session?.Destroy();
                    return CreateErrorResponse(400, nex.Message);
                }
            });

            Handle.GET(DataLocationUriPrefix + Handle.UriParameterIndicator, (Request request, Session session) =>
            {
                SetDefaultCorsReponseHeaders(request);

                if (session == null)
                    return CreateErrorResponse(404, ERR_SESSION_NOT_FOUND);

                if (request.WebSocketUpgrade)
                {
                    // Sending an upgrade (note that we attach the existing session).
                    request.SendUpgrade(JsonPatchWebSocketGroupName, null, null, session);
                    return HandlerStatus.Handled;
                }
                else if (request.PreferredMimeType == MimeType.Application_Json)
                {
                    Json root = session.GetClientRoot();
                    if (root == null)
                        return CreateErrorResponse(404, ERR_SESSION_NO_ROOT);

                    return CreateJsonBodyResponse(session, root);
                }
                else
                {
                    return CreateErrorResponse(513, String.Format(ERR_UNSUPPORTED_MIMETYPE, request.PreferredMimeTypeString));
                }
            });

            Handle.PATCH(DataLocationUriPrefix + Handle.UriParameterIndicator + ReconnectUriPart, (Request request, Session session) =>
            {
                SetDefaultCorsReponseHeaders(request);

                if (session == null)
                {
                    return CreateErrorResponse(404, ERR_SESSION_NOT_FOUND);
                }
                Json root = session.GetClientRoot();
                if (root == null)
                {
                    return CreateErrorResponse(404, ERR_SESSION_NO_ROOT);
                }

                if (request.PreferredMimeType != MimeType.Application_Json)
                {
                    return CreateErrorResponse(513, string.Format(ERR_UNSUPPORTED_MIMETYPE, request.PreferredMimeTypeString));
                }

                jsonPatch.Apply(root, request.Body, session.HasFlag(Session.Flags.StrictPatchRejection));

                session.ActiveWebSocket = null; // since this is reconnection call we can assume that any web socket is dead
                return CreateJsonBodyResponse(session, root);
            });

            // Handling WebSocket JsonPatch string message.
            Handle.WebSocket(JsonPatchWebSocketGroupName, (String s, WebSocket ws) =>
            {
                // Calling bytes data handler.
                HandleWebSocketJson(s, ws);
            });

            // Handling WebSocket JsonPatch byte array.
            Handle.WebSocket(JsonPatchWebSocketGroupName, HandleWebSocketJson);

            // Handling JsonPatch WebSocket disconnect here.
            Handle.WebSocketDisconnect(JsonPatchWebSocketGroupName, (WebSocket ws) =>
            {
                // Do nothing!
            });
        }

        private static Response CreateJsonPingResponse(Json root, bool forceEmptyPatch)
        {
            string outgoingPatch = jsonPatch.GenerateNoUpdate(root, forceEmptyPatch);

            return new Response()
            {
                Body = outgoingPatch,
                ContentType = MimeTypeHelper.MimeTypeAsString(MimeType.Application_JsonPatch__Json)
            };
        }

        private static Response CreateJsonBodyResponse(Session session, Json root)
        {
            string body = string.Empty;

            XSONInternals.RunWithNamespacingEnabled(session, () =>
            {
                body = root.ToJson(rootSettings);
                root.ChangeLog?.Checkpoint();
            });

            return new Response()
            {
                Body = body,
                ContentType = MimeTypeHelper.MimeTypeAsString(MimeType.Application_Json)
            };
        }

        private static Response CreateErrorResponse(int statusCode, string message)
        {
            var response = Response.FromStatusCode(statusCode);
            response.Body = message;
            return response;
        }

        // Cross-Origin Resource Sharing (CORS)
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS
        public static void SetDefaultCorsReponseHeaders(Request request)
        {
            Handle.AddOutgoingHeader("Access-Control-Allow-Origin", request.HeadersDictionary["Origin"]);
            Handle.AddOutgoingHeader("Vary", "Origin");
            Handle.AddOutgoingHeader("Access-Control-Allow-Credentials", "true");
            Handle.AddOutgoingHeader("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept, X-Location, Referer, User-Agent, Cache-Control, Content-Length, Date, Server");
            Handle.AddOutgoingHeader("Access-Control-Request-Method", "POST, GET, OPTIONS, DELETE, PATCH, PUT");
            Handle.AddOutgoingHeader("Access-Control-Request-Headers", "Origin, X-Requested-With, Content-Type, Accept, X-Location, Referer, User-Agent, Cache-Control, Content-Length, Date, Server");
            Handle.AddOutgoingHeader("Access-Control-Max-Age", "86400");
        }
    }
}
