using System;
using System.Collections.Generic;

using UnityEngine;

using Edger.Unity;
using Edger.Unity.Weak;

namespace Edger.Unity.Context {
    public enum StatusCode : int {
        Ok = 200,
        Accepted= 202,

        InternalError = 500,
    }

    public abstract class HandleLog<TReq, TRes> : AspectLog {
        public readonly DateTime RequestTime;
        public readonly TReq Request;

        public DateTime ResponseTime { get => Time; }
        public readonly TRes Response;

        public readonly StatusCode StatusCode;
        public readonly Exception Error;

        public bool IsOk { get => StatusCode == StatusCode.Ok; }
        public bool IsAccepted { get => StatusCode == StatusCode.Accepted; }
        public bool IsError { get => Error != null; }

        public HandleLog(Handler<TReq, TRes> handler, DateTime reqTime, TReq req, TRes res) : base(handler) {
            RequestTime = reqTime;
            Request = req;
            Response = res;
            StatusCode = res == null ? StatusCode.Accepted : StatusCode.Ok;
        }

        public HandleLog(Handler<TReq, TRes> handler, DateTime reqTime, TReq req, StatusCode statusCode, Exception error) : base(handler) {
            RequestTime = reqTime;
            Request = req;
            Response = default(TRes);
            StatusCode = statusCode;
            Error = error;
        }

        public HandleLog(Handler<TReq, TRes> handler, DateTime reqTime, TReq req, StatusCode statusCode, string format, params object[] values)
            : this(handler, reqTime, req, statusCode, new EdgerException(format, values)) {
        }

        public override string ToString() {
            if (IsError) {
                return string.Format("[{0}] {1} -> {2}", StatusCode, Request, Error);
            } else if (log.IsOk) {
                return string.Format("[{0}] {1} -> {2}", StatusCode, Request, Response);
            } else {
                return string.Format("[{0}] {1} ->", StatusCode, Request);
            }
        }
    }

    public abstract class Handler<TReq, TRes> : Aspect {
        public Type RequestType { get => typeof(TReq); }
        public Type ResponseType { get => typeof(TRes); }

        public HandleLog<TReq, TRes> Last { get; private set; }

        public Response<TReq, TRes> HandleRequest(TReq req) {
            DateTime reqTime = DateTime.UtcNow;
            HandleLog<TReq, TRes> result = null;
            try {
                var Response = DoHandle(reqTime, req);
            } catch (Exception e) {
                result = new HandleLog<TReq, TRes>(this, reqTime, req, StatusCode.InternalError, e);
            }
            Last = result;
            AdvanceRevision();
            if (log.IsError) {
                Error("HandleRequest Failed: {0}", Last);
            } else if (LogDebug) {
                Debug("HandleRequest: {0}", Last);
            }
            NotifyHandlerWatchers(Last);
            return Last;
        }

        protected abstract HandleLog<TReq, TRes> DoHandle(DateTime reqTime, TReq req);

        protected void NotifyHandlerWatchers(HandleLog<TReq, TRes> log) {
            WeakListUtil.ForEach(_HandlerWatchers, (watcher) => {
                watcher.OnEvent(this, log);
            });
        }

        private WeakList<IEventWatcher<HandleLog<TReq, TRes>>> _HandlerWatchers = null;

        public int ResponseWatcherCount {
            get { return WeakListUtil.Count(_HandlerWatchers); }
        }

        public bool AddResponseWatcher(IEventWatcher<HandleLog<TReq, TRes>> watcher) {
            return WeakListUtil.Add(ref _HandlerWatchers, watcher);
        }

        public bool RemoveResponseWatcher(IEventWatcher<HandleLog<TReq, TRes>> watcher) {
            return WeakListUtil.Remove(_HandlerWatchers, watcher);
        }
    }
}
