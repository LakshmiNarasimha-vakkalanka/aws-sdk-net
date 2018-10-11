﻿/*
 * Copyright 2010-2018 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using Amazon.Util;
using Amazon.Runtime.Internal.Util;
using System.Globalization;
using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace Amazon.Runtime.Internal
{
    /// <summary>
    /// The CSM handler has the logic for capturing data for CSM events when enabled.
    /// </summary>
    public class CSMCallEventHandler : PipelineHandler
    {
        // Stopwatch to measure API call latency.
        private Stopwatch stopWatch;
        /// <summary>
        /// Invokes the CSM handler and captures data for the CSM events.
        /// </summary>
        public override void InvokeSync(IExecutionContext executionContext)
        {
            try
            {
                PreInvoke(executionContext);
                base.InvokeSync(executionContext);
            }
            catch (Exception e)
            {
                CaptureCSMCallEventExceptionData(executionContext.RequestContext);
                throw;
            }
            finally
            {
                CSMCallEventMetricsCapture(executionContext);
                CSMUtilities.SerializetoJsonAndPostOverUDP(executionContext.RequestContext.CSMCallEvent);
            }
        }

#if AWS_ASYNC_API
        /// <summary>
        /// Calls the PreInvoke and PostInvoke methods before and after calling the next handler 
        /// in the pipeline.
        /// </summary>
        public override async System.Threading.Tasks.Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        {
            try
            {
                PreInvoke(executionContext);
                var response = await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
                return response;
            }
            catch (Exception e)
            {
                CaptureCSMCallEventExceptionData(executionContext.RequestContext);
                throw;
            }
            finally
            {
                CSMCallEventMetricsCapture(executionContext);
                CSMUtilities.SerializetoJsonAndPostOverUDPAsync(executionContext.RequestContext.CSMCallEvent).ConfigureAwait(false);
            }            
        }

#elif AWS_APM_API
        /// <summary>
        /// Invokes the CSM handler and captures data for the CSM events.
        /// </summary>
        public override IAsyncResult InvokeAsync(IAsyncExecutionContext executionContext)
        {
            PreInvoke(ExecutionContext.CreateFromAsyncContext(executionContext));
            return base.InvokeAsync(executionContext);
        }
        protected override void InvokeAsyncCallback(IAsyncExecutionContext executionContext)
        {
            if (executionContext.ResponseContext.AsyncResult.Exception != null)
            {
                CaptureCSMCallEventExceptionData(executionContext.RequestContext);
            }
            CSMCallEventMetricsCapture(ExecutionContext.CreateFromAsyncContext(executionContext));
            CSMUtilities.BeginSerializetoJsonAndPostOverUDP(executionContext.RequestContext.CSMCallEvent);
            base.InvokeAsyncCallback(executionContext);
        }

#endif
        /// <summary>
        /// Invoked from the finally block of CSMCallEventHandler's Invoke method.
        /// This method is used to capture CSM Call event metrics.
        /// </summary>
        /// <param name="executionContext"></param>
        private void CSMCallEventMetricsCapture(IExecutionContext executionContext)
        {
            // Stop timer for measuring API call latency.
            stopWatch.Stop();
            // Record CSM call event metrics.
            executionContext.RequestContext.CSMCallEvent.AttemptCount = executionContext.RequestContext.Retries + 1;
            executionContext.RequestContext.CSMCallEvent.Service = executionContext.RequestContext.ServiceMetaData.ServiceId;
            executionContext.RequestContext.CSMCallEvent.Api = executionContext.RequestContext.CSMCallAttempt.Api;
            executionContext.RequestContext.CSMCallEvent.Region = executionContext.RequestContext.CSMCallAttempt.Region;
            executionContext.RequestContext.CSMCallEvent.Latency = stopWatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Method that gets invoked in case of an exception in the API request completion.
        /// </summary>
        /// <param name="requestContext"></param>
        private static void CaptureCSMCallEventExceptionData(IRequestContext requestContext)
        {
            // Set IsLastExceptionRetryable value on CSMCallEvent to whether the final exception thrown as part of 
            // of the API request completion was retryable or  not. 
            requestContext.CSMCallEvent.IsLastExceptionRetryable = requestContext.IsLastExceptionRetryable;
        }
        protected void PreInvoke(IExecutionContext executionContext)
        {
            stopWatch = Stopwatch.StartNew();
        }

    }
}
