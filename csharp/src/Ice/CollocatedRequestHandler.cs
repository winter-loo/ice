//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace IceInternal
{
    public class CollocatedRequestHandler : IRequestHandler, IResponseHandler
    {
        private void
        fillInValue(Ice.OutputStream os, int pos, int value)
        {
            os.RewriteInt(value, pos);
        }

        public
        CollocatedRequestHandler(Reference @ref, Ice.ObjectAdapter adapter)
        {
            _reference = @ref;
            _dispatcher = _reference.getCommunicator().Dispatcher != null;
            _response = _reference.getMode() == Ice.InvocationMode.Twoway;
            _adapter = adapter;

            _logger = _reference.getCommunicator().Logger; // Cached for better performance.
            _traceLevels = _reference.getCommunicator().TraceLevels; // Cached for better performance.
            _requestId = 0;
        }

        public IRequestHandler? update(IRequestHandler previousHandler, IRequestHandler? newHandler) =>
            previousHandler == this ? newHandler : this;

        public int sendAsyncRequest(ProxyOutgoingAsyncBase outAsync) => outAsync.invokeCollocated(this);

        public void AsyncRequestCanceled(OutgoingAsyncBase outAsync, Ice.LocalException ex)
        {
            lock (this)
            {
                int requestId;
                if (_sendAsyncRequests.TryGetValue(outAsync, out requestId))
                {
                    if (requestId > 0)
                    {
                        _asyncRequests.Remove(requestId);
                    }
                    _sendAsyncRequests.Remove(outAsync);
                    if (outAsync.exception(ex))
                    {
                        outAsync.invokeExceptionAsync();
                    }
                    _adapter.decDirectCount(); // invokeAll won't be called, decrease the direct count.
                    return;
                }
                if (outAsync is OutgoingAsync)
                {
                    OutgoingAsync o = (OutgoingAsync)outAsync;
                    Debug.Assert(o != null);
                    foreach (KeyValuePair<int, OutgoingAsyncBase> e in _asyncRequests)
                    {
                        if (e.Value == o)
                        {
                            _asyncRequests.Remove(e.Key);
                            if (outAsync.exception(ex))
                            {
                                outAsync.invokeExceptionAsync();
                            }
                            return;
                        }
                    }
                }
            }
        }

        public void SendResponse(int requestId, Ice.OutputStream os, byte status, bool amd)
        {
            OutgoingAsyncBase? outAsync;
            lock (this)
            {
                Debug.Assert(_response);

                if (_traceLevels.protocol >= 1)
                {
                    fillInValue(os, 10, os.Size);
                }

                // Adopt the OutputStream's buffer.
                Ice.InputStream iss = new Ice.InputStream(os.Communicator, os.Encoding, os.GetBuffer(), true);

                iss.Pos = Protocol.replyHdr.Length + 4;

                if (_traceLevels.protocol >= 1)
                {
                    TraceUtil.traceRecv(iss, _logger, _traceLevels);
                }

                if (_asyncRequests.TryGetValue(requestId, out outAsync))
                {
                    outAsync.getIs().Swap(iss);
                    if (!outAsync.response())
                    {
                        outAsync = null;
                    }
                    _asyncRequests.Remove(requestId);
                }
            }

            if (outAsync != null)
            {
                if (amd)
                {
                    outAsync.invokeResponseAsync();
                }
                else
                {
                    outAsync.invokeResponse();
                }
            }
            _adapter.decDirectCount();
        }

        public void SendNoResponse() => _adapter.decDirectCount();

        public bool
        SystemException(int requestId, Ice.SystemException ex, bool amd)
        {
            handleException(requestId, ex, amd);
            _adapter.decDirectCount();
            return true;
        }

        public void
        InvokeException(int requestId, Ice.LocalException ex, int invokeNum, bool amd)
        {
            handleException(requestId, ex, amd);
            _adapter.decDirectCount();
        }

        public Reference getReference() => _reference;

        public Ice.Connection? getConnection() => null;

        public int invokeAsyncRequest(OutgoingAsyncBase outAsync, bool synchronous)
        {
            //
            // Increase the direct count to prevent the thread pool from being destroyed before
            // invokeAll is called. This will also throw if the object adapter has been deactivated.
            //
            _adapter.incDirectCount();

            int requestId = 0;
            try
            {
                lock (this)
                {
                    outAsync.cancelable(this); // This will throw if the request is canceled

                    if (_response)
                    {
                        requestId = ++_requestId;
                        _asyncRequests.Add(requestId, outAsync);
                    }

                    _sendAsyncRequests.Add(outAsync, requestId);
                }
            }
            catch (Exception)
            {
                _adapter.decDirectCount();
                throw;
            }

            outAsync.attachCollocatedObserver(_adapter, requestId);
            if (!synchronous || !_response || _reference.getInvocationTimeout() > 0)
            {
                // Don't invoke from the user thread if async or invocation timeout is set
                _adapter.getThreadPool().dispatch(
                    () =>
                    {
                        if (sentAsync(outAsync))
                        {
                            invokeAll(outAsync.getOs(), requestId);
                        }
                    }, null);
            }
            else if (_dispatcher)
            {
                _adapter.getThreadPool().dispatchFromThisThread(
                    () =>
                    {
                        if (sentAsync(outAsync))
                        {
                            invokeAll(outAsync.getOs(), requestId);
                        }
                    }, null);
            }
            else // Optimization: directly call invokeAll if there's no dispatcher.
            {
                if (sentAsync(outAsync))
                {
                    invokeAll(outAsync.getOs(), requestId);
                }
            }
            return OutgoingAsyncBase.AsyncStatusQueued;
        }

        private bool sentAsync(OutgoingAsyncBase outAsync)
        {
            lock (this)
            {
                if (!_sendAsyncRequests.Remove(outAsync))
                {
                    return false; // The request timed-out.
                }

                if (!outAsync.sent())
                {
                    return true;
                }
            }
            outAsync.invokeSent();
            return true;
        }

        private void invokeAll(Ice.OutputStream os, int requestId)
        {
            if (_traceLevels.protocol >= 1)
            {
                fillInValue(os, 10, os.Size);
                if (requestId > 0)
                {
                    fillInValue(os, Protocol.headerSize, requestId);
                }
                TraceUtil.traceSend(os, _logger, _traceLevels);
            }

            Ice.InputStream iss = new Ice.InputStream(os.Communicator, os.Encoding, os.GetBuffer(), false);

            iss.Pos = Protocol.requestHdr.Length;

            int invokeNum = 1;
            ServantManager servantManager = _adapter.getServantManager();
            try
            {
                while (invokeNum > 0)
                {
                    //
                    // Increase the direct count for the dispatch. We increase it again here for
                    // each dispatch. It's important for the direct count to be > 0 until the last
                    // collocated request response is sent to make sure the thread pool isn't
                    // destroyed before.
                    //
                    try
                    {
                        _adapter.incDirectCount();
                    }
                    catch (Ice.ObjectAdapterDeactivatedException ex)
                    {
                        handleException(requestId, ex, false);
                        break;
                    }

                    Incoming inS = new Incoming(_reference.getCommunicator(), this, null, _adapter, _response, 0,
                                                requestId);
                    inS.invoke(servantManager, iss);
                    --invokeNum;
                }
            }
            catch (Ice.LocalException ex)
            {
                InvokeException(requestId, ex, invokeNum, false); // Fatal invocation exception
            }

            _adapter.decDirectCount();
        }

        private void
        handleException(int requestId, Ice.Exception ex, bool amd)
        {
            if (requestId == 0)
            {
                return; // Ignore exception for oneway messages.
            }

            OutgoingAsyncBase? outAsync;
            lock (this)
            {
                if (_asyncRequests.TryGetValue(requestId, out outAsync))
                {
                    if (!outAsync.exception(ex))
                    {
                        outAsync = null;
                    }
                    _asyncRequests.Remove(requestId);
                }
            }

            if (outAsync != null)
            {
                //
                // If called from an AMD dispatch, invoke asynchronously
                // the completion callback since this might be called from
                // the user code.
                //
                if (amd)
                {
                    outAsync.invokeExceptionAsync();
                }
                else
                {
                    outAsync.invokeException();
                }
            }
        }

        private readonly Reference _reference;
        private readonly bool _dispatcher;
        private readonly bool _response;
        private readonly Ice.ObjectAdapter _adapter;
        private readonly Ice.ILogger _logger;
        private readonly TraceLevels _traceLevels;

        private int _requestId;

        private Dictionary<OutgoingAsyncBase, int> _sendAsyncRequests = new Dictionary<OutgoingAsyncBase, int>();
        private Dictionary<int, OutgoingAsyncBase> _asyncRequests = new Dictionary<int, OutgoingAsyncBase>();
    }
}
