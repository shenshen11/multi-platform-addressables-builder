using System;
using System.Collections.Generic;
using System.Text;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [Serializable]
    public sealed class MpabValidationMessage
    {
        public MpabLogSeverity Severity;
        public string Message;
    }

    [Serializable]
    public sealed class MpabValidationResult
    {
        public List<MpabValidationMessage> Messages = new List<MpabValidationMessage>();

        public bool HasErrors
        {
            get
            {
                return Messages.Exists(m => m.Severity == MpabLogSeverity.Error);
            }
        }

        public void Info(string message)
        {
            Add(MpabLogSeverity.Info, message);
        }

        public void Warning(string message)
        {
            Add(MpabLogSeverity.Warning, message);
        }

        public void Error(string message)
        {
            Add(MpabLogSeverity.Error, message);
        }

        public void Add(MpabLogSeverity severity, string message)
        {
            Messages.Add(new MpabValidationMessage
            {
                Severity = severity,
                Message = message
            });
        }

        public string ToDisplayString()
        {
            if (Messages.Count == 0)
                return "No validation messages.";

            var builder = new StringBuilder();
            foreach (var message in Messages)
                builder.Append('[').Append(message.Severity).Append("] ").AppendLine(message.Message);

            return builder.ToString();
        }
    }
}
