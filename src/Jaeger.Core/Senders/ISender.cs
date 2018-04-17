using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jaeger.Core.Senders
{
    public interface ISender
    {
        int Append(Span span);

        int Flush();

        int Close();
    }
}