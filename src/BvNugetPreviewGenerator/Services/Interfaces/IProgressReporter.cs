using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Services.Interfaces
{
        public interface IProgressReporter
        {
            void ReportProgress(int percentage, string message);
            void LogMessage(string message);
        }
}
 