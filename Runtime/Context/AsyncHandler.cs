using System;
using System.Collections.Generic;

using UnityEngine;

using Edger.Unity;
using Edger.Unity.Weak;

namespace Edger.Unity.Context {
    public abstract class AsyncHandler<TReq, TRes> : Handler<TReq, TRes> {
        public HandleLog<TReq, TRes> LastAsync { get; private set; }

        private Dictionary<int, IEnumerator> _RunningCoroutines = new Dictionary<int, IEnumerator>();
        public int RunningCount { get => _RunningCoroutines.Count; }

        public void ClearRunningCoroutines() {
            foreach (var coroutine in _RunningCoroutines.Values) {
                StopCoroutine(coroutine);
            }
            _RunningCoroutines.Clear();
        }

        protected override HandleLog<TReq, TRes> DoHandle(DateTime reqTime, TReq req) {
            var log = new HandleLog<TReq, TRes>(this, reqTime, req, null);
            var coroutine = StartCoroutine(DoHandleInternalAsync());
            _RunningCoroutines[log.Identity] = coroutine;
            return log;
        }

        private void OnAsyncResult(HandleLog<TReq, TRes> log) {
            LastAsync = log;
            AdvanceRevision();
            if (LogDebug) {
                Debug("OnAsyncResponse: {0}", LastAsync);
            }
            NotifyHandlerWatchers(LastAsync);
        }

        private IEnumerator DoHandleInternalAsync(HandleLog<TReq, TRes> log) {
            HandleLog<TReq, TRes> result = null;
            try {
                TRes response = default(TRes);
                IEnumerator handle = DoHandleAsync(log.RequestTime, log.Request, out response);
                while (handle.MoveNext()) {
                    yield return handle.Current;
                }
                if (response != null) {
                    result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, response);
                } else {
                    result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, StatusCode.InternalError, "<{0}>.DoHandleAsync Failed: reponse == null", GetType());
                }
            } catch (Exception e) {
                result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, StatusCode.InternalError, e);
            }
            _RunningCoroutines[log.Identity] = null;
            OnAsyncResult(result);
        }

        protected abstract IEnumerator DoHandleAsync(DateTime reqTime, TReq req, out TRes res);
    }
}
