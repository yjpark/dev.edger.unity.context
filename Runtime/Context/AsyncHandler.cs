using System;
using System.Collections;
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
            var log = new HandleLog<TReq, TRes>(this, reqTime, req);
            var coroutine = DoHandleInternalAsync(log);
            _RunningCoroutines[log.Identity] = coroutine;
            StartCoroutine(coroutine);
            return log;
        }

        private void OnAsyncResult(int reqIdentity, HandleLog<TReq, TRes> log) {
            if (_RunningCoroutines.ContainsKey(reqIdentity)) {
                _RunningCoroutines.Remove(reqIdentity);
            }
            LastAsync = log;
            AdvanceRevision();
            if (LogDebug) {
                Debug("OnAsyncResponse: {0}", LastAsync);
            }
            NotifyHandlerWatchers(LastAsync);
        }

        private IEnumerator DoHandleInternalAsync(HandleLog<TReq, TRes> log) {
            HandleLog<TReq, TRes> result = null;
            TRes response = default(TRes);
            IEnumerator handle = DoHandleAsync(log.RequestTime, log.Request, out response);
            while (true) {
                try {
                    if (handle.MoveNext() == false) {
                        break;
                    }
                } catch (Exception e) {
                    result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, StatusCode.InternalError, e);
                    OnAsyncResult(log.Identity, result);
                    yield break;
                }
                yield return handle.Current;
            }
            if (response != null) {
                result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, response);
            } else {
                result = new HandleLog<TReq, TRes>(this, log.RequestTime, log.Request, StatusCode.InternalError, "<{0}>.DoHandleAsync Failed: reponse == null", GetType());
            }
            OnAsyncResult(log.Identity, result);
        }

        protected abstract IEnumerator DoHandleAsync(DateTime reqTime, TReq req, out TRes res);
    }
}
