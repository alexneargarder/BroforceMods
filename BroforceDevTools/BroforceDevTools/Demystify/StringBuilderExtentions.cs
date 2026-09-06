// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Modified from the original; see THIRD-PARTY-NOTICES.md.

using System.Collections.Generic.Enumerable;
using System.Collections.ObjectModel;
using System.Text;

namespace System.Diagnostics
{
    public static class StringBuilderExtentions
    {
        public static StringBuilder AppendDemystified(this StringBuilder builder, Exception exception)
        {
            builder.Append(exception.GetType());
            if (!string.IsNullOrEmpty(exception.Message)) builder.Append(": ").Append(exception.Message);
            builder.Append(Environment.NewLine);

            try
            {
                var stackTrace = new EnhancedStackTrace(exception);

                if (stackTrace.FrameCount > 0) stackTrace.Append(builder);
            }
            catch
            {
                // Reading exception.StackTrace here would re-enter the patched getter, so only mark the gap.
                builder.Append("   [stack trace unavailable]");
            }

            try
            {
                if (InteropHelper.Types.AggregateException != null &&
                    InteropHelper.Types.AggregateException.IsInstanceOfType(exception))
                {
                    var innerExceptions =
                        InteropHelper.Types.InnerExceptions.GetValue(exception, null) as ReadOnlyCollection<Exception>;
                    foreach (var ex in EnumerableIList.Create(innerExceptions)) builder.AppendInnerException(ex);
                }

                if (exception.InnerException != null) builder.AppendInnerException(exception.InnerException);
            }
            catch
            {
                // Processing exceptions shouldn't throw exceptions; if it fails
            }

            return builder;
        }

        private static void AppendInnerException(this StringBuilder builder, Exception exception)
        {
            builder.Append(" ---> ")
                .AppendDemystified(exception)
                .AppendLine()
                .Append("   --- End of inner exception stack trace ---");
        }
    }
}
